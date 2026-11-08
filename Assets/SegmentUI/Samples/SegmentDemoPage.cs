using UnityEngine;
using UnityEngine.UI;

namespace SegmentUI.Samples
{
    /// <summary>普通 UGUI 示例页，页面缓存期间保留 ScrollRect 位置。</summary>
    // 普通/原生列表示例页：展示一次初始化的按钮监听及列表状态复用。
    public sealed class SegmentDemoPage : SegmentPage
    {
        public ScrollRect Scroll;
        public Button TestButton;
        public Text TestLabel;
        public int TapCount { get; private set; }
        protected override void OnInitialize()
        {
            if (TestButton != null) TestButton.onClick.AddListener(CountTap);
        }
        void CountTap()
        {
            TapCount++;
            if (TestLabel != null) TestLabel.text = "TAP OR DRAG THIS BUTTON  /  TAPS " + TapCount + "\nUp/down: scroll   -   Sideways: switch page";
        }
        public int ResetCount { get; private set; }
        /// <summary>停止当前惯性并恢复列表顶部，用于演示宿主操作缓存页面。</summary>
        public void ResetList()
        {
            if (Scroll == null) return;
            Scroll.StopMovement();
            Scroll.verticalNormalizedPosition = 1;
            ResetCount++;
        }
    }
}
