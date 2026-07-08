using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenWorld.EditorTools
{
    /// <summary>
    /// ベイク済みシーンにランタイム側の必須オブジェクト
    /// (WorldStreamingManager + HUD + StreamingSource) を配置する。
    /// 既存シーン変換フローの仕上げ用。
    /// </summary>
    public static class SceneSetupUtility
    {
        [MenuItem("Tools/OpenWorld/変換ツール/マネージャと HUD をシーンに配置", false, 22)]
        public static void EnsureRuntimeObjects()
        {
            if (Application.isPlaying) return;

            var manifest = OpenWorldPaths.FindManifest();
            var config = OpenWorldPaths.FindConfig();

            if (manifest == null || config == null)
            {
                EditorUtility.DisplayDialog("OpenWorld",
                    "WorldManifest または Config が見つかりません。\n先に World Baker でベイクしてください。", "OK");
                return;
            }

            // マネージャ + HUD
            var manager = Object.FindFirstObjectByType<WorldStreamingManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                var go = new GameObject("WorldStreamingManager");
                Undo.RegisterCreatedObjectUndo(go, "OpenWorld Setup");
                manager = go.AddComponent<WorldStreamingManager>();
            }
            manager.Configure(config, manifest);
            if (manager.GetComponent<StreamingDebugHUD>() == null)
                manager.gameObject.AddComponent<StreamingDebugHUD>();
            EditorUtility.SetDirty(manager);

            // ストリーミングソース (Player 優先)
            string sourceInfo;
            if (Object.FindFirstObjectByType<StreamingSource>(FindObjectsInactive.Include) != null)
            {
                sourceInfo = "StreamingSource: 既存のものを使用";
            }
            else
            {
                var player = GameObject.FindWithTag("Player");
                var target = player != null ? player : Camera.main?.gameObject;
                if (target != null)
                {
                    Undo.AddComponent<StreamingSource>(target);
                    if (player != null && target.GetComponent<StreamingSpawnGate>() == null)
                        Undo.AddComponent<StreamingSpawnGate>(target);
                    sourceInfo = $"StreamingSource: '{target.name}' に追加";
                }
                else
                {
                    sourceInfo = "StreamingSource: 追加先が見つかりません。Player かカメラに手動で追加してください";
                }
            }

            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            EditorUtility.DisplayDialog("OpenWorld",
                $"配置完了:\n・WorldStreamingManager (Config / Manifest 割当済み)\n・StreamingDebugHUD (F3)\n・{sourceInfo}\n\n" +
                "プレイすると WorldRegion が自動無効化され、ストリーミングが始まります。", "OK");
        }
    }
}
