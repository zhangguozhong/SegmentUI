using UnityEngine;
using UnityEngine.UI;

namespace SegmentUI
{
    /// <summary>内置下划线、圆角背景、描边与图片指示器；样式和运动均由传入帧驱动。</summary>
    public class SegmentBuiltInIndicator : SegmentIndicator
    {
        /// <summary>视觉形状；None 隐藏，Image 使用指定 Sprite。</summary>
        public SegmentIndicatorStyle Style = SegmentIndicatorStyle.Underline;
        /// <summary>帧间表现：离散切换、滑动插值或先拉伸再收拢。</summary>
        public SegmentIndicatorMotion Motion = SegmentIndicatorMotion.Slide;
        /// <summary>宽度依据：固定数值、标题内容矩形或整个标题矩形。</summary>
        public SegmentIndicatorWidth WidthMode = SegmentIndicatorWidth.MatchItemContent;
        /// <summary>指示器相对标题矩形位于顶部、底部或中间。</summary>
        public SegmentIndicatorPosition Position = SegmentIndicatorPosition.Bottom;
        /// <summary>尺寸样式参数：固定宽、高度、内缩、圆角和描边宽度。</summary>
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
        /// <summary>根据宽度策略选取标题/内容矩形，再应用离散、滑动或拉伸插值。</summary>
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
                // 先向目标方向伸展，再收拢尾部，形成拉伸过渡。
                bool forward = right.center.x >= left.center.x;
                float leading = Mathf.Min(1, t * 2), trailing = Mathf.Max(0, t * 2 - 1);
                float min = Mathf.Lerp(left.xMin, right.xMin, forward ? trailing : leading);
                float max = Mathf.Lerp(left.xMax, right.xMax, forward ? leading : trailing);
                result = Rect.MinMaxRect(min, Mathf.Lerp(left.yMin, right.yMin, t), max, Mathf.Lerp(left.yMax, right.yMax, t));
            }
            else result = new Rect(Vector2.Lerp(left.position, right.position, t), Vector2.Lerp(left.size, right.size, t));
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f);
            // 帧矩形使用所在层的局部坐标，设置 anchoredPosition 时还需考虑自身锚点。
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
