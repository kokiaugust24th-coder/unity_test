using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenWorld.EditorTools
{
    /// <summary>
    /// 既存シーンのオープンワールド変換ウィザード。
    /// シーンルートのオブジェクトを走査して分類し、選択したものを OpenWorldRegion 直下へ
    /// 移動 + 静的化する。その後 World Baker でベイクすれば分割完了。
    /// Tools > OpenWorld > 既存シーンを変換
    /// </summary>
    public class SceneConverterWindow : EditorWindow
    {
        class Candidate
        {
            public GameObject Go;
            public bool Include;
            public string Note;
            public int RendererCount;
        }

        readonly List<Candidate> _candidates = new List<Candidate>();
        bool _makeStatic = true;
        Vector2 _scroll;
        bool _scanned;

        [MenuItem("Tools/OpenWorld/シーン変換ウィザード", false, 2)]
        static void Open() => GetWindow<SceneConverterWindow>("OpenWorld 変換");

        void OnEnable() => Scan();

        void Scan()
        {
            _candidates.Clear();
            var scene = SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
            {
                if (ShouldSkipEntirely(go)) continue;

                var c = new Candidate
                {
                    Go = go,
                    RendererCount = go.GetComponentsInChildren<Renderer>(true).Length,
                };

                if (c.RendererCount == 0 && go.GetComponentsInChildren<Collider>(true).Length == 0)
                {
                    c.Include = false;
                    c.Note = "描画/コリジョンなし";
                }
                else if (IsProbablyDynamic(go, out string reason))
                {
                    c.Include = false;
                    c.Note = $"動的の可能性 ({reason})";
                }
                else
                {
                    c.Include = true;
                    c.Note = go.isStatic ? "静的" : "静的化される";
                }
                _candidates.Add(c);
            }
            _scanned = true;
        }

        /// <summary>変換対象として表示すらしないもの (システム系)。</summary>
        static bool ShouldSkipEntirely(GameObject go)
        {
            return go.GetComponent<OpenWorldRegion>() != null
                   || go.GetComponentInChildren<WorldStreamingManager>(true) != null
                   || go.GetComponentInChildren<Camera>(true) != null
                   || go.GetComponentInChildren<StreamingSource>(true) != null
                   || go.GetComponentInChildren<Light>(true) != null    // ライトはシーンに残す
                   || go.name.Contains("EventSystem");
        }

        static bool IsProbablyDynamic(GameObject go, out string reason)
        {
            if (go.CompareTag("Player")) { reason = "Player タグ"; return true; }
            if (go.GetComponentInChildren<CharacterController>(true) != null) { reason = "CharacterController"; return true; }
            if (go.GetComponentInChildren<Rigidbody>(true) != null) { reason = "Rigidbody"; return true; }
            if (go.GetComponentInChildren<Animator>(true) != null) { reason = "Animator"; return true; }
            reason = null;
            return false;
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "選択したルートオブジェクトを OpenWorldRegion 直下へ移動します (Undo 可)。\n" +
                "移動後、World Baker で「全体リベイク」すればセル分割されます。\n" +
                "カメラ・ライト・プレイヤー・マネージャ類はシーンに残してください。",
                MessageType.Info);

            if (GUILayout.Button("再スキャン")) Scan();
            _makeStatic = EditorGUILayout.ToggleLeft("変換時に階層全体を Static にする", _makeStatic);

            EditorGUILayout.Space();
            if (!_scanned || _candidates.Count == 0)
            {
                EditorGUILayout.LabelField("変換候補がありません。");
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("すべて選択", GUILayout.Width(90)))
                    _candidates.ForEach(c => { if (c.Note?.StartsWith("動的") != true) c.Include = true; });
                if (GUILayout.Button("すべて解除", GUILayout.Width(90)))
                    _candidates.ForEach(c => c.Include = false);
                EditorGUILayout.EndHorizontal();

                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                foreach (var c in _candidates)
                {
                    if (c.Go == null) continue;
                    EditorGUILayout.BeginHorizontal("box");
                    c.Include = EditorGUILayout.Toggle(c.Include, GUILayout.Width(18));
                    EditorGUILayout.ObjectField(c.Go, typeof(GameObject), true);
                    EditorGUILayout.LabelField($"{c.RendererCount} renderers", GUILayout.Width(90));
                    EditorGUILayout.LabelField(c.Note ?? "", GUILayout.Width(170));
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("選択したオブジェクトを変換", GUILayout.Height(30)))
                    Convert();

                // 変換後の仕上げ (メニューの「変換ツール」と同じ機能)
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("仕上げ", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("グループを展開\n(Hierarchy の選択)"))
                    GroupFlattenTool.FlattenSelected();
                if (GUILayout.Button("メッシュをセル分割\n(Hierarchy の選択)"))
                    MeshCellSplitter.SplitSelected();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("マネージャと HUD を配置"))
                    SceneSetupUtility.EnsureRuntimeObjects();
                if (GUILayout.Button("World Baker を開く"))
                    GetWindow<WorldBakerWindow>("World Baker");
                EditorGUILayout.EndHorizontal();
            }
        }

        void Convert()
        {
            var region = FindFirstObjectByType<OpenWorldRegion>(FindObjectsInactive.Include);
            if (region == null)
            {
                var go = new GameObject("WorldRegion");
                Undo.RegisterCreatedObjectUndo(go, "OpenWorld Convert");
                region = go.AddComponent<OpenWorldRegion>();
            }
            region.gameObject.SetActive(true);

            int converted = 0;
            foreach (var c in _candidates)
            {
                if (!c.Include || c.Go == null) continue;

                Undo.RegisterFullObjectHierarchyUndo(c.Go, "OpenWorld Convert");
                Undo.SetTransformParent(c.Go.transform, region.transform, "OpenWorld Convert");
                if (_makeStatic)
                    SetStaticRecursive(c.Go);
                converted++;
            }

            EditorSceneManager.MarkSceneDirty(region.gameObject.scene);
            Scan();

            if (converted > 0 && EditorUtility.DisplayDialog("OpenWorld",
                    $"{converted} オブジェクトを WorldRegion 直下へ移動しました。\n" +
                    "続けて World Baker でベイクしますか?", "World Baker を開く", "あとで"))
            {
                GetWindow<WorldBakerWindow>("World Baker");
            }
        }

        static void SetStaticRecursive(GameObject go)
        {
            go.isStatic = true;
            foreach (Transform child in go.transform)
                SetStaticRecursive(child.gameObject);
        }
    }
}
