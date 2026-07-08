using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OpenWorld
{
    /// <summary>
    /// オープンワールドストリーミングの中核 (spec: world-streaming)。
    /// WorldManifest のセルを StreamingSource との距離で評価し、
    /// 優先度付きキュー + フレーム予算で非同期ロード/アンロードする。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class WorldStreamingManager : MonoBehaviour
    {
        public static WorldStreamingManager Instance { get; private set; }

        [SerializeField] WorldPartitionConfig config;
        [SerializeField] WorldManifest manifest;

        public WorldPartitionConfig Config => config;
        public WorldManifest Manifest => manifest;

        /// <summary>
        /// ローダ差し替え (テスト用注入点)。Awake 前に設定する。未設定なら Addressables。
        /// </summary>
        public ICellLoader Loader { get; set; }

        readonly List<CellRuntime> _cells = new List<CellRuntime>();
        readonly Dictionary<CellCoord, CellRuntime> _grid = new Dictionary<CellCoord, CellRuntime>();
        readonly Dictionary<CellCoord, CellState> _overrides = new Dictionary<CellCoord, CellState>();

        readonly List<CellRuntime> _loadQueue = new List<CellRuntime>();
        readonly List<CellRuntime> _activateQueue = new List<CellRuntime>();
        readonly List<CellRuntime> _deactivateQueue = new List<CellRuntime>();
        readonly List<CellRuntime> _unloadQueue = new List<CellRuntime>();
        readonly Queue<CellRuntime> _layerReapply = new Queue<CellRuntime>();
        readonly HashSet<CellRuntime> _layerReapplySet = new HashSet<CellRuntime>();

        static readonly Stopwatch _budgetWatch = new Stopwatch();

        int _inFlight;
        float _nextEvalTime;
        bool _paused;
        bool _forceLoadAll;
        float _lastBudgetUsedMs;

        /// <summary>読み取り専用のセル一覧 (デバッグ表示用)。</summary>
        public IReadOnlyList<CellRuntime> Cells => _cells;

        /// <summary>
        /// コードからの初期化 (テスト用)。Awake 前 (非アクティブ GameObject 上) に呼ぶこと。
        /// </summary>
        public void Configure(WorldPartitionConfig cfg, WorldManifest mf, ICellLoader loader = null)
        {
            config = cfg;
            manifest = mf;
            if (loader != null) Loader = loader;
        }
        public bool IsPaused => _paused;
        public bool IsForceLoadAll => _forceLoadAll;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[OpenWorld] WorldStreamingManager が複数存在します。", this);
                enabled = false;
                return;
            }
            Instance = this;

            if (config == null || manifest == null)
            {
                Debug.LogError("[OpenWorld] config / manifest が未設定です。", this);
                enabled = false;
                return;
            }

            Loader ??= new AddressablesCellLoader();

            BuildCells();
            DataLayerManager.Initialize(manifest.layers);
            DataLayerManager.LayerChanged += OnLayerChanged;

            if (config.disableAuthoringRegionOnPlay)
            {
                foreach (var region in FindObjectsByType<OpenWorldRegion>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    region.gameObject.SetActive(false);
                    Debug.Log($"[OpenWorld] オーサリングリージョン '{region.name}' を無効化しました。");
                }
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DataLayerManager.LayerChanged -= OnLayerChanged;
            if (Loader == null) return;
            // 全ハンドル解放 (spec: world-asset-pipeline / メモリ管理)
            foreach (var cell in _cells)
            {
                if (cell.Instance != null) Destroy(cell.Instance);
                if (cell.HlodInstance != null) Destroy(cell.HlodInstance);
                if (cell.AssetHandle != null) Loader.Release(cell.AssetHandle);
                if (cell.HlodHandle != null) Loader.Release(cell.HlodHandle);
                cell.AssetHandle = null;
                cell.HlodHandle = null;
                foreach (var ex in cell.Extras)
                {
                    if (ex.Instance != null) Destroy(ex.Instance);
                    if (ex.Handle != null) Loader.Release(ex.Handle);
                    ex.Handle = null;
                }
            }
        }

        void BuildCells()
        {
            _cells.Clear();
            _grid.Clear();
            foreach (var entry in manifest.cells)
            {
                var cell = new CellRuntime { Entry = entry, Coord = entry.Coord };
                _cells.Add(cell);
                if (!entry.alwaysLoaded)
                {
                    if (_grid.ContainsKey(cell.Coord))
                        Debug.LogError($"[OpenWorld] マニフェストに重複セル {cell.Coord}");
                    else
                        _grid.Add(cell.Coord, cell);
                }
            }
        }

        void Update()
        {
            if (_paused) return;

            if (Time.time >= _nextEvalTime)
            {
                _nextEvalTime = Time.time + config.evaluationInterval;
                Evaluate();
            }
            ProcessLoadQueue();
            ProcessBudgeted();
            ProcessHlod();
        }

        // ---------------------------------------------------------------- 評価

        bool _noSourceWarned;

        void Evaluate()
        {
            var sources = StreamingSourceRegistry.Sources;

            // ソース未登録の検出 (動的ストリーミングが動かない典型原因)
            if (sources.Count == 0 && !_forceLoadAll && _overrides.Count == 0)
            {
                if (!_noSourceWarned)
                {
                    _noSourceWarned = true;
                    Debug.LogWarning(
                        "[OpenWorld] StreamingSource が 1 つも登録されていません。" +
                        "移動するオブジェクト (プレイヤー等) に StreamingSource コンポーネントを付けてください。" +
                        "登録されるまで全セルがアンロード対象になります。");
                }
            }
            else
            {
                _noSourceWarned = false;
            }

            foreach (var cell in _cells)
            {
                CellState desired;
                float minDist = float.MaxValue;
                int maxPriority = int.MinValue;
                bool hlodDesired = false;

                if (_overrides.TryGetValue(cell.Coord, out var forced))
                {
                    desired = forced;
                }
                else if (cell.Entry.alwaysLoaded || _forceLoadAll)
                {
                    desired = CellState.Activated;
                    minDist = 0f;
                }
                else
                {
                    desired = CellState.Unloaded;
                    for (int i = 0; i < sources.Count; i++)
                    {
                        var src = sources[i];
                        float mult = Mathf.Max(0.01f, src.RadiusMultiplier);
                        float dist = cell.Coord.DistanceXZ(src.Position, manifest.cellSize);

                        var forSource = StreamingEvaluation.DesiredForSource(
                            cell.State, dist,
                            config.activationRadius * mult,
                            config.loadRadius * mult,
                            config.hysteresis);
                        // 複数ソースの合成は最大値 (spec: 複数ソースの合成)
                        desired = StreamingEvaluation.Max(desired, forSource);

                        if (dist < minDist) minDist = dist;
                        if (src.Priority > maxPriority) maxPriority = src.Priority;

                        hlodDesired |= StreamingEvaluation.HlodDesired(
                            cell.State, cell.Entry.HasHlod, cell.HlodShown,
                            dist, config.HlodRadius * mult, config.hysteresis);
                    }
                }

                cell.Desired = desired;
                cell.Distance = minDist;
                cell.SourcePriority = maxPriority == int.MinValue ? 0 : maxPriority;
                cell.HlodDesired = hlodDesired && cell.State != CellState.Activated;
            }

            RebuildQueues();
        }

        void RebuildQueues()
        {
            _loadQueue.Clear();
            _activateQueue.Clear();
            _deactivateQueue.Clear();
            _unloadQueue.Clear();

            foreach (var cell in _cells)
            {
                if (cell.State == CellState.Unloaded && cell.Desired >= CellState.Loaded)
                    _loadQueue.Add(cell);
                else if (cell.State == CellState.Loaded && cell.Desired == CellState.Activated)
                    _activateQueue.Add(cell);
                else if (cell.State == CellState.Activated && cell.Desired <= CellState.Loaded)
                    _deactivateQueue.Add(cell);
                else if (cell.State == CellState.Loaded && cell.Desired == CellState.Unloaded)
                    _unloadQueue.Add(cell);
            }

            // 近いセル優先 + ソース優先度 (spec: 近いセルの優先)
            _loadQueue.Sort(CompareByPriorityThenDistance);
            _activateQueue.Sort(CompareByDistance);
        }

        static int CompareByPriorityThenDistance(CellRuntime a, CellRuntime b)
        {
            int p = b.SourcePriority.CompareTo(a.SourcePriority);
            return p != 0 ? p : a.Distance.CompareTo(b.Distance);
        }

        static int CompareByDistance(CellRuntime a, CellRuntime b) =>
            a.Distance.CompareTo(b.Distance);

        // ---------------------------------------------------------------- ロード

        void ProcessLoadQueue()
        {
            int index = 0;
            // in-flight 上限 (spec: 非同期ロードとフレーム予算)
            while (_inFlight < config.maxInFlightLoads && index < _loadQueue.Count)
            {
                var cell = _loadQueue[index++];
                if (cell.State != CellState.Unloaded || cell.Desired < CellState.Loaded) continue;

                cell.State = CellState.Loading;
                _inFlight++;
                StartCellLoad(cell);
            }
            _loadQueue.RemoveRange(0, index);
        }

        /// <summary>
        /// セルの全コンテンツ (プレハブ + 追加コンテンツ) のロードを開始する
        /// (spec: terrain-streaming / セルコンテンツ抽象化)。
        /// </summary>
        void StartCellLoad(CellRuntime cell)
        {
            bool hasPrimary = !string.IsNullOrEmpty(cell.Entry.address);
            var contents = cell.Entry.contents ?? System.Array.Empty<WorldManifest.CellContent>();

            cell.AnyLoadFailed = false;
            cell.Extras.Clear();
            foreach (var c in contents)
                cell.Extras.Add(new CellRuntime.ExtraContent { Content = c });

            cell.PendingLoads = (hasPrimary ? 1 : 0) + cell.Extras.Count;
            if (cell.PendingLoads == 0)
            {
                _inFlight--;
                cell.State = CellState.Loaded; // 空セル (メタのみ)
                return;
            }

            if (hasPrimary)
            {
                var handle = Loader.LoadAsync(cell.Entry.address,
                    (h, prefab) => OnPartLoaded(cell, -1, h, prefab));
                if (cell.State == CellState.Loading && cell.AssetHandle == null)
                    cell.AssetHandle = handle; // 同期完了ローダ対策
            }
            for (int i = 0; i < cell.Extras.Count; i++)
            {
                int idx = i;
                var handle = Loader.LoadAsync(cell.Extras[idx].Content.address,
                    (h, prefab) => OnPartLoaded(cell, idx, h, prefab));
                if (cell.Extras[idx].Handle == null) cell.Extras[idx].Handle = handle;
            }
        }

        void OnPartLoaded(CellRuntime cell, int extraIndex, object handle, GameObject prefab)
        {
            // マネージャ破棄後に届いた非同期コールバック → 即解放 (シャットダウン時のリーク防止)
            if (Instance != this)
            {
                Loader.Release(handle);
                return;
            }

            if (extraIndex < 0)
            {
                cell.AssetHandle = handle;
                cell.Prefab = prefab;
            }
            else
            {
                cell.Extras[extraIndex].Handle = handle;
                cell.Extras[extraIndex].Prefab = prefab;
            }
            if (prefab == null) cell.AnyLoadFailed = true;

            if (--cell.PendingLoads > 0) return;

            // 全パート完了
            _inFlight--;
            if (cell.AnyLoadFailed || cell.Desired < CellState.Loaded)
            {
                // 失敗 or ロード中に圏外へ → 全て Release (spec: ロード中キャンセル)
                ReleaseAllHandles(cell);
                cell.State = CellState.Unloaded;
                return;
            }

            cell.State = CellState.Loaded;
            if (cell.Desired == CellState.Activated)
                _activateQueue.Add(cell);
        }

        void ReleaseAllHandles(CellRuntime cell)
        {
            if (cell.AssetHandle != null) Loader.Release(cell.AssetHandle);
            cell.AssetHandle = null;
            cell.Prefab = null;
            foreach (var ex in cell.Extras)
            {
                if (ex.Handle != null) Loader.Release(ex.Handle);
                ex.Handle = null;
                ex.Prefab = null;
            }
        }

        // ---------------------------------------------------- 予算内メインスレッド処理

        void ProcessBudgeted()
        {
            _budgetWatch.Restart();
            float budget = config.frameBudgetMs;

            // アクティベート (インスタンス化) — 最も重い処理を予算で分割
            while (_activateQueue.Count > 0 && _budgetWatch.Elapsed.TotalMilliseconds < budget)
            {
                var cell = _activateQueue[0];
                _activateQueue.RemoveAt(0);
                ActivateCell(cell);
            }

            while (_deactivateQueue.Count > 0 && _budgetWatch.Elapsed.TotalMilliseconds < budget)
            {
                var cell = _deactivateQueue[0];
                _deactivateQueue.RemoveAt(0);
                DeactivateCell(cell);
            }

            while (_unloadQueue.Count > 0 && _budgetWatch.Elapsed.TotalMilliseconds < budget)
            {
                var cell = _unloadQueue[0];
                _unloadQueue.RemoveAt(0);
                UnloadCell(cell);
            }

            // レイヤー切替の反映も予算対象 (spec: world-data-layers / ランタイムレイヤー切替)
            while (_layerReapply.Count > 0 && _budgetWatch.Elapsed.TotalMilliseconds < budget)
            {
                var cell = _layerReapply.Dequeue();
                _layerReapplySet.Remove(cell);
                if (cell.State == CellState.Activated && cell.Instance != null)
                    ApplyDataLayers(cell.Instance);
            }

            _lastBudgetUsedMs = (float)_budgetWatch.Elapsed.TotalMilliseconds;
        }

        void ActivateCell(CellRuntime cell)
        {
            if (cell.State != CellState.Loaded || cell.Desired != CellState.Activated)
                return;
            if (cell.Prefab == null && cell.Extras.Count == 0)
                return;

            if (cell.Prefab != null)
            {
                cell.Instance = Instantiate(cell.Prefab, transform);
                cell.Instance.name = $"{cell.Coord}";
                if (!cell.Instance.activeSelf) cell.Instance.SetActive(true);
                ApplyDataLayers(cell.Instance);
                if (config.staticBatchOnActivate)
                    StaticBatchingUtility.Combine(cell.Instance);
            }

            // 追加コンテンツ: インスタンス化しハンドラへ委譲 (例: Terrain の SetNeighbors)
            foreach (var ex in cell.Extras)
            {
                if (ex.Prefab == null) continue;
                ex.Instance = Instantiate(ex.Prefab, transform);
                ex.Instance.name = $"{ex.Content.kind}_{cell.Coord}";
                if (!ex.Instance.activeSelf) ex.Instance.SetActive(true);
                if (CellContentHandlers.TryGet(ex.Content.kind, out var handler))
                    handler.OnActivated(cell.Coord, ex.Instance, this);
            }

            cell.State = CellState.Activated;
            // HLOD は 1 フレーム重複表示してから隠す (spec: ポップのないスワップ)
            cell.HlodReleaseAfterFrame = Time.frameCount + 1;
            cell.HlodDesired = false;
        }

        void DeactivateCell(CellRuntime cell)
        {
            if (cell.State != CellState.Activated) return;

            // キャッシュ済み HLOD があれば実体を消す前に表示 (穴のないスワップ)
            if (cell.Entry.HasHlod && cell.HlodPrefab != null && cell.HlodInstance == null)
            {
                cell.HlodInstance = Instantiate(cell.HlodPrefab, transform);
                cell.HlodInstance.name = $"HLOD_{cell.Coord}";
                if (!cell.HlodInstance.activeSelf) cell.HlodInstance.SetActive(true);
                cell.HlodDesired = true;
            }

            if (cell.Instance != null) Destroy(cell.Instance);
            cell.Instance = null;
            foreach (var ex in cell.Extras)
            {
                if (ex.Instance == null) continue;
                if (CellContentHandlers.TryGet(ex.Content.kind, out var handler))
                    handler.OnDeactivating(cell.Coord, ex.Instance, this);
                Destroy(ex.Instance);
                ex.Instance = null;
            }
            cell.State = CellState.Loaded;
        }

        void UnloadCell(CellRuntime cell)
        {
            if (cell.State != CellState.Loaded || cell.Desired != CellState.Unloaded) return;
            ReleaseAllHandles(cell);
            cell.State = CellState.Unloaded;
        }

        /// <summary>指定セルの追加コンテンツのインスタンスを取得 (隣接接続などハンドラ用)。</summary>
        public GameObject GetExtraContentInstance(CellCoord coord, string kind)
        {
            if (!_grid.TryGetValue(coord, out var cell)) return null;
            foreach (var ex in cell.Extras)
                if (ex.Content.kind == kind)
                    return ex.Instance;
            return null;
        }

        // ---------------------------------------------------------------- HLOD

        void ProcessHlod()
        {
            int frame = Time.frameCount;
            foreach (var cell in _cells)
            {
                if (cell.HlodDesired)
                {
                    if (cell.HlodInstance != null)
                    {
                        if (!cell.HlodInstance.activeSelf) cell.HlodInstance.SetActive(true);
                    }
                    else if (cell.HlodPrefab != null)
                    {
                        cell.HlodInstance = Instantiate(cell.HlodPrefab, transform);
                        cell.HlodInstance.name = $"HLOD_{cell.Coord}";
                        if (!cell.HlodInstance.activeSelf) cell.HlodInstance.SetActive(true);
                    }
                    else if (!cell.HlodLoading && cell.HlodHandle == null)
                    {
                        cell.HlodLoading = true;
                        Loader.LoadAsync(cell.Entry.hlodAddress, (h, prefab) => OnHlodLoaded(cell, h, prefab));
                    }
                }
                else if ((cell.HlodInstance != null || cell.HlodHandle != null)
                         && frame >= cell.HlodReleaseAfterFrame)
                {
                    if (cell.HlodInstance != null) Destroy(cell.HlodInstance);
                    cell.HlodInstance = null;
                    // Activated 中でも HLOD 半径内ならプレハブ/ハンドルを保持
                    // (デアクティベート時に再ロード待ちの「穴」が出るのを防ぐ)
                    bool keepCached = cell.State == CellState.Activated
                                      && cell.Entry.HasHlod
                                      && cell.Distance <= config.HlodRadius + config.hysteresis;
                    if (!keepCached)
                    {
                        if (cell.HlodHandle != null) Loader.Release(cell.HlodHandle);
                        cell.HlodHandle = null;
                        cell.HlodPrefab = null;
                    }
                }
            }
        }

        void OnHlodLoaded(CellRuntime cell, object handle, GameObject prefab)
        {
            if (Instance != this)
            {
                Loader.Release(handle);
                return;
            }

            cell.HlodLoading = false;
            if (prefab == null)
            {
                Loader.Release(handle);
                return;
            }
            if (cell.HlodDesired)
            {
                cell.HlodHandle = handle;
                cell.HlodPrefab = prefab;
            }
            else
            {
                Loader.Release(handle);
            }
        }

        // ---------------------------------------------------------------- レイヤー

        void ApplyDataLayers(GameObject cellInstance)
        {
            var subtrees = cellInstance.GetComponentsInChildren<DataLayerSubtree>(true);
            foreach (var st in subtrees)
                st.gameObject.SetActive(DataLayerManager.IsSubtreeVisible(st.layerNames));
        }

        void OnLayerChanged(string layerName, bool enabled)
        {
            foreach (var cell in _cells)
            {
                if (cell.State != CellState.Activated || cell.Instance == null) continue;
                if (_layerReapplySet.Add(cell))
                    _layerReapply.Enqueue(cell);
            }
        }

        // ------------------------------------------------------------ デバッグ API
        // (spec: world-debug / デバッグコマンド)

        /// <summary>ストリーミング一時停止/再開。停止中はセル状態が変化しない。</summary>
        public void SetPaused(bool paused)
        {
            _paused = paused;
            if (!paused) _nextEvalTime = 0f; // 再開時は即再評価
        }

        /// <summary>全セルを距離判定を無視して Activated にする。</summary>
        public void SetForceLoadAll(bool on)
        {
            _forceLoadAll = on;
            _nextEvalTime = 0f;
        }

        /// <summary>指定セルの状態を強制する (デバッグ)。</summary>
        public void SetCellOverride(CellCoord coord, CellState state)
        {
            _overrides[coord] = state;
            _nextEvalTime = 0f;
        }

        public void ClearCellOverrides()
        {
            _overrides.Clear();
            _nextEvalTime = 0f;
        }

        public bool TryGetCellState(CellCoord coord, out CellState state)
        {
            if (_grid.TryGetValue(coord, out var cell))
            {
                state = cell.State;
                return true;
            }
            state = CellState.Unloaded;
            return false;
        }

        public StreamingStats GetStats()
        {
            var s = new StreamingStats
            {
                QueuedLoads = _loadQueue.Count,
                InFlight = _inFlight,
                LastBudgetUsedMs = _lastBudgetUsedMs,
                ActiveHandles = Loader?.ActiveHandleCount ?? 0,
            };
            foreach (var cell in _cells)
            {
                switch (cell.State)
                {
                    case CellState.Unloaded: s.Unloaded++; break;
                    case CellState.Loading: s.Loading++; break;
                    case CellState.Loaded: s.Loaded++; break;
                    case CellState.Activated: s.Activated++; break;
                }
                if (cell.HlodShown) s.HlodShown++;
            }
            return s;
        }
    }
}
