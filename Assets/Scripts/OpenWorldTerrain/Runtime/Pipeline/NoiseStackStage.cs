using System;
using Unity.Mathematics;
using UnityEngine;

namespace OpenWorldTerrain
{
    /// <summary>ノイズスタック合成 (spec: ノイズスタック合成)。</summary>
    public class NoiseStackStage : IHeightFieldStage
    {
        public string Name => "Noise Stack";

        public void Execute(WorldHeightField f, TerrainGenerationSettings s, Action<float> progress)
        {
            int layerIndex = 0;
            foreach (var layer in s.noiseLayers)
            {
                layerIndex++;
                if (!layer.enabled) continue;
                var rng = DeterministicRandom.ForStage(s.seed, (uint)(100 + layerIndex));
                float2 offset = rng.NextFloat2(0f, 100000f);
                float[] mask = null;
                if (!string.IsNullOrEmpty(layer.maskField))
                    f.TryGetField(layer.maskField, out mask);

                for (int z = 0; z < f.Height; z++)
                {
                    for (int x = 0; x < f.Width; x++)
                    {
                        int i = f.Index(x, z);
                        float wx = x * f.MetersPerPixel;
                        float wz = z * f.MetersPerPixel;
                        float v = Evaluate(layer, wx, wz, offset, f.Heights[i]);
                        float m = mask != null ? mask[i] : 1f;
                        float amp = layer.amplitude * m;
                        switch (layer.blend)
                        {
                            case BlendMode.Add: f.Heights[i] += v * amp; break;
                            case BlendMode.Multiply: f.Heights[i] *= Mathf.Lerp(1f, v, m); break;
                            case BlendMode.Min: f.Heights[i] = Mathf.Min(f.Heights[i], v * layer.amplitude); break;
                            case BlendMode.Max: f.Heights[i] = Mathf.Max(f.Heights[i], v * amp); break;
                        }
                    }
                    if ((z & 63) == 0) progress?.Invoke(z / (float)f.Height);
                }
            }
            // 0..1 に正規化はせずクランプ (レイヤー設計を素直に反映)
            for (int i = 0; i < f.Heights.Length; i++)
                f.Heights[i] = Mathf.Clamp01(f.Heights[i]);
        }

        static float Evaluate(NoiseLayer l, float wx, float wz, float2 offset, float current)
        {
            float2 p = (new float2(wx, wz) + offset) * l.frequency;
            switch (l.type)
            {
                case NoiseType.FBm: return Fbm(p, l.octaves, l.lacunarity, l.persistence);
                case NoiseType.Ridged: return Ridged(p, l.octaves, l.lacunarity, l.persistence);
                case NoiseType.Voronoi: return noise.cellular(p).x;
                case NoiseType.Terrace:
                    // 現在高さを段化 (v はターゲット値)
                    float steps = math.max(2, l.terraceSteps);
                    return math.floor(current * steps) / steps
                           + math.smoothstep(0.7f, 1f, math.frac(current * steps)) / steps;
                default: return 0f;
            }
        }

        static float Fbm(float2 p, int octaves, float lacunarity, float persistence)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += (noise.snoise(p) * 0.5f + 0.5f) * amp;
                norm += amp;
                amp *= persistence;
                p *= lacunarity;
            }
            return sum / norm;
        }

        static float Ridged(float2 p, int octaves, float lacunarity, float persistence)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += (1f - math.abs(noise.snoise(p))) * amp;
                norm += amp;
                amp *= persistence;
                p *= lacunarity;
            }
            return sum / norm;
        }
    }
}
