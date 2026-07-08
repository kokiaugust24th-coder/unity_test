using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using OpenWorld.Samples;

namespace OpenWorld.EditorTools
{
    /// <summary>
    /// 現在開いているシーンをオープンワールド機能のデモ用にセットアップする
    /// (tasks.md 7.1 サンプルワールド)。
    /// 2km 四方 (8x8 セル x 256m) のサンプルコンテンツを生成し、ベイクまで実行する。
    /// </summary>
    public static class SampleSceneSetup
    {
        // パス定義は OpenWorldPaths に集約 (後方互換のため別名を維持)
        public const string RootFolder = OpenWorldPaths.RootFolder;
        public const string SampleFolder = OpenWorldPaths.SampleFolder;
        public const string ConfigPath = OpenWorldPaths.ConfigPath;
        const int GridCells = 8;          // 8x8 セル = 2km 四方
        const float CellSize = 256f;

        [MenuItem("Tools/OpenWorld/サンプル/サンプルコンテンツを再生成", false, 41)]
        public static void Regenerate()
        {
            if (Application.isPlaying) return;
            var region = Object.FindFirstObjectByType<OpenWorldRegion>(FindObjectsInactive.Include);
            if (region != null)
            {
                if (!EditorUtility.DisplayDialog("OpenWorld",
                        $"'{region.name}' を削除してサンプルコンテンツを作り直し、再ベイクします。よろしいですか?",
                        "再生成", "キャンセル"))
                    return;
                Object.DestroyImmediate(region.gameObject);
            }
            Setup();
        }

        [MenuItem("Tools/OpenWorld/サンプル/現在のシーンをサンプルセットアップ", false, 40)]
        public static void Setup()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("OpenWorld", "プレイモード中は実行できません。", "OK");
                return;
            }

            HLODBaker.EnsureFolder(SampleFolder);

            // 1. Config
            var config = AssetDatabase.LoadAssetAtPath<WorldPartitionConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<WorldPartitionConfig>();
                config.cellSize = CellSize;
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            // 2. データレイヤー例 (QuestA: 初期無効の赤い球)
            var questLayer = AssetDatabase.LoadAssetAtPath<DataLayerAsset>(
                $"{SampleFolder}/DataLayer_QuestA.asset");
            if (questLayer == null)
            {
                questLayer = ScriptableObject.CreateInstance<DataLayerAsset>();
                questLayer.layerName = "QuestA";
                questLayer.initiallyEnabled = false;
                questLayer.description = "サンプル: HUD (F3) で切替できるクエストレイヤー";
                AssetDatabase.CreateAsset(questLayer, $"{SampleFolder}/DataLayer_QuestA.asset");
            }

            // 3. サンプルマテリアル
            var matGround = EnsureMaterial("Sample_Ground", new Color(0.35f, 0.45f, 0.3f));
            var matBox = EnsureMaterial("Sample_Box", new Color(0.6f, 0.55f, 0.5f));
            var matQuest = EnsureMaterial("Sample_Quest", new Color(0.9f, 0.2f, 0.2f));

            // 4. オーサリングリージョンとサンプルコンテンツ
            var region = Object.FindFirstObjectByType<OpenWorldRegion>(FindObjectsInactive.Include);
            if (region == null)
            {
                var regionGo = new GameObject("WorldRegion");
                region = regionGo.AddComponent<OpenWorldRegion>();
                PopulateSampleWorld(region.transform, matGround, matBox, matQuest, questLayer);
                Debug.Log($"[OpenWorld] サンプルワールド生成: {GridCells}x{GridCells} セル ({GridCells * CellSize / 1000f:F1}km 四方)");
            }
            else
            {
                region.gameObject.SetActive(true); // 前回プレイで無効化されていた場合
                Debug.Log("[OpenWorld] 既存の OpenWorldRegion を使用します (コンテンツ生成スキップ)");
            }

            // 5. ベイク実行
            var report = WorldBaker.Bake(region, config, incremental: false);
            if (!report.Success)
            {
                EditorUtility.DisplayDialog("OpenWorld", $"ベイク失敗: {report.Error}", "OK");
                return;
            }
            var manifest = AssetDatabase.LoadAssetAtPath<WorldManifest>(WorldBaker.ManifestPath);

            // 6. ストリーミングマネージャ
            var manager = Object.FindFirstObjectByType<WorldStreamingManager>(FindObjectsInactive.Include);
            if (manager == null)
                manager = new GameObject("WorldStreamingManager").AddComponent<WorldStreamingManager>();
            manager.Configure(config, manifest);
            if (manager.GetComponent<StreamingDebugHUD>() == null)
                manager.gameObject.AddComponent<StreamingDebugHUD>();
            EditorUtility.SetDirty(manager);

            // 7. カメラ = ストリーミングソース + フライ操作
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            float worldCenter = GridCells * CellSize * 0.5f;
            cam.transform.position = new Vector3(worldCenter, 40f, worldCenter);
            cam.transform.rotation = Quaternion.Euler(20f, 45f, 0f);
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, config.HlodRadius + 500f);
            if (cam.GetComponent<StreamingSource>() == null)
                cam.gameObject.AddComponent<StreamingSource>();
            if (cam.GetComponent<SampleFlyCamera>() == null)
                cam.gameObject.AddComponent<SampleFlyCamera>();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(region.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();

            string msg = $"セットアップ完了 ({report.BakedCells} セルをベイク)。\n\n" +
                         "プレイ開始で確認:\n" +
                         "・WASD + 右ドラッグで移動 (Shift 加速)\n" +
                         "・F3: 統計 HUD / QuestA レイヤー切替\n" +
                         "・Scene ビュー: Tools > OpenWorld > Grid Overlay で状態色分け";
            if (report.Warnings.Count > 0)
                msg += $"\n\n警告 {report.Warnings.Count} 件 (World Baker ウィンドウで確認可)";
            EditorUtility.DisplayDialog("OpenWorld", msg, "OK");
        }

        static void PopulateSampleWorld(
            Transform region, Material ground, Material box, Material quest, DataLayerAsset questLayer)
        {
            var rand = new System.Random(12345);

            for (int cx = 0; cx < GridCells; cx++)
            for (int cz = 0; cz < GridCells; cz++)
            {
                Vector3 cellMin = new Vector3(cx * CellSize, 0f, cz * CellSize);
                Vector3 center = cellMin + new Vector3(CellSize * 0.5f, 0f, CellSize * 0.5f);

                // 地面 (フルサイズ。ベイクの境界イプシロン分類により単一セル扱いになる)
                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                plane.name = $"Ground_{cx}_{cz}";
                plane.transform.position = center;
                plane.transform.localScale = Vector3.one * (CellSize / 10f);
                plane.GetComponent<Renderer>().sharedMaterial = ground;
                MakeUnit(plane, region);

                // 建物風の箱をランダム配置
                int boxCount = rand.Next(6, 12);
                for (int i = 0; i < boxCount; i++)
                {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = $"Box_{cx}_{cz}_{i}";
                    float h = 4f + (float)rand.NextDouble() * 30f;
                    cube.transform.localScale = new Vector3(
                        6f + (float)rand.NextDouble() * 12f, h, 6f + (float)rand.NextDouble() * 12f);
                    cube.transform.position = cellMin + new Vector3(
                        20f + (float)rand.NextDouble() * (CellSize - 40f), h * 0.5f,
                        20f + (float)rand.NextDouble() * (CellSize - 40f));
                    cube.GetComponent<Renderer>().sharedMaterial = box;
                    MakeUnit(cube, region);
                }

                // QuestA レイヤーの赤い球 (2 セルに 1 個)
                if ((cx + cz) % 2 == 0)
                {
                    var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphere.name = $"QuestOrb_{cx}_{cz}";
                    sphere.transform.localScale = Vector3.one * 8f;
                    sphere.transform.position = center + new Vector3(0f, 12f, 0f);
                    sphere.GetComponent<Renderer>().sharedMaterial = quest;
                    var tag = sphere.AddComponent<DataLayerTag>();
                    tag.layers = new[] { questLayer };
                    MakeUnit(sphere, region);
                }
            }
        }

        static void MakeUnit(GameObject go, Transform region)
        {
            go.isStatic = true;
            go.transform.SetParent(region, true);
        }

        static Material EnsureMaterial(string name, Color color)
        {
            string path = $"{SampleFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetColor("_BaseColor", color);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }
    }
}
