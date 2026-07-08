using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenWorld
{
    /// <summary>
    /// データレイヤーのランタイム状態 (spec: world-data-layers / ランタイムレイヤー切替)。
    /// </summary>
    public static class DataLayerManager
    {
        static readonly Dictionary<string, bool> _states = new Dictionary<string, bool>();

        /// <summary>レイヤー状態が変化したときに発火 (name, enabled)。</summary>
        public static event Action<string, bool> LayerChanged;

        public static void Initialize(IEnumerable<WorldManifest.LayerDef> defs)
        {
            _states.Clear();
            if (defs == null) return;
            foreach (var d in defs)
                if (!string.IsNullOrEmpty(d.name))
                    _states[d.name] = d.initiallyEnabled;
        }

        /// <summary>未定義レイヤーは有効扱い。</summary>
        public static bool IsEnabled(string layerName) =>
            !_states.TryGetValue(layerName, out bool enabled) || enabled;

        public static void SetLayerEnabled(string layerName, bool enabled)
        {
            if (string.IsNullOrEmpty(layerName)) return;
            if (_states.TryGetValue(layerName, out bool cur) && cur == enabled) return;
            _states[layerName] = enabled;
            LayerChanged?.Invoke(layerName, enabled);
        }

        /// <summary>
        /// サブツリーの可視判定。レイヤー未所属は常に可視、
        /// 複数所属はいずれか有効なら可視 (OR。spec: 複数レイヤーの OR 評価)。
        /// </summary>
        public static bool IsSubtreeVisible(string[] layerNames)
        {
            if (layerNames == null || layerNames.Length == 0) return true;
            for (int i = 0; i < layerNames.Length; i++)
                if (IsEnabled(layerNames[i]))
                    return true;
            return false;
        }

        public static IReadOnlyDictionary<string, bool> States => _states;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnDomainReload()
        {
            _states.Clear();
            LayerChanged = null;
        }
    }
}
