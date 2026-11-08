using UnityEngine;
using UnityEngine.EventSystems;
namespace SegmentUI
{
    public sealed class SegmentGestureScope : MonoBehaviour
    {
        [SerializeField, Range(1, 2)] float horizontalBias = 1.2f;
        SegmentView view;
        ISegmentScrollReceiver receiver;
        PointerEventData pointer;
        Vector2 pressLocal, lastLocal;
        float pressPosition, lastTime, velocity, lastMoveTime;
        bool started, toScroll, forcedHorizontal, paging;
        SegmentGestureScope activeScope;
        public SegmentGestureScope Root
        {
            get
            {
                var parent = transform.parent != null ? transform.parent.GetComponentInParent<SegmentView>() : null;
                var scope = parent != null ? parent.Gesture : null;
                return scope != null ? scope.Root : this;
            }
        }
        internal static SegmentGestureScope Find(Component owner)
        {
            var page = owner.GetComponentInParent<SegmentPage>();
            // Released borrowed pages may remain under Content, but no longer belong to its input scope.
            if (page != null && !page.IsCurrent(page.PageId, page.BindingVersion)) return null;
            return owner.GetComponentInParent<SegmentView>()?.Gesture;
        }
        public int? ActivePointerId { get; private set; }
        public bool HasActivePointer => Root.activeScope != null;
        public bool OwnsReceiver(ISegmentScrollReceiver candidate) => Root.activeScope != null && Root.activeScope.receiver == candidate;
        public bool DispatchingVertical { get; private set; }
        public bool Press(PointerEventData data, ISegmentScrollReceiver scroll)
        {
            if (data.button != PointerEventData.InputButton.Left) return false;
            if (view == null) view = GetComponent<SegmentView>();
            var root = Root;
            if (root.activeScope != null)
            {
                bool same = root.activeScope == this && ActivePointerId == data.pointerId;
                if (!same) data.eligibleForClick = false;
                return same;
            }
            if (!view.CanReceiveInput) { data.eligibleForClick = false; return false; }
            root.activeScope = this; ActivePointerId = data.pointerId; receiver = scroll; pointer = data;
            pressLocal = Local(data, data.pressPosition); lastLocal = pressLocal;
            pressPosition = view.Pager.Position; forcedHorizontal = !view.IsAtCommittedPage;
            lastTime = lastMoveTime = Time.unscaledTime; velocity = 0; started = toScroll = paging = false;
            view.BeginInput(); return true;
        }
        public void Begin(PointerEventData data, ISegmentScrollReceiver scroll)
        {
            if (ActivePointerId != data.pointerId || Root.activeScope != this || started) return;
            receiver = scroll;
            Vector2 delta = Local(data, data.position) - pressLocal;
            bool horizontal = forcedHorizontal || Mathf.Abs(delta.x) > horizontalBias * Mathf.Abs(delta.y);
            float logicalDelta = delta.x * view.Pager.DragSign;
            // A nested pager at its edge can yield before consumption, never mid-drag.
            var parentView = transform.parent != null ? transform.parent.GetComponentInParent<SegmentView>() : null;
            if (horizontal && !forcedHorizontal && parentView != null && (receiver == null || !receiver.IsTitleBar) &&
                ((logicalDelta < 0 && pressPosition <= 0) || (logicalDelta > 0 && pressPosition >= view.Pager.PageCount - 1)))
            {
                Clear(); view.FinishInput(false, 0);
                var parent = parentView.Gesture;
                if (parent.Press(data, null)) parent.Begin(data, null);
                return;
            }
            toScroll = receiver != null && !forcedHorizontal && (receiver.IsTitleBar ? horizontal : !horizontal);
            paging = horizontal && !toScroll && (receiver == null || !receiver.IsTitleBar);
            started = true; data.eligibleForClick = false;
            if (toScroll)
            {
                view.SetInputPhase(receiver.IsTitleBar ? SegmentPhase.DraggingHorizontal : SegmentPhase.DraggingVertical);
                var start = CopyAt(data, data.pressPosition);
                DispatchingVertical = true;
                try { receiver.BeginOwned(start); } finally { DispatchingVertical = false; }
            }
            else view.SetInputPhase(SegmentPhase.DraggingHorizontal);
        }
        public void Drag(PointerEventData data)
        {
            var owner = Root.activeScope;
            if (owner != null && owner != this) { owner.Drag(data); return; }
            if (!started || ActivePointerId != data.pointerId) return;
            pointer = data;
            if (toScroll)
            {
                DispatchingVertical = true;
                try { receiver.DragOwned(data); } finally { DispatchingVertical = false; }
            }
            else if (paging)
            {
                Vector2 local = Local(data, data.position);
                float width = view.Pager.Viewport.rect.width;
                if (width <= 0) { view.CancelInteraction(SegmentCancelReason.LayoutChanged); return; }
                view.Pager.SetPosition(pressPosition + view.Pager.DragSign * (local.x - pressLocal.x) / width);
                float now = Time.unscaledTime, dt = now - lastTime;
                if (dt > .001f)
                {
                    float sample = view.Pager.DragSign * (local.x - lastLocal.x) / width / dt;
                    velocity = Mathf.Lerp(velocity, sample, .7f);
                }
                if (Mathf.Abs(local.x - lastLocal.x) > .01f) lastMoveTime = now;
                lastLocal = local; lastTime = now;
            }
        }
        public void End(PointerEventData data)
        {
            var owner = Root.activeScope;
            if (owner != null && owner != this) { owner.End(data); return; }
            if (ActivePointerId != data.pointerId) return;
            bool horizontal = started && paging;
            try
            {
                if (started && toScroll)
                {
                    DispatchingVertical = true;
                    try { receiver.EndOwned(data); } finally { DispatchingVertical = false; }
                }
            }
            finally
            {
                float releaseVelocity = Time.unscaledTime - lastMoveTime > .12f ? 0 : velocity;
                Clear(); view.FinishInput(horizontal, releaseVelocity);
            }
        }
        public void Release(PointerEventData data)
        {
            var owner = Root.activeScope;
            if (owner != null && owner != this) { owner.Release(data); return; }
            // UGUI sends PointerUp before EndDrag. A drag owns its EndDrag.
            if (ActivePointerId == data.pointerId && !started) End(data);
        }
        public void Cancel(SegmentCancelReason reason)
        {
            var owner = Root.activeScope;
            if (owner != null && (owner == this || owner.transform.IsChildOf(transform)))
            {
                if (owner.pointer != null) owner.pointer.eligibleForClick = false;
                try { if (owner.started && owner.toScroll && owner.receiver != null) owner.receiver.CancelOwned(owner.pointer); }
                finally
                {
                    var oldView = owner.view; owner.Clear();
                    if (oldView != null && owner != this) oldView.CancelInteraction(reason);
                }
            }
        }
        void Clear()
        {
            if (Root.activeScope == this) Root.activeScope = null;
            ActivePointerId = null; pointer = null; receiver = null;
            started = toScroll = paging = DispatchingVertical = false;
        }
        Vector2 Local(PointerEventData data, Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(view.Pager.Viewport, screen, data.pressEventCamera, out var local);
            return local;
        }
        static PointerEventData CopyAt(PointerEventData source, Vector2 position) => new PointerEventData(EventSystem.current)
        {
            pointerId = source.pointerId, button = source.button, position = position, pressPosition = source.pressPosition,
            pointerPressRaycast = source.pointerPressRaycast, pointerCurrentRaycast = source.pointerCurrentRaycast, eligibleForClick = false
        };
        void OnDisable() { if (view != null) view.CancelInteraction(SegmentCancelReason.Disabled); }
    }
}
