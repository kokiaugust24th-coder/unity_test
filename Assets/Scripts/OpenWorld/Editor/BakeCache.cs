using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenWorld.EditorTools
{
    /// <summary>
    /// 差分ベイク用のセル内容ハッシュキャッシュ (spec: world-asset-pipeline / 差分ベイク)。
    /// Generated/ 以下に保存される。
    /// </summary>
    public class BakeCache : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string cellKey; // "x_z" or "always"
            public string hash;
        }

        public List<Entry> entries = new List<Entry>();

        public string GetHash(string cellKey)
        {
            foreach (var e in entries)
                if (e.cellKey == cellKey)
                    return e.hash;
            return null;
        }

        public void SetHash(string cellKey, string hash)
        {
            foreach (var e in entries)
            {
                if (e.cellKey == cellKey)
                {
                    e.hash = hash;
                    return;
                }
            }
            entries.Add(new Entry { cellKey = cellKey, hash = hash });
        }

        public void RemoveMissing(HashSet<string> validKeys)
        {
            entries.RemoveAll(e => !validKeys.Contains(e.cellKey));
        }
    }
}
