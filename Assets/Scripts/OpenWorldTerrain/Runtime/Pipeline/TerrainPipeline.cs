using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenWorldTerrain
{
    /// <summary>
    /// 生成パイプラインの実行体 (design.md D1)。ステージ順は固定:
    /// [インポート or ノイズ] → 台地 → 水力侵食 → 熱侵食 → Feature → (手動差分は呼び出し側) → バイオーム
    /// </summary>
    public class TerrainPipeline
    {
        public FeatureInput Features = new FeatureInput();
        public readonly List<string> Warnings = new List<string>();

        /// <summary>externalHeights が非 null ならインポート入力を使う (補助入力)。</summary>
        public WorldHeightField Run(
            TerrainGenerationSettings settings,
            bool preview,
            float[] externalHeights = null,
            int externalWidth = 0,
            Action<string, float> progress = null)
        {
            int downscale = preview ? settings.previewDownscale : 1;
            float mpp = settings.metersPerPixel * downscale;
            int res = Mathf.RoundToInt(settings.worldSize / mpp) + 1;
            var field = new WorldHeightField(res, res, mpp, settings.maxAltitude, Vector2.zero);

            var carve = new FeatureCarveStage { Input = Features };
            var stages = new List<IHeightFieldStage>();

            if (externalHeights != null)
            {
                Resample(externalHeights, externalWidth, field);
            }
            else
            {
                stages.Add(new NoiseStackStage());
                stages.Add(new PlateauStage());
            }
            stages.Add(new HydraulicErosionStage());
            stages.Add(new ThermalErosionStage());
            stages.Add(carve);
            stages.Add(new BiomeStage());

            foreach (var stage in stages)
            {
                progress?.Invoke(stage.Name, 0f);
                stage.Execute(field, settings, p => progress?.Invoke(stage.Name, p));
            }

            Warnings.Clear();
            Warnings.AddRange(carve.Warnings);
            return field;
        }

        /// <summary>外部ハイトマップ (正方) をバイリニアで field 解像度へリサンプル。</summary>
        public static void Resample(float[] src, int srcWidth, WorldHeightField dst)
        {
            int srcH = src.Length / srcWidth;
            for (int z = 0; z < dst.Height; z++)
            for (int x = 0; x < dst.Width; x++)
            {
                float sx = x / (float)(dst.Width - 1) * (srcWidth - 1);
                float sz = z / (float)(dst.Height - 1) * (srcH - 1);
                int x0 = Mathf.FloorToInt(sx), z0 = Mathf.FloorToInt(sz);
                int x1 = Mathf.Min(x0 + 1, srcWidth - 1), z1 = Mathf.Min(z0 + 1, srcH - 1);
                float tx = sx - x0, tz = sz - z0;
                float v = Mathf.Lerp(
                    Mathf.Lerp(src[z0 * srcWidth + x0], src[z0 * srcWidth + x1], tx),
                    Mathf.Lerp(src[z1 * srcWidth + x0], src[z1 * srcWidth + x1], tx), tz);
                dst.Heights[dst.Index(x, z)] = Mathf.Clamp01(v);
            }
        }
    }
}
