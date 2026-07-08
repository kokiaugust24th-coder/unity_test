using System;
using UnityEngine;

namespace OpenWorld
{
    /// <summary>
    /// セル/HLOD プレハブの非同期ロード抽象。
    /// 本番は AddressablesCellLoader、テストは FakeCellLoader を注入する。
    /// </summary>
    public interface ICellLoader
    {
        /// <summary>
        /// 非同期ロードを開始しハンドルを返す。完了時に onLoaded(handle, prefab) が呼ばれる。
        /// 失敗時は prefab = null。
        /// </summary>
        object LoadAsync(string address, Action<object, GameObject> onLoaded);

        /// <summary>ハンドルを解放しメモリを返却する。</summary>
        void Release(object handle);

        /// <summary>未解放ハンドル数 (リーク検出用。spec: world-asset-pipeline / メモリ管理)。</summary>
        int ActiveHandleCount { get; }
    }
}
