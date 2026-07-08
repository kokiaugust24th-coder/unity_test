using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace OpenWorld
{
    /// <summary>Addressables 実装の ICellLoader (design.md D3)。</summary>
    public class AddressablesCellLoader : ICellLoader
    {
        readonly HashSet<object> _active = new HashSet<object>();

        public int ActiveHandleCount => _active.Count;

        public object LoadAsync(string address, Action<object, GameObject> onLoaded)
        {
            AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(address);
            object boxed = handle;
            _active.Add(boxed);
            handle.Completed += h =>
            {
                GameObject result = h.Status == AsyncOperationStatus.Succeeded ? h.Result : null;
                if (result == null)
                    Debug.LogError($"[OpenWorld] Failed to load address '{address}'");
                onLoaded?.Invoke(boxed, result);
            };
            return boxed;
        }

        public void Release(object handle)
        {
            if (handle == null) return;
            if (_active.Remove(handle) && handle is AsyncOperationHandle<GameObject> h && h.IsValid())
                Addressables.Release(h);
        }
    }
}
