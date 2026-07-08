using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using OpenWorld;

namespace OpenWorldTerrain.EditorTools
{
    /// <summary>
    /// ルールベース散布 (spec: terrain-scattering)。
    /// 出力: Terrain Detail / Tree はタイルへ、ScatterPrefab は OpenWorldRegion 直下へ。
    /// ジッタ付きグリッド + シード決定論。抑制/ロックマスクを尊重する。
    /// </summary>
    public static class ScatterBaker
    {
        /// <summary>再散布前の掃除: 生成タグ付きのみ削除 (手動配置は保護)。</summary>
        public static int CleanupGenerated(OpenWorldRegion region)
        {
            if (region == null) return 0;
            var tagged = region.GetComponentsInChildren<GeneratedScatterTag>(true);
            foreach (var t in tagged)
                Object.DestroyImmediate(t.gameObject);
            return tagged.Length;
        }

        /// <summary>1 タイル分の Tree/Detail を TerrainData に書き込み、プレハブ散布を region に配置する。</summary>
        public static void PopulateTile(
            WorldHeightField f, TerrainGenerationSettings s, TerrainData data,
            int cellX, int cellZ, float cellSize, OpenWorldRegion region, List<string> warnings)
        {
            f.TryGetField(WorldHeightField.FieldBiome, out var biomeField);
            f.TryGetField(WorldHeightField.FieldScatterSuppress, out var suppress);
            f.TryGetField(WorldHeightField.FieldLock, out var lockMask);

            // プロトタイプ登録 (全バイオームの union、決定論のため安定ソート)
            var treeRules = new List<(int biome, ScatterRule rule)>();
            var detailRules = new List<(int biome, ScatterRule rule)>();
            var prefabRules = new List<(int biome, ScatterRule rule)>();
            for (int b = 0; b < s.biomes.Count; b++)
                foreach (var r in s.biomes[b].scatter.Where(r => r.enabled))
                {
                    if (r.output == ScatterOutput.TerrainTree && r.prefab != null) treeRules.Add((b, r));
                    else if (r.output == ScatterOutput.TerrainDetail && r.detailTexture != null) detailRules.Add((b, r));
                    else if (r.output == ScatterOutput.ScatterPrefab && r.prefab != null) prefabRules.Add((b, r));
                }

            data.treePrototypes = treeRules
                .Select(t => new TreePrototype { prefab = t.rule.prefab }).ToArray();
            data.detailPrototypes = detailRules
                .Select(t => new DetailPrototype
                {
                    prototypeTexture = t.rule.detailTexture,
                    renderMode = DetailRenderMode.GrassBillboard,
                    useInstancing = false,
                }).ToArray();
            data.RefreshPrototypes();

            var trees = new List<TreeInstance>();
            float minWx = cellX * cellSize, minWz = cellZ * cellSize;

            // Tree / Prefab: ジッタ付きグリッド
            for (int ri = 0; ri < treeRules.Count; ri++)
                ScatterPoints(f, s, treeRules[ri].rule, treeRules[ri].biome, cellX, cellZ, cellSize,
                    biomeField, suppress, lockMask, (uint)(500 + ri), (wx, wz, h01, rng) =>
                    {
                        trees.Add(new TreeInstance
                        {
                            position = new Vector3((wx - minWx) / cellSize, h01, (wz - minWz) / cellSize),
                            prototypeIndex = ri,
                            widthScale = rng.NextFloat(treeRules[ri].rule.scaleMin, treeRules[ri].rule.scaleMax),
                            heightScale = rng.NextFloat(treeRules[ri].rule.scaleMin, treeRules[ri].rule.scaleMax),
                            color = Color.white,
                            lightmapColor = Color.white,
                        });
                    });

            for (int ri = 0; ri < prefabRules.Count; ri++)
            {
                var rule = prefabRules[ri].rule;
                ScatterPoints(f, s, rule, prefabRules[ri].biome, cellX, cellZ, cellSize,
                    biomeField, suppress, lockMask, (uint)(700 + ri), (wx, wz, h01, rng) =>
                    {
                        var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(rule.prefab);
                        go.transform.position = new Vector3(wx, h01 * s.maxAltitude, wz);
                        go.transform.rotation = Quaternion.Euler(0f, rng.NextFloat(0f, 360f), 0f);
                        go.transform.localScale = Vector3.one * rng.NextFloat(rule.scaleMin, rule.scaleMax);
                        go.isStatic = true;
                        go.AddComponent<GeneratedScatterTag>();
                        go.transform.SetParent(region.transform, true);
                    });
            }

            data.SetTreeInstances(trees.ToArray(), true);
            if (trees.Count > 4000)
                warnings?.Add($"タイル ({cellX},{cellZ}): 樹木 {trees.Count} 本は多すぎます (上限目安 4000)");

            // Detail: 解像度グリッドへ密度書き込み
            int dres = data.detailResolution;
            for (int ri = 0; ri < detailRules.Count; ri++)
            {
                var rule = detailRules[ri].rule;
                int biomeId = detailRules[ri].biome;
                var layer = new int[dres, dres];
                var rng = DeterministicRandom.ForCell(s.seed, (uint)(900 + ri), cellX, cellZ);
                for (int dz = 0; dz < dres; dz++)
                for (int dx = 0; dx < dres; dx++)
                {
                    float wx = minWx + (dx + 0.5f) / dres * cellSize;
                    float wz = minWz + (dz + 0.5f) / dres * cellSize;
                    if (!PassesRule(f, s, rule, biomeId, wx, wz, biomeField, suppress, lockMask)) continue;
                    float n = noise.snoise(new float2(wx, wz) * rule.noiseFrequency) * 0.5f + 0.5f;
                    if (n < rule.noiseThreshold) continue;
                    layer[dz, dx] = Mathf.RoundToInt(rule.density * 8f);
                }
                data.SetDetailLayer(0, 0, ri, layer);
            }
        }

        static void ScatterPoints(
            WorldHeightField f, TerrainGenerationSettings s, ScatterRule rule, int biomeId,
            int cellX, int cellZ, float cellSize,
            float[] biomeField, float[] suppress, float[] lockMask, uint stageId,
            System.Action<float, float, float, Unity.Mathematics.Random> place)
        {
            var rng = DeterministicRandom.ForCell(s.seed, stageId, cellX, cellZ);
            int count = Mathf.FloorToInt(cellSize / rule.spacing);
            for (int gz = 0; gz < count; gz++)
            for (int gx = 0; gx < count; gx++)
            {
                if (rng.NextFloat() > rule.density) { rng.NextFloat2(); continue; } // 消費数を固定
                float2 jitter = rng.NextFloat2(0.1f, 0.9f);
                float wx = cellX * cellSize + (gx + jitter.x) * rule.spacing;
                float wz = cellZ * cellSize + (gz + jitter.y) * rule.spacing;
                if (!PassesRule(f, s, rule, biomeId, wx, wz, biomeField, suppress, lockMask)) continue;
                float n = noise.snoise(new float2(wx, wz) * rule.noiseFrequency) * 0.5f + 0.5f;
                if (n < rule.noiseThreshold) continue;
                float h01 = f.SampleBilinear(
                    (wx - f.OriginXZ.x) / f.MetersPerPixel, (wz - f.OriginXZ.y) / f.MetersPerPixel);
                place(wx, wz, h01, rng);
            }
        }

        static bool PassesRule(
            WorldHeightField f, TerrainGenerationSettings s, ScatterRule rule, int biomeId,
            float wx, float wz, float[] biomeField, float[] suppress, float[] lockMask)
        {
            int px = Mathf.Clamp(Mathf.RoundToInt((wx - f.OriginXZ.x) / f.MetersPerPixel), 0, f.Width - 1);
            int pz = Mathf.Clamp(Mathf.RoundToInt((wz - f.OriginXZ.y) / f.MetersPerPixel), 0, f.Height - 1);
            int i = f.Index(px, pz);

            if (biomeField != null && (int)biomeField[i] != biomeId) return false;
            if (suppress != null && suppress[i] > 0.5f) return false;       // 道・川・POI (spec: 抑制マスクの尊重)
            if (lockMask != null && lockMask[i] > 0.5f) return false;      // ロック領域
            float alt = f.Heights[i] * s.maxAltitude;
            if (alt < rule.minAltitude || alt > rule.maxAltitude) return false;
            if (BiomeStage.SlopeDeg(f, px, pz, s.maxAltitude) > rule.maxSlopeDeg) return false;
            return true;
        }
    }
}
