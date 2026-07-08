using Unity.Mathematics;

namespace OpenWorldTerrain
{
    /// <summary>
    /// 決定論 RNG 派生 (spec: 決定論的な高さマップ合成)。
    /// シード + ステージ ID + セル座標から独立した乱数列を導出する。
    /// </summary>
    public static class DeterministicRandom
    {
        public static Random ForStage(uint worldSeed, uint stageId) =>
            new Random(math.max(1u, math.hash(new uint2(worldSeed, stageId))));

        public static Random ForCell(uint worldSeed, uint stageId, int cellX, int cellZ) =>
            new Random(math.max(1u, math.hash(new uint4(worldSeed, stageId, (uint)cellX, (uint)cellZ))));
    }
}
