using UnityEngine;

namespace OpenWorld
{
    /// <summary>
    /// ランタイム統計 HUD + デバッグコマンド (spec: world-debug)。
    /// 開発ビルド/エディタでのみ描画され、リリースビルドではコードごと除外される。
    /// </summary>
    public class StreamingDebugHUD : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool _visible = true;
        Rect _rect = new Rect(10, 10, 300, 10);

        void Update()
        {
            // F3 で表示切替 (新 Input System)
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f3Key.wasPressedThisFrame) _visible = !_visible;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F3)) _visible = !_visible;
#endif
        }

        void OnGUI()
        {
            if (!_visible) return;
            var mgr = WorldStreamingManager.Instance;
            if (mgr == null)
            {
                GUI.Label(new Rect(10, 10, 400, 24), "[OpenWorld] WorldStreamingManager なし");
                return;
            }

            const int windowId = 0x0731D; // 固定 ID (HUD は 1 つ想定)
            _rect = GUILayout.Window(windowId, _rect, id => DrawWindow(mgr), "OpenWorld Streaming");
        }

        void DrawWindow(WorldStreamingManager mgr)
        {
            var s = mgr.GetStats();
            GUILayout.Label($"Cells  U:{s.Unloaded}  Lo:{s.Loading}  L:{s.Loaded}  A:{s.Activated}");
            GUILayout.Label($"Queue:{s.QueuedLoads}  InFlight:{s.InFlight}  HLOD:{s.HlodShown}");
            GUILayout.Label($"Budget: {s.LastBudgetUsedMs:F2} / {mgr.Config.frameBudgetMs:F2} ms");
            GUILayout.Label($"Handles: {s.ActiveHandles}");

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(mgr.IsPaused ? "Resume" : "Pause"))
                mgr.SetPaused(!mgr.IsPaused);
            if (GUILayout.Button(mgr.IsForceLoadAll ? "Normal Load" : "Load All"))
                mgr.SetForceLoadAll(!mgr.IsForceLoadAll);
            GUILayout.EndHorizontal();

            // データレイヤー切替
            if (mgr.Manifest != null && mgr.Manifest.layers.Count > 0)
            {
                GUILayout.Space(4);
                GUILayout.Label("Data Layers:");
                foreach (var def in mgr.Manifest.layers)
                {
                    bool cur = DataLayerManager.IsEnabled(def.name);
                    bool next = GUILayout.Toggle(cur, def.name);
                    if (next != cur) DataLayerManager.SetLayerEnabled(def.name, next);
                }
            }
            GUI.DragWindow();
        }
#endif
    }
}
