using System;
using UnityEngine;
namespace SegmentUI
{
    public sealed partial class SegmentView
    {
        /// <summary>配置方向；Inherit 表示从最近父 SegmentView 解析。</summary>
        public SegmentLayoutDirection Direction => direction;
        /// <summary>实际生效方向；没有父分页时 Inherit 按 LTR 处理。</summary>
        public bool IsRightToLeft
        {
            get
            {
                if (direction != SegmentLayoutDirection.Inherit) return direction == SegmentLayoutDirection.RightToLeft;
                var parent = transform.parent != null ? transform.parent.GetComponentInParent<SegmentView>() : null;
                return parent != null && parent.IsRightToLeft;
            }
        }
        /// <summary>实际方向改变后通知，供外部业务布局或文本适配器同步。</summary>
        public event Action<bool> DirectionChanged;
        /// <summary>切换方向会取消当前交互并回到已提交页，但保留 Page 实例及业务绑定。</summary>
        public void SetDirection(SegmentLayoutDirection value)
        {
            if (!Enum.IsDefined(typeof(SegmentLayoutDirection), value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (disposing) return;
            Schedule(() => { direction = value; RefreshDirection(); });
        }
        /// <summary>自身容器更新后逐层通知直属子分页，使继承与显式覆盖各自生效。</summary>
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
