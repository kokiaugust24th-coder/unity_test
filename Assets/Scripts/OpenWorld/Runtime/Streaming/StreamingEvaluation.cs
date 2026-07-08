using UnityEngine;

namespace OpenWorld
{
    /// <summary>
    /// ストリーミング判定の純粋関数群。テスト可能にするため分離 (EditMode テスト対象)。
    /// </summary>
    public static class StreamingEvaluation
    {
        /// <summary>
        /// 単一ソースに対する要求状態を返す。
        /// ヒステリシス: 現在その状態以上のセルは「半径 + hysteresis」を使ってしか降格しない
        /// (spec: world-streaming / 距離判定とヒステリシス)。
        /// </summary>
        public static CellState DesiredForSource(
            CellState current, float distance,
            float activationRadius, float loadRadius, float hysteresis)
        {
            float actR = current >= CellState.Activated ? activationRadius + hysteresis : activationRadius;
            float loadR = current >= CellState.Loading ? loadRadius + hysteresis : loadRadius;

            if (distance <= actR) return CellState.Activated;
            if (distance <= loadR) return CellState.Loaded;
            return CellState.Unloaded;
        }

        /// <summary>HLOD を表示すべきか (spec: world-hlod / HLOD ランタイム表示制御)。</summary>
        public static bool HlodDesired(
            CellState current, bool hasHlod, bool currentlyShown,
            float distance, float hlodRadius, float hysteresis)
        {
            if (!hasHlod) return false;
            if (current == CellState.Activated) return false; // 実セル表示中
            float r = currentlyShown ? hlodRadius + hysteresis : hlodRadius;
            return distance <= r;
        }

        /// <summary>2 状態の大きい方 (複数ソースの合成は最大値)。</summary>
        public static CellState Max(CellState a, CellState b) => a >= b ? a : b;
    }
}
