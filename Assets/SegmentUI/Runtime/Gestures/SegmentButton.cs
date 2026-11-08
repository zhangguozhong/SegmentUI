using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SegmentUI
{
    /// <summary>可与分页作用域共享指针的按钮。保留标准 Button 导航和点击接口。</summary>
    public class SegmentButton : Button
    {
        int? acceptedPointer;
        SegmentGestureScope Scope => SegmentGestureScope.Find(this);
        public override void OnPointerDown(PointerEventData e)
        {
            var scope = Scope;
            if (acceptedPointer.HasValue && acceptedPointer != e.pointerId && scope != null && scope.HasActivePointer) { e.eligibleForClick = false; return; }
            bool accepted = scope == null || scope.Press(e, SegmentScrollRectAdapter.FindReceiver(this));
            if (accepted) { acceptedPointer = e.pointerId; base.OnPointerDown(e); }
        }
        public override void OnPointerUp(PointerEventData e)
        {
            base.OnPointerUp(e); if (Scope != null) Scope.Release(e);
            if (acceptedPointer == e.pointerId && (e.dragging || !e.eligibleForClick)) acceptedPointer = null;
        }
        public override void OnPointerClick(PointerEventData e)
        {
            if (acceptedPointer != e.pointerId) return;
            if (e.eligibleForClick) base.OnPointerClick(e);
            acceptedPointer = null;
        }
        public override void OnSubmit(BaseEventData e)
        { if (Scope == null || !Scope.HasActivePointer) base.OnSubmit(e); }
        protected override void OnDisable() { acceptedPointer = null; base.OnDisable(); }
    }
}
