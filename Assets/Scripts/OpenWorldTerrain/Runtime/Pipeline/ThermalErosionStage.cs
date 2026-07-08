using System;
using UnityEngine;

namespace OpenWorldTerrain
{
    /// <summary>熱侵食: 安息角を超える斜面の土砂を隣接へ再分配し、崖裾に崖錐を作る。</summary>
    public class ThermalErosionStage : IHeightFieldStage
    {
        public string Name => "Thermal Erosion";
        static readonly (int dx, int dz)[] Neighbors =
            { (-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, -1), (-1, 1), (1, 1) };

        public void Execute(WorldHeightField f, TerrainGenerationSettings s, Action<float> progress)
        {
            var e = s.erosion;
            if (!e.enabled || e.thermalIterations <= 0) return;

            // 安息角 → 正規化高さ差の閾値
            float talus = Mathf.Tan(e.talusAngleDeg * Mathf.Deg2Rad) * f.MetersPerPixel / s.MaxAltitudeSafe();

            for (int it = 0; it < e.thermalIterations; it++)
            {
                for (int z = 1; z < f.Height - 1; z++)
                for (int x = 1; x < f.Width - 1; x++)
                {
                    int i = f.Index(x, z);
                    float h = f.Heights[i];
                    float maxDrop = 0f;
                    int target = -1;
                    foreach (var (dx, dz) in Neighbors)
                    {
                        float diag = (dx != 0 && dz != 0) ? 1.4142f : 1f;
                        float drop = (h - f.Heights[f.Index(x + dx, z + dz)]) / diag;
                        if (drop > maxDrop) { maxDrop = drop; target = f.Index(x + dx, z + dz); }
                    }
                    if (target >= 0 && maxDrop > talus)
                    {
                        float move = (maxDrop - talus) * 0.25f;
                        f.Heights[i] -= move;
                        f.Heights[target] += move;
                    }
                }
                progress?.Invoke(it / (float)e.thermalIterations);
            }
        }
    }
}
