using NUnit.Framework;
using UnityEngine;

namespace OpenWorld.Tests
{
    public class CellCoordTests
    {
        // spec: world-streaming / 座標からセルへの解決
        [Test]
        public void FromWorld_ResolvesCell()
        {
            var c = CellCoord.FromWorld(new Vector3(300f, 0f, -100f), 256f);
            Assert.AreEqual(new CellCoord(1, -1), c);
        }

        [Test]
        public void FromWorld_OriginIsCellZero()
        {
            Assert.AreEqual(new CellCoord(0, 0), CellCoord.FromWorld(new Vector3(0.1f, 50f, 0.1f), 256f));
            Assert.AreEqual(new CellCoord(-1, -1), CellCoord.FromWorld(new Vector3(-0.1f, 0f, -0.1f), 256f));
        }

        [Test]
        public void DistanceXZ_InsideCellIsZero()
        {
            var c = new CellCoord(0, 0);
            Assert.AreEqual(0f, c.DistanceXZ(new Vector3(128f, 999f, 128f), 256f), 1e-4f);
        }

        [Test]
        public void DistanceXZ_UsesClosestPointOnAabb()
        {
            var c = new CellCoord(1, 0); // X: 256..512, Z: 0..256
            // セル左端 (256, z=128) までの距離 = 56
            Assert.AreEqual(56f, c.DistanceXZ(new Vector3(200f, 0f, 128f), 256f), 1e-3f);
            // コーナー距離
            float expected = Mathf.Sqrt(56f * 56f + 100f * 100f);
            Assert.AreEqual(expected, c.DistanceXZ(new Vector3(200f, 0f, -100f), 256f), 1e-3f);
        }

        [Test]
        public void CenterAndMinCorner()
        {
            var c = new CellCoord(1, -1);
            Assert.AreEqual(new Vector3(256f, 0f, -256f), c.MinCorner(256f));
            Assert.AreEqual(new Vector3(384f, 0f, -128f), c.Center(256f));
        }
    }
}
