using UnityEngine;
using UnityEngine.UI;

namespace SegmentUI.Samples
{
    /// <summary>A normal UGUI page: its ScrollRect retains its position while the page is cached.</summary>
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
        public void ResetList()
        {
            if (Scroll == null) return;
            Scroll.StopMovement();
            Scroll.verticalNormalizedPosition = 1;
            ResetCount++;
        }
    }
}
