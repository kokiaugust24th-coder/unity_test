using UnityEngine;

namespace OpenWorld
{
    /// <summary>
    /// オーサリング時にストリーミング単位へレイヤーを割り当てるコンポーネント。
    /// 複数レイヤー割当可。ベイク時にセルプレハブ内のレイヤー別サブツリーへ分類される。
    /// </summary>
    public class DataLayerTag : MonoBehaviour
    {
        public DataLayerAsset[] layers = new DataLayerAsset[0];
    }
}
