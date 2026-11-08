using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SegmentUI
{
    /// <summary>Preserves the original ScrollRect and routes its drag through a separate event target.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ScrollRect))]
    public sealed class SegmentScrollRectAdapter : MonoBehaviour, IInitializePotentialDragHandler,
        IPointerDownHandler, IPointerUpHandler, ISegmentScrollReceiver
    {
        ScrollRect target;
        SegmentScrollDragProxy proxy;
        public ScrollRect Target => target != null ? target : (target = GetComponent<ScrollRect>());
        public bool IsTitleBar => false;
        public event Action<PointerEventData> OwnedBegin, OwnedDrag, OwnedEnd, OwnedCancel;
        SegmentGestureScope Scope => SegmentGestureScope.Find(this);
        bool Available => isActiveAndEnabled && Target != null && Target.isActiveAndEnabled;
        public void OnPointerDown(PointerEventData data)
        { if (Available && Scope != null) Scope.Press(data, this); }
        public void OnPointerUp(PointerEventData data)
        { if (Scope != null) Scope.Release(data); }
        public void OnInitializePotentialDrag(PointerEventData data)
        {
            if (!Available || Scope == null) return;
            if (Target is SegmentScrollRect) return;
            if (proxy == null)
            {
                var node = new GameObject("SegmentDragProxy");
                node.transform.SetParent(transform, false);
                proxy = node.AddComponent<SegmentScrollDragProxy>(); proxy.Owner = this;
            }
            // Input module has selected the native ScrollRect but has not started dragging yet.
            // Always redirect, including rejected pointers, so native OnBeginDrag cannot bypass arbitration.
            data.pointerDrag = proxy.gameObject;
            if (Scope.Press(data, this)) Target.StopMovement();
        }
        internal void Begin(PointerEventData data) { if (CheckAvailable()) Scope?.Begin(data, this); }
        internal void Drag(PointerEventData data) { if (CheckAvailable()) Scope?.Drag(data); }
        internal void End(PointerEventData data) { if (CheckAvailable()) Scope?.End(data); }
        bool CheckAvailable()
        {
            if (Available) return true;
            CancelIfOwner(); return false;
        }
        public void BeginOwned(PointerEventData data)
        {
            if (!Available) return;
            Target.OnBeginDrag(data);
            OwnedBegin?.Invoke(data);
        }
        public void DragOwned(PointerEventData data)
        { if (Available) { Target.OnDrag(data); OwnedDrag?.Invoke(data); } }
        public void EndOwned(PointerEventData data)
        { if (Target != null) Target.OnEndDrag(data); OwnedEnd?.Invoke(data); }
        public void CancelOwned(PointerEventData data)
        {
            if (Target != null) { if (data != null) Target.OnEndDrag(data); Target.StopMovement(); }
            OwnedCancel?.Invoke(data);
        }
        void CancelIfOwner()
        {
            var scope = Scope;
            if (scope != null && scope.OwnsReceiver(this))
                GetComponentInParent<SegmentView>().CancelInteraction(SegmentCancelReason.Disabled);
        }
        void LateUpdate() { if (!Available) CancelIfOwner(); }
        void OnDisable() => CancelIfOwner();
        void OnDestroy()
        {
            if (proxy != null) SegmentPageHost.Destroy(proxy.gameObject);
        }
        /// <summary>Idempotently registers native vertical ScrollRects and ordinary buttons in new page content.</summary>
        public static void Install(Transform root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            foreach (var scroll in root.GetComponentsInChildren<ScrollRect>(true))
            {
                if (scroll is SegmentScrollRect || !scroll.vertical || scroll.horizontal) continue;
                if (scroll.GetComponent<SegmentScrollRectAdapter>() == null)
                    scroll.gameObject.AddComponent<SegmentScrollRectAdapter>();
            }
            SegmentPointerRelay.Install(root);
        }
        internal static ISegmentScrollReceiver FindReceiver(Component node)
        {
            var scroll = node.GetComponentInParent<ScrollRect>();
            if (scroll is SegmentScrollRect controlled) return controlled;
            return scroll != null ? scroll.GetComponent<SegmentScrollRectAdapter>() : null;
        }
    }
}
