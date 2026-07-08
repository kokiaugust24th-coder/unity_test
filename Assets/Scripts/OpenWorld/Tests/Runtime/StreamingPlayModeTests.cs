using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenWorld.Tests
{
    public class StreamingPlayModeTests
    {
        WorldPartitionConfig _config;
        WorldManifest _manifest;
        FakeCellLoader _loader;
        WorldStreamingManager _manager;
        GameObject _managerGo;
        GameObject _sourceGo;
        GameObject _prefabTemplate;
        GameObject _hlodTemplate;

        const float CellSize = 100f;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<WorldPartitionConfig>();
            _config.cellSize = CellSize;
            _config.activationRadius = 100f;
            _config.loadRadius = 200f;
            _config.hlodRadiusMultiplier = 4f; // HLOD 半径 800
            _config.hysteresis = 10f;
            _config.maxInFlightLoads = 4;
            _config.frameBudgetMs = 10f;
            _config.evaluationInterval = 0.02f;
            _config.staticBatchOnActivate = false;

            _manifest = ScriptableObject.CreateInstance<WorldManifest>();
            _manifest.cellSize = CellSize;
            _manifest.layers.Add(new WorldManifest.LayerDef { name = "QuestA", initiallyEnabled = false });

            // セル (0,0): レイヤーサブツリー付き / セル (5,0): HLOD 付き遠方セル
            _prefabTemplate = new GameObject("cell_prefab_template");
            var layered = new GameObject("Layers_QuestA");
            layered.transform.SetParent(_prefabTemplate.transform);
            layered.AddComponent<DataLayerSubtree>().layerNames = new[] { "QuestA" };
            _prefabTemplate.SetActive(false);

            _hlodTemplate = new GameObject("hlod_prefab_template");
            _hlodTemplate.SetActive(false);

            _manifest.cells.Add(new WorldManifest.CellEntry { x = 0, z = 0, address = "cell_0_0" });
            _manifest.cells.Add(new WorldManifest.CellEntry
            { x = 5, z = 0, address = "cell_5_0", hlodAddress = "hlod_5_0" });

            _loader = new FakeCellLoader();
            _loader.RegisterPrefab("cell_0_0", _prefabTemplate);
            _loader.RegisterPrefab("cell_5_0", _prefabTemplate);
            _loader.RegisterPrefab("hlod_5_0", _hlodTemplate);

            _managerGo = new GameObject("manager");
            _managerGo.SetActive(false);
            _manager = _managerGo.AddComponent<WorldStreamingManager>();
            _manager.Configure(_config, _manifest, _loader);

            _sourceGo = new GameObject("source");
            _sourceGo.transform.position = new Vector3(50f, 0f, 50f); // セル (0,0) 内
            _sourceGo.AddComponent<StreamingSource>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_sourceGo);
            Object.Destroy(_managerGo);
            Object.Destroy(_prefabTemplate);
            Object.Destroy(_hlodTemplate);
            Object.Destroy(_config);
            Object.Destroy(_manifest);
        }

        IEnumerator Settle(float seconds = 0.3f)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        // spec: world-streaming / アクティベート + 近いセルの優先
        [UnityTest]
        public IEnumerator NearCellActivates_FarCellStaysUnloaded()
        {
            _managerGo.SetActive(true);
            yield return Settle();

            Assert.IsTrue(_manager.TryGetCellState(new CellCoord(0, 0), out var near));
            Assert.AreEqual(CellState.Activated, near);

            _manager.TryGetCellState(new CellCoord(5, 0), out var far); // 距離 400 > loadRadius
            Assert.AreEqual(CellState.Unloaded, far);
        }

        // spec: world-streaming / アンロードと資源解放, world-asset-pipeline / リークなしの解放
        [UnityTest]
        public IEnumerator MovingAway_UnloadsAndReleasesAllHandles()
        {
            _managerGo.SetActive(true);
            yield return Settle();
            Assert.Greater(_loader.ActiveHandleCount, 0);

            _sourceGo.transform.position = new Vector3(100000f, 0f, 100000f);
            yield return Settle(0.5f);

            _manager.TryGetCellState(new CellCoord(0, 0), out var state);
            Assert.AreEqual(CellState.Unloaded, state);
            Assert.AreEqual(0, _loader.ActiveHandleCount, "未解放ハンドルが残っています");
        }

        // spec: world-hlod / 遠景での HLOD 表示 + ポップのないスワップ
        [UnityTest]
        public IEnumerator FarCellShowsHlod_ThenSwapsToRealCellOnApproach()
        {
            _managerGo.SetActive(true);
            yield return Settle();

            var farCell = _manager.Cells.First(c => c.Coord == new CellCoord(5, 0));
            Assert.IsNotNull(farCell.HlodInstance, "HLOD 半径内なのに HLOD が表示されていない");
            Assert.AreEqual(CellState.Unloaded, farCell.State);

            // セル (5,0) 内へ移動 → 実セル表示 + HLOD 解放
            _sourceGo.transform.position = new Vector3(550f, 0f, 50f);
            yield return Settle(0.5f);

            Assert.AreEqual(CellState.Activated, farCell.State);
            Assert.IsNull(farCell.HlodInstance, "アクティベート後も HLOD が残っている");
        }

        // spec: world-data-layers / 無効レイヤーの抑制 + ランタイム有効化の即時反映
        [UnityTest]
        public IEnumerator DisabledLayerHidden_EnableShowsIt()
        {
            _managerGo.SetActive(true);
            yield return Settle();

            var cell = _manager.Cells.First(c => c.Coord == new CellCoord(0, 0));
            var subtree = cell.Instance.GetComponentsInChildren<DataLayerSubtree>(true).First();
            Assert.IsFalse(subtree.gameObject.activeSelf, "無効レイヤーのサブツリーが表示されている");

            DataLayerManager.SetLayerEnabled("QuestA", true);
            yield return Settle();
            Assert.IsTrue(subtree.gameObject.activeSelf, "有効化がロード済みセルに反映されていない");
        }

        // spec: world-debug / 全ロード + ストリーミング一時停止
        [UnityTest]
        public IEnumerator ForceLoadAll_ActivatesEverything_PauseFreezesState()
        {
            _managerGo.SetActive(true);
            _manager.SetForceLoadAll(true);
            yield return Settle(0.5f);

            foreach (var cell in _manager.Cells)
                Assert.AreEqual(CellState.Activated, cell.State, $"{cell.Coord} が Activated でない");

            _manager.SetPaused(true);
            _manager.SetForceLoadAll(false);
            _sourceGo.transform.position = new Vector3(100000f, 0f, 100000f);
            yield return Settle();

            foreach (var cell in _manager.Cells)
                Assert.AreEqual(CellState.Activated, cell.State, "一時停止中に状態が変化した");
        }
    }
}
