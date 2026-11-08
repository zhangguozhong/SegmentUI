using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SegmentUI
{
    public enum SegmentIndicatorStyle { None, Underline, RoundedBackground, Outline, Image }
    public enum SegmentIndicatorMotion { Discrete, Slide, Stretch }
    public enum SegmentIndicatorWidth { Fixed, MatchItemContent, MatchItem }
    public enum SegmentIndicatorPosition { Top, Bottom, Center }
    public enum SegmentIndicatorLayer { Background, Foreground }
    /// <summary>指示器单帧输入：矩形已转换到所在层局部坐标，Progress 是相邻页间的插值。</summary>
    public struct SegmentIndicatorFrame
    {
        /// <summary>相邻项、当前项及其内容矩形，均已转换到指示器父层的局部坐标。</summary>
        public Rect LeftItem, RightItem, CommittedItem, LeftContent, RightContent, CommittedContent;
        public string LeftPageId, RightPageId, CurrentPageId, TargetPageId;
        /// <summary>相邻逻辑页之间的进度，范围 0 到 1，不是整个分页总进度。</summary>
        public float Progress;
        /// <summary>是否存在有效已提交标题，访问 Committed 矩形前应检查。</summary>
        public bool HasCommitted;
        public SegmentSelectionSource Source;
        /// <summary>布局与过渡版本；自定义附加动画可用它们识别陈旧帧。</summary>
        public int LayoutVersion, TransitionVersion;
    }
    /// <summary>指示器扩展基类；只消费分页帧，不自行驱动分页或拦截射线。</summary>
    [RequireComponent(typeof(RectTransform))]
    public abstract class SegmentIndicator : MonoBehaviour
    {
        static readonly Vector3[] corners = new Vector3[4];
        readonly List<Graphic> graphics = new List<Graphic>();
        /// <summary>实例挂载层选择；由 SegmentBar 在安装时使用。</summary>
        public SegmentIndicatorLayer Layer = SegmentIndicatorLayer.Foreground;
        /// <summary>按帧更新视觉；自定义异步效果应使用 LayoutVersion/TransitionVersion 判断过期。</summary>
        public abstract void ApplyFrame(SegmentIndicatorFrame frame);
        /// <summary>没有可用页面或有效布局时隐藏视觉。</summary>
        public virtual void ResetVisual() { gameObject.SetActive(false); }
        /// <summary>替换或销毁指示器前取消自定义动画/任务，子类按需覆盖。</summary>
        public virtual void CancelEffects() { }
        /// <summary>递归关闭 Graphic 与 CanvasGroup 的射线，避免指示器遮挡标题点击。</summary>
        public void DisableRaycasts()
        {
            GetComponentsInChildren(true, graphics);
            foreach (var graphic in graphics) graphic.raycastTarget = false;
            var group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
        }
        /// <summary>把目标矩形四角转换到指定层局部坐标，不假设两者直接同父。</summary>
        public static Rect RectInLayer(RectTransform source, RectTransform layer)
        {
            source.GetWorldCorners(corners);
            Vector2 min = layer.InverseTransformPoint(corners[0]), max = min;
            for (int i = 1; i < 4; i++) { Vector2 p = layer.InverseTransformPoint(corners[i]); min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
