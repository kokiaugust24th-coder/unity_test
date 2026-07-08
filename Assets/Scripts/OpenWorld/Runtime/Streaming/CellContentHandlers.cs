using System.Collections.Generic;
using UnityEngine;

namespace OpenWorld
{
    /// <summary>
    /// コンテンツ種別ごとのアクティベートフック (spec: terrain-streaming / セルコンテンツ抽象化)。
    /// ロード/状態機械はコア (WorldStreamingManager) が所有し、ハンドラは
    /// インスタンス化直後/破棄直前の種別固有処理 (例: Terrain の SetNeighbors) のみを行う。
    /// </summary>
    public interface ICellContentHandler
    {
        void OnActivated(CellCoord coord, GameObject instance, WorldStreamingManager manager);
        void OnDeactivating(CellCoord coord, GameObject instance, WorldStreamingManager manager);
    }

    public static class CellContentHandlers
    {
        static readonly Dictionary<string, ICellContentHandler> _handlers =
            new Dictionary<string, ICellContentHandler>();

        public static void Register(string kind, ICellContentHandler handler)
        {
            if (!string.IsNullOrEmpty(kind) && handler != null)
                _handlers[kind] = handler;
        }

        public static bool TryGet(string kind, out ICellContentHandler handler) =>
            _handlers.TryGetValue(kind ?? "", out handler);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => _handlers.Clear();
    }
}
