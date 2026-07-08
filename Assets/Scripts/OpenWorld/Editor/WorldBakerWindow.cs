using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OpenWorld.EditorTools
{
    /// <summary>
    /// ベイク実行とセル情報の確認を行うウィンドウ (spec: world-debug / エディタグリッド可視化)。
    /// Tools > OpenWorld > World Baker
    /// </summary>
    public class WorldBakerWindow : EditorWindow
    {
        OpenWorldRegion _region;
        WorldPartitionConfig _config;
        BakeReport _lastReport;
        Vector2 _scroll;
        Vector2 _cellScroll;
        bool _showCells = true;

        [MenuItem("Tools/OpenWorld/World Baker", false, 1)]
        static void Open() => GetWindow<WorldBakerWindow>("World Baker");

        void OnEnable()
        {
            if (_region == null)
                _region = FindFirstObjectByType<OpenWorldRegion>();
            if (_config == null)
                _config = AssetDatabase.FindAssets("t:WorldPartitionConfig")
                    .Select(g => AssetDatabase.LoadAssetAtPath<WorldPartitionConfig>(AssetDatabase.GUIDToAssetPath(g)))
                    .FirstOrDefault();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("OpenWorld ベイクパイプライン", EditorStyles.boldLabel);

            _region = (OpenWorldRegion)EditorGUILayout.ObjectField(
                "オーサリングルート", _region, typeof(OpenWorldRegion), true);
            _config = (WorldPartitionConfig)EditorGUILayout.ObjectField(
                "Config", _config, typeof(WorldPartitionConfig), false);

            if (_config == null && GUILayout.Button("Config を新規作成"))
            {
                var config = CreateInstance<WorldPartitionConfig>();
                HLODBaker.EnsureFolder(SampleSceneSetup.RootFolder);
                AssetDatabase.CreateAsset(config, SampleSceneSetup.ConfigPath);
                AssetDatabase.SaveAssets();
                _config = config;
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_region == null || _config == null || Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("差分ベイク", GUILayout.Height(28)))
                    _lastReport = WorldBaker.Bake(_region, _config, incremental: true);
                if (GUILayout.Button("全体リベイク", GUILayout.Height(28)))
                    _lastReport = WorldBaker.Bake(_region, _config, incremental: false);
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("生成物をすべて削除 (ロールバック)"))
            {
                if (EditorUtility.DisplayDialog("確認",
                        "Generated/ 以下の全ベイク成果物と Addressables 登録を削除します。よろしいですか?",
                        "削除", "キャンセル"))
                {
                    WorldBaker.ClearGenerated();
                    _lastReport = null;
                }
            }

            DrawReport();
            DrawCellList();
        }

        void DrawReport()
        {
            if (_lastReport == null) return;
            EditorGUILayout.Space();
            if (_lastReport.Success)
                EditorGUILayout.HelpBox(
                    $"ベイク完了: {_lastReport.BakedCells} セル生成 / {_lastReport.SkippedCells} セルスキップ (差分なし)",
                    MessageType.Info);
            else
                EditorGUILayout.HelpBox($"ベイク失敗 (成果物はロールバック済み): {_lastReport.Error}",
                    MessageType.Error);

            if (_lastReport.Warnings.Count > 0)
            {
                EditorGUILayout.LabelField($"警告 ({_lastReport.Warnings.Count})", EditorStyles.boldLabel);
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(120));
                foreach (var w in _lastReport.Warnings)
                    EditorGUILayout.HelpBox(w, MessageType.Warning);
                EditorGUILayout.EndScrollView();
            }
        }

        void DrawCellList()
        {
            var manifest = AssetDatabase.LoadAssetAtPath<WorldManifest>(WorldBaker.ManifestPath);
            if (manifest == null) return;

            EditorGUILayout.Space();
            _showCells = EditorGUILayout.Foldout(_showCells, $"ベイク済みセル ({manifest.cells.Count})");
            if (!_showCells) return;

            _cellScroll = EditorGUILayout.BeginScrollView(_cellScroll);
            foreach (var cell in manifest.cells)
            {
                EditorGUILayout.BeginHorizontal("box");
                string label = cell.alwaysLoaded ? "AlwaysLoaded" : $"({cell.x},{cell.z})";
                EditorGUILayout.LabelField(label, GUILayout.Width(100));
                EditorGUILayout.LabelField($"{cell.objectCount} objs", GUILayout.Width(64));
                EditorGUILayout.LabelField(cell.HasHlod ? "HLOD" : "-", GUILayout.Width(44));
                EditorGUILayout.LabelField(cell.layerNames.Length > 0
                    ? string.Join(", ", cell.layerNames) : "(no layers)");
                if (!cell.alwaysLoaded && GUILayout.Button("Focus", GUILayout.Width(50)))
                {
                    var center = new CellCoord(cell.x, cell.z).Center(manifest.cellSize);
                    SceneView.lastActiveSceneView?.Frame(
                        new Bounds(center, Vector3.one * manifest.cellSize), false);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
