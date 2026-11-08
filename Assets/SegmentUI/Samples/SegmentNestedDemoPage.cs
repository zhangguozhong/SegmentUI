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
        /// <summary>在父页绑定时配置子分页描述，子页面实例仍保持懒加载。</summary>
        protected override void OnBind()
        {
            if (ChildView.EnabledPageIds.Count != 0) return;
            ChildView.SetPages(new[]
            {
                new SegmentPageDescriptor { Id="left", Item=new SegmentItemData { Title="First" }, Source=PageSource.FromPrefab(NativePagePrefab) },
                new SegmentPageDescriptor { Id="middle", Item=new SegmentItemData { Title="Middle" }, Source=PageSource.FromPrefab(NativePagePrefab) },
                new SegmentPageDescriptor { Id="right", Item=new SegmentItemData { Title="Last" }, Source=PageSource.FromPrefab(NativePagePrefab) }
            }, "left");
        }
        protected override void OnDirectionChanged(bool rtl)
        {
            if (Help == null) return;
            Help.text = rtl
                ? "Swipe right for next child; swipe vertically to scroll.\nAt First swipe left / Last swipe right to change parent."
                : "Swipe left for next child; swipe vertically to scroll.\nAt First swipe right / Last swipe left to change parent.";
        }
        /// <summary>结束父页管理会话时清空子分页数据，释放其受管页面。</summary>
        protected override void OnRelease()
        {
            if (ChildView != null) ChildView.SetPages(Array.Empty<SegmentPageDescriptor>());
        }
    }
}
