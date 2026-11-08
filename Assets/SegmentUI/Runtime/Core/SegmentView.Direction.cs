using System;
using UnityEngine;
namespace SegmentUI
{
    public sealed partial class SegmentView
    {
        public SegmentLayoutDirection Direction => direction;
        public bool IsRightToLeft
        {
            get
            {
                if (direction != SegmentLayoutDirection.Inherit) return direction == SegmentLayoutDirection.RightToLeft;
                var parent = transform.parent != null ? transform.parent.GetComponentInParent<SegmentView>() : null;
                return parent != null && parent.IsRightToLeft;
            }
        }
        public event Action<bool> DirectionChanged;
        public void SetDirection(SegmentLayoutDirection value)
        {
            if (!Enum.IsDefined(typeof(SegmentLayoutDirection), value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (disposing) return;
            Schedule(() => { direction = value; RefreshDirection(); });
        }
        void RefreshDirection()
        {
            if (!configured || disposing) return;
            bool rtl = IsRightToLeft;
            if (pager.RightToLeft != rtl)
            {
                CancelInteraction(SegmentCancelReason.LayoutChanged);
                pager.SetRightToLeft(rtl);
                if (bar != null) bar.RightToLeft = rtl;
                host.ApplyDirection(rtl); Render();
                Notify(() => DirectionChanged?.Invoke(rtl));
            }
            foreach (var child in GetComponentsInChildren<SegmentView>(true))
                if (child != this && child.transform.parent.GetComponentInParent<SegmentView>() == this)
                    child.Schedule(child.RefreshDirection);
        }
    }
}
