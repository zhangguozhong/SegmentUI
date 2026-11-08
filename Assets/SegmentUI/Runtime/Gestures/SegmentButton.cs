using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SegmentUI
{
    /// <summary>可与分页作用域共享指针的按钮。保留标准 Button 导航和点击接口。</summary>
    public class SegmentButton : Button
    {
        int? acceptedPointer;
        SegmentGestureScope Scope => SegmentGestureScope.Find(this);
        /// <summary>先争取指针所有权，再更新按钮按下状态；第二指针不能夺取已有手势。</summary>
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
        /// <summary>只有原先获准且未被拖拽/取消剥夺点击资格的指针才能触发点击。</summary>
        public override void OnPointerClick(PointerEventData e)
        {
            if (acceptedPointer != e.pointerId) return;
            if (e.eligibleForClick) base.OnPointerClick(e);
            acceptedPointer = null;
        }
        /// <summary>嵌套层级存在活动指针时阻止键盘提交，避免同时产生第二种选择输入。</summary>
        public override void OnSubmit(BaseEventData e)
        { if (Scope == null || !Scope.HasActivePointer) base.OnSubmit(e); }
        protected override void OnDisable() { acceptedPointer = null; base.OnDisable(); }
    }
}
