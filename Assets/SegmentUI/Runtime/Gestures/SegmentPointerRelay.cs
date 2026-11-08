using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace SegmentUI
{
    /// <summary>普通 Button 的按下/抬起桥；UGUI 不会把 PointerUp 冒泡给祖先 ScrollRect。</summary>
    [DisallowMultipleComponent]
    public sealed class SegmentPointerRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        SegmentGestureScope Scope => SegmentGestureScope.Find(this);
        /// <summary>普通按钮命中后主动申请祖先作用域，避免按下事件不冒泡导致手势未登记。</summary>
        public void OnPointerDown(PointerEventData e)
        { if (Scope != null) Scope.Press(e, SegmentScrollRectAdapter.FindReceiver(this)); }
        /// <summary>显式补转抬起事件；已开始拖拽时作用域等待 EndDrag，不提前释放。</summary>
        public void OnPointerUp(PointerEventData e) { if (Scope != null) Scope.Release(e); }
        /// <summary>为运行时新建的普通按钮安装桥；自带 SegmentButton 的模板无需调用。</summary>
        public static void Install(Transform root)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                if (!(button is SegmentButton) && button.GetComponent<SegmentPointerRelay>() == null)
                    button.gameObject.AddComponent<SegmentPointerRelay>();
        }
    }
}
