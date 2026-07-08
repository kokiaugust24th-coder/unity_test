using System;
using Unity.Mathematics;
using UnityEngine;

namespace OpenWorldTerrain
{
    /// <summary>
    /// ドロップレット水力侵食 (spec: 侵食シミュレーション / design.md D4)。
    /// シードから粒子列を逐次生成・適用するため完全に決定論的。
    /// flow / deposit フィールドを出力し、バイオーム・散布の入力になる。
    /// </summary>
    public class HydraulicErosionStage : IHeightFieldStage
    {
        public string Name => "Hydraulic Erosion";
        const int BrushRadius = 3;

        public void Execute(WorldHeightField f, TerrainGenerationSettings s, Action<float> progress)
        {
            var e = s.erosion;
            if (!e.enabled || e.dropletCount <= 0) return;

            float[] flow = f.GetOrCreateField(WorldHeightField.FieldFlow);
            float[] deposit = f.GetOrCreateField(WorldHeightField.FieldDeposit);
            var rng = DeterministicRandom.ForStage(s.seed, 300);

            // ブラシ (半径内の重み)
            var brush = BuildBrush(BrushRadius);

            for (int d = 0; d < e.dropletCount; d++)
            {
                float2 pos = new float2(rng.NextFloat(1f, f.Width - 2f), rng.NextFloat(1f, f.Height - 2f));
                float2 dir = float2.zero;
                float speed = 1f, water = 1f, sediment = 0f;

                for (int life = 0; life < e.dropletLifetime; life++)
                {
                    int xi = (int)pos.x, zi = (int)pos.y;
                    float2 cell = new float2(pos.x - xi, pos.y - zi);

                    // 勾配 (バイリニア)
                    float hL = f.Sample(xi, zi), hR = f.Sample(xi + 1, zi);
                    float hD = f.Sample(xi, zi + 1), hRD = f.Sample(xi + 1, zi + 1);
                    float2 grad = new float2(
                        (hR - hL) * (1 - cell.y) + (hRD - hD) * cell.y,
                        (hD - hL) * (1 - cell.x) + (hRD - hR) * cell.x);
                    float height = hL * (1 - cell.x) * (1 - cell.y) + hR * cell.x * (1 - cell.y)
                                   + hD * (1 - cell.x) * cell.y + hRD * cell.x * cell.y;

                    dir = dir * e.inertia - grad * (1 - e.inertia);
                    float len = math.length(dir);
                    if (len < 1e-8f) dir = rng.NextFloat2Direction();
                    else dir /= len;

                    pos += dir;
                    if (pos.x < 1 || pos.x >= f.Width - 2 || pos.y < 1 || pos.y >= f.Height - 2) break;

                    float newHeight = f.SampleBilinear(pos.x, pos.y);
                    float deltaH = newHeight - height;

                    float capacity = math.max(-deltaH, 0.01f) * speed * water * e.capacity;

                    if (sediment > capacity || deltaH > 0)
                    {
                        // 堆積
                        float amount = deltaH > 0
                            ? math.min(deltaH, sediment)
                            : (sediment - capacity) * e.depositSpeed;
                        sediment -= amount;
                        DepositAt(f, deposit, xi, zi, cell, amount);
                    }
                    else
                    {
                        // 侵食 (ブラシで柔らかく削る)
                        float amount = math.min((capacity - sediment) * e.erodeSpeed, -deltaH);
                        ErodeBrush(f, brush, xi, zi, amount);
                        sediment += amount;
                    }

                    speed = math.sqrt(math.max(0f, speed * speed + deltaH * -9.81f * 0.01f));
                    water *= 1 - e.evaporation;

                    int fi = f.Index(math.clamp(xi, 0, f.Width - 1), math.clamp(zi, 0, f.Height - 1));
                    flow[fi] += water;
                    if (water < 0.01f) break;
                }
                if ((d & 8191) == 0) progress?.Invoke(d / (float)e.dropletCount);
            }

            NormalizeLog(flow);
            Normalize(deposit);
        }

        static void DepositAt(WorldHeightField f, float[] deposit, int xi, int zi, float2 cell, float amount)
        {
            int i = f.Index(xi, zi);
            f.Heights[i] += amount * (1 - cell.x) * (1 - cell.y);
            f.Heights[f.Index(xi + 1, zi)] += amount * cell.x * (1 - cell.y);
            f.Heights[f.Index(xi, zi + 1)] += amount * (1 - cell.x) * cell.y;
            f.Heights[f.Index(xi + 1, zi + 1)] += amount * cell.x * cell.y;
            deposit[i] += amount;
        }

        static void ErodeBrush(WorldHeightField f, (int dx, int dz, float w)[] brush, int xi, int zi, float amount)
        {
            foreach (var (dx, dz, w) in brush)
            {
                int x = xi + dx, z = zi + dz;
                if (x < 0 || x >= f.Width || z < 0 || z >= f.Height) continue;
                int i = f.Index(x, z);
                float delta = amount * w;
                f.Heights[i] = math.max(0f, f.Heights[i] - delta);
            }
        }

        static (int, int, float)[] BuildBrush(int radius)
        {
            var list = new System.Collections.Generic.List<(int, int, float)>();
            float sum = 0f;
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                float dist = math.sqrt(dx * dx + dz * dz);
                if (dist > radius) continue;
                float w = 1f - dist / radius;
                list.Add((dx, dz, w));
                sum += w;
            }
            for (int i = 0; i < list.Count; i++)
                list[i] = (list[i].Item1, list[i].Item2, list[i].Item3 / sum);
            return list.ToArray();
        }

        static void Normalize(float[] a)
        {
            float max = 1e-6f;
            foreach (var v in a) if (v > max) max = v;
            for (int i = 0; i < a.Length; i++) a[i] /= max;
        }

        /// <summary>フローは動的レンジが広いので対数正規化。</summary>
        static void NormalizeLog(float[] a)
        {
            float max = 1e-6f;
            for (int i = 0; i < a.Length; i++) { a[i] = math.log(1 + a[i]); if (a[i] > max) max = a[i]; }
            for (int i = 0; i < a.Length; i++) a[i] /= max;
        }
    }
}
