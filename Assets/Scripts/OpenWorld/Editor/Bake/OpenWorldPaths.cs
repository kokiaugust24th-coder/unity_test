using UnityEditor;

namespace OpenWorld.EditorTools
{
    /// <summary>
    /// OpenWorld が使用するプロジェクト内パスと共通アセット検索の単一定義。
    /// パスを変更する場合はここだけを編集する。
    /// </summary>
    public static class OpenWorldPaths
    {
        public const string RootFolder = "Assets/Scripts/OpenWorld";

        // ベイク生成物 (削除すれば完全ロールバック)
        public const string GeneratedRoot = RootFolder + "/Generated";
        public const string CellsFolder = GeneratedRoot + "/Cells";
        public const string HlodFolder = GeneratedRoot + "/HLOD";
        public const string ManifestPath = GeneratedRoot + "/WorldManifest.asset";
        public const string CachePath = GeneratedRoot + "/BakeCache.asset";

        // ツール生成物
        public const string SplitMeshesFolder = RootFolder + "/SplitMeshes";

        // 設定・サンプル
        public const string ConfigPath = RootFolder + "/WorldPartitionConfig.asset";
        public const string SampleFolder = RootFolder + "/Sample";

        /// <summary>既定パス → プロジェクト全体の順で Config を探す。</summary>
        public static WorldPartitionConfig FindConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<WorldPartitionConfig>(ConfigPath);
            if (config != null) return config;
            foreach (var guid in AssetDatabase.FindAssets("t:WorldPartitionConfig"))
                return AssetDatabase.LoadAssetAtPath<WorldPartitionConfig>(AssetDatabase.GUIDToAssetPath(guid));
            return null;
        }

        public static WorldManifest FindManifest() =>
            AssetDatabase.LoadAssetAtPath<WorldManifest>(ManifestPath);
    }
}
