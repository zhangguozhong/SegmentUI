using System;
using UnityEngine;
using UnityEngine.UI;
namespace SegmentUI.Samples
{
    /// <summary>A page hosting an independent child pager; child lists are native ScrollRect prefabs.</summary>
    public sealed class SegmentNestedDemoPage : SegmentPage
    {
        public SegmentView ChildView;
        public SegmentDemoPage NativePagePrefab;
        public Text Help;
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
        protected override void OnRelease()
        {
            if (ChildView != null) ChildView.SetPages(Array.Empty<SegmentPageDescriptor>());
        }
    }
}
