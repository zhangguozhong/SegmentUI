using UnityEngine;
using UnityEngine.EventSystems;
namespace SegmentUI
{
    /// <summary>Non-raycasting drag target; click targets remain the original business controls.</summary>
    [AddComponentMenu("")]
    public sealed class SegmentScrollDragProxy : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        internal SegmentScrollRectAdapter Owner;
        public void OnBeginDrag(PointerEventData data) { if (Owner != null) Owner.Begin(data); }
        public void OnDrag(PointerEventData data) { if (Owner != null) Owner.Drag(data); }
        public void OnEndDrag(PointerEventData data) { if (Owner != null) Owner.End(data); }
    }
}
