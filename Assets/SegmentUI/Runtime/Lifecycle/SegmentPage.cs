using System;
using System.Threading;
using UnityEngine;

namespace SegmentUI
{
    /// <summary>业务页外壳。预览不等于 Enter；异步结果必须核对绑定/展示版本。</summary>
    public class SegmentPage : MonoBehaviour
    {
        bool initialized, bound, presented, directionApplied;
        /// <summary>页面当前方向；业务在 OnDirectionChanged 中处理语义或文本渲染。</summary>
        public bool IsRightToLeft { get; private set; }
        /// <summary>绑定后的方向同步通知，外部布局桥接可订阅。</summary>
        public event Action<bool> DirectionChanged;
        CancellationTokenSource bindingCancellation, presentationCancellation;
        /// <summary>当前绑定的稳定业务 ID，异步回调应配合版本校验。</summary>
        public string PageId { get; private set; }
        /// <summary>每次绑定或解绑递增，防止旧绑定的异步结果写入新数据。</summary>
        public int BindingVersion { get; private set; }
        /// <summary>每次进入或退出展示递增，用于识别过期的展示任务。</summary>
        public int PresentationVersion { get; private set; }
        /// <summary>正式展示周期是否有效；实例激活或相邻页预览不代表已进入。</summary>
        public bool IsPresented => presented;
        /// <summary>当前绑定的取消令牌；解绑/释放时取消，未绑定时返回已取消令牌。</summary>
        public CancellationToken BindingCancellation => bindingCancellation?.Token ?? new CancellationToken(true);
        /// <summary>当前展示周期的令牌；退出展示即取消，适合可见期任务。</summary>
        public CancellationToken PresentationCancellation => presentationCancellation?.Token ?? new CancellationToken(true);
        /// <summary>校验 ID 与绑定版本；任务结束后仍需回到 Unity 主线程再更新 UI。</summary>
        public bool IsCurrent(string id, int bindingVersion) => bound && PageId == id && BindingVersion == bindingVersion;
        /// <summary>校验展示版本及可见状态，拒绝上一轮 Enter 的迟到结果。</summary>
        public bool IsCurrentPresentation(int version) => presented && PresentationVersion == version;
        /// <summary>每个实例首次绑定时调用一次，适合缓存组件与注册实例级监听。</summary>
        protected virtual void OnInitialize() { }
        /// <summary>每次绑定调用，执行顺序在描述项 BindPage 注入业务依赖之后。</summary>
        protected virtual void OnBind() { }
        /// <summary>正式进入展示周期时调用；滑到一半的预览页不会触发。</summary>
        protected virtual void OnEnter() { }
        /// <summary>退出展示周期时调用；展示任务已取消，可按原因清理临时状态。</summary>
        protected virtual void OnExit(SegmentCancelReason reason) { }
        /// <summary>解除业务绑定时调用；绑定任务已取消，但实例可能继续复用。</summary>
        protected virtual void OnUnbind() { }
        /// <summary>结束本次组件管理会话；工厂可以回收对象，借用实例不会由组件销毁。</summary>
        protected virtual void OnRelease() { }

        /// <summary>业务可覆盖以处理内容方向或接入独立的阿拉伯字形与文本排版组件。</summary>
        protected virtual void OnDirectionChanged(bool rightToLeft) { }
        internal void ApplyDirection(bool rightToLeft)
        {
            if (directionApplied && IsRightToLeft == rightToLeft) return;
            directionApplied = true; IsRightToLeft = rightToLeft;
            InvokeSafe(() => OnDirectionChanged(rightToLeft));
            InvokeSafe(() => DirectionChanged?.Invoke(rightToLeft));
        }
        /// <summary>先执行一次性初始化，再建立新的绑定令牌；方向通知留待业务内容配置完成后。</summary>
        internal void Bind(SegmentPageDescriptor descriptor)
        {
            if (!initialized) { initialized = true; OnInitialize(); }
            PageId = descriptor.Id; BindingVersion++; bound = true; directionApplied = false;
            bindingCancellation = new CancellationTokenSource();
            descriptor.BindPage?.Invoke(this); OnBind();
        }
        /// <summary>展示转换幂等；退出时先停止子分页展示，再取消自身任务并通知业务。</summary>
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
        /// <summary>按 Exit → Unbind → Release 顺序结束会话，已退出/解绑的步骤不重复执行。</summary>
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
