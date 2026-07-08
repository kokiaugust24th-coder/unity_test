using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenWorld.EditorTools
{
    /// <summary>
    /// 「Environment」のような 1 つのコンテナに全ワールドがまとまっている場合、
    /// そのままでは 1 ストリーミング単位 = AlwaysLoaded になってしまう。
    /// このツールは選択したコンテナの子を WorldRegion 直下へ展開する。
    /// プレハブインスタンスは自動でアンパックする (内部の付け替えは Unity が禁止しているため)。
    /// </summary>
    public static class GroupFlattenTool
    {
        const int UndoLimit = 300; // これを超える子は Undo なし高速モードを提案

        [MenuItem("Tools/OpenWorld/変換ツール/選択グループを展開 (子をリージョン直下へ)", false, 20)]
        public static void FlattenSelected()
        {
            var region = Object.FindFirstObjectByType<OpenWorldRegion>(FindObjectsInactive.Include);
            if (region == null)
            {
                EditorUtility.DisplayDialog("OpenWorld", "OpenWorldRegion がシーンにありません。", "OK");
                return;
            }

            // 対象コンテナと子の数を先に集計
            var containers = new List<Transform>();
            int totalChildren = 0;
            foreach (var go in Selection.gameObjects)
            {
                if (go == null || go == region.gameObject) continue;
                if (go.GetComponent<OpenWorldRegion>() != null) continue;
                containers.Add(go.transform);
                totalChildren += go.transform.childCount;
            }
            if (totalChildren == 0)
            {
                EditorUtility.DisplayDialog("OpenWorld",
                    "展開する子がありません。Hierarchy でコンテナ (例: Environment) を選択してから実行してください。",
                    "OK");
                return;
            }

            bool useUndo = true;
            if (totalChildren > UndoLimit)
            {
                int choice = EditorUtility.DisplayDialogComplex("OpenWorld",
                    $"子オブジェクトが {totalChildren} 個あります。\n" +
                    "Undo 対応のまま実行すると時間がかかる場合があります。",
                    "高速実行 (Undo なし)", "キャンセル", "Undo 付きで実行");
                if (choice == 1) return;
                useUndo = choice == 2;
            }

            int moved = 0, failed = 0, removedContainers = 0;
            try
            {
                foreach (var t in containers)
                {
                    // プレハブインスタンスは子の付け替えができないため先にアンパック
                    var go = t.gameObject;
                    if (PrefabUtility.IsPartOfPrefabInstance(go))
                    {
                        var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
                        if (root != null)
                        {
                            PrefabUtility.UnpackPrefabInstance(root,
                                PrefabUnpackMode.OutermostRoot,
                                useUndo ? InteractionMode.UserAction : InteractionMode.AutomatedAction);
                            Debug.Log($"[OpenWorld] プレハブインスタンス '{root.name}' をアンパックしました。");
                        }
                    }

                    // スナップショットしてから移動 (途中で失敗してもループしない)
                    var children = new List<Transform>(t.childCount);
                    foreach (Transform c in t) children.Add(c);

                    for (int i = 0; i < children.Count; i++)
                    {
                        if (EditorUtility.DisplayCancelableProgressBar(
                                "OpenWorld 展開", $"{children[i].name} ({moved + failed + 1}/{totalChildren})",
                                (moved + failed) / (float)totalChildren))
                            break;

                        bool ok = MoveToRegion(children[i], region.transform, useUndo);
                        if (ok) moved++; else failed++;
                    }

                    // 空になったコンテナ (Transform 以外なし) を削除
                    if (t != null && t.childCount == 0
                        && t.GetComponents<Component>().Length == 1)
                    {
                        if (useUndo) Undo.DestroyObjectImmediate(t.gameObject);
                        else Object.DestroyImmediate(t.gameObject);
                        removedContainers++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorSceneManager.MarkSceneDirty(region.gameObject.scene);

            string msg = $"{moved} オブジェクトをリージョン直下へ展開しました。";
            if (failed > 0) msg += $"\n移動できなかったもの: {failed} 件 (Console 参照)";
            if (removedContainers > 0) msg += $"\n空コンテナ削除: {removedContainers} 件";
            msg += "\n\nWorld Baker で「全体リベイク」してください。";
            EditorUtility.DisplayDialog("OpenWorld", msg, "OK");
        }

        static bool MoveToRegion(Transform child, Transform region, bool useUndo)
        {
            if (child == null) return false;

            // 子自身がプレハブインスタンスの内部 (= 親コンテナごとプレハブ) の場合はここでも検出
            if (PrefabUtility.IsPartOfPrefabInstance(child.gameObject)
                && PrefabUtility.GetOutermostPrefabInstanceRoot(child.gameObject) != child.gameObject)
            {
                Debug.LogWarning($"[OpenWorld] '{child.name}' はプレハブインスタンス内部のため移動できません。" +
                                 "親プレハブをアンパックしてください。", child);
                return false;
            }

            var before = child.parent;
            if (useUndo) Undo.SetTransformParent(child, region, "OpenWorld Flatten");
            else child.SetParent(region, true);

            if (child.parent == before && before != region)
            {
                Debug.LogWarning($"[OpenWorld] '{child.name}' を移動できませんでした。", child);
                return false;
            }
            return true;
        }

        [MenuItem("Tools/OpenWorld/変換ツール/選択グループを展開 (子をリージョン直下へ)", true)]
        static bool Validate() => Selection.gameObjects.Length > 0 && !Application.isPlaying;
    }
}
