using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace OpenWorld.EditorTools
{
    /// <summary>Addressables 登録ユーティリティ (design.md D3)。</summary>
    public static class AddressablesBakeUtil
    {
        public const string CellGroupName = "OpenWorld-Cells";
        public const string HlodGroupName = "OpenWorld-HLOD";

        public static AddressableAssetSettings GetSettings() =>
            AddressableAssetSettingsDefaultObject.GetSettings(true);

        public static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string groupName)
        {
            var group = settings.FindGroup(groupName);
            if (group == null)
            {
                group = settings.CreateGroup(
                    groupName, false, false, false, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            }
            return group;
        }

        /// <summary>アセットを指定グループ・アドレスで Addressable にする (冪等)。</summary>
        public static void MakeAddressable(string assetPath, string address, string groupName)
        {
            var settings = GetSettings();
            var group = EnsureGroup(settings, groupName);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = address;
        }

        /// <summary>
        /// OpenWorld グループから、実体が存在しない・または現行の Generated フォルダ外を指す
        /// 古いエントリを除去する (フォルダ移動後のアドレス重複対策)。除去数を返す。
        /// </summary>
        public static int PurgeStaleEntries()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return 0;
            int removed = 0;
            foreach (var groupName in new[] { CellGroupName, HlodGroupName })
            {
                var group = settings.FindGroup(groupName);
                if (group == null) continue;
                var entries = new List<AddressableAssetEntry>(group.entries);
                foreach (var e in entries)
                {
                    string path = AssetDatabase.GUIDToAssetPath(e.guid);
                    if (string.IsNullOrEmpty(path) || !path.StartsWith(WorldBaker.GeneratedRoot))
                    {
                        settings.RemoveAssetEntry(e.guid);
                        removed++;
                    }
                }
            }
            if (removed > 0)
                UnityEngine.Debug.Log($"[OpenWorld] 古い Addressables エントリを {removed} 件除去しました。");
            return removed;
        }

        /// <summary>Addressables から登録解除する (存在しなければ何もしない)。</summary>
        public static void RemoveEntry(string assetPath)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (!string.IsNullOrEmpty(guid))
                settings.RemoveAssetEntry(guid);
        }
    }
}
