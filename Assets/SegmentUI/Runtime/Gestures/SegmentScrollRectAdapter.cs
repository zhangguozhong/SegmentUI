using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SegmentUI
{
    /// <summary>保留原生 ScrollRect，通过独立事件代理将拖拽接入统一仲裁。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ScrollRect))]
    public sealed class SegmentScrollRectAdapter : MonoBehaviour, IInitializePotentialDragHandler,
        IPointerDownHandler, IPointerUpHandler, ISegmentScrollReceiver
    {
        ScrollRect target;
        SegmentScrollDragProxy proxy;
        /// <summary>适配的原生 ScrollRect，保留原组件引用、参数及 UnityEvent。</summary>
        public ScrollRect Target => target != null ? target : (target = GetComponent<ScrollRect>());
        public bool IsTitleBar => false;
        public event Action<PointerEventData> OwnedBegin, OwnedDrag, OwnedEnd, OwnedCancel;
        SegmentGestureScope Scope => SegmentGestureScope.Find(this);
        bool Available => isActiveAndEnabled && Target != null && Target.isActiveAndEnabled;
        public void OnPointerDown(PointerEventData data)
        { if (Available && Scope != null) Scope.Press(data, this); }
        public void OnPointerUp(PointerEventData data)
        { if (Scope != null) Scope.Release(data); }
        /// <summary>将 UGUI 的拖拽目标改为代理，避免原生 ScrollRect 绕过方向仲裁直接起拖。</summary>
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
            // 输入模块已选中原生 ScrollRect，但尚未开始分发拖拽。
            // 即使指针被拒绝也重定向，防止原生 OnBeginDrag 绕过仲裁。
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
        /// <summary>仅授权后调用原生 BeginDrag，避免父分页与子列表同时响应。</summary>
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
        /// <summary>取消时结束原生拖拽并清空速度；不模拟普通松手惯性。</summary>
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
        /// <summary>为新增页面内容幂等安装原生纵向列表和普通按钮的输入适配。</summary>
        // 幂等扫描新增内容，只适配纯纵向原生列表，并为普通按钮安装按下/抬起桥。
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
