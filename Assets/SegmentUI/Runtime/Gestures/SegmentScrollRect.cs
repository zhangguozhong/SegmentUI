using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace SegmentUI
{
    public class SegmentScrollRect : ScrollRect, IPointerDownHandler, IPointerUpHandler, ISegmentScrollReceiver
    {
        [Tooltip("标题栏横向滚动只移动标题，不拖动页面。普通页面列表保持关闭。")]
        public bool IsTitleBar;
        bool ISegmentScrollReceiver.IsTitleBar => IsTitleBar;
        public event Action<PointerEventData> OwnedBegin, OwnedDrag, OwnedEnd, OwnedCancel;
        public SegmentGestureScope Scope => SegmentGestureScope.Find(this);
        public void OnPointerDown(PointerEventData data) { if (Scope != null) Scope.Press(data, this); }
        public void OnPointerUp(PointerEventData data) { if (Scope != null) Scope.Release(data); }
        public override void OnInitializePotentialDrag(PointerEventData data)
        {
            if (Scope == null) { base.OnInitializePotentialDrag(data); return; }
            // Called even when a child Button received PointerDown; no ancestor bubbling assumed.
            if (Scope.Press(data, this)) StopMovement();
        }
        public override void OnBeginDrag(PointerEventData data)
        { if (Scope == null) base.OnBeginDrag(data); else Scope.Begin(data, this); }
        public override void OnDrag(PointerEventData data)
        { if (Scope == null) base.OnDrag(data); else Scope.Drag(data); }
        public override void OnEndDrag(PointerEventData data)
        { if (Scope == null) base.OnEndDrag(data); else Scope.End(data); }
        public override void OnScroll(PointerEventData data)
        {
            if (Scope != null && (Scope.HasActivePointer || !GetComponentInParent<SegmentView>().CanReceiveInput)) return;
            if (!IsTitleBar) { var delta = data.scrollDelta; data.scrollDelta = new Vector2(0, delta.y); base.OnScroll(data); data.scrollDelta = delta; }
        }
        public void BeginOwned(PointerEventData data) { OwnedBegin?.Invoke(data); base.OnInitializePotentialDrag(data); base.OnBeginDrag(data); }
        public void DragOwned(PointerEventData data) { base.OnDrag(data); OwnedDrag?.Invoke(data); }
        public void EndOwned(PointerEventData data) { base.OnEndDrag(data); OwnedEnd?.Invoke(data); }
        public void CancelOwned(PointerEventData data)
        { if (data != null) base.OnEndDrag(data); StopMovement(); OwnedCancel?.Invoke(data); }
        protected override void OnDisable()
        {
            if (Scope != null && Scope.OwnsReceiver(this)) GetComponentInParent<SegmentView>().CancelInteraction(SegmentCancelReason.Disabled);
            base.OnDisable();
        }
    }
}
