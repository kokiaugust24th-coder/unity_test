using System;
using Unity.Mathematics;
using UnityEngine;

namespace OpenWorldTerrain
{
    /// <summary>台地・崖の大規模構造 (spec: 大規模構造の合成)。Voronoi 閾値で台地マスクを作る。</summary>
    public class PlateauStage : IHeightFieldStage
    {
        public string Name => "Plateau / Cliffs";

        public void Execute(WorldHeightField f, TerrainGenerationSettings s, Action<float> progress)
        {
            if (s.plateauFrequency <= 0f) return;
            var rng = DeterministicRandom.ForStage(s.seed, 200);
            float2 offset = rng.NextFloat2(0f, 100000f);
            float lift = s.plateauHeight / s.MaxAltitudeSafe();
            // 遷移幅を Voronoi 値域に写像 (経験則: 値勾配 ≈ frequency に比例)
            float band = math.max(0.02f, s.cliffTransition * s.plateauFrequency * 2f);

            for (int z = 0; z < f.Height; z++)
            {
                for (int x = 0; x < f.Width; x++)
                {
                    float2 p = (new float2(x, z) * f.MetersPerPixel + offset) * s.plateauFrequency;
                    float v = noise.cellular(p).x; // 0 近傍=セル中心
                    // smoothstep で崖の遷移帯を形成
                    float t = math.smoothstep(s.plateauThreshold, s.plateauThreshold + band, v);
                    int i = f.Index(x, z);
                    f.Heights[i] = Mathf.Clamp01(f.Heights[i] + lift * t);
                }
                if ((z & 63) == 0) progress?.Invoke(z / (float)f.Height);
            }
        }
    }

    public static class SettingsExt
    {
        public static float MaxAltitudeSafe(this TerrainGenerationSettings s) =>
            Mathf.Max(1f, s.maxAltitude);
    }
}
