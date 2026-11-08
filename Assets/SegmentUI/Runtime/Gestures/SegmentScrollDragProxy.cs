using UnityEngine;
using UnityEngine.EventSystems;
namespace SegmentUI
{
    /// <summary>不参与射线命中的拖拽代理；点击目标仍是原业务控件。</summary>
    [AddComponentMenu("")]
    public sealed class SegmentScrollDragProxy : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        internal SegmentScrollRectAdapter Owner;
        public void OnBeginDrag(PointerEventData data) { if (Owner != null) Owner.Begin(data); }
        public void OnDrag(PointerEventData data) { if (Owner != null) Owner.Drag(data); }
        public void OnEndDrag(PointerEventData data) { if (Owner != null) Owner.End(data); }
    }
}
