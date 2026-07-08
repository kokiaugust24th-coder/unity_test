using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenWorld
{
    /// <summary>
    /// ベイクが生成するワールド定義。ランタイムはこれだけを参照してストリーミングする。
    /// (spec: world-asset-pipeline / ワールドベイク)
    /// </summary>
    public class WorldManifest : ScriptableObject
    {
        /// <summary>追加コンテンツ (種別 + アドレス)。例: kind = "terrain"。</summary>
        [Serializable]
        public class CellContent
        {
            public string kind;
            public string address;
        }

        [Serializable]
        public class CellEntry
        {
            public int x;
            public int z;
            public bool alwaysLoaded;
            public string address;      // セルプレハブの Addressables アドレス
            public string hlodAddress;  // HLOD プレハブのアドレス (無ければ空)
            public int objectCount;
            public string[] layerNames = Array.Empty<string>(); // セル内に出現するレイヤー
            [Tooltip("追加コンテンツ (Terrain タイル等)。プレハブ (address) 無しのセルも可")]
            public CellContent[] contents = Array.Empty<CellContent>();

            public CellCoord Coord => alwaysLoaded ? CellCoord.AlwaysLoaded : new CellCoord(x, z);
            public bool HasHlod => !string.IsNullOrEmpty(hlodAddress);
        }

        [Serializable]
        public class LayerDef
        {
            public string name;
            public bool initiallyEnabled = true;
        }

        public float cellSize = 256f;
        public List<CellEntry> cells = new List<CellEntry>();
        public List<LayerDef> layers = new List<LayerDef>();
    }
}
