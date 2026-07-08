using UnityEngine;

namespace OpenWorld
{
    /// <summary>
    /// データレイヤー定義 (spec: world-data-layers)。UE の Data Layer Asset 相当。
    /// </summary>
    [CreateAssetMenu(menuName = "OpenWorld/Data Layer", fileName = "DataLayer")]
    public class DataLayerAsset : ScriptableObject
    {
        [Tooltip("レイヤー名 (一意)。空ならアセット名を使う")]
        public string layerName;
        public bool initiallyEnabled = true;
        [TextArea] public string description;

        public string EffectiveName => string.IsNullOrEmpty(layerName) ? name : layerName;
    }
}
