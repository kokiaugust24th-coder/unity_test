using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenWorldTerrain
{
    public enum NoiseType { FBm, Ridged, Voronoi, Terrace }
    public enum BlendMode { Add, Multiply, Min, Max }

    [Serializable]
    public class NoiseLayer
    {
        public string name = "Layer";
        public bool enabled = true;
        public NoiseType type = NoiseType.FBm;
        public BlendMode blend = BlendMode.Add;
        [Min(0.0001f)] public float frequency = 0.001f; // 1/m
        public float amplitude = 0.5f;                   // 0..1 (正規化高さ)
        [Range(1, 10)] public int octaves = 5;
        [Range(1.5f, 3.5f)] public float lacunarity = 2f;
        [Range(0.1f, 0.9f)] public float persistence = 0.5f;
        [Tooltip("Terrace 用: 段の数")] public int terraceSteps = 6;
        [Tooltip("適用マスクフィールド名 (空 = 全域)")] public string maskField = "";
    }

    [Serializable]
    public class ErosionSettings
    {
        public bool enabled = true;
        [Min(0)] public int dropletCount = 200000;
        [Range(1, 256)] public int dropletLifetime = 48;
        [Range(0f, 1f)] public float inertia = 0.06f;
        [Min(0.01f)] public float capacity = 4f;
        [Range(0f, 1f)] public float erodeSpeed = 0.3f;
        [Range(0f, 1f)] public float depositSpeed = 0.3f;
        [Range(0f, 0.2f)] public float evaporation = 0.02f;
        [Min(0)] public int thermalIterations = 40;
        [Range(20f, 50f)] public float talusAngleDeg = 34f;
    }

    public enum ScatterOutput
    {
        TerrainDetail, // 草など (テクスチャ)
        TerrainTree,   // 木 (プレハブ、ビルボード LOD 付き)
        ScatterPrefab, // 大岩・遺跡など (OpenWorldRegion 直下 → セルベイク対象)
    }

    [Serializable]
    public class ScatterRule
    {
        public string name = "Rule";
        public bool enabled = true;
        public ScatterOutput output = ScatterOutput.TerrainTree;
        public GameObject prefab;        // Tree / ScatterPrefab 用
        public Texture2D detailTexture;  // TerrainDetail 用
        [Min(0.5f)] public float spacing = 8f;
        [Range(0f, 1f)] public float density = 0.5f;
        [Range(0f, 90f)] public float maxSlopeDeg = 35f;
        public float minAltitude = 0f;
        public float maxAltitude = 10000f;
        [Min(0.0001f)] public float noiseFrequency = 0.01f;
        [Range(0f, 1f)] public float noiseThreshold = 0.4f;
        [Min(0.1f)] public float scaleMin = 0.8f;
        [Min(0.1f)] public float scaleMax = 1.3f;
    }

    [Serializable]
    public class BiomeDefinition
    {
        public string name = "Biome";
        [Range(0f, 1f)] public float minHeight01;
        [Range(0f, 1f)] public float maxHeight01 = 1f;
        [Range(0f, 90f)] public float maxSlopeDeg = 90f;
        [Range(0f, 1f)] public float minMoisture;
        [Range(0f, 1f)] public float maxMoisture = 1f;
        public TerrainLayer[] terrainLayers = Array.Empty<TerrainLayer>();
        [Tooltip("この傾斜以上は岩肌レイヤー (terrainLayers の最後) を使う")]
        [Range(0f, 90f)] public float rockSlopeDeg = 40f;
        public List<ScatterRule> scatter = new List<ScatterRule>();
    }

    /// <summary>地形生成の全設定 (spec: terrain-generation / design.md D2)。</summary>
    [CreateAssetMenu(menuName = "OpenWorld/Terrain Generation Settings", fileName = "TerrainGenerationSettings")]
    public class TerrainGenerationSettings : ScriptableObject
    {
        [Header("World")]
        public uint seed = 12345;
        [Min(256f)] public float worldSize = 2048f;      // m (正方)
        [Min(0.25f)] public float metersPerPixel = 1f;
        [Min(10f)] public float maxAltitude = 400f;

        [Header("Synthesis")]
        public List<NoiseLayer> noiseLayers = new List<NoiseLayer>();
        [Tooltip("台地マスクの閾値化に使う Voronoi 周波数 (0 で無効)")]
        public float plateauFrequency = 0.0006f;
        [Range(0f, 1f)] public float plateauThreshold = 0.62f;
        [Min(1f)] public float plateauHeight = 80f;      // m
        [Min(1f)] public float cliffTransition = 24f;    // m (崖の遷移帯幅)

        [Header("Erosion")]
        public ErosionSettings erosion = new ErosionSettings();

        [Header("Biomes")]
        public List<BiomeDefinition> biomes = new List<BiomeDefinition>();

        [Header("Preview")]
        [Range(2, 8)] public int previewDownscale = 4;

        public int ResolutionPx => Mathf.RoundToInt(worldSize / metersPerPixel) + 1;

        void OnValidate()
        {
            if (noiseLayers.Count == 0)
                noiseLayers.Add(new NoiseLayer { name = "Base fBm", amplitude = 0.5f, frequency = 0.0008f });
        }
    }
}
