namespace OpenWorld
{
    /// <summary>
    /// セル状態機械 (spec: world-streaming / セル状態機械)。
    /// Loaded = アセットがメモリ上にあり非表示。Activated = インスタンス化され表示・有効。
    /// </summary>
    public enum CellState
    {
        Unloaded = 0,
        Loading = 1,
        Loaded = 2,
        Activated = 3,
    }
}
