using System;
using UnityEngine;
using UnityEngine.UI;
namespace SegmentUI.Samples
{
    /// <summary>承载独立子分页的示例页，内部列表使用原生 ScrollRect 预制体。</summary>
    // 嵌套分页示例：子页使用原生 ScrollRect，由绑定阶段安装输入适配器。
    public sealed class SegmentNestedDemoPage : SegmentPage
    {
        public SegmentView ChildView;
        public SegmentDemoPage NativePagePrefab;
        public Text Help;
        /// <summary>三个子标题共用的角标模板，由 Seeder 注入；业务页面仍使用原生 ScrollRect。</summary>
        public SegmentItemView BadgeItem;
        /// <summary>此缓存父页实际接收的更新次数，独立于父演示开启前的累计次数。</summary>
        public int LiveUpdateCount { get; private set; }
        /// <summary>在父页绑定时配置子分页描述，子页面实例仍保持懒加载。</summary>
        protected override void OnBind()
        {
            if (ChildView.EnabledPageIds.Count != 0) return;
            ChildView.SetPages(new[]
            {
                new SegmentPageDescriptor { Id="left", Item=new SegmentItemData { Title="First" }, ItemTemplate=BadgeItem, Source=PageSource.FromPrefab(NativePagePrefab) },
                new SegmentPageDescriptor { Id="middle", Item=new SegmentItemData { Title="Middle" }, ItemTemplate=BadgeItem, Source=PageSource.FromPrefab(NativePagePrefab) },
                new SegmentPageDescriptor { Id="right", Item=new SegmentItemData { Title="Last" }, ItemTemplate=BadgeItem, Source=PageSource.FromPrefab(NativePagePrefab) }
            }, "left");
        }
        /// <summary>由父演示的 LIVE 计时器驱动，只刷新已绑定标题，不选择页面或实例化隐藏子页。</summary>
        public void UpdateLiveItems()
        {
            if (ChildView == null || ChildView.EnabledPageIds.Count == 0) return;
            LiveUpdateCount++;
            int number = LiveUpdateCount % 100, badge = LiveUpdateCount % 10;
            ChildView.UpdateItem("left", new SegmentItemData { Title = "First " + number, BadgeCount = badge });
            ChildView.UpdateItem("middle", new SegmentItemData { Title = "Middle " + number, BadgeCount = badge });
            ChildView.UpdateItem("right", new SegmentItemData { Title = "Last " + number, BadgeCount = badge });
        }
        protected override void OnDirectionChanged(bool rtl)
        {
            if (Help == null) return;
            Help.text = rtl
                ? "LIVE also updates child titles + badges. Right: next; up/down: scroll.\nAt First swipe left / Last swipe right to change parent."
                : "LIVE also updates child titles + badges. Left: next; up/down: scroll.\nAt First swipe right / Last swipe left to change parent.";
        }
        /// <summary>结束父页管理会话时清空子分页数据，释放其受管页面。</summary>
        protected override void OnRelease()
        {
            LiveUpdateCount = 0;
            if (ChildView != null) ChildView.SetPages(Array.Empty<SegmentPageDescriptor>());
        }
    }
}
