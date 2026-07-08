using System;

namespace OpenWorldTerrain
{
    /// <summary>
    /// 生成パイプラインの 1 ステージ (design.md D1/D4)。差替え・将来拡張の分離点。
    /// 実装は決定論的であること (シードは DeterministicRandom から導出)。
    /// </summary>
    public interface IHeightFieldStage
    {
        string Name { get; }
        /// <summary>field を in-place で加工する。progress は 0..1 (キャンセル判定は呼び出し側)。</summary>
        void Execute(WorldHeightField field, TerrainGenerationSettings settings, Action<float> progress);
    }
}
