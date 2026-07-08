using UnityEngine;

namespace OpenWorld
{
    /// <summary>
    /// ストリーミングソース抽象 (spec: world-streaming / ストリーミングソース抽象)。
    /// 複数登録可能。将来のマルチプレイヤー (サーバー上の複数プレイヤー) や
    /// カットシーンカメラ・先読みポイントへの拡張点。
    /// </summary>
    public interface IStreamingSource
    {
        Vector3 Position { get; }
        /// <summary>半径倍率。1 = 設定値どおり。高速移動体は大きく。</summary>
        float RadiusMultiplier { get; }
        /// <summary>優先度。高いほどロードキューで先に処理される。</summary>
        int Priority { get; }
    }
}
