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
        public void OnPointerDown(PointerEventData e)
        { if (Scope != null) Scope.Press(e, SegmentScrollRectAdapter.FindReceiver(this)); }
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
