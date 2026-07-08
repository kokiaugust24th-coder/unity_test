using UnityEngine;

namespace OpenWorld
{
    /// <summary>ワールド分割・ストリーミングの全設定 (design.md D1/D4/D5/D6)。</summary>
    [CreateAssetMenu(menuName = "OpenWorld/World Partition Config", fileName = "WorldPartitionConfig")]
    public class WorldPartitionConfig : ScriptableObject
    {
        [Header("Grid")]
        [Tooltip("セル一辺のサイズ (m)")]
        [Min(16f)] public float cellSize = 256f;

        [Header("Streaming Radii")]
        [Tooltip("この距離以内のセルは Activated (表示・有効)")]
        [Min(0f)] public float activationRadius = 256f;
        [Tooltip("この距離以内のセルは Loaded (メモリ常駐・非表示)")]
        [Min(0f)] public float loadRadius = 512f;
        [Tooltip("HLOD 表示半径 = loadRadius * この倍率")]
        [Min(1f)] public float hlodRadiusMultiplier = 4f;
        [Tooltip("アンロード境界のヒステリシス幅 (m)。境界振動を防ぐ")]
        [Min(0f)] public float hysteresis = 32f;

        [Header("Async Budget")]
        [Tooltip("同時 in-flight ロード数の上限")]
        [Min(1)] public int maxInFlightLoads = 4;
        [Tooltip("1 フレームあたりのメインスレッド処理予算 (ms)")]
        [Min(0.1f)] public float frameBudgetMs = 2f;
        [Tooltip("ストリーミング判定の実行間隔 (秒)")]
        [Min(0.02f)] public float evaluationInterval = 0.1f;

        [Header("Activation")]
        [Tooltip("アクティベート時に StaticBatchingUtility.Combine を適用する")]
        public bool staticBatchOnActivate = true;
        [Tooltip("プレイ開始時にシーン内の OpenWorldRegion (オーサリングルート) を自動で無効化する")]
        public bool disableAuthoringRegionOnPlay = true;

        [Header("HLOD Bake")]
        [Tooltip("HLOD テクスチャアトラスの最大解像度")]
        public int hlodAtlasSize = 2048;
        [Tooltip("HLOD で影を描画する")]
        public bool hlodCastShadows = false;

        public float HlodRadius => loadRadius * hlodRadiusMultiplier;
    }
}
