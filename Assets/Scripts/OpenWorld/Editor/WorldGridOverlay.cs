using UnityEditor;
using UnityEngine;

namespace OpenWorld.EditorTools
{
    /// <summary>
    /// Scene ビューのワールドグリッド + セル状態オーバーレイ
    /// (spec: world-debug / エディタグリッド可視化)。
    /// Tools > OpenWorld > Grid Overlay で切替。プレイ中は状態別色分け。
    /// </summary>
    [InitializeOnLoad]
    public static class WorldGridOverlay
    {
        const string PrefKey = "OpenWorld.GridOverlay";
        const string MenuPath = "Tools/OpenWorld/Grid Overlay";

        static readonly Color UnloadedColor = new Color(0.5f, 0.5f, 0.5f, 0.9f);
        static readonly Color LoadingColor = new Color(1f, 0.9f, 0.2f, 0.9f);
        static readonly Color LoadedColor = new Color(1f, 0.6f, 0.1f, 0.9f);
        static readonly Color ActivatedColor = new Color(0.2f, 1f, 0.2f, 0.9f);

        static WorldGridOverlay()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        [MenuItem(MenuPath, false, 3)]
        static void Toggle()
        {
            bool on = !EditorPrefs.GetBool(PrefKey, true);
            EditorPrefs.SetBool(PrefKey, on);
            Menu.SetChecked(MenuPath, on);
            SceneView.RepaintAll();
        }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PrefKey, true));
            return true;
        }

        static void OnSceneGUI(SceneView view)
        {
            if (!EditorPrefs.GetBool(PrefKey, true)) return;

            WorldManifest manifest;
            WorldStreamingManager mgr = Application.isPlaying ? WorldStreamingManager.Instance : null;
            manifest = mgr != null
                ? mgr.Manifest
                : AssetDatabase.LoadAssetAtPath<WorldManifest>(WorldBaker.ManifestPath);
            if (manifest == null) return;

            float size = manifest.cellSize;
            bool drawLabels = view.camera != null && view.camera.transform.position.y < size * 8f;

            foreach (var cell in manifest.cells)
            {
                if (cell.alwaysLoaded) continue;
                var coord = new CellCoord(cell.x, cell.z);
                Vector3 min = coord.MinCorner(size);
                var verts = new[]
                {
                    min,
                    min + new Vector3(size, 0, 0),
                    min + new Vector3(size, 0, size),
                    min + new Vector3(0, 0, size),
                };

                Color color = UnloadedColor;
                if (mgr != null && mgr.TryGetCellState(coord, out var state))
                {
                    color = state switch
                    {
                        CellState.Loading => LoadingColor,
                        CellState.Loaded => LoadedColor,
                        CellState.Activated => ActivatedColor,
                        _ => UnloadedColor,
                    };
                }

                var face = color;
                face.a = 0.06f;
                Handles.DrawSolidRectangleWithOutline(verts, face, color);

                if (drawLabels)
                    Handles.Label(coord.Center(size),
                        $"({cell.x},{cell.z})  {cell.objectCount} objs{(cell.HasHlod ? "  HLOD" : "")}");
            }
        }
    }
}
