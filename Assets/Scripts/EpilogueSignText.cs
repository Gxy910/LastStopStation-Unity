using UnityEngine;

namespace YuDian
{
    /// <summary>供第四章场景摆放点在 Inspector 中编辑告示内容。</summary>
    public sealed class EpilogueSignText : MonoBehaviour
    {
        [InspectorName("告示文字")]
        [TextArea(1, 3)]
        public string text = "暖棚入口";
    }
}
