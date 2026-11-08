using UnityEngine;
using UnityEngine.EventSystems;

namespace SegmentUI
{
    /// <summary>页空白处的输入入口；不覆盖子列表或按钮的射线。</summary>
    public sealed class SegmentPagerInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        SegmentGestureScope Scope => GetComponentInParent<SegmentView>()?.Gesture;
        public void OnPointerDown(PointerEventData e) { if (Scope != null) Scope.Press(e, null); }
        public void OnPointerUp(PointerEventData e) { if (Scope != null) Scope.Release(e); }
        public void OnInitializePotentialDrag(PointerEventData e) { if (Scope != null) Scope.Press(e, null); }
        public void OnBeginDrag(PointerEventData e) { if (Scope != null) Scope.Begin(e, null); }
        public void OnDrag(PointerEventData e) { if (Scope != null) Scope.Drag(e); }
        public void OnEndDrag(PointerEventData e) { if (Scope != null) Scope.End(e); }
    }
}
