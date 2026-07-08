using NUnit.Framework;

namespace OpenWorld.Tests
{
    public class StreamingEvaluationTests
    {
        const float ActR = 256f;
        const float LoadR = 512f;
        const float Hyst = 32f;

        static CellState Eval(CellState current, float dist) =>
            StreamingEvaluation.DesiredForSource(current, dist, ActR, LoadR, Hyst);

        // spec: world-streaming / 段階的アクティベーション
        [Test]
        public void BetweenRadii_IsLoadedNotActivated()
        {
            Assert.AreEqual(CellState.Loaded, Eval(CellState.Unloaded, 400f));
        }

        [Test]
        public void InsideActivationRadius_IsActivated()
        {
            Assert.AreEqual(CellState.Activated, Eval(CellState.Unloaded, 100f));
        }

        [Test]
        public void OutsideLoadRadius_IsUnloaded()
        {
            Assert.AreEqual(CellState.Unloaded, Eval(CellState.Unloaded, 600f));
        }

        // spec: world-streaming / 境界振動の防止
        [Test]
        public void Hysteresis_KeepsLoadedJustOutsideRadius()
        {
            // ロード済みセルは loadRadius + hysteresis までロード維持
            Assert.AreEqual(CellState.Loaded, Eval(CellState.Loaded, LoadR + Hyst - 1f));
            Assert.AreEqual(CellState.Unloaded, Eval(CellState.Loaded, LoadR + Hyst + 1f));
        }

        [Test]
        public void Hysteresis_DoesNotWidenLoadForUnloadedCell()
        {
            // 未ロードセルはヒステリシスの恩恵なし → 素の半径で判定
            Assert.AreEqual(CellState.Unloaded, Eval(CellState.Unloaded, LoadR + 1f));
        }

        [Test]
        public void Hysteresis_KeepsActivatedJustOutsideActivationRadius()
        {
            Assert.AreEqual(CellState.Activated, Eval(CellState.Activated, ActR + Hyst - 1f));
            Assert.AreEqual(CellState.Loaded, Eval(CellState.Activated, ActR + Hyst + 1f));
        }

        // spec: world-streaming / 複数ソースの合成
        [Test]
        public void MultiSource_TakesMax()
        {
            Assert.AreEqual(CellState.Activated,
                StreamingEvaluation.Max(CellState.Loaded, CellState.Activated));
        }

        // spec: world-hlod / HLOD ランタイム表示制御
        [Test]
        public void Hlod_ShownOutsideActivationInsideHlodRadius()
        {
            Assert.IsTrue(StreamingEvaluation.HlodDesired(
                CellState.Unloaded, hasHlod: true, currentlyShown: false,
                distance: 1000f, hlodRadius: 2048f, hysteresis: Hyst));
        }

        [Test]
        public void Hlod_HiddenWhenActivated()
        {
            Assert.IsFalse(StreamingEvaluation.HlodDesired(
                CellState.Activated, hasHlod: true, currentlyShown: true,
                distance: 10f, hlodRadius: 2048f, hysteresis: Hyst));
        }

        [Test]
        public void Hlod_SkippedWhenCellHasNoHlod()
        {
            Assert.IsFalse(StreamingEvaluation.HlodDesired(
                CellState.Unloaded, hasHlod: false, currentlyShown: false,
                distance: 100f, hlodRadius: 2048f, hysteresis: Hyst));
        }
    }
}
