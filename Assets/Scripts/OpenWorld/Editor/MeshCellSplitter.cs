using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenWorld.EditorTools
{
    /// <summary>
    /// 床や地形のような「セル境界を跨ぐ大きな静的メッシュ」をセル境界で実際に切断し、
    /// セルごとのピースに分割するツール。ピースはリージョン直下に配置され、
    /// 元オブジェクトは無効化される (Undo でメッシュ以外は戻せる)。
    /// 切断は XZ のセル境界平面によるトライアングルクリッピング (頂点属性は線形補間)。
    /// </summary>
    public static class MeshCellSplitter
    {
        const string MeshFolder = OpenWorldPaths.SplitMeshesFolder;

        // 頂点属性 (ワールド空間)
        struct V
        {
            public Vector3 P;
            public Vector3 N;
            public Vector2 Uv;

            public static V Lerp(V a, V b, float t) => new V
            {
                P = Vector3.Lerp(a.P, b.P, t),
                N = Vector3.Lerp(a.N, b.N, t).normalized,
                Uv = Vector2.Lerp(a.Uv, b.Uv, t),
            };
        }

        [MenuItem("Tools/OpenWorld/変換ツール/選択メッシュをセル境界で分割", false, 21)]
        public static void SplitSelected()
        {
            var config = FindConfig();
            var region = Object.FindFirstObjectByType<OpenWorldRegion>(FindObjectsInactive.Include);
            if (config == null || region == null)
            {
                EditorUtility.DisplayDialog("OpenWorld", "WorldPartitionConfig と OpenWorldRegion が必要です。", "OK");
                return;
            }
            float cellSize = config.cellSize;

            var targets = new List<MeshFilter>();
            foreach (var go in Selection.gameObjects)
                targets.AddRange(go.GetComponentsInChildren<MeshFilter>(false));
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("OpenWorld", "MeshFilter を持つオブジェクトを選択してください。", "OK");
                return;
            }

            HLODBaker.EnsureFolder(MeshFolder);
            int split = 0, skipped = 0;
            try
            {
                foreach (var mf in targets)
                {
                    var r = mf.GetComponent<MeshRenderer>();
                    if (mf.sharedMesh == null || r == null) { skipped++; continue; }

                    var b = r.bounds;
                    var minCell = CellCoord.FromWorld(b.min, cellSize);
                    var maxCell = CellCoord.FromWorld(b.max, cellSize);
                    if (minCell == maxCell)
                    {
                        Debug.Log($"[OpenWorld] '{mf.name}' は単一セル内のため分割不要。");
                        skipped++;
                        continue;
                    }

                    EditorUtility.DisplayProgressBar("OpenWorld メッシュ分割", mf.name, split / (float)targets.Count);
                    SplitOne(mf, r, region.transform, cellSize, minCell, maxCell);
                    split++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(region.gameObject.scene);
            EditorUtility.DisplayDialog("OpenWorld",
                $"分割: {split} 件 / スキップ: {skipped} 件\n" +
                "元オブジェクトは無効化しました (確認後に削除可)。\n" +
                "World Baker で「全体リベイク」してください。", "OK");
        }

        static void SplitOne(
            MeshFilter mf, MeshRenderer renderer, Transform region,
            float cellSize, CellCoord minCell, CellCoord maxCell)
        {
            var mesh = mf.sharedMesh;
            var l2w = mf.transform.localToWorldMatrix;

            // ワールド空間の頂点属性を展開
            var verts = mesh.vertices;
            var normals = mesh.normals.Length == verts.Length ? mesh.normals : null;
            var uvs = mesh.uv.Length == verts.Length ? mesh.uv : null;
            var world = new V[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                world[i] = new V
                {
                    P = l2w.MultiplyPoint3x4(verts[i]),
                    N = normals != null ? mf.transform.TransformDirection(normals[i]).normalized : Vector3.up,
                    Uv = uvs?[i] ?? Vector2.zero,
                };
            }

            bool hadCollider = mf.GetComponent<Collider>() != null;
            int pieces = 0;

            for (int cx = minCell.X; cx <= maxCell.X; cx++)
            for (int cz = minCell.Z; cz <= maxCell.Z; cz++)
            {
                float x0 = cx * cellSize, x1 = x0 + cellSize;
                float z0 = cz * cellSize, z1 = z0 + cellSize;

                var subTris = new List<List<V>>(); // サブメッシュごとの三角形頂点列 (3 個ずつ)
                bool any = false;

                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var outList = new List<V>();
                    var tris = mesh.GetTriangles(s);
                    var poly = new List<V>(8);
                    var tmp = new List<V>(8);
                    for (int t = 0; t < tris.Length; t += 3)
                    {
                        poly.Clear();
                        poly.Add(world[tris[t]]);
                        poly.Add(world[tris[t + 1]]);
                        poly.Add(world[tris[t + 2]]);

                        // セルの 4 平面でクリップ (Sutherland–Hodgman)
                        ClipAxis(poly, tmp, 0, x0, true);
                        ClipAxis(poly, tmp, 0, x1, false);
                        ClipAxis(poly, tmp, 2, z0, true);
                        ClipAxis(poly, tmp, 2, z1, false);

                        // 扇状に三角形化
                        for (int i = 1; i + 1 < poly.Count; i++)
                        {
                            outList.Add(poly[0]);
                            outList.Add(poly[i]);
                            outList.Add(poly[i + 1]);
                        }
                    }
                    subTris.Add(outList);
                    any |= outList.Count > 0;
                }
                if (!any) continue;

                CreatePiece(mf, renderer, region, subTris, cx, cz, hadCollider);
                pieces++;
            }

            if (pieces > 0)
            {
                Undo.RecordObject(mf.gameObject, "OpenWorld Mesh Split");
                mf.gameObject.SetActive(false);
                Debug.Log($"[OpenWorld] '{mf.name}' を {pieces} ピースに分割しました。");
            }
        }

        /// <summary>axis (0=X, 2=Z) の値が limit より大きい側 (keepGreater) を残すクリップ。</summary>
        static void ClipAxis(List<V> poly, List<V> tmp, int axis, float limit, bool keepGreater)
        {
            if (poly.Count == 0) return;
            tmp.Clear();
            for (int i = 0; i < poly.Count; i++)
            {
                V cur = poly[i];
                V next = poly[(i + 1) % poly.Count];
                float cv = axis == 0 ? cur.P.x : cur.P.z;
                float nv = axis == 0 ? next.P.x : next.P.z;
                bool curIn = keepGreater ? cv >= limit : cv <= limit;
                bool nextIn = keepGreater ? nv >= limit : nv <= limit;

                if (curIn) tmp.Add(cur);
                if (curIn != nextIn)
                {
                    float t = Mathf.Approximately(nv, cv) ? 0f : (limit - cv) / (nv - cv);
                    tmp.Add(V.Lerp(cur, next, Mathf.Clamp01(t)));
                }
            }
            poly.Clear();
            poly.AddRange(tmp);
        }

        static void CreatePiece(
            MeshFilter src, MeshRenderer srcRenderer, Transform region,
            List<List<V>> subTris, int cx, int cz, bool addCollider)
        {
            var m = new Mesh { indexFormat = IndexFormat.UInt32, name = $"{src.name}_{cx}_{cz}" };
            var positions = new List<Vector3>();
            var norms = new List<Vector3>();
            var uv = new List<Vector2>();
            var submeshIndices = new List<int[]>();

            foreach (var list in subTris)
            {
                var indices = new int[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    indices[i] = positions.Count;
                    positions.Add(list[i].P);
                    norms.Add(list[i].N);
                    uv.Add(list[i].Uv);
                }
                submeshIndices.Add(indices);
            }

            m.SetVertices(positions);
            m.SetNormals(norms);
            m.SetUVs(0, uv);
            m.subMeshCount = submeshIndices.Count;
            for (int s = 0; s < submeshIndices.Count; s++)
                m.SetTriangles(submeshIndices[s], s);
            m.RecalculateBounds();
            m.RecalculateTangents();

            string meshPath = $"{MeshFolder}/{src.name}_{cx}_{cz}.asset";
            AssetDatabase.CreateAsset(m, AssetDatabase.GenerateUniqueAssetPath(meshPath));

            var go = new GameObject($"{src.name}_{cx}_{cz}");
            go.isStatic = true;
            go.transform.SetParent(region, false); // メッシュはワールド座標で焼き込み済み
            var pmf = go.AddComponent<MeshFilter>();
            pmf.sharedMesh = m;
            var pmr = go.AddComponent<MeshRenderer>();
            pmr.sharedMaterials = srcRenderer.sharedMaterials;
            pmr.shadowCastingMode = srcRenderer.shadowCastingMode;
            if (addCollider)
            {
                var col = go.AddComponent<MeshCollider>();
                col.sharedMesh = m;
            }
            Undo.RegisterCreatedObjectUndo(go, "OpenWorld Mesh Split");
        }

        static WorldPartitionConfig FindConfig() => OpenWorldPaths.FindConfig();

        [MenuItem("Tools/OpenWorld/変換ツール/選択メッシュをセル境界で分割", true)]
        static bool Validate() => Selection.gameObjects.Length > 0 && !Application.isPlaying;
    }
}
