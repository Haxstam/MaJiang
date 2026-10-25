using UnityEngine;
using UnityEngine.UI;

namespace MaJiang.Presentation
{
    /// <summary>轻量圆角矩形/描边，使用UI网格而非新增位图，场景中可直接调色和调整圆角。</summary>
    [AddComponentMenu("Mahjong/UI/Table Shape")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TableShape : MaskableGraphic
    {
        public float Radius = 12;
        public float Stroke;

        protected override void OnEnable()
        {
            // RequireComponent不会补齐已经保存的旧场景/预制体；注册到GraphicRegistry前完成兼容修复。
            if (GetComponent<CanvasRenderer>() == null) { gameObject.AddComponent<CanvasRenderer>(); }
            base.OnEnable();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect bounds = rectTransform.rect;
            float radius = Mathf.Clamp(Radius, 0, Mathf.Min(bounds.width, bounds.height) / 2);
            const int steps = 12;
            const int count = 4 * (steps + 1);
            Vector2[] outer = Outline(bounds, radius, steps);
            if (Stroke <= 0)
            {
                helper.AddVert(bounds.center, color, Vector2.zero);
                foreach (Vector2 point in outer) { helper.AddVert(point, color, Vector2.zero); }
                for (int index = 0; index < count; index++) { helper.AddTriangle(0, index + 1, (index + 1) % count + 1); }
            }
            else
            {
                float stroke = Mathf.Min(Stroke, Mathf.Min(bounds.width, bounds.height) / 2);
                Rect inside = new(bounds.x + stroke, bounds.y + stroke, bounds.width - stroke * 2, bounds.height - stroke * 2);
                Vector2[] inner = Outline(inside, Mathf.Max(0, radius - stroke), steps);
                for (int index = 0; index < count; index++)
                {
                    helper.AddVert(outer[index], color, Vector2.zero);
                    helper.AddVert(inner[index], color, Vector2.zero);
                }
                for (int index = 0; index < count; index++)
                {
                    int next = (index + 1) % count;
                    helper.AddTriangle(index * 2, next * 2, index * 2 + 1);
                    helper.AddTriangle(next * 2, next * 2 + 1, index * 2 + 1);
                }
            }
        }

        private static Vector2[] Outline(Rect bounds, float radius, int steps)
        {
            Vector2[] points = new Vector2[4 * (steps + 1)];
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 center = new(corner == 0 || corner == 3 ? bounds.xMax - radius : bounds.xMin + radius,
                    corner < 2 ? bounds.yMax - radius : bounds.yMin + radius);
                for (int step = 0; step <= steps; step++)
                {
                    float angle = (corner * 90 + step * 90f / steps) * Mathf.Deg2Rad;
                    points[corner * (steps + 1) + step] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }
            }
            return points;
        }
    }
}
