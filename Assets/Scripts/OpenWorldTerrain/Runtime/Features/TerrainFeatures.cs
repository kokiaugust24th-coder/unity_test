using UnityEngine;
using UnityEngine.Splines;

namespace OpenWorldTerrain
{
    /// <summary>道: スプラインに沿って整地・法面・スプラット上書き・散布抑制 (spec: 道の生成)。</summary>
    [RequireComponent(typeof(SplineContainer))]
    public class RoadFeature : MonoBehaviour
    {
        [Min(1f)] public float width = 6f;
        [Min(0.5f)] public float shoulderWidth = 8f;   // 法面幅
        [Range(1f, 45f)] public float maxGradeDeg = 12f;
        [Tooltip("スプラットで使う TerrainLayer (道マテリアル)")]
        public TerrainLayer roadLayer;
        public SplineContainer Spline => GetComponent<SplineContainer>();
    }

    /// <summary>川: 河道彫り込み + 水面メッシュ (spec: 川の生成)。</summary>
    [RequireComponent(typeof(SplineContainer))]
    public class RiverFeature : MonoBehaviour
    {
        [Min(1f)] public float width = 10f;
        [Min(0.2f)] public float depth = 2.5f;
        [Min(0.5f)] public float bankWidth = 6f;
        [Min(0f)] public float waterOffset = 0.4f; // 水面は河床からこの高さ
        public Material waterMaterial;
        public SplineContainer Spline => GetComponent<SplineContainer>();
    }

    /// <summary>POI: 半径内を平坦化 (spec: POI スタンプ)。</summary>
    public class POIFeature : MonoBehaviour
    {
        [Min(1f)] public float radius = 30f;
        [Min(1f)] public float blendWidth = 20f;
        [Tooltip("true なら transform.position.y を目標高度にする")]
        public bool useTransformHeight = true;
    }
}
