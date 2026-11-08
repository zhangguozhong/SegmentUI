using UnityEngine;
using UnityEngine.UI;

namespace SegmentUI.Samples
{
    /// <summary>示例入口：组织页面、按钮、方向与压力演示，不属于核心业务 API。</summary>
    public sealed class SegmentDemo : MonoBehaviour
    {
        public const int CurrentSchema = 10;
        public int Schema = CurrentSchema;
        public SegmentView View;
        public SegmentDemoPage PageA, PageB, PageNative;
        public SegmentNestedDemoPage PageNested;
        public SegmentItemView BadgeItem;
        public Sprite ItemIcon;
        public SegmentIndicator Underline, Background, Outline;
        public Text Diagnostics;
        public Button Stress, Cancel, Reset, Style, DirectionButton, LiveUpdateButton;
        bool initialized, alternate;
        float nextStress, nextLiveUpdate;
        /// <summary>持续更新开关，默认关闭；计时器不依赖分页是否处于拖拽或动画状态。</summary>
        public bool LiveUpdatesEnabled { get; private set; }
        public int LiveUpdateCount { get; private set; }
        int stressRemaining;
        /// <summary>通过固定 PageId 配置演示数据与操作入口；初始化失败后可补齐引用再调用。</summary>
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
            LiveUpdateButton.onClick.AddListener(ToggleLiveUpdates);
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
        /// <summary>重置已创建页面中的列表，包括已经懒加载的嵌套子页。</summary>
        public void ResetLists()
        {
            CancelInput();
            foreach (var page in View.GetComponentsInChildren<SegmentDemoPage>(true)) page.ResetList();
        }
        /// <summary>切换单一下划线与背景/描边组合，演示多个指示器同时工作。</summary>
        public void ToggleStyle()
        {
            alternate = !alternate;
            // 示例 BadgeItem 的文字左右预留共 136；扣除图标与间距后仍需至少 100，避免动态标题换行。
            View.Bar.ItemPadding = alternate ? 100 : 32;
            View.Bar.LayoutMode = alternate ? SegmentBarLayout.ContentWidth : SegmentBarLayout.Equal;
            View.SetIndicators(alternate ? new[] { Background, Outline } : new[] { Underline });
            Style.GetComponentInChildren<Text>().text = alternate ? "STYLE: PILL" : "STYLE: LINE";
        }
        /// <summary>开启后每 0.1 秒原地更新 Activity；关闭只停计时器，不恢复旧数据或取消交互。</summary>
        public void ToggleLiveUpdates()
        {
            LiveUpdatesEnabled = !LiveUpdatesEnabled;
            LiveUpdateButton.GetComponentInChildren<Text>().text = LiveUpdatesEnabled ? "LIVE: ON" : "LIVE: OFF";
            if (LiveUpdatesEnabled) UpdateLiveItem();
        }
        /// <summary>演示调用 UpdateItem：保留图标，角标循环包含零以便检查隐藏；不提交 SetPages。</summary>
        public void UpdateLiveItem()
        {
            if (!LiveUpdatesEnabled) return;
            LiveUpdateCount++;
            View.UpdateItem("activity", new SegmentItemData {
                Title = "Activity " + (LiveUpdateCount % 100), Icon = ItemIcon, BadgeCount = LiveUpdateCount % 10
            });
            nextLiveUpdate = Time.unscaledTime + .1f;
        }
        void Update()
        {
            if (!initialized) return;
            if (stressRemaining > 0 && Time.unscaledTime >= nextStress)
            {
                View.Select(View.EnabledPageIds[stressRemaining % View.EnabledPageIds.Count]);
                stressRemaining--; nextStress = Time.unscaledTime + .055f;
            }
            if (LiveUpdatesEnabled && Time.unscaledTime >= nextLiveUpdate) UpdateLiveItem();
            UpdateDiagnostics();
        }
        public void UpdateDiagnostics()
        {
            int resets = 0;
            foreach (var page in View.GetComponentsInChildren<SegmentDemoPage>(true)) resets += page.ResetCount;
            Diagnostics.text = "CURRENT " + (View.CurrentPageId ?? "--") + "   TARGET " + (View.TargetPageId ?? "--") + "\n" + View.Phase + " / RESETS " + resets + " / STRESS " + stressRemaining + " / UPDATES " + LiveUpdateCount;
            if (View.CurrentPageId == "nested" && View.GetPage("nested") is SegmentNestedDemoPage nested)
                Diagnostics.text += "\nCHILD " + nested.ChildView.CurrentPageId + "  /  " + nested.ChildView.Phase;
        }
    }
}
