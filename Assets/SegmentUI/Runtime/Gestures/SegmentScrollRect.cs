using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace SegmentUI
{
    /// <summary>可授权的 ScrollRect：先申请手势，只有 Owned 回调才进入原生拖拽处理。</summary>
    public class SegmentScrollRect : ScrollRect, IPointerDownHandler, IPointerUpHandler, ISegmentScrollReceiver
    {
        [Tooltip("标题栏横向滚动只移动标题，不拖动页面。普通页面列表保持关闭。")]
        public bool IsTitleBar;
        bool ISegmentScrollReceiver.IsTitleBar => IsTitleBar;
        /// <summary>获准起拖时通知；业务刷新/拖拽扩展应订阅此组事件，不旁路仲裁。</summary>
        public event Action<PointerEventData> OwnedBegin, OwnedDrag, OwnedEnd, OwnedCancel;
        public SegmentGestureScope Scope => SegmentGestureScope.Find(this);
        public void OnPointerDown(PointerEventData data) { if (Scope != null) Scope.Press(data, this); }
        public void OnPointerUp(PointerEventData data) { if (Scope != null) Scope.Release(data); }
        /// <summary>申请成功才停止惯性；不在此处直接调用原生初始化以避免重复分发。</summary>
        public override void OnInitializePotentialDrag(PointerEventData data)
        {
            if (Scope == null) { base.OnInitializePotentialDrag(data); return; }
            // 即使按下命中子按钮也会调用，不依赖按下事件向祖先冒泡。
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
        /// <summary>正常松手走原生 EndDrag，保留 ScrollRect 惯性。</summary>
        public void EndOwned(PointerEventData data) { base.OnEndDrag(data); OwnedEnd?.Invoke(data); }
        /// <summary>取消与正常松手不同：结束原生拖拽后强制停速，不能用取消提交刷新。</summary>
        public void CancelOwned(PointerEventData data)
        { if (data != null) base.OnEndDrag(data); StopMovement(); OwnedCancel?.Invoke(data); }
        /// <summary>仅当前接收器拥有手势时才取消；禁用其他缓存列表不影响当前输入。</summary>
        protected override void OnDisable()
        {
            if (Scope != null && Scope.OwnsReceiver(this)) GetComponentInParent<SegmentView>().CancelInteraction(SegmentCancelReason.Disabled);
            base.OnDisable();
        }
    }
}
