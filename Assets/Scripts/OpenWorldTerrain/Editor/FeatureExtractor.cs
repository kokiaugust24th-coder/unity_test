using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;
using OpenWorld;
using OpenWorld.EditorTools;

namespace OpenWorldTerrain.EditorTools
{
    /// <summary>
    /// シーンの Feature コンポーネント (Road/River/POI) を FeatureInput へ抽出し、
    /// 川の水面メッシュを生成する (spec: terrain-features)。
    /// </summary>
    public static class FeatureExtractor
    {
        const float SampleStep = 2f; // m

        public static FeatureInput ExtractFromScene(out List<TerrainLayer> roadLayers)
        {
            var input = new FeatureInput();
            roadLayers = new List<TerrainLayer>();

            foreach (var road in Object.FindObjectsByType<RoadFeature>(FindObjectsSortMode.None))
            {
                input.Roads.Add(new FeatureInput.Path
                {
                    Points = SampleSpline(road.Spline),
                    Width = road.width,
                    Falloff = road.shoulderWidth,
                    MaxGradeDeg = road.maxGradeDeg,
                    Name = road.name,
                });
                if (road.roadLayer != null && !roadLayers.Contains(road.roadLayer))
                    roadLayers.Add(road.roadLayer);
            }
            foreach (var river in Object.FindObjectsByType<RiverFeature>(FindObjectsSortMode.None))
            {
                input.Rivers.Add(new FeatureInput.Path
                {
                    Points = SampleSpline(river.Spline),
                    Width = river.width,
                    Falloff = river.bankWidth,
                    Depth = river.depth,
                    Name = river.name,
                });
            }
            foreach (var poi in Object.FindObjectsByType<POIFeature>(FindObjectsSortMode.None))
            {
                input.Pois.Add(new FeatureInput.Poi
                {
                    Position = poi.transform.position,
                    Radius = poi.radius,
                    Blend = poi.blendWidth,
                    Name = poi.name,
                });
            }
            return input;
        }

        static Vector3[] SampleSpline(SplineContainer container)
        {
            float length = container.CalculateLength();
            int count = Mathf.Max(2, Mathf.CeilToInt(length / SampleStep));
            var points = new Vector3[count + 1];
            for (int i = 0; i <= count; i++)
                points[i] = container.EvaluatePosition(i / (float)count);
            return points;
        }

        /// <summary>
        /// 川の水面リボンメッシュを生成し OpenWorldRegion 直下に配置する
        /// (spec: 川の生成 — 既存セルベイクの対象としてストリーミング)。
        /// </summary>
        public static void BuildWaterMeshes(OpenWorldRegion region, List<string> warnings)
        {
            if (region == null) return;
            string folder = TerrainTileExporter.TerrainFolder + "/Water";
            HLODBaker.EnsureFolder(folder);

            foreach (var river in Object.FindObjectsByType<RiverFeature>(FindObjectsSortMode.None))
            {
                var pts = SampleSpline(river.Spline);
                if (pts.Length < 2) continue;
                float half = river.width * 0.5f;

                var verts = new Vector3[pts.Length * 2];
                var uv = new Vector2[pts.Length * 2];
                for (int i = 0; i < pts.Length; i++)
                {
                    Vector3 fwd = (pts[Mathf.Min(i + 1, pts.Length - 1)]
                                   - pts[Mathf.Max(i - 1, 0)]).normalized;
                    Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;
                    float y = pts[i].y - river.depth + river.waterOffset;
                    verts[i * 2] = new Vector3(pts[i].x, y, pts[i].z) - side * half;
                    verts[i * 2 + 1] = new Vector3(pts[i].x, y, pts[i].z) + side * half;
                    uv[i * 2] = new Vector2(0f, i * SampleStep / river.width);
                    uv[i * 2 + 1] = new Vector2(1f, i * SampleStep / river.width);
                }
                var tris = new int[(pts.Length - 1) * 6];
                int t = 0;
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    int v = i * 2;
                    tris[t++] = v; tris[t++] = v + 2; tris[t++] = v + 1;
                    tris[t++] = v + 1; tris[t++] = v + 2; tris[t++] = v + 3;
                }
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = $"water_{river.name}" };
                mesh.vertices = verts;
                mesh.uv = uv;
                mesh.triangles = tris;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                string meshPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/Water_{river.name}.asset");
                AssetDatabase.CreateAsset(mesh, meshPath);

                var go = new GameObject($"Water_{river.name}");
                go.isStatic = true;
                go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = river.waterMaterial != null
                    ? river.waterMaterial
                    : AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
                if (river.waterMaterial == null)
                    warnings?.Add($"川 '{river.name}': waterMaterial 未設定のため既定マテリアルを使用");
                go.AddComponent<GeneratedScatterTag>();
                go.transform.SetParent(region.transform, true);
            }
        }
    }
}
