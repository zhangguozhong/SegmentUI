using UnityEngine;
using UnityEngine.UI;

namespace SegmentUI
{
    public class SegmentBuiltInIndicator : SegmentIndicator
    {
        public SegmentIndicatorStyle Style = SegmentIndicatorStyle.Underline;
        public SegmentIndicatorMotion Motion = SegmentIndicatorMotion.Slide;
        public SegmentIndicatorWidth WidthMode = SegmentIndicatorWidth.MatchItemContent;
        public SegmentIndicatorPosition Position = SegmentIndicatorPosition.Bottom;
        public float FixedWidth = 32, Height = 4, Inset = 8, Radius = 12, OutlineWidth = 2;
        public Color Color = new Color(.18f, .42f, .94f);
        public Sprite Sprite;
        SegmentShapeGraphic shape;
        Image image;
        Rect Geometry(Rect item, Rect content)
        {
            float width = WidthMode == SegmentIndicatorWidth.Fixed ? FixedWidth : WidthMode == SegmentIndicatorWidth.MatchItem ? item.width : content.width;
            width = Mathf.Max(0, width - (WidthMode == SegmentIndicatorWidth.Fixed ? 0 : Inset * 2));
            float height = Style == SegmentIndicatorStyle.RoundedBackground || Style == SegmentIndicatorStyle.Outline ? Mathf.Max(0, item.height - Inset * 2) : Mathf.Max(0, Height);
            float x = WidthMode == SegmentIndicatorWidth.MatchItemContent ? content.center.x : item.center.x;
            float y = Position == SegmentIndicatorPosition.Top ? item.yMax - Inset - height * .5f : Position == SegmentIndicatorPosition.Bottom ? item.yMin + Inset + height * .5f : item.center.y;
            return new Rect(x - width * .5f, y - height * .5f, width, height);
        }
        public override void ApplyFrame(SegmentIndicatorFrame frame)
        {
            if (Style == SegmentIndicatorStyle.None || (Motion == SegmentIndicatorMotion.Discrete && !frame.HasCommitted)) { ResetVisual(); return; }
            gameObject.SetActive(true);
            var left = Geometry(frame.LeftItem, frame.LeftContent);
            var right = Geometry(frame.RightItem, frame.RightContent);
            float t = Mathf.Clamp01(frame.Progress);
            Rect result;
            if (Motion == SegmentIndicatorMotion.Discrete) result = Geometry(frame.CommittedItem, frame.CommittedContent);
            else if (Motion == SegmentIndicatorMotion.Stretch)
            {
                // Grow toward the destination first, then pull the trailing edge in.
                bool forward = right.center.x >= left.center.x;
                float leading = Mathf.Min(1, t * 2), trailing = Mathf.Max(0, t * 2 - 1);
                float min = Mathf.Lerp(left.xMin, right.xMin, forward ? trailing : leading);
                float max = Mathf.Lerp(left.xMax, right.xMax, forward ? leading : trailing);
                result = Rect.MinMaxRect(min, Mathf.Lerp(left.yMin, right.yMin, t), max, Mathf.Lerp(left.yMax, right.yMax, t));
            }
            else result = new Rect(Vector2.Lerp(left.position, right.position, t), Vector2.Lerp(left.size, right.size, t));
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f);
            // Frame rects are in the layer's local coordinates; anchoredPosition is relative to its anchor.
            var parent = rect.parent as RectTransform;
            Vector2 anchorOffset = parent == null ? Vector2.zero : parent.rect.center;
            rect.anchoredPosition = result.center - anchorOffset; rect.sizeDelta = result.size;
            if (Style == SegmentIndicatorStyle.Image)
            {
                if (shape != null) shape.enabled = false;
                if (image == null) { var go = new GameObject("Image", typeof(RectTransform), typeof(Image)); go.transform.SetParent(transform, false); image = go.GetComponent<Image>(); Fill(image.rectTransform); }
                image.enabled = true; image.sprite = Sprite; image.type = Image.Type.Sliced; image.color = Color;
            }
            else
            {
                if (image != null) image.enabled = false;
                if (shape == null) { var go = new GameObject("Shape", typeof(RectTransform), typeof(SegmentShapeGraphic)); go.transform.SetParent(transform, false); shape = go.GetComponent<SegmentShapeGraphic>(); Fill(shape.rectTransform); }
                shape.enabled = true; shape.color = Color; shape.Radius = Style == SegmentIndicatorStyle.Underline ? Mathf.Min(Radius, Height * .5f) : Radius; shape.Stroke = Style == SegmentIndicatorStyle.Outline ? OutlineWidth : 0; shape.SetVerticesDirty();
            }
            DisableRaycasts();
        }
        static void Fill(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
