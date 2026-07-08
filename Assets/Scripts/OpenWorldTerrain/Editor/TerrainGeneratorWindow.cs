using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using OpenWorld;
using OpenWorld.EditorTools;

namespace OpenWorldTerrain.EditorTools
{
    /// <summary>
    /// 地形生成ウィンドウ (spec: terrain-authoring / 生成ウィンドウ)。
    /// プレビュー → フル生成 → 手動編集キャプチャ → タイル書き出しの反復を 1 画面で行う。
    /// Tools > OpenWorld > Terrain Generator
    /// </summary>
    public class TerrainGeneratorWindow : EditorWindow
    {
        TerrainGenerationSettings _settings;
        string _externalHeightmap = "";
        Texture2D _lockMask;
        bool _partialRegen;
        Vector2Int _regenMin, _regenMax = new Vector2Int(1, 1); // セル座標
        int _viewIndex;
        WorldHeightField _field;
        Texture2D _preview;
        readonly List<string> _warnings = new List<string>();
        Vector2 _scroll;

        [MenuItem("Tools/OpenWorld/Terrain Generator", false, 4)]
        static void Open() => GetWindow<TerrainGeneratorWindow>("Terrain Generator");

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _settings = (TerrainGenerationSettings)EditorGUILayout.ObjectField(
                "Settings", _settings, typeof(TerrainGenerationSettings), false);
            if (_settings == null)
            {
                if (GUILayout.Button("Settings を新規作成"))
                {
                    var s = CreateInstance<TerrainGenerationSettings>();
                    AssetDatabase.CreateAsset(s, "Assets/Scripts/OpenWorldTerrain/TerrainGenerationSettings.asset");
                    AssetDatabase.SaveAssets();
                    _settings = s;
                }
                EditorGUILayout.EndScrollView();
                return;
            }

            // 外部インポート (補助入力)
            EditorGUILayout.BeginHorizontal();
            _externalHeightmap = EditorGUILayout.TextField("外部ハイトマップ (任意)", _externalHeightmap);
            if (GUILayout.Button("...", GUILayout.Width(28)))
                _externalHeightmap = EditorUtility.OpenFilePanel("Heightmap", "", "raw,r16,exr,png") ?? "";
            EditorGUILayout.EndHorizontal();

            _lockMask = (Texture2D)EditorGUILayout.ObjectField(
                new GUIContent("ロックマスク (R>0.5 で固定)"), _lockMask, typeof(Texture2D), false);

            _partialRegen = EditorGUILayout.ToggleLeft("部分再生成 (セル範囲)", _partialRegen);
            if (_partialRegen)
            {
                _regenMin = EditorGUILayout.Vector2IntField("Min セル", _regenMin);
                _regenMax = EditorGUILayout.Vector2IntField("Max セル", _regenMax);
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("プレビュー生成", GUILayout.Height(26))) Generate(true);
                if (GUILayout.Button("フル生成", GUILayout.Height(26))) Generate(false);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("手動編集をキャプチャ")) CaptureManualEdits();
                using (new EditorGUI.DisabledScope(_field == null))
                    if (GUILayout.Button("タイル書き出し + ベイク統合", GUILayout.Height(26))) ExportTiles();
                EditorGUILayout.EndHorizontal();
            }

            // フィールド可視化 (spec: フィールド可視化デバッガ)
            if (_field != null)
            {
                int newView = EditorGUILayout.Popup("表示フィールド", _viewIndex, FieldVisualizer.Views);
                if (newView != _viewIndex || _preview == null)
                {
                    _viewIndex = newView;
                    _preview = FieldVisualizer.Render(_field, FieldVisualizer.Views[_viewIndex]);
                }
                if (_preview != null)
                {
                    float size = Mathf.Min(position.width - 24, 512);
                    var rect = GUILayoutUtility.GetRect(size, size);
                    EditorGUI.DrawPreviewTexture(rect, _preview, null, ScaleMode.ScaleToFit);
                }
            }

            foreach (var w in _warnings)
                EditorGUILayout.HelpBox(w, MessageType.Warning);
            EditorGUILayout.EndScrollView();
        }

        void Generate(bool preview)
        {
            _warnings.Clear();
            var pipeline = new TerrainPipeline();
            pipeline.Features = FeatureExtractor.ExtractFromScene(out _);

            float[] ext = null;
            int extW = 0;
            if (!string.IsNullOrEmpty(_externalHeightmap))
                (ext, extW) = HeightFieldImporter.Load(_externalHeightmap);

            try
            {
                _field = pipeline.Run(_settings, preview, ext, extW,
                    (stage, p) => EditorUtility.DisplayProgressBar("Terrain 生成", stage, p));
            }
            finally { EditorUtility.ClearProgressBar(); }

            _warnings.AddRange(pipeline.Warnings);

            if (!preview)
            {
                // ベースライン (純生成結果) を保存 → 差分キャプチャの基準
                TerrainAuthoringStore.Save(TerrainAuthoringStore.BaselinePath,
                    (float[])_field.Heights.Clone(), _field.Width, _field.Height);
                ApplyManualDiff(_field);
                ApplyLockMask(_field);
                ApplyPartialRegen(_field);
                new BiomeStage().Execute(_field, _settings, null); // 差分適用後に再分類
            }
            _preview = null;
            Repaint();
        }

        /// <summary>手動差分の再適用 (spec: 手動差分レイヤー)。急勾配は警告。</summary>
        void ApplyManualDiff(WorldHeightField f)
        {
            if (!TerrainAuthoringStore.TryLoad(TerrainAuthoringStore.ManualDiffPath,
                    out var diff, out int w, out int h)) return;
            if (w != f.Width || h != f.Height)
            {
                _warnings.Add("手動差分の解像度が現在の設定と不一致のためスキップしました");
                return;
            }
            int steep = 0;
            float talus01 = Mathf.Tan(60f * Mathf.Deg2Rad) * f.MetersPerPixel / f.MaxAltitude;
            for (int i = 0; i < diff.Length; i++)
            {
                if (diff[i] == 0f) continue;
                f.Heights[i] = Mathf.Clamp01(f.Heights[i] + diff[i]);
                if (Mathf.Abs(diff[i]) > talus01 * 4f) steep++;
            }
            if (steep > 0)
                _warnings.Add($"手動差分の適用で急勾配の可能性が {steep} px あります (spec: 不整合の警告)");
        }

        /// <summary>ロック領域はベースラインで固定し、散布からも除外 (spec: ロックマスク)。</summary>
        void ApplyLockMask(WorldHeightField f)
        {
            if (_lockMask == null) return;
            if (!_lockMask.isReadable)
            {
                _warnings.Add("ロックマスクが Readable ではありません (Import Settings で Read/Write を有効に)");
                return;
            }
            bool hasBaseline = TerrainAuthoringStore.TryLoad(TerrainAuthoringStore.BaselinePath,
                out var baseline, out int bw, out int bh) && bw == f.Width && bh == f.Height;
            var lockField = f.GetOrCreateField(WorldHeightField.FieldLock);
            for (int z = 0; z < f.Height; z++)
            for (int x = 0; x < f.Width; x++)
            {
                float v = _lockMask.GetPixelBilinear(x / (float)f.Width, z / (float)f.Height).r;
                int i = f.Index(x, z);
                lockField[i] = v;
                if (v > 0.5f && hasBaseline)
                    f.Heights[i] = baseline[i];
            }
        }

        /// <summary>範囲外は前回書き出し結果を維持し、境界をフェザーで接続 (spec: 部分再生成)。</summary>
        void ApplyPartialRegen(WorldHeightField f)
        {
            if (!_partialRegen) return;
            var config = OpenWorldPaths.FindConfig();
            if (config == null) return;
            if (!TerrainAuthoringStore.TryLoad(TerrainAuthoringStore.Folder + "/lastexport.bytes",
                    out var prev, out int w, out int h) || w != f.Width || h != f.Height)
            {
                _warnings.Add("前回の書き出し結果が無いため部分再生成をスキップしました");
                return;
            }
            float pxPerCell = config.cellSize / f.MetersPerPixel;
            float feather = 32f;
            var min = new Vector2(_regenMin.x * pxPerCell, _regenMin.y * pxPerCell);
            var max = new Vector2((_regenMax.x + 1) * pxPerCell, (_regenMax.y + 1) * pxPerCell);
            for (int z = 0; z < f.Height; z++)
            for (int x = 0; x < f.Width; x++)
            {
                float dx = Mathf.Max(min.x - x, x - max.x, 0f);
                float dz = Mathf.Max(min.y - z, z - max.y, 0f);
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                float blend = 1f - Mathf.SmoothStep(0f, 1f, dist / feather); // 1=新規, 0=既存
                int i = f.Index(x, z);
                f.Heights[i] = Mathf.Lerp(prev[i], f.Heights[i], blend);
            }
        }

        /// <summary>現在のタイル群とベースラインの差 = 手動編集として記録。</summary>
        void CaptureManualEdits()
        {
            _warnings.Clear();
            if (!TerrainAuthoringStore.TryLoad(TerrainAuthoringStore.BaselinePath,
                    out var baseline, out int w, out int h))
            {
                _warnings.Add("ベースラインがありません。先にフル生成してください");
                return;
            }
            var config = OpenWorldPaths.FindConfig();
            var current = ReadCurrentTiles(w, h, config, baseline);
            var diff = new float[baseline.Length];
            int edited = 0;
            for (int i = 0; i < diff.Length; i++)
            {
                diff[i] = current[i] - baseline[i];
                if (Mathf.Abs(diff[i]) > 1e-5f) edited++;
            }
            TerrainAuthoringStore.Save(TerrainAuthoringStore.ManualDiffPath, diff, w, h);
            Debug.Log($"[Terrain] 手動編集をキャプチャ: {edited} px");
        }

        float[] ReadCurrentTiles(int w, int h, WorldPartitionConfig config, float[] fallback)
        {
            var result = (float[])fallback.Clone();
            if (config == null || _settings == null) return result;
            float mpp = _settings.metersPerPixel;
            int tiles = Mathf.RoundToInt(_settings.worldSize / config.cellSize);
            int tileRes = Mathf.RoundToInt(config.cellSize / mpp) + 1;
            for (int cz = 0; cz < tiles; cz++)
            for (int cx = 0; cx < tiles; cx++)
            {
                var data = AssetDatabase.LoadAssetAtPath<TerrainData>(
                    $"{TerrainTileExporter.TerrainFolder}/TerrainData_{cx}_{cz}.asset");
                if (data == null) continue;
                var heights = data.GetHeights(0, 0, tileRes, tileRes);
                int px0 = Mathf.RoundToInt(cx * config.cellSize / mpp);
                int pz0 = Mathf.RoundToInt(cz * config.cellSize / mpp);
                for (int z = 0; z < tileRes; z++)
                for (int x = 0; x < tileRes; x++)
                {
                    int ix = px0 + x, iz = pz0 + z;
                    if (ix < w && iz < h) result[iz * w + ix] = heights[z, x];
                }
            }
            return result;
        }

        void ExportTiles()
        {
            var region = FindFirstObjectByType<OpenWorldRegion>(FindObjectsInactive.Include);
            var features = FeatureExtractor.ExtractFromScene(out var roadLayers);
            ScatterBaker.CleanupGenerated(region); // 生成分のみ掃除 (手動配置は保護)
            FeatureExtractor.BuildWaterMeshes(region, _warnings);
            TerrainTileExporter.Export(_field, _settings, region, roadLayers, _warnings);
            TerrainAuthoringStore.Save(TerrainAuthoringStore.Folder + "/lastexport.bytes",
                (float[])_field.Heights.Clone(), _field.Width, _field.Height);
            Debug.Log("[Terrain] タイル書き出し完了。World Baker で全体リベイクし、" +
                      "マネージャ配置済みならそのままプレイできます。");
        }
    }
}
