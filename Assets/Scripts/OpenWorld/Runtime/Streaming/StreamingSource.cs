using UnityEngine;

namespace OpenWorld
{
    /// <summary>
    /// プレイヤー・カメラ等に付けるストリーミングソース。
    /// OnEnable/OnDisable で自動登録/解除される。
    /// </summary>
    public class StreamingSource : MonoBehaviour, IStreamingSource
    {
        [Min(0.1f)] public float radiusMultiplier = 1f;
        public int priority = 0;

        [Tooltip("ギズモ表示用。未設定なら WorldStreamingManager から取得")]
        [SerializeField] WorldPartitionConfig configForGizmos;

        public Vector3 Position => transform.position;
        public float RadiusMultiplier => radiusMultiplier;
        public int Priority => priority;

        void OnEnable() => StreamingSourceRegistry.Register(this);
        void OnDisable() => StreamingSourceRegistry.Unregister(this);

#if UNITY_EDITOR
        // spec: world-debug / ストリーミングソースギズモ
        void OnDrawGizmosSelected()
        {
            var config = configForGizmos;
            if (config == null && WorldStreamingManager.Instance != null)
                config = WorldStreamingManager.Instance.Config;
            if (config == null) return;

            float m = radiusMultiplier;
            DrawRing(config.activationRadius * m, new Color(0.2f, 1f, 0.2f));            // Activate
            DrawRing((config.activationRadius + config.hysteresis) * m, new Color(0.2f, 1f, 0.2f, 0.35f));
            DrawRing(config.loadRadius * m, new Color(1f, 0.9f, 0.2f));                  // Load
            DrawRing((config.loadRadius + config.hysteresis) * m, new Color(1f, 0.9f, 0.2f, 0.35f));
            DrawRing(config.HlodRadius * m, new Color(0.4f, 0.6f, 1f));                  // HLOD
        }

        void DrawRing(float radius, Color color)
        {
            Gizmos.color = color;
            const int segments = 64;
            Vector3 c = transform.position;
            Vector3 prev = c + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
#endif
    }
}
