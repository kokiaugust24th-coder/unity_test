using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OpenWorld.EditorTools
{
    public class BakeReport
    {
        public bool Success;
        public string Error;
        public int BakedCells;
        public int SkippedCells;
        public readonly List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// ワールドベイクパイプライン (spec: world-asset-pipeline)。
    /// オーサリングシーンの OpenWorldRegion 直下の静的オブジェクトをセル分類し、
    /// セルプレハブ + HLOD + WorldManifest を生成して Addressables 登録する。
    /// オーサリングシーンは一切変更しない (冪等・非破壊)。
    /// </summary>
    public static class WorldBaker
    {
        // パス定義は OpenWorldPaths に集約 (後方互換のため別名を維持)
        public const string GeneratedRoot = OpenWorldPaths.GeneratedRoot;
        public const string CellsFolder = OpenWorldPaths.CellsFolder;
        public const string HlodFolder = OpenWorldPaths.HlodFolder;
        public const string ManifestPath = OpenWorldPaths.ManifestPath;
        public const string CachePath = OpenWorldPaths.CachePath;
        const int MaxHierarchyDepthWarning = 8;

        class Unit
        {
            public GameObject Go;
            public Bounds Bounds;
            public string[] LayerNames;
            public bool ForceAlwaysLoaded;
        }

        class CellBuild
        {
            public string Key;               // "x_z" or "always"
            public bool AlwaysLoaded;
            public int X, Z;
            public readonly List<Unit> Units = new List<Unit>();
            public string Hash;
        }

        public static BakeReport Bake(OpenWorldRegion region, WorldPartitionConfig config, bool incremental)
        {
            var report = new BakeReport();
            var createdThisRun = new List<string>();
            try
            {
                if (region == null) throw new InvalidOperationException("OpenWorldRegion が指定されていません。");
                if (config == null) throw new InvalidOperationException("WorldPartitionConfig が指定されていません。");

                HLODBaker.EnsureFolder(GeneratedRoot);
                HLODBaker.EnsureFolder(CellsFolder);
                HLODBaker.EnsureFolder(HlodFolder);

                var manifest = LoadOrCreate<WorldManifest>(ManifestPath, createdThisRun);
                var cache = LoadOrCreate<BakeCache>(CachePath, createdThisRun);
                if (!incremental) cache.entries.Clear();

                // 1. ストリーミング単位の収集 (region 直下の各子 = 1 単位)
                var units = CollectUnits(region, report);

                // 2. セル分類 (spec: world-streaming / グリッドセル分割)
                var cells = ClassifyCells(units, config.cellSize, report);

                // 3. ハッシュ計算 (spec: 差分ベイク)
                foreach (var cell in cells.Values)
                    cell.Hash = ComputeCellHash(cell);

                // 4. セルごとにビルド
                var validKeys = new HashSet<string>(cells.Keys);
                foreach (var cell in cells.Values)
                {
                    string prefabPath = $"{CellsFolder}/Cell_{cell.Key}.prefab";
                    bool upToDate = incremental
                                    && cache.GetHash(cell.Key) == cell.Hash
                                    && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null
                                    && manifest.cells.Any(c => CellKey(c) == cell.Key);
                    if (upToDate)
                    {
                        report.SkippedCells++;
                        continue;
                    }

                    BuildCell(cell, config, prefabPath, manifest, report, createdThisRun);
                    cache.SetHash(cell.Key, cell.Hash);
                    report.BakedCells++;
                }

                // 5. 消滅したセルの掃除 + 古い場所を指す Addressables エントリの除去
                RemoveStaleCells(manifest, validKeys);
                cache.RemoveMissing(validKeys);
                AddressablesBakeUtil.PurgeStaleEntries();

                // 6. マニフェスト仕上げ
                manifest.cellSize = config.cellSize;
                manifest.layers = CollectLayerDefs();

                // 7. 検証 (spec: ベイク検証と警告)
                ValidateDuplicateDependencies(manifest, report);

                EditorUtility.SetDirty(manifest);
                EditorUtility.SetDirty(cache);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                report.Success = true;
            }
            catch (Exception e)
            {
                // エラー時は途中成果物を残さない (spec: エラー時の中断)
                foreach (var path in createdThisRun)
                    if (!string.IsNullOrEmpty(path) && AssetDatabase.LoadMainAssetAtPath(path) != null)
                        AssetDatabase.DeleteAsset(path);
                AssetDatabase.SaveAssets();
                report.Success = false;
                report.Error = e.Message;
                Debug.LogException(e);
            }
            return report;
        }

        // ------------------------------------------------------------ 収集と分類

        static List<Unit> CollectUnits(OpenWorldRegion region, BakeReport report)
        {
            var units = new List<Unit>();
            foreach (Transform child in region.transform)
            {
                var go = child.gameObject;
                if (!go.activeSelf) continue;

                if (!go.isStatic && go.GetComponent<AlwaysLoadedTag>() == null)
                {
                    report.Warnings.Add($"'{go.name}': 非静的のためベイク対象外 (シーンに残ります)。" +
                                        "静的にするか AlwaysLoadedTag を付けてください。");
                    continue;
                }
                if (HierarchyDepth(child) > MaxHierarchyDepthWarning)
                    report.Warnings.Add($"'{go.name}': 階層が深すぎます (> {MaxHierarchyDepthWarning})。インスタンス化コストに注意。");

                var tag = go.GetComponent<DataLayerTag>();
                var layerNames = tag != null
                    ? tag.layers.Where(l => l != null).Select(l => l.EffectiveName).Distinct().OrderBy(n => n).ToArray()
                    : Array.Empty<string>();

                units.Add(new Unit
                {
                    Go = go,
                    Bounds = ComputeBounds(go),
                    LayerNames = layerNames,
                    ForceAlwaysLoaded = go.GetComponent<AlwaysLoadedTag>() != null,
                });
            }
            return units;
        }

        /// <summary>セル境界にちょうど接するオブジェクトが「境界跨ぎ」と誤分類されるのを防ぐ許容量。</summary>
        const float BoundaryEpsilon = 0.05f;

        static Dictionary<string, CellBuild> ClassifyCells(List<Unit> units, float cellSize, BakeReport report)
        {
            var cells = new Dictionary<string, CellBuild>();
            foreach (var unit in units)
            {
                // バウンズをイプシロン分縮めてから判定 (境界ぴったりの地面タイル等を単一セル扱いに)
                Vector3 bMin = unit.Bounds.min;
                Vector3 bMax = unit.Bounds.max;
                if (bMax.x - bMin.x > BoundaryEpsilon * 2f) { bMin.x += BoundaryEpsilon; bMax.x -= BoundaryEpsilon; }
                if (bMax.z - bMin.z > BoundaryEpsilon * 2f) { bMin.z += BoundaryEpsilon; bMax.z -= BoundaryEpsilon; }

                var minCell = CellCoord.FromWorld(bMin, cellSize);
                var maxCell = CellCoord.FromWorld(bMax, cellSize);
                bool spansCells = minCell != maxCell;
                bool always = unit.ForceAlwaysLoaded || spansCells; // 境界跨ぎ → Always Loaded (spec)

                if (spansCells && !unit.ForceAlwaysLoaded)
                {
                    int spanX = maxCell.X - minCell.X + 1;
                    int spanZ = maxCell.Z - minCell.Z + 1;
                    report?.Warnings.Add(
                        $"'{unit.Go.name}': {spanX}x{spanZ} セルに跨るため AlwaysLoaded に分類されます。" +
                        "コンテナの場合は Hierarchy で選択して " +
                        "Tools > OpenWorld > 選択グループを展開 を実行し、個々のオブジェクトに分割してください。");
                }

                string key = always ? "always" : $"{minCell.X}_{minCell.Z}";
                if (!cells.TryGetValue(key, out var cell))
                {
                    cells[key] = cell = new CellBuild
                    {
                        Key = key,
                        AlwaysLoaded = always,
                        X = minCell.X,
                        Z = minCell.Z,
                    };
                }
                cell.Units.Add(unit);
            }
            return cells;
        }

        static Bounds ComputeBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                return b;
            }
            var colliders = go.GetComponentsInChildren<Collider>(false);
            if (colliders.Length > 0)
            {
                var b = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++) b.Encapsulate(colliders[i].bounds);
                return b;
            }
            return new Bounds(go.transform.position, Vector3.zero);
        }

        static int HierarchyDepth(Transform t)
        {
            int max = 0;
            foreach (Transform child in t)
                max = Mathf.Max(max, HierarchyDepth(child) + 1);
            return max;
        }

        // ------------------------------------------------------------ セルビルド

        static void BuildCell(
            CellBuild cell, WorldPartitionConfig config, string prefabPath,
            WorldManifest manifest, BakeReport report, List<string> createdThisRun)
        {
            var root = new GameObject($"Cell_{cell.Key}");
            try
            {
                // レイヤーセット別サブツリーに分類 (spec: world-data-layers)
                var subtreeParents = new Dictionary<string, Transform>();
                foreach (var unit in cell.Units)
                {
                    Transform parent;
                    if (unit.LayerNames.Length == 0)
                    {
                        parent = root.transform;
                    }
                    else
                    {
                        string setKey = string.Join("+", unit.LayerNames);
                        if (!subtreeParents.TryGetValue(setKey, out parent))
                        {
                            var sub = new GameObject($"Layers_{setKey}");
                            sub.transform.SetParent(root.transform, false);
                            sub.AddComponent<DataLayerSubtree>().layerNames = unit.LayerNames;
                            subtreeParents[setKey] = parent = sub.transform;
                        }
                    }

                    var copy = Object.Instantiate(unit.Go);
                    copy.name = unit.Go.name;
                    copy.transform.SetParent(parent, true); // ワールド変換を維持
                    StripAuthoringComponents(copy);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool ok);
                if (!ok) throw new InvalidOperationException($"セルプレハブ保存失敗: {prefabPath}");
                createdThisRun.Add(prefabPath);

                string address = $"ow_cell_{cell.Key}";
                AddressablesBakeUtil.MakeAddressable(prefabPath, address, AddressablesBakeUtil.CellGroupName);

                // HLOD (レイヤー付きコンテンツは対象外: 無効時に HLOD だけ見えるのを防ぐ)
                // AlwaysLoaded セルは常に実体表示のため HLOD 不要
                string hlodAddress = "";
                if (!cell.AlwaysLoaded)
                {
                    var hlodUnits = cell.Units.Where(u => u.LayerNames.Length == 0).Select(u => u.Go).ToList();
                    string hlodPath = HLODBaker.BakeCell(hlodUnits, cell.Key, config, HlodFolder, report.Warnings);
                    if (hlodPath != null)
                    {
                        createdThisRun.Add(hlodPath);
                        hlodAddress = $"ow_hlod_{cell.Key}";
                        AddressablesBakeUtil.MakeAddressable(hlodPath, hlodAddress, AddressablesBakeUtil.HlodGroupName);
                    }
                }

                // マニフェスト更新
                var entry = manifest.cells.FirstOrDefault(c => CellKey(c) == cell.Key);
                if (entry == null)
                {
                    entry = new WorldManifest.CellEntry();
                    manifest.cells.Add(entry);
                }
                entry.x = cell.X;
                entry.z = cell.Z;
                entry.alwaysLoaded = cell.AlwaysLoaded;
                entry.address = address;
                entry.hlodAddress = hlodAddress;
                entry.objectCount = cell.Units.Count;
                entry.layerNames = cell.Units.SelectMany(u => u.LayerNames).Distinct().OrderBy(n => n).ToArray();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void StripAuthoringComponents(GameObject copy)
        {
            foreach (var tag in copy.GetComponentsInChildren<DataLayerTag>(true))
                Object.DestroyImmediate(tag);
            foreach (var tag in copy.GetComponentsInChildren<AlwaysLoadedTag>(true))
                Object.DestroyImmediate(tag);
        }

        static void RemoveStaleCells(WorldManifest manifest, HashSet<string> validKeys)
        {
            for (int i = manifest.cells.Count - 1; i >= 0; i--)
            {
                var entry = manifest.cells[i];
                string key = CellKey(entry);
                if (validKeys.Contains(key)) continue;

                string prefabPath = $"{CellsFolder}/Cell_{key}.prefab";
                AddressablesBakeUtil.RemoveEntry(prefabPath);
                AssetDatabase.DeleteAsset(prefabPath);

                // Terrain タイル統合 HLOD (ow_thlod_) は Terrain 側の生成物なので消さない
                bool terrainHlod = entry.hlodAddress != null && entry.hlodAddress.StartsWith("ow_thlod_");
                if (entry.HasHlod && !terrainHlod)
                {
                    foreach (var suffix in new[] { ".prefab", "_mesh.asset", "_atlas.asset", "_mat.mat" })
                    {
                        string p = $"{HlodFolder}/HLOD_{key}{suffix}";
                        AddressablesBakeUtil.RemoveEntry(p);
                        AssetDatabase.DeleteAsset(p);
                    }
                }

                // 追加コンテンツ (Terrain 等) を持つエントリは削除せず、プレハブ情報のみクリア
                if (entry.contents != null && entry.contents.Length > 0)
                {
                    entry.address = "";
                    entry.objectCount = 0;
                    entry.layerNames = Array.Empty<string>();
                    if (!terrainHlod) entry.hlodAddress = "";
                    continue;
                }
                manifest.cells.RemoveAt(i);
            }
        }

        static List<WorldManifest.LayerDef> CollectLayerDefs()
        {
            var defs = new List<WorldManifest.LayerDef>();
            foreach (var guid in AssetDatabase.FindAssets("t:DataLayerAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<DataLayerAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && defs.All(d => d.name != asset.EffectiveName))
                    defs.Add(new WorldManifest.LayerDef { name = asset.EffectiveName, initiallyEnabled = asset.initiallyEnabled });
            }
            return defs;
        }

        // ------------------------------------------------------------ ハッシュ・検証

        static string ComputeCellHash(CellBuild cell)
        {
            var sb = new StringBuilder();
            foreach (var unit in cell.Units.OrderBy(u => u.Go.name).ThenBy(u => u.Go.transform.GetSiblingIndex()))
            {
                sb.Append(unit.Go.name);
                sb.Append(unit.Go.transform.localToWorldMatrix.ToString("F4"));
                sb.Append(string.Join(",", unit.LayerNames));
                foreach (var r in unit.Go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                        sb.Append(AssetDatabase.GetAssetPath(mf.sharedMesh));
                    foreach (var m in r.sharedMaterials)
                        if (m != null) sb.Append(AssetDatabase.GetAssetPath(m));
                    sb.Append(r.transform.localToWorldMatrix.ToString("F4"));
                }
            }
            using var sha = SHA1.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return Convert.ToBase64String(hash);
        }

        /// <summary>複数セルバンドルに重複する依存アセットの検出 (spec: 依存重複の検出)。</summary>
        static void ValidateDuplicateDependencies(WorldManifest manifest, BakeReport report)
        {
            var seenIn = new Dictionary<string, int>();
            foreach (var entry in manifest.cells)
            {
                string prefabPath = $"{CellsFolder}/Cell_{CellKey(entry)}.prefab";
                foreach (var dep in AssetDatabase.GetDependencies(prefabPath, true))
                {
                    if (dep == prefabPath) continue;
                    if (dep.EndsWith(".cs") || dep.EndsWith(".asmdef")) continue;
                    if (dep.StartsWith(GeneratedRoot)) continue;
                    seenIn[dep] = seenIn.TryGetValue(dep, out int n) ? n + 1 : 1;
                }
            }
            var duplicated = seenIn.Where(kv => kv.Value > 1)
                                   .OrderByDescending(kv => kv.Value)
                                   .Take(20)
                                   .ToList();
            foreach (var kv in duplicated)
                report.Warnings.Add($"依存重複: '{kv.Key}' が {kv.Value} セルに含まれます。" +
                                    "Addressables の共有グループへの移動を検討してください。");
        }

        static string CellKey(WorldManifest.CellEntry e) => e.alwaysLoaded ? "always" : $"{e.x}_{e.z}";

        /// <summary>生成物をすべて削除する (ロールバック。spec: ワールドベイク)。</summary>
        public static void ClearGenerated()
        {
            if (AssetDatabase.IsValidFolder(GeneratedRoot))
            {
                // Addressables 登録も掃除
                foreach (var guid in AssetDatabase.FindAssets("", new[] { GeneratedRoot }))
                    AddressablesBakeUtil.RemoveEntry(AssetDatabase.GUIDToAssetPath(guid));
                AssetDatabase.DeleteAsset(GeneratedRoot);
                AssetDatabase.SaveAssets();
            }
        }

        static T LoadOrCreate<T>(string path, List<string> createdThisRun) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
                createdThisRun.Add(path);
            }
            return asset;
        }
    }
}
