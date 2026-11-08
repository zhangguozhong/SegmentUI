using UnityEngine;
using UnityEngine.UI;

namespace SegmentUI
{
    /// <summary>通过网格绘制圆角填充或内缩描边，避免用方形精灵近似圆角。</summary>
    // 使用 UGUI 网格绘制圆角填充或描边，作为默认指示器的视觉元素。
    [RequireComponent(typeof(CanvasRenderer))]
    public class SegmentShapeGraphic : MaskableGraphic
    {
        public float Radius = 12;
        public float Stroke;
        const int Steps = 8;
        /// <summary>形状参数变化时重建顶点；仅绘制，不参与分页逻辑。</summary>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            if (r.width <= 0 || r.height <= 0) return;
            float radius = Mathf.Clamp(Radius, 0, Mathf.Min(r.width, r.height) * .5f);
            float stroke = Mathf.Clamp(Stroke, 0, Mathf.Min(r.width, r.height) * .5f);
            int count = 4 * (Steps + 1);
            if (stroke <= 0)
            {
                vh.AddVert(r.center, color, Vector2.zero);
                for (int i = 0; i < count; i++) vh.AddVert(Point(r, radius, i), color, Vector2.zero);
                for (int i = 0; i < count; i++) vh.AddTriangle(0, 1 + i, 1 + (i + 1) % count);
            }
            else
            {
                var inner = new Rect(r.x + stroke, r.y + stroke, r.width - stroke * 2, r.height - stroke * 2);
                for (int i = 0; i < count; i++) { vh.AddVert(Point(r, radius, i), color, Vector2.zero); vh.AddVert(Point(inner, Mathf.Max(0, radius - stroke), i), color, Vector2.zero); }
                for (int i = 0; i < count; i++) { int next = (i + 1) % count; vh.AddTriangle(i * 2, next * 2, i * 2 + 1); vh.AddTriangle(next * 2, next * 2 + 1, i * 2 + 1); }
            }
        }
        static Vector2 Point(Rect r, float radius, int i)
        {
            int corner = i / (Steps + 1), step = i % (Steps + 1);
            Vector2 center = new Vector2(corner == 0 || corner == 3 ? r.xMax - radius : r.xMin + radius, corner < 2 ? r.yMax - radius : r.yMin + radius);
            float angle = (corner * 90 + step * 90f / Steps) * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
    }
}
