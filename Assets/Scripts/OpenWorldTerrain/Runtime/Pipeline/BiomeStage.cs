using System;
using Unity.Mathematics;
using UnityEngine;

namespace OpenWorldTerrain
{
    /// <summary>
    /// バイオーム分類 (spec: バイオーム分類)。
    /// 湿度 = フロー (侵食出力) とノイズの合成。biome フィールドに ID (float) を書く。
    /// </summary>
    public class BiomeStage : IHeightFieldStage
    {
        public string Name => "Biome Classification";

        public void Execute(WorldHeightField f, TerrainGenerationSettings s, Action<float> progress)
        {
            float[] biome = f.GetOrCreateField(WorldHeightField.FieldBiome);
            float[] moisture = f.GetOrCreateField(WorldHeightField.FieldMoisture);
            f.TryGetField(WorldHeightField.FieldFlow, out var flow);

            var rng = DeterministicRandom.ForStage(s.seed, 400);
            float2 offset = rng.NextFloat2(0f, 100000f);
            float mpp = f.MetersPerPixel;
            float maxAlt = s.MaxAltitudeSafe();

            for (int z = 0; z < f.Height; z++)
            {
                for (int x = 0; x < f.Width; x++)
                {
                    int i = f.Index(x, z);
                    // 湿度 = 低周波ノイズ 60% + フロー 40%
                    float n = noise.snoise((new float2(x, z) * mpp + offset) * 0.0004f) * 0.5f + 0.5f;
                    float m = math.saturate(n * 0.6f + (flow != null ? flow[i] : 0f) * 0.4f);
                    moisture[i] = m;

                    float h01 = f.Heights[i];
                    float slopeDeg = SlopeDeg(f, x, z, maxAlt);

                    int id = 0;
                    for (int b = 0; b < s.biomes.Count; b++)
                    {
                        var def = s.biomes[b];
                        if (h01 >= def.minHeight01 && h01 <= def.maxHeight01
                            && slopeDeg <= def.maxSlopeDeg
                            && m >= def.minMoisture && m <= def.maxMoisture)
                        {
                            id = b;
                            break; // 定義順 = 優先順
                        }
                    }
                    biome[i] = id;
                }
                if ((z & 63) == 0) progress?.Invoke(z / (float)f.Height);
            }
        }

        public static float SlopeDeg(WorldHeightField f, int x, int z, float maxAlt)
        {
            float dx = (f.Sample(x + 1, z) - f.Sample(x - 1, z)) * maxAlt / (2f * f.MetersPerPixel);
            float dz = (f.Sample(x, z + 1) - f.Sample(x, z - 1)) * maxAlt / (2f * f.MetersPerPixel);
            return math.degrees(math.atan(math.sqrt(dx * dx + dz * dz)));
        }
    }
}
