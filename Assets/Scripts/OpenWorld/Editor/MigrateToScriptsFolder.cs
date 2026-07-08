using UnityEditor;
using UnityEngine;

namespace OpenWorld.EditorTools
{
    /// <summary>
    /// Assets/OpenWorld に残っている生成物・設定を Assets/Scripts/OpenWorld へ移動統合する
    /// 一回限りの移行処理。AssetDatabase.MoveAsset を使うため GUID (シーン参照) は維持される。
    /// </summary>
    public static class MigrateToScriptsFolder
    {
        const string OldRoot = "Assets/OpenWorld";

        [MenuItem("Tools/OpenWorld/Assets-OpenWorld を Scripts-OpenWorld に統合")]
        public static void Migrate()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("OpenWorld", "プレイモード中は実行できません。", "OK");
                return;
            }
            if (!AssetDatabase.IsValidFolder(OldRoot))
            {
                EditorUtility.DisplayDialog("OpenWorld", $"{OldRoot} は存在しません。移行済みです。", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("OpenWorld",
                    $"{OldRoot} の内容を {SampleSceneSetup.RootFolder} へ移動します。\n" +
                    "移動先に古い同名フォルダがある場合は削除されます。\n" +
                    "GUID は維持されるためシーン参照はそのまま有効です。", "実行", "キャンセル"))
                return;

            int moved = 0, failed = 0;
            (string from, string to)[] moves =
            {
                (OldRoot + "/Generated", WorldBaker.GeneratedRoot),
                (OldRoot + "/Sample", SampleSceneSetup.SampleFolder),
                (OldRoot + "/WorldPartitionConfig.asset", SampleSceneSetup.ConfigPath),
            };

            foreach (var (from, to) in moves)
            {
                bool fromExists = AssetDatabase.IsValidFolder(from) ||
                                  AssetDatabase.LoadMainAssetAtPath(from) != null;
                if (!fromExists) continue;

                // 移動先に古い残骸 (フォルダ移動前の重複) があれば先に削除
                bool toExists = AssetDatabase.IsValidFolder(to) ||
                                AssetDatabase.LoadMainAssetAtPath(to) != null;
                if (toExists)
                {
                    Debug.Log($"[OpenWorld] 古い残骸を削除: {to}");
                    AssetDatabase.DeleteAsset(to);
                }

                string error = AssetDatabase.MoveAsset(from, to);
                if (string.IsNullOrEmpty(error))
                {
                    Debug.Log($"[OpenWorld] 移動: {from} → {to}");
                    moved++;
                }
                else
                {
                    Debug.LogError($"[OpenWorld] 移動失敗: {from} → {to} : {error}");
                    failed++;
                }
            }

            // 空になった旧フォルダを削除
            if (failed == 0 && AssetDatabase.FindAssets("", new[] { OldRoot }).Length == 0)
                AssetDatabase.DeleteAsset(OldRoot);

            // 実体が消えた・場所が古いままの Addressables エントリを掃除
            int purged = AddressablesBakeUtil.PurgeStaleEntries();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("OpenWorld",
                $"統合完了:\n・移動 {moved} 件 / 失敗 {failed} 件\n・古い Addressables エントリ除去 {purged} 件\n\n" +
                "確認: WorldStreamingManager の Config / Manifest 参照が有効なこと\n" +
                "(GUID 維持のため通常はそのまま動きます)",
                "OK");
        }
    }
}
