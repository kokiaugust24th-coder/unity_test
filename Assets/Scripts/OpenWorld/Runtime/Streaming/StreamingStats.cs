namespace OpenWorld
{
    /// <summary>ランタイム統計 (spec: world-debug / ランタイム統計 HUD)。</summary>
    public struct StreamingStats
    {
        public int Unloaded;
        public int Loading;
        public int Loaded;
        public int Activated;
        public int QueuedLoads;
        public int InFlight;
        public int HlodShown;
        public float LastBudgetUsedMs;
        public int ActiveHandles;
    }
}
