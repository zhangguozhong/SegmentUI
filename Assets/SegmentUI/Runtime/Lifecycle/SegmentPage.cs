using System;
using System.Threading;
using UnityEngine;

namespace SegmentUI
{
    /// <summary>业务页外壳。预览不等于 Enter；异步结果必须核对绑定/展示版本。</summary>
    public class SegmentPage : MonoBehaviour
    {
        bool initialized, bound, presented, directionApplied;
        public bool IsRightToLeft { get; private set; }
        public event Action<bool> DirectionChanged;
        CancellationTokenSource bindingCancellation, presentationCancellation;
        public string PageId { get; private set; }
        public int BindingVersion { get; private set; }
        public int PresentationVersion { get; private set; }
        public bool IsPresented => presented;
        public CancellationToken BindingCancellation => bindingCancellation?.Token ?? new CancellationToken(true);
        public CancellationToken PresentationCancellation => presentationCancellation?.Token ?? new CancellationToken(true);
        public bool IsCurrent(string id, int bindingVersion) => bound && PageId == id && BindingVersion == bindingVersion;
        public bool IsCurrentPresentation(int version) => presented && PresentationVersion == version;
        protected virtual void OnInitialize() { }
        protected virtual void OnBind() { }
        protected virtual void OnEnter() { }
        protected virtual void OnExit(SegmentCancelReason reason) { }
        protected virtual void OnUnbind() { }
        protected virtual void OnRelease() { }

        /// <summary>Override for business content alignment or a separate Arabic shaping/text renderer.</summary>
        protected virtual void OnDirectionChanged(bool rightToLeft) { }
        internal void ApplyDirection(bool rightToLeft)
        {
            if (directionApplied && IsRightToLeft == rightToLeft) return;
            directionApplied = true; IsRightToLeft = rightToLeft;
            InvokeSafe(() => OnDirectionChanged(rightToLeft));
            InvokeSafe(() => DirectionChanged?.Invoke(rightToLeft));
        }
        internal void Bind(SegmentPageDescriptor descriptor)
        {
            if (!initialized) { initialized = true; OnInitialize(); }
            PageId = descriptor.Id; BindingVersion++; bound = true; directionApplied = false;
            bindingCancellation = new CancellationTokenSource();
            descriptor.BindPage?.Invoke(this); OnBind();
        }
        internal void Present(bool value, SegmentCancelReason reason)
        {
            if (presented == value || (value && !bound)) return;
            presented = value; PresentationVersion++;
            if (value)
            {
                presentationCancellation = new CancellationTokenSource();
                InvokeSafe(OnEnter);
                SetChildrenPresented(true, reason);
            }
            else
            {
                SetChildrenPresented(false, reason);
                Cancel(ref presentationCancellation);
                InvokeSafe(() => OnExit(reason));
            }
        }
        void SetChildrenPresented(bool value, SegmentCancelReason reason)
        {
            foreach (var view in GetComponentsInChildren<SegmentView>(true))
                if (view.GetComponentInParent<SegmentPage>() == this)
                    view.SetAncestorPresented(value, reason);
        }
        internal void Release()
        {
            Present(false, SegmentCancelReason.Destroyed);
            if (bound)
            {
                bound = false; BindingVersion++; Cancel(ref bindingCancellation); InvokeSafe(OnUnbind);
            }
            InvokeSafe(OnRelease);
        }
        internal void Unbind()
        {
            Present(false, SegmentCancelReason.DataChanged);
            if (!bound) return;
            bound = false; BindingVersion++; Cancel(ref bindingCancellation); InvokeSafe(OnUnbind);
        }
        static void Cancel(ref CancellationTokenSource source)
        {
            if (source == null) return;
            try { source.Cancel(); } catch (Exception e) { Debug.LogException(e); }
            finally { source.Dispose(); source = null; }
        }
        internal static void InvokeSafe(Action action)
        { try { action(); } catch (Exception e) { Debug.LogException(e); } }
    }
}
