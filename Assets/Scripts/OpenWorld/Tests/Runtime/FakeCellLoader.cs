using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenWorld.Tests
{
    /// <summary>テスト用の同期完了ローダ。Addressables 不要でストリーミングを検証できる。</summary>
    public class FakeCellLoader : ICellLoader
    {
        readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
        readonly HashSet<object> _active = new HashSet<object>();
        int _nextId;

        public int ActiveHandleCount => _active.Count;
        public int TotalLoads { get; private set; }

        public void RegisterPrefab(string address, GameObject prefab) => _prefabs[address] = prefab;

        public object LoadAsync(string address, Action<object, GameObject> onLoaded)
        {
            TotalLoads++;
            object handle = _nextId++;
            _active.Add(handle);
            _prefabs.TryGetValue(address, out var prefab);
            onLoaded?.Invoke(handle, prefab);
            return handle;
        }

        public void Release(object handle) => _active.Remove(handle);
    }
}
