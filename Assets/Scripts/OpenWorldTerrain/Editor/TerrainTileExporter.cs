using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using OpenWorld;
using OpenWorld.EditorTools;

namespace OpenWorldTerrain.EditorTools
{
    /// <summary>
    /// 高さフィールド → セル単位の Unity Terrain タイル書き出し (spec: terrain-streaming)。
    /// 境界頂点共有・Addressables 登録・マニフェスト contents 追加・遠景 HLOD 統合を行う。
    /// </summary>
    public static class TerrainTileExporter
    {
        public const string TerrainFolder = OpenWorldPaths.GeneratedRoot + "/Terrain";
        public const string TerrainGroup = "OpenWorld-Terrain";
        const int HlodDecimation = 8; // 遠景メッシュの間引き係数

        public static void Export(
            WorldHeightField f, TerrainGenerationSettings s,
            OpenWorldRegion region, List<TerrainLayer> extraLayers, List<string> warnings)
        {
            var config = OpenWorldPaths.FindConfig();
            if (config == null) { warnings.Add("WorldPartitionConfig が見つかりません"); return; }
            float cellSize = config.cellSize;
            int tileRes = Mathf.RoundToInt(cellSize / f.MetersPerPixel) + 1;
            if (!Mathf.IsPowerOfTwo(tileRes - 1))
                warnings.Add($"タイル解像度 {tileRes} が 2^n+1 ではありません。cellSize/metersPerPixel を調整してください");

            HLODBaker.EnsureFolder(TerrainFolder);
            var manifest = OpenWorldPaths.FindManifest();
            if (manifest == null)
            {
                HLODBaker.EnsureFolder(OpenWorldPaths.GeneratedRoot);
                manifest = ScriptableObject.CreateInstance<WorldManifest>();
                manifest.cellSize = cellSize;
                AssetDatabase.CreateAsset(manifest, OpenWorldPaths.ManifestPath);
            }

            // スプラット用レイヤー配列 (バイオーム union + 道等の追加レイヤー)
            var layers = new List<TerrainLayer>();
            foreach (var b in s.biomes)
                foreach (var l in b.terrainLayers)
                    if (l != null && !layers.Contains(l)) layers.Add(l);
            foreach (var l in extraLayers.Where(l => l != null && !layers.Contains(l)))
                layers.Add(l);

            int tiles = Mathf.RoundToInt(s.worldSize / cellSize);
            f.TryGetField(WorldHeightField.FieldBiome, out var biomeField);
            f.TryGetField("roadMask", out var roadMask);

            for (int cz = 0; cz < tiles; cz++)
            for (int cx = 0; cx < tiles; cx++)
            {
                EditorUtility.DisplayProgressBar("Terrain タイル書き出し",
                    $"Tile ({cx},{cz})", (cz * tiles + cx) / (float)(tiles * tiles));
                ExportTile(f, s, cx, cz, cellSize, tileRes, layers, biomeField, roadMask,
                    region, manifest, warnings);
            }
            EditorUtility.ClearProgressBar();

            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();
        }

        static void ExportTile(
            WorldHeightField f, TerrainGenerationSettings s, int cx, int cz,
            float cellSize, int tileRes, List<TerrainLayer> layers,
            float[] biomeField, float[] roadMask,
            OpenWorldRegion region, WorldManifest manifest, List<string> warnings)
        {
            // --- TerrainData (境界頂点は隣接タイルと共有 = 同一ワールド px を参照)
            var data = new TerrainData
            {
                heightmapResolution = tileRes,
                alphamapResolution = tileRes - 1,
                size = new Vector3(cellSize, s.maxAltitude, cellSize),
            };
            int px0 = Mathf.RoundToInt(cx * cellSize / f.MetersPerPixel);
            int pz0 = Mathf.RoundToInt(cz * cellSize / f.MetersPerPixel);

            var heights = new float[tileRes, tileRes];
            for (int z = 0; z < tileRes; z++)
            for (int x = 0; x < tileRes; x++)
                heights[z, x] = f.Sample(px0 + x, pz0 + z);
            data.SetHeights(0, 0, heights);

            // --- スプラット
            if (layers.Count > 0)
            {
                data.terrainLayers = layers.ToArray();
                int ares = data.alphamapResolution;
                var alpha = new float[ares, ares, layers.Count];
                for (int z = 0; z < ares; z++)
                for (int x = 0; x < ares; x++)
                {
                    int pxx = px0 + Mathf.RoundToInt(x / (float)(ares - 1) * (tileRes - 1));
                    int pzz = pz0 + Mathf.RoundToInt(z / (float)(ares - 1) * (tileRes - 1));
                    int fi = f.Index(Mathf.Min(pxx, f.Width - 1), Mathf.Min(pzz, f.Height - 1));
                    int layerIndex = PickLayer(f, s, pxx, pzz, fi, biomeField, roadMask, layers);
                    alpha[z, x, Mathf.Clamp(layerIndex, 0, layers.Count - 1)] = 1f;
                }
                data.SetAlphamaps(0, 0, alpha);
            }

            string dataPath = $"{TerrainFolder}/TerrainData_{cx}_{cz}.asset";
            AssetDatabase.DeleteAsset(dataPath);
            AssetDatabase.CreateAsset(data, dataPath);

            // --- 散布 (Tree/Detail はタイルへ、プレハブは region へ)
            if (region != null)
                ScatterBaker.PopulateTile(f, s, data, cx, cz, cellSize, region, warnings);

            // --- タイルプレハブ
            var go = new GameObject($"TerrainTile_{cx}_{cz}");
            var terrain = go.AddComponent<UnityEngine.Terrain>();
            terrain.terrainData = data;
            terrain.allowAutoConnect = false; // 接続はハンドラが SetNeighbors で行う
            terrain.drawInstanced = true;
            var col = go.AddComponent<TerrainCollider>();
            col.terrainData = data;
            go.transform.position = new Vector3(cx * cellSize, 0f, cz * cellSize);

            string prefabPath = $"{TerrainFolder}/TerrainTile_{cx}_{cz}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            string address = $"ow_terrain_{cx}_{cz}";
            AddressablesBakeUtil.MakeAddressable(prefabPath, address, TerrainGroup);

            // --- マニフェスト (contents に terrain を追加)
            var entry = manifest.cells.FirstOrDefault(c => !c.alwaysLoaded && c.x == cx && c.z == cz);
            if (entry == null)
            {
                entry = new WorldManifest.CellEntry { x = cx, z = cz, address = "" };
                manifest.cells.Add(entry);
            }
            var contents = (entry.contents ?? System.Array.Empty<WorldManifest.CellContent>())
                .Where(c => c.kind != TerrainContentHandler.Kind).ToList();
            contents.Add(new WorldManifest.CellContent { kind = TerrainContentHandler.Kind, address = address });
            entry.contents = contents.ToArray();

            // --- 遠景 HLOD (コンテンツ HLOD と 1 プレハブに統合。spec: 遠景 Terrain HLOD)
            entry.hlodAddress = BuildMergedHlod(f, s, cx, cz, cellSize, tileRes, entry, warnings);
        }

        static int PickLayer(
            WorldHeightField f, TerrainGenerationSettings s, int px, int pz, int fi,
            float[] biomeField, float[] roadMask, List<TerrainLayer> layers)
        {
            // 道は最優先で道レイヤー (extraLayers 末尾に入っている想定)
            if (roadMask != null && roadMask[fi] > 0.5f && layers.Count > 0)
                return layers.Count - 1;

            int biomeId = biomeField != null ? (int)biomeField[fi] : 0;
            if (biomeId < 0 || biomeId >= s.biomes.Count) return 0;
            var biome = s.biomes[biomeId];
            if (biome.terrainLayers.Length == 0) return 0;

            float slope = BiomeStage.SlopeDeg(f, px, pz, s.maxAltitude);
            var pick = slope >= biome.rockSlopeDeg
                ? biome.terrainLayers[biome.terrainLayers.Length - 1]  // 岩肌 (spec: 急傾斜の岩肌)
                : biome.terrainLayers[0];
            int idx = layers.IndexOf(pick);
            return idx >= 0 ? idx : 0;
        }

        /// <summary>低ポリ遠景メッシュを生成し、既存コンテンツ HLOD と統合した 1 プレハブを作る。</summary>
        static string BuildMergedHlod(
            WorldHeightField f, TerrainGenerationSettings s, int cx, int cz,
            float cellSize, int tileRes, WorldManifest.CellEntry entry, List<string> warnings)
        {
            int px0 = Mathf.RoundToInt(cx * cellSize / f.MetersPerPixel);
            int pz0 = Mathf.RoundToInt(cz * cellSize / f.MetersPerPixel);
            int step = HlodDecimation;
            int n = (tileRes - 1) / step + 1;

            var verts = new Vector3[n * n];
            var uv = new Vector2[n * n];
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                float h = f.Sample(px0 + x * step, pz0 + z * step) * s.maxAltitude;
                verts[z * n + x] = new Vector3(cx * cellSize + x * step * f.MetersPerPixel, h,
                                               cz * cellSize + z * step * f.MetersPerPixel);
                uv[z * n + x] = new Vector2(x / (float)(n - 1), z / (float)(n - 1));
            }
            var tris = new int[(n - 1) * (n - 1) * 6];
            int t = 0;
            for (int z = 0; z < n - 1; z++)
            for (int x = 0; x < n - 1; x++)
            {
                int i = z * n + x;
                tris[t++] = i; tris[t++] = i + n; tris[t++] = i + 1;
                tris[t++] = i + 1; tris[t++] = i + n; tris[t++] = i + n + 1;
            }
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = $"terrain_hlod_{cx}_{cz}" };
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            string meshPath = $"{TerrainFolder}/TerrainHLOD_{cx}_{cz}_mesh.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var root = new GameObject($"HLOD_merged_{cx}_{cz}");
            var mf = root.AddComponent<MeshFilter>();
            mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            var mr = root.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = $"terrain_hlod_{cx}_{cz}_mat" };
            var baseColor = s.biomes.Count > 0 && s.biomes[0].terrainLayers.Length > 0
                ? Color.white : new Color(0.35f, 0.4f, 0.3f);
            mat.SetColor("_BaseColor", baseColor);
            string matPath = $"{TerrainFolder}/TerrainHLOD_{cx}_{cz}_mat.mat";
            AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(mat, matPath);
            mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            mr.shadowCastingMode = ShadowCastingMode.Off;

            // 既存コンテンツ HLOD があれば子として統合 (セル HLOD の一本化。design.md D11)
            if (entry.HasHlod && !entry.hlodAddress.StartsWith("ow_thlod_"))
            {
                string oldPath = $"{OpenWorldPaths.HlodFolder}/HLOD_{cx}_{cz}.prefab";
                var contentHlod = AssetDatabase.LoadAssetAtPath<GameObject>(oldPath);
                if (contentHlod != null)
                {
                    var child = (GameObject)PrefabUtility.InstantiatePrefab(contentHlod);
                    PrefabUtility.UnpackPrefabInstance(child, PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                    child.transform.SetParent(root.transform, true);
                }
            }

            string prefabPath = $"{TerrainFolder}/TerrainHLOD_{cx}_{cz}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            string address = $"ow_thlod_{cx}_{cz}";
            AddressablesBakeUtil.MakeAddressable(prefabPath, address, AddressablesBakeUtil.HlodGroupName);
            return address;
        }
    }
}
