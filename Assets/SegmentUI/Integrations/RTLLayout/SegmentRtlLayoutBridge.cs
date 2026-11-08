using RTLLayout;
using UnityEngine;

namespace SegmentUI.Integrations
{
    /// <summary>可选方向桥接，保持 SegmentUI 与 RTLLayout 两个核心程序集独立。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RtlLayoutRoot))]
    public sealed class SegmentRtlLayoutBridge : MonoBehaviour
    {
        /// <summary>独立容器的方向来源；同节点的 SegmentPage 或 SegmentItemView 优先级更高。</summary>
        [Tooltip("For standalone demo chrome. Page and Item on the same object take precedence.")]
        public SegmentView SourceView;
        RtlLayoutRoot layout;
        SegmentPage page;
        SegmentItemView item;
        SegmentView view;
        /// <summary>先订阅方向事件；仅已绑定页面/标题才立即同步，避免缓存业务绑定前的错误基准。</summary>
        void OnEnable()
        {
            layout = GetComponent<RtlLayoutRoot>();
            page = GetComponent<SegmentPage>();
            item = GetComponent<SegmentItemView>();
            if (page != null) { page.DirectionChanged += Apply; if (!page.BindingCancellation.IsCancellationRequested) Apply(page.IsRightToLeft); }
            else if (item != null) { item.DirectionChanged += Apply; if (item.PageId != null) Apply(item.RightToLeft); }
            else
            {
                view = SourceView != null ? SourceView : GetComponent<SegmentView>();
                if (view != null) { view.DirectionChanged += Apply; Apply(view.IsRightToLeft); }
            }
        }
        /// <summary>成对取消订阅，重新启用时重新解析来源并同步当前方向。</summary>
        void OnDisable()
        {
            if (page != null) page.DirectionChanged -= Apply;
            if (item != null) item.DirectionChanged -= Apply;
            if (view != null) view.DirectionChanged -= Apply;
            page = null; item = null; view = null;
        }
        /// <summary>显式同步预览/截图方向，不依赖编辑模式下的生命周期回调。</summary>
        public void Synchronize()
        {
            layout = GetComponent<RtlLayoutRoot>();
            var sourcePage = GetComponent<SegmentPage>();
            var sourceItem = GetComponent<SegmentItemView>();
            if (sourcePage != null)
            {
                if (!sourcePage.BindingCancellation.IsCancellationRequested) Apply(sourcePage.IsRightToLeft);
            }
            else if (sourceItem != null)
            {
                if (sourceItem.PageId != null) Apply(sourceItem.RightToLeft);
            }
            else
            {
                var source = SourceView != null ? SourceView : GetComponent<SegmentView>();
                if (source != null) Apply(source.IsRightToLeft);
            }
        }
        void Apply(bool rtl) => layout.SetRightToLeft(rtl);
    }
}
