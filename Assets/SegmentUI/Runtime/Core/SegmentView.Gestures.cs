using UnityEngine;
namespace SegmentUI
{
    public sealed partial class SegmentView
    {
        SegmentGestureScope gesture;
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
        internal bool BeginInput()
        {
            if (!CanReceiveInput) return false;
            pager.Stop(); TargetPageId = null; host.StopVerticalMotion();
            Phase = SegmentPhase.PendingGesture; Render(); return true;
        }
        internal bool IsAtCommittedPage => CurrentPageId != null && Mathf.Abs(pager.Position - enabledIds.IndexOf(CurrentPageId)) < .00001f;
        internal void SetInputPhase(SegmentPhase phase) { Phase = phase; Render(); }
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
