using System.Collections.Generic;
using NUnit.Framework;

namespace OpenWorld.Tests
{
    public class DataLayerTests
    {
        [SetUp]
        public void SetUp()
        {
            DataLayerManager.Initialize(new List<WorldManifest.LayerDef>
            {
                new WorldManifest.LayerDef { name = "QuestA", initiallyEnabled = false },
                new WorldManifest.LayerDef { name = "Village", initiallyEnabled = true },
            });
        }

        // spec: world-data-layers / 無効レイヤーの抑制
        [Test]
        public void DisabledLayerOnly_IsHidden()
        {
            Assert.IsFalse(DataLayerManager.IsSubtreeVisible(new[] { "QuestA" }));
        }

        // spec: world-data-layers / 複数レイヤーの OR 評価
        [Test]
        public void MultiLayer_VisibleIfAnyEnabled()
        {
            Assert.IsTrue(DataLayerManager.IsSubtreeVisible(new[] { "QuestA", "Village" }));
        }

        [Test]
        public void NoLayers_AlwaysVisible()
        {
            Assert.IsTrue(DataLayerManager.IsSubtreeVisible(new string[0]));
            Assert.IsTrue(DataLayerManager.IsSubtreeVisible(null));
        }

        [Test]
        public void UnknownLayer_TreatedAsEnabled()
        {
            Assert.IsTrue(DataLayerManager.IsSubtreeVisible(new[] { "Undefined" }));
        }

        // spec: world-data-layers / ランタイムレイヤー切替
        [Test]
        public void SetLayerEnabled_FiresEventOnceAndUpdatesState()
        {
            int fired = 0;
            DataLayerManager.LayerChanged += (_, _) => fired++;

            DataLayerManager.SetLayerEnabled("QuestA", true);
            DataLayerManager.SetLayerEnabled("QuestA", true); // 変化なし → 発火しない

            Assert.AreEqual(1, fired);
            Assert.IsTrue(DataLayerManager.IsEnabled("QuestA"));
            Assert.IsTrue(DataLayerManager.IsSubtreeVisible(new[] { "QuestA" }));
        }
    }
}
