using UnityEngine;
using UnityEngine.EventSystems;
namespace SegmentUI
{
    /// <summary>统一仲裁嵌套分页与内部滚动：一组嵌套层级同时只允许一个指针、一个手势所有者。</summary>
    public sealed class SegmentGestureScope : MonoBehaviour
    {
        /// <summary>横向位移必须明显大于纵向才判为横拖；阈值附近优先纵向，减少斜滑误切页。</summary>
        [SerializeField, Range(1, 2)] float horizontalBias = 1.2f;
        SegmentView view;
        ISegmentScrollReceiver receiver;
        PointerEventData pointer;
        Vector2 pressLocal, lastLocal;
        float pressPosition, lastTime, velocity, lastMoveTime;
        bool started, toScroll, forcedHorizontal, paging;
        SegmentGestureScope activeScope;
        /// <summary>嵌套输入的共享根，所有子分页在这里竞争唯一活动所有权。</summary>
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
            // 已释放的借用页面即使仍挂在 Content 下，也不再属于该分页的输入作用域。
            if (page != null && !page.IsCurrent(page.PageId, page.BindingVersion)) return null;
            return owner.GetComponentInParent<SegmentView>()?.Gesture;
        }
        /// <summary>当前作用域持有的指针；没有持有时为 null。</summary>
        public int? ActivePointerId { get; private set; }
        /// <summary>整组嵌套分页是否正在处理输入，不仅检查当前节点。</summary>
        public bool HasActivePointer => Root.activeScope != null;
        /// <summary>判断候选滚动接收器是否是当前唯一所有者，避免无关列表禁用时误取消手势。</summary>
        public bool OwnsReceiver(ISegmentScrollReceiver candidate) => Root.activeScope != null && Root.activeScope.receiver == candidate;
        /// <summary>正在向获准的滚动接收器分发回调；标题栏横滚也使用此标记。</summary>
        public bool DispatchingVertical { get; private set; }
        /// <summary>申请指针所有权，同一指针重复申请幂等；其他指针被拒绝并取消点击资格。</summary>
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
        /// <summary>由 EventSystem 超过起拖阈值后调用，只判定一次方向，后续转向不重新仲裁。</summary>
        public void Begin(PointerEventData data, ISegmentScrollReceiver scroll)
        {
            if (ActivePointerId != data.pointerId || Root.activeScope != this || started) return;
            receiver = scroll;
            Vector2 delta = Local(data, data.position) - pressLocal;
            // 分页停在中间时强制接管横向位置；其余情况按本地坐标位移判断方向。
            bool horizontal = forcedHorizontal || Mathf.Abs(delta.x) > horizontalBias * Mathf.Abs(delta.y);
            float logicalDelta = delta.x * view.Pager.DragSign;
            // 嵌套分页仅在起拖且位于边缘时交给父级，消费位移后不再中途交接。
            var parentView = transform.parent != null ? transform.parent.GetComponentInParent<SegmentView>() : null;
            if (horizontal && !forcedHorizontal && parentView != null && (receiver == null || !receiver.IsTitleBar) &&
                ((logicalDelta < 0 && pressPosition <= 0) || (logicalDelta > 0 && pressPosition >= view.Pager.PageCount - 1)))
            {
                Clear(); view.FinishInput(false, 0);
                var parent = parentView.Gesture;
                if (parent.Press(data, null)) parent.Begin(data, null);
                return;
            }
            // 标题栏消费横拖，页面列表消费纵拖；未交给列表的有效横拖才驱动分页。
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
        /// <summary>事件可能仍发到原子节点，必须转发给已经完成交接的新所有者。</summary>
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
        /// <summary>正常松手保留原生列表惯性；分页按最近移动速度吸附，停顿过久则忽略旧速度。</summary>
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
        /// <summary>仅处理尚未起拖的抬起；已开始拖拽必须等待 EndDrag 完成清理。</summary>
        public void Release(PointerEventData data)
        {
            var owner = Root.activeScope;
            if (owner != null && owner != this) { owner.Release(data); return; }
            // UGUI 先发送 PointerUp 再发送 EndDrag；已起拖时由 EndDrag 完成释放。
            if (ActivePointerId == data.pointerId && !started) End(data);
        }
        /// <summary>取消当前作用域或其后代的手势，调用 CancelOwned 停速并清空所有权；迟到事件不再生效。</summary>
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
        /// <summary>一次性清空共享根、指针及方向锁；不能保留上次手势的接收器。</summary>
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
