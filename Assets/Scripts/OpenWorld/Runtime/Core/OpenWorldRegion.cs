using UnityEngine;

namespace OpenWorld
{
    /// <summary>
    /// オーサリングシーンのワールドコンテンツルートに付けるマーカー。
    /// この直下の各子オブジェクト (静的) が 1 ストリーミング単位としてベイクされる。
    /// プレイ時は WorldStreamingManager が自動で無効化する (設定可)。
    /// </summary>
    public class OpenWorldRegion : MonoBehaviour
    {
    }
}
