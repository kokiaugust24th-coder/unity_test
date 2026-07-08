using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace OpenWorldTerrain
{
    /// <summary>Feature データ (シーン非依存の中間表現。エディタ側で抽出して渡す)。</summary>
    public class FeatureInput
    {
        public class Path
        {
            public Vector3[] Points;   // ワールド座標 (1m 間隔サンプル済み)
            public float Width;
            public float Falloff;
            public float Depth;        // 川のみ (>0 で彫り込み)
            public float MaxGradeDeg;  // 道のみ (>0 で検証)
            public string Name;
        }

        public class Poi
        {
            public Vector3 Position;
            public float Radius;
            public float Blend;
            public string Name;
        }

        public readonly List<Path> Roads = new List<Path>();
        public readonly List<Path> Rivers = new List<Path>();
        public readonly List<Poi> Pois = new List<Poi>();
    }

    /// <summary>
    /// 道・川・POI の彫り込み (spec: terrain-features)。散布抑制マスクを出力する。
    /// 手動差分より先・侵食より後に適用される (design.md D1)。
    /// </summary>
    public class FeatureCarveStage : IHeightFieldStage
    {
        public string Name => "Feature Carve";
        public FeatureInput Input = new FeatureInput();
        public readonly List<string> Warnings = new List<string>();

        public void Execute(WorldHeightField f, TerrainGenerationSettings s, Action<float> progress)
        {
            Warnings.Clear();
            float[] suppress = f.GetOrCreateField(WorldHeightField.FieldScatterSuppress);
            float[] roadMask = f.GetOrCreateField("roadMask");
            float maxAlt = s.MaxAltitudeSafe();

            int total = Input.Roads.Count + Input.Rivers.Count + Input.Pois.Count;
            int done = 0;

            foreach (var road in Input.Roads)
            {
                CarvePath(f, suppress, road, maxAlt, carveDepth: 0f, roadMask);
                CheckGrade(road);
                progress?.Invoke(++done / (float)math.max(1, total));
            }
            foreach (var river in Input.Rivers)
            {
                CarvePath(f, suppress, river, maxAlt, carveDepth: river.Depth, null);
                progress?.Invoke(++done / (float)math.max(1, total));
            }
            foreach (var poi in Input.Pois)
            {
                CarvePoi(f, suppress, poi, maxAlt);
                progress?.Invoke(++done / (float)math.max(1, total));
            }
        }

        /// <summary>経路に沿って整地。carveDepth > 0 なら中心を掘り下げる (川)。</summary>
        void CarvePath(WorldHeightField f, float[] suppress, FeatureInput.Path path, float maxAlt,
            float carveDepth, float[] roadMask)
        {
            if (path.Points == null || path.Points.Length < 2) return;
            float half = path.Width * 0.5f;
            float reach = half + path.Falloff;

            for (int seg = 0; seg < path.Points.Length - 1; seg++)
            {
                Vector3 a = path.Points[seg], b = path.Points[seg + 1];
                int minX = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - f.OriginXZ.x - reach) / f.MetersPerPixel);
                int maxX = Mathf.CeilToInt((Mathf.Max(a.x, b.x) - f.OriginXZ.x + reach) / f.MetersPerPixel);
                int minZ = Mathf.FloorToInt((Mathf.Min(a.z, b.z) - f.OriginXZ.y - reach) / f.MetersPerPixel);
                int maxZ = Mathf.CeilToInt((Mathf.Max(a.z, b.z) - f.OriginXZ.y + reach) / f.MetersPerPixel);

                for (int z = math.max(0, minZ); z <= math.min(f.Height - 1, maxZ); z++)
                for (int x = math.max(0, minX); x <= math.min(f.Width - 1, maxX); x++)
                {
                    var w = new Vector2(f.OriginXZ.x + x * f.MetersPerPixel, f.OriginXZ.y + z * f.MetersPerPixel);
                    float t = ClosestT(new Vector2(a.x, a.z), new Vector2(b.x, b.z), w, out float dist);
                    if (dist > reach) continue;

                    float targetY = Mathf.Lerp(a.y, b.y, t) - (dist <= half ? carveDepth
                        : carveDepth * (1f - (dist - half) / path.Falloff));
                    float target01 = Mathf.Clamp01(targetY / maxAlt);

                    int i = f.Index(x, z);
                    float blend = dist <= half ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (dist - half) / path.Falloff);
                    f.Heights[i] = Mathf.Lerp(f.Heights[i], target01, blend);
                    suppress[i] = Mathf.Max(suppress[i], blend);
                    if (roadMask != null && dist <= half)
                        roadMask[i] = 1f; // スプラット上書き用 (spec: 道の生成)
                }
            }
        }

        void CarvePoi(WorldHeightField f, float[] suppress, FeatureInput.Poi poi, float maxAlt)
        {
            float reach = poi.Radius + poi.Blend;
            float target01 = Mathf.Clamp01(poi.Position.y / maxAlt);
            int minX = Mathf.FloorToInt((poi.Position.x - f.OriginXZ.x - reach) / f.MetersPerPixel);
            int maxX = Mathf.CeilToInt((poi.Position.x - f.OriginXZ.x + reach) / f.MetersPerPixel);
            int minZ = Mathf.FloorToInt((poi.Position.z - f.OriginXZ.y - reach) / f.MetersPerPixel);
            int maxZ = Mathf.CeilToInt((poi.Position.z - f.OriginXZ.y + reach) / f.MetersPerPixel);

            for (int z = math.max(0, minZ); z <= math.min(f.Height - 1, maxZ); z++)
            for (int x = math.max(0, minX); x <= math.min(f.Width - 1, maxX); x++)
            {
                var w = new Vector2(f.OriginXZ.x + x * f.MetersPerPixel, f.OriginXZ.y + z * f.MetersPerPixel);
                float dist = Vector2.Distance(w, new Vector2(poi.Position.x, poi.Position.z));
                if (dist > reach) continue;
                float blend = dist <= poi.Radius ? 1f
                    : 1f - Mathf.SmoothStep(0f, 1f, (dist - poi.Radius) / poi.Blend);
                int i = f.Index(x, z);
                f.Heights[i] = Mathf.Lerp(f.Heights[i], target01, blend);
                suppress[i] = Mathf.Max(suppress[i], blend);
            }
        }

        void CheckGrade(FeatureInput.Path road)
        {
            if (road.MaxGradeDeg <= 0f) return;
            for (int i = 0; i < road.Points.Length - 1; i++)
            {
                Vector3 a = road.Points[i], b = road.Points[i + 1];
                float run = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
                if (run < 0.01f) continue;
                float grade = Mathf.Atan2(Mathf.Abs(b.y - a.y), run) * Mathf.Rad2Deg;
                if (grade > road.MaxGradeDeg)
                {
                    Warnings.Add($"道 '{road.Name}': 区間 {i} の縦断勾配 {grade:F1}° が上限 {road.MaxGradeDeg}° を超過");
                    return; // 経路ごとに 1 回で十分
                }
            }
        }

        static float ClosestT(Vector2 a, Vector2 b, Vector2 p, out float dist)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            dist = Vector2.Distance(a + ab * t, p);
            return t;
        }
    }
}
