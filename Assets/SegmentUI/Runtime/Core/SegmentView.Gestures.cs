using UnityEngine;
namespace SegmentUI
{
    public sealed partial class SegmentView
    {
        SegmentGestureScope gesture;
        /// <summary>懒创建手势作用域；嵌套分页通过根作用域共享同一个活动指针。</summary>
        public SegmentGestureScope Gesture
        {
            get
            {
                if (disposing) return gesture;
                if (gesture == null) gesture = GetComponent<SegmentGestureScope>();
                if (gesture == null) gesture = gameObject.AddComponent<SegmentGestureScope>();
                return gesture;
            }
        }
        partial void CancelGesture(SegmentCancelReason reason) { if (gesture != null) gesture.Cancel(reason); }
        /// <summary>按下先停止分页吸附和已创建列表的惯性，进入等待方向判定状态。</summary>
        internal bool BeginInput()
        {
            if (!CanReceiveInput) return false;
            pager.Stop(); TargetPageId = null; host.StopVerticalMotion();
            Phase = SegmentPhase.PendingGesture; Render(); return true;
        }
        internal bool IsAtCommittedPage => CurrentPageId != null && Mathf.Abs(pager.Position - enabledIds.IndexOf(CurrentPageId)) < .00001f;
        internal void SetInputPhase(SegmentPhase phase) { Phase = phase; Render(); }
        /// <summary>松手优先执行手势期间最后一次代码选择，否则按当前位置与释放速度确定吸附页。</summary>
        internal void FinishInput(bool horizontal, float velocity)
        {
            Phase = SegmentPhase.Idle;
            if (pendingSelection != null)
            {
                string target = pendingSelection; bool animated = pendingAnimated; pendingSelection = null;
                RequestSelect(target, animated, SegmentSelectionSource.Code); return;
            }
            if (horizontal || !IsAtCommittedPage)
            {
                float position = pager.Position;
                int lower = Mathf.FloorToInt(position), upper = Mathf.CeilToInt(position);
                int committed = enabledIds.IndexOf(CurrentPageId);
                // 恰好停在两页中点时优先保留已提交页，避免偶数舍入导致不一致。
                bool tie = Mathf.Abs(position - lower - .5f) < .00001f;
                int target = tie ? (committed == lower || committed == upper ? committed : lower) : Mathf.FloorToInt(position + .5f);
                if (Mathf.Abs(velocity) > 1.2f)
                    target = velocity > 0 ? Mathf.CeilToInt(position) : Mathf.FloorToInt(position);
                target = Mathf.Clamp(target, 0, enabledIds.Count - 1);
                if (target >= 0) Run(() => StartSelection(enabledIds[target], true, SegmentSelectionSource.Gesture));
            }
            else { RefreshPhase(); Render(); }
        }
    }
}
