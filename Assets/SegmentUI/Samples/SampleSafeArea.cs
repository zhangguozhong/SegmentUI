using UnityEngine;

namespace SegmentUI.Samples
{
    /// <summary>仅用于示例宿主的安全区；SegmentView 及子页面继承此区域。</summary>
    // 仅示例 Canvas 使用的安全区适配；生产组件继承宿主区域，避免重复缩进。
    [ExecuteAlways, RequireComponent(typeof(RectTransform))]
    public sealed class SampleSafeArea : MonoBehaviour
    {
        void OnEnable() { Apply(); }
        void Update() { Apply(); }
        void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var area = Screen.safeArea;
            if (area.width <= 0 || area.height <= 0 || area.xMin < 0 || area.yMin < 0 || area.xMax > Screen.width || area.yMax > Screen.height)
                area = new Rect(0,0,Screen.width,Screen.height);
            var rect = (RectTransform)transform;
            var min = new Vector2(area.xMin / Screen.width,area.yMin / Screen.height);
            var max = new Vector2(area.xMax / Screen.width,area.yMax / Screen.height);
            if (rect.anchorMin == min && rect.anchorMax == max) return;
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
