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
    public struct SegmentIndicatorFrame
    {
        public Rect LeftItem, RightItem, CommittedItem, LeftContent, RightContent, CommittedContent;
        public string LeftPageId, RightPageId, CurrentPageId, TargetPageId;
        public float Progress;
        public bool HasCommitted;
        public SegmentSelectionSource Source;
        public int LayoutVersion, TransitionVersion;
    }
    [RequireComponent(typeof(RectTransform))]
    public abstract class SegmentIndicator : MonoBehaviour
    {
        static readonly Vector3[] corners = new Vector3[4];
        readonly List<Graphic> graphics = new List<Graphic>();
        public SegmentIndicatorLayer Layer = SegmentIndicatorLayer.Foreground;
        public abstract void ApplyFrame(SegmentIndicatorFrame frame);
        public virtual void ResetVisual() { gameObject.SetActive(false); }
        public virtual void CancelEffects() { }
        public void DisableRaycasts()
        {
            GetComponentsInChildren(true, graphics);
            foreach (var graphic in graphics) graphic.raycastTarget = false;
            var group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
        }
        public static Rect RectInLayer(RectTransform source, RectTransform layer)
        {
            source.GetWorldCorners(corners);
            Vector2 min = layer.InverseTransformPoint(corners[0]), max = min;
            for (int i = 1; i < 4; i++) { Vector2 p = layer.InverseTransformPoint(corners[i]); min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
