using RTLLayout;
using UnityEngine;

namespace SegmentUI.Integrations
{
    /// <summary>Optional integration: SegmentUI and RTLLayout runtime assemblies stay independent.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RtlLayoutRoot))]
    public sealed class SegmentRtlLayoutBridge : MonoBehaviour
    {
        [Tooltip("For standalone demo chrome. Page and Item on the same object take precedence.")]
        public SegmentView SourceView;
        RtlLayoutRoot layout;
        SegmentPage page;
        SegmentItemView item;
        SegmentView view;
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
        void OnDisable()
        {
            if (page != null) page.DirectionChanged -= Apply;
            if (item != null) item.DirectionChanged -= Apply;
            if (view != null) view.DirectionChanged -= Apply;
            page = null; item = null; view = null;
        }
        /// <summary>Explicit preview/capture sync; does not rely on edit-mode lifecycle callbacks.</summary>
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
