using UnityEngine;

namespace SegmentUI.Samples
{
    /// <summary>Sample-only host safe area. SegmentView and child pages inherit this region.</summary>
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
