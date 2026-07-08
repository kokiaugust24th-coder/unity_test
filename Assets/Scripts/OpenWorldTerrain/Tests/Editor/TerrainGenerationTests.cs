using NUnit.Framework;
using UnityEngine;

namespace OpenWorldTerrain.Tests
{
    public class TerrainGenerationTests
    {
        static TerrainGenerationSettings MakeSettings(uint seed = 42)
        {
            var s = ScriptableObject.CreateInstance<TerrainGenerationSettings>();
            s.seed = seed;
            s.worldSize = 256f;
            s.metersPerPixel = 4f; // 65px — テスト用に小さく
            s.maxAltitude = 200f;
            s.noiseLayers.Add(new NoiseLayer { name = "base", frequency = 0.004f, amplitude = 0.5f });
            s.erosion.dropletCount = 2000;
            s.erosion.thermalIterations = 4;
            s.biomes.Add(new BiomeDefinition { name = "lowland", minHeight01 = 0f, maxHeight01 = 0.5f });
            s.biomes.Add(new BiomeDefinition { name = "highland", minHeight01 = 0.5f, maxHeight01 = 1f });
            return s;
        }

        // spec: 決定論的な高さマップ合成 / 侵食の決定論
        [Test]
        public void SameSeed_ProducesIdenticalHash()
        {
            var s = MakeSettings();
            var a = new TerrainPipeline().Run(s, preview: false);
            var b = new TerrainPipeline().Run(s, preview: false);
            Assert.AreEqual(a.ComputeHash(), b.ComputeHash());
        }

        // spec: シード変更
        [Test]
        public void DifferentSeed_ProducesDifferentTerrain()
        {
            var a = new TerrainPipeline().Run(MakeSettings(1), false);
            var b = new TerrainPipeline().Run(MakeSettings(2), false);
            Assert.AreNotEqual(a.ComputeHash(), b.ComputeHash());
        }

        // spec: 侵食による谷の形成 (フローフィールドが出力される)
        [Test]
        public void Erosion_OutputsFlowField()
        {
            var f = new TerrainPipeline().Run(MakeSettings(), false);
            Assert.IsTrue(f.TryGetField(WorldHeightField.FieldFlow, out var flow));
            float sum = 0f;
            foreach (var v in flow) sum += v;
            Assert.Greater(sum, 0f, "フローが空です");
        }

        // spec: バイオーム分類 (高度による遷移)
        [Test]
        public void Biome_TransitionsByHeight()
        {
            var f = new TerrainPipeline().Run(MakeSettings(), false);
            Assert.IsTrue(f.TryGetField(WorldHeightField.FieldBiome, out var biome));
            bool hasLow = false, hasHigh = false;
            for (int i = 0; i < biome.Length; i++)
            {
                if ((int)biome[i] == 0) hasLow = true;
                if ((int)biome[i] == 1) hasHigh = true;
            }
            Assert.IsTrue(hasLow || hasHigh, "バイオームが 1 種も分類されていない");
        }

        // spec: 外部ハイトマップのインポート (リサンプル)
        [Test]
        public void Resample_Bilinear()
        {
            var src = new float[] { 0f, 1f, 0f, 1f }; // 2x2
            var dst = new WorldHeightField(3, 3, 1f, 100f, Vector2.zero);
            TerrainPipeline.Resample(src, 2, dst);
            Assert.AreEqual(0.5f, dst.Sample(1, 1), 1e-4f); // 中心は平均
            Assert.AreEqual(0f, dst.Sample(0, 0), 1e-4f);
            Assert.AreEqual(1f, dst.Sample(2, 2), 1e-4f);
        }

        // spec: プレビューモード (同シード系列)
        [Test]
        public void Preview_RunsAtLowerResolution()
        {
            var s = MakeSettings();
            var full = new TerrainPipeline().Run(s, false);
            var prev = new TerrainPipeline().Run(s, true);
            Assert.Less(prev.Width, full.Width);
        }
    }
}
