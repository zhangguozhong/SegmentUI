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
        /// <summary>空白区域没有滚动接收器，直接申请分页手势；重复 Press 由作用域去重。</summary>
        public void OnInitializePotentialDrag(PointerEventData e) { if (Scope != null) Scope.Press(e, null); }
        /// <summary>超过 EventSystem 起拖阈值后才让作用域锁定方向。</summary>
        public void OnBeginDrag(PointerEventData e) { if (Scope != null) Scope.Begin(e, null); }
        /// <summary>只转发位移，不直接修改内容坐标，所有权由作用域统一判断。</summary>
        public void OnDrag(PointerEventData e) { if (Scope != null) Scope.Drag(e); }
        /// <summary>交给作用域确定正常释放目标和清理指针状态。</summary>
        public void OnEndDrag(PointerEventData e) { if (Scope != null) Scope.End(e); }
    }
}
