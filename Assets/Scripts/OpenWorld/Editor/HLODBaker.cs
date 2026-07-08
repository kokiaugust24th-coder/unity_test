using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenWorld.EditorTools
{
    /// <summary>
    /// セル単位 HLOD ベイク (spec: world-hlod / HLOD ベイク)。
    /// 静的メッシュを結合し、可能ならテクスチャをアトラス化する。
    /// アトラス不可 (非 Readable テクスチャ等) の場合はマテリアル別サブメッシュ結合にフォールバック。
    /// </summary>
    public static class HLODBaker
    {
        /// <summary>
        /// HLOD プレハブを生成しアセットパスを返す。静的メッシュが無ければ null (spec: 空セルのスキップ)。
        /// </summary>
        public static string BakeCell(
            List<GameObject> units, string cellKey, WorldPartitionConfig config,
            string outputFolder, List<string> warnings)
        {
            // マテリアル別に CombineInstance を収集
            var byMaterial = new Dictionary<Material, List<CombineInstance>>();
            foreach (var unit in units)
            {
                foreach (var mf in unit.GetComponentsInChildren<MeshFilter>(false))
                {
                    var mesh = mf.sharedMesh;
                    var mr = mf.GetComponent<MeshRenderer>();
                    if (mesh == null || mr == null || !mr.enabled) continue;

                    var mats = mr.sharedMaterials;
                    int subCount = Mathf.Min(mesh.subMeshCount, mats.Length);
                    for (int s = 0; s < subCount; s++)
                    {
                        if (mats[s] == null) continue;
                        if (!byMaterial.TryGetValue(mats[s], out var list))
                            byMaterial[mats[s]] = list = new List<CombineInstance>();
                        list.Add(new CombineInstance
                        {
                            mesh = mesh,
                            subMeshIndex = s,
                            transform = mf.transform.localToWorldMatrix,
                        });
                    }
                }
            }
            if (byMaterial.Count == 0) return null;

            // マテリアルごとに 1 メッシュへ結合
            var materials = new List<Material>(byMaterial.Keys);
            var perMaterialMeshes = new List<Mesh>();
            foreach (var mat in materials)
            {
                var m = new Mesh { indexFormat = IndexFormat.UInt32, name = $"hlod_{cellKey}_{mat.name}" };
                m.CombineMeshes(byMaterial[mat].ToArray(), true, true);
                perMaterialMeshes.Add(m);
            }

            EnsureFolder(outputFolder);
            string prefabPath = $"{outputFolder}/HLOD_{cellKey}.prefab";
            Mesh finalMesh;
            Material[] finalMats;

            if (TryBuildAtlas(materials, perMaterialMeshes, cellKey, config, outputFolder,
                    out finalMesh, out var atlasMat))
            {
                finalMats = new[] { atlasMat };
            }
            else
            {
                warnings?.Add($"HLOD {cellKey}: テクスチャアトラス化不可のためマテリアル別サブメッシュにフォールバック");
                finalMesh = new Mesh { indexFormat = IndexFormat.UInt32, name = $"hlod_{cellKey}" };
                var combine = new CombineInstance[perMaterialMeshes.Count];
                for (int i = 0; i < perMaterialMeshes.Count; i++)
                    combine[i] = new CombineInstance { mesh = perMaterialMeshes[i], transform = Matrix4x4.identity };
                finalMesh.CombineMeshes(combine, false, false);
                finalMats = materials.ToArray();
            }

            foreach (var m in perMaterialMeshes)
                if (m != finalMesh) Object.DestroyImmediate(m);

            // アセット保存
            string meshPath = $"{outputFolder}/HLOD_{cellKey}_mesh.asset";
            AssetDatabase.CreateAsset(finalMesh, meshPath);

            var go = new GameObject($"HLOD_{cellKey}");
            go.isStatic = true;
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = finalMats;
            renderer.shadowCastingMode = config.hlodCastShadows
                ? ShadowCastingMode.On : ShadowCastingMode.Off;

            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            return prefabPath;
        }

        /// <summary>
        /// 全マテリアルのベーステクスチャが Readable ならアトラス化して単一マテリアルにする。
        /// UV が [0,1] 外 (タイリング) の場合は精度が落ちるため警告対象。
        /// </summary>
        static bool TryBuildAtlas(
            List<Material> materials, List<Mesh> perMaterialMeshes, string cellKey,
            WorldPartitionConfig config, string outputFolder,
            out Mesh atlasMesh, out Material atlasMaterial)
        {
            atlasMesh = null;
            atlasMaterial = null;
            if (config.hlodAtlasSize <= 0) return false;

            var textures = new Texture2D[materials.Count];
            for (int i = 0; i < materials.Count; i++)
            {
                var tex = GetBaseTexture(materials[i]);
                if (tex == null)
                {
                    // 無テクスチャ → マテリアル色の 4x4 テクスチャで代用
                    tex = SolidTexture(GetBaseColor(materials[i]));
                }
                else if (!tex.isReadable)
                {
                    return false; // 1 つでも読めなければフォールバック
                }
                textures[i] = tex;
            }

            var atlas = new Texture2D(config.hlodAtlasSize, config.hlodAtlasSize);
            Rect[] rects = atlas.PackTextures(textures, 2, config.hlodAtlasSize);
            if (rects == null) return false;
            atlas.name = $"HLOD_{cellKey}_atlas";

            // 各メッシュの UV をアトラス矩形へ再マップ
            for (int i = 0; i < perMaterialMeshes.Count; i++)
            {
                var mesh = perMaterialMeshes[i];
                var uv = mesh.uv;
                var r = rects[i];
                for (int v = 0; v < uv.Length; v++)
                {
                    // タイリング UV は繰り返し境界で歪むため frac で近似
                    float u = uv[v].x - Mathf.Floor(uv[v].x);
                    float w = uv[v].y - Mathf.Floor(uv[v].y);
                    uv[v] = new Vector2(r.x + u * r.width, r.y + w * r.height);
                }
                mesh.uv = uv;
            }

            atlasMesh = new Mesh { indexFormat = IndexFormat.UInt32, name = $"hlod_{cellKey}" };
            var combine = new CombineInstance[perMaterialMeshes.Count];
            for (int i = 0; i < perMaterialMeshes.Count; i++)
                combine[i] = new CombineInstance { mesh = perMaterialMeshes[i], transform = Matrix4x4.identity };
            atlasMesh.CombineMeshes(combine, true, false);

            string texPath = $"{outputFolder}/HLOD_{cellKey}_atlas.asset";
            AssetDatabase.CreateAsset(atlas, texPath);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            atlasMaterial = new Material(shader) { name = $"HLOD_{cellKey}_mat" };
            atlasMaterial.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            string matPath = $"{outputFolder}/HLOD_{cellKey}_mat.mat";
            AssetDatabase.CreateAsset(atlasMaterial, matPath);
            atlasMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            return true;
        }

        static Texture2D GetBaseTexture(Material mat)
        {
            if (mat.HasProperty("_BaseMap")) return mat.GetTexture("_BaseMap") as Texture2D;
            if (mat.HasProperty("_MainTex")) return mat.GetTexture("_MainTex") as Texture2D;
            return mat.mainTexture as Texture2D;
        }

        static Color GetBaseColor(Material mat)
        {
            if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
            if (mat.HasProperty("_Color")) return mat.GetColor("_Color");
            return Color.white;
        }

        static Texture2D SolidTexture(Color color)
        {
            var tex = new Texture2D(4, 4);
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
