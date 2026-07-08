using System.Collections.Generic;
using UnityEngine;

namespace OpenWorld
{
    /// <summary>
    /// プレイヤー等に付けると、足元のセルが Activated になるまで移動系コンポーネントを
    /// 無効化して落下を防ぐ。地面ロード完了後に自動で元に戻り、自身は無効化される。
    /// 対象未指定なら同じ GameObject の自分以外の全 MonoBehaviour + CharacterController + Rigidbody を凍結する。
    /// </summary>
    public class StreamingSpawnGate : MonoBehaviour
    {
        [Tooltip("足元セルのロード完了まで無効化するコンポーネント (空なら自動収集)")]
        public Behaviour[] gatedBehaviours = new Behaviour[0];

        [Tooltip("これ以上待たない (秒)。ワールド外スポーン等での永久停止を防ぐ")]
        public float timeoutSeconds = 10f;

        readonly List<Behaviour> _disabledBehaviours = new List<Behaviour>();
        CharacterController _cc;
        Rigidbody _rb;
        bool _rbWasKinematic;
        float _startTime;
        bool _released;

        void Start()
        {
            _startTime = Time.time;

            if (gatedBehaviours != null && gatedBehaviours.Length > 0)
            {
                foreach (var b in gatedBehaviours)
                    if (b != null && b.enabled) { b.enabled = false; _disabledBehaviours.Add(b); }
            }
            else
            {
                foreach (var b in GetComponents<MonoBehaviour>())
                    if (b != null && b != this && b.enabled) { b.enabled = false; _disabledBehaviours.Add(b); }
            }

            _cc = GetComponent<CharacterController>();
            if (_cc != null) _cc.enabled = false;

            _rb = GetComponent<Rigidbody>();
            if (_rb != null)
            {
                _rbWasKinematic = _rb.isKinematic;
                _rb.isKinematic = true;
            }
        }

        void Update()
        {
            if (_released) return;

            bool timeout = Time.time - _startTime > timeoutSeconds;
            if (timeout)
            {
                Debug.LogWarning($"[OpenWorld] StreamingSpawnGate: {timeoutSeconds} 秒待っても足元セルが " +
                                 "Activated になりませんでした。強制解除します (位置がワールド外の可能性)。", this);
                Release();
                return;
            }

            var mgr = WorldStreamingManager.Instance;
            if (mgr == null || mgr.Manifest == null) return;

            var coord = CellCoord.FromWorld(transform.position, mgr.Manifest.cellSize);
            if (!mgr.TryGetCellState(coord, out var state))
            {
                Release(); // 足元にセルが存在しない (ワールド外/Always Loaded 上) → 待つ意味なし
                return;
            }
            if (state == CellState.Activated)
                Release();
        }

        void Release()
        {
            _released = true;
            foreach (var b in _disabledBehaviours)
                if (b != null) b.enabled = true;
            if (_cc != null) _cc.enabled = true;
            if (_rb != null) _rb.isKinematic = _rbWasKinematic;
            enabled = false;
        }
    }
}
