using UnityEngine;
using UnityEngine.UI;

namespace SegmentUI.Samples
{
    public sealed class SegmentDemo : MonoBehaviour
    {
        public const int CurrentSchema = 9;
        public int Schema = CurrentSchema;
        public SegmentView View;
        public SegmentDemoPage PageA, PageB, PageNative;
        public SegmentNestedDemoPage PageNested;
        public SegmentItemView BadgeItem;
        public Sprite ItemIcon;
        public SegmentIndicator Underline, Background, Outline;
        public Text Diagnostics;
        public Button Stress, Cancel, Reset, Style, DirectionButton;
        bool initialized, alternate;
        float nextStress;
        int stressRemaining;
        public void Initialize()
        {
            if (initialized) return;
            Canvas.ForceUpdateCanvases();
            View.SetPages(new[] {
                new SegmentPageDescriptor { Id = "dispatch", Item = new SegmentItemData { Title = "Dispatch", Icon = ItemIcon }, Source = PageSource.FromPrefab(PageA) },
                new SegmentPageDescriptor { Id = "activity", Item = new SegmentItemData { Title = "Activity", Icon = ItemIcon, BadgeCount = 8 }, ItemTemplate = BadgeItem, Source = PageSource.FromPrefab(PageB) },
                new SegmentPageDescriptor { Id = "nested", Item = new SegmentItemData { Title = "Nested" }, Source = PageSource.FromPrefab(PageNested) },
                new SegmentPageDescriptor { Id = "native", Item = new SegmentItemData { Title = "Native", Icon = ItemIcon }, Source = PageSource.FromPrefab(PageNative) }
            }, "dispatch");
            View.SetIndicators(new[] { Underline });
            Stress.onClick.AddListener(StartStress);
            Cancel.onClick.AddListener(CancelInput);
            Reset.onClick.AddListener(ResetLists);
            Style.onClick.AddListener(ToggleStyle);
            DirectionButton.onClick.AddListener(ToggleDirection);
            View.DirectionChanged += ApplyDemoDirection;
            ApplyDemoDirection(View.IsRightToLeft);
            UpdateDiagnostics();
            initialized = true;
        }
        public void ToggleDirection() => View.SetDirection(View.IsRightToLeft ? SegmentLayoutDirection.LeftToRight : SegmentLayoutDirection.RightToLeft);
        void ApplyDemoDirection(bool rtl)
        {
            DirectionButton.GetComponentInChildren<Text>().text = rtl ? "DIR: RTL" : "DIR: LTR";
        }
        void OnDestroy() { if (View != null) View.DirectionChanged -= ApplyDemoDirection; }
        void Start() { Initialize(); }
        public void StartStress() { stressRemaining = 30; nextStress = 0; }
        public void CancelInput() { stressRemaining = 0; View.CancelInteraction(SegmentCancelReason.PointerCancelled); }
        public void ResetLists()
        {
            CancelInput();
            foreach (var page in View.GetComponentsInChildren<SegmentDemoPage>(true)) page.ResetList();
        }
        public void ToggleStyle()
        {
            alternate = !alternate;
            View.Bar.LayoutMode = alternate ? SegmentBarLayout.ContentWidth : SegmentBarLayout.Equal;
            View.SetIndicators(alternate ? new[] { Background, Outline } : new[] { Underline });
            Style.GetComponentInChildren<Text>().text = alternate ? "STYLE: PILL" : "STYLE: LINE";
        }
        void Update()
        {
            if (!initialized) return;
            if (stressRemaining > 0 && Time.unscaledTime >= nextStress)
            {
                View.Select(View.EnabledPageIds[stressRemaining % View.EnabledPageIds.Count]);
                stressRemaining--; nextStress = Time.unscaledTime + .055f;
            }
            UpdateDiagnostics();
        }
        public void UpdateDiagnostics()
        {
            int resets = 0;
            foreach (var page in View.GetComponentsInChildren<SegmentDemoPage>(true)) resets += page.ResetCount;
            Diagnostics.text = "CURRENT " + (View.CurrentPageId ?? "--") + "   TARGET " + (View.TargetPageId ?? "--") + "\n" + View.Phase + "  /  LIST RESETS " + resets + "  /  STRESS LEFT " + stressRemaining;
            if (View.CurrentPageId == "nested" && View.GetPage("nested") is SegmentNestedDemoPage nested)
                Diagnostics.text += "\nCHILD " + nested.ChildView.CurrentPageId + "  /  " + nested.ChildView.Phase;
        }
    }
}
