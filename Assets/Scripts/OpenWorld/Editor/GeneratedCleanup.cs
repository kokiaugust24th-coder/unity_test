using UnityEditor;
using UnityEngine;

namespace OpenWorld.EditorTools
{
    /// <summary>
    /// フォルダ移動などで残った古いベイク生成物と Addressables 重複登録を掃除する。
    /// (同一アドレスの重複はロード失敗・誤ロードの原因になる)
    /// </summary>
    public static class GeneratedCleanup
    {
        [MenuItem("Tools/OpenWorld/メンテナンス/旧生成物と重複登録を掃除", false, 60)]
        public static void CleanStale()
        {
            int deletedFolders = 0;

            // 現行の GeneratedRoot 以外にある WorldManifest → その Generated フォルダごと古い生成物
            foreach (var guid in AssetDatabase.FindAssets("t:WorldManifest"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || path == WorldBaker.ManifestPath) continue;
                string folder = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(folder) || folder == WorldBaker.GeneratedRoot) continue;

                if (EditorUtility.DisplayDialog("OpenWorld",
                        $"古い生成物フォルダを削除します:\n{folder}", "削除", "スキップ"))
                {
                    AssetDatabase.DeleteAsset(folder);
                    deletedFolders++;
                }
            }

            // 現行フォルダ外を指す Addressables エントリを除去
            int purged = AddressablesBakeUtil.PurgeStaleEntries();

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("OpenWorld",
                $"掃除完了:\n・旧生成物フォルダ削除: {deletedFolders} 件\n・古い Addressables エントリ除去: {purged} 件\n\n" +
                "このあと「全体リベイク」を実行してください。", "OK");
        }
    }
}
