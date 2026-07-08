using UnityEngine;

namespace OpenWorld
{
    /// <summary>セル 1 つ分のランタイム状態。WorldStreamingManager 内部用。</summary>
    public class CellRuntime
    {
        public WorldManifest.CellEntry Entry;
        public CellCoord Coord;

        public CellState State = CellState.Unloaded;
        public CellState Desired = CellState.Unloaded;

        public object AssetHandle;
        public GameObject Prefab;
        public GameObject Instance;

        /// <summary>追加コンテンツ (Terrain タイル等) のロード状態。</summary>
        public class ExtraContent
        {
            public WorldManifest.CellContent Content;
            public object Handle;
            public GameObject Prefab;
            public GameObject Instance;
        }

        public readonly System.Collections.Generic.List<ExtraContent> Extras =
            new System.Collections.Generic.List<ExtraContent>();
        public int PendingLoads;
        public bool AnyLoadFailed;

        // HLOD
        public bool HlodDesired;
        public bool HlodLoading;
        public object HlodHandle;
        public GameObject HlodPrefab;
        public GameObject HlodInstance;
        /// <summary>この Unity フレーム以降なら HLOD を隠してよい (1 フレーム重複表示でポップ抑制)。</summary>
        public int HlodReleaseAfterFrame;

        public float Distance = float.MaxValue;
        public int SourcePriority;

        public bool HlodShown => HlodInstance != null && HlodInstance.activeSelf;
    }
}
