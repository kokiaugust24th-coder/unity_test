using UnityEngine;

namespace OpenWorld
{
    /// <summary>
    /// ベイクが生成する、セルプレハブ内のレイヤー別サブツリーのルート。
    /// 所属レイヤーのいずれかが有効なら表示される (OR 評価)。
    /// </summary>
    public class DataLayerSubtree : MonoBehaviour
    {
        public string[] layerNames = new string[0];
    }
}
