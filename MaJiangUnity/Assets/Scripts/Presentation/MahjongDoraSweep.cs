using System;
using UnityEngine;
using UnityEngine.UI;

namespace MaJiang.Presentation
{
    [Serializable]
    public sealed class MahjongDoraSweepSettings
    {
        public bool Enabled = true;
        public Color Tint = new(1f, 1f, 1f, 0.66f);
        [Min(0.05f)] public float Duration = 0.8f;
        [Min(0)] public float Pause = 1.2f;
        [Range(0.01f, 1)] public float Width = 0.30f;
        [Range(0, 90)] public float Angle = 45;
        [Range(0, 0.2f)] public float EdgeInset = 0.032f;
        [Min(0)] public float Stagger = 0.35f;
    }

    /// <summary>在牌体局部坐标中绘制双侧渐隐的白色光带，横牌和侧家牌会随牌体旋转。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MahjongDoraSweep : MaskableGraphic
    {
        private readonly Vector2[] polygon = new Vector2[8];
        private readonly Vector2[] clipped = new Vector2[8];
        private MahjongDoraSweepSettings settings;
        private float phaseSeed;
        private float progress = -1;

        public void Play(MahjongDoraSweepSettings parameters, int face)
        {
            settings = parameters;
            phaseSeed = Mathf.Repeat(face * 0.618034f, 1);
            enabled = true;
            Update();
        }

        private void Update()
        {
            if (settings == null) { return; }
            float duration = Mathf.Max(0.05f, settings.Duration);
            double elapsed = (Time.unscaledTimeAsDouble + phaseSeed * settings.Stagger) % (duration + Mathf.Max(0, settings.Pause));
            float next = settings.Enabled && elapsed < duration ? (float)(elapsed / duration) : -1;
            if (next == progress) { return; }
            progress = next;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (settings == null || progress < 0) { return; }

            Rect bounds = GetPixelAdjustedRect();
            float side = Mathf.Min(bounds.width, bounds.height);
            if (side <= 0) { return; }
            Vector2 inset = Vector2.one * (side * Mathf.Clamp(settings.EdgeInset, 0, 0.2f));
            bounds.min += inset;
            bounds.max -= inset;
            float radians = settings.Angle * Mathf.Deg2Rad;
            Vector2 direction = new(Mathf.Cos(radians), -Mathf.Sin(radians));
            float halfWidth = Mathf.Max(0.01f, settings.Width) * side * 0.5f;
            float first = Vector2.Dot(direction, new Vector2(bounds.xMin, bounds.yMax));
            float last = Vector2.Dot(direction, new Vector2(bounds.xMax, bounds.yMin));
            float center = Mathf.Lerp(first - halfWidth, last + halfWidth, progress);
            AddHalf(helper, bounds, direction, center - halfWidth, center, false);
            AddHalf(helper, bounds, direction, center, center + halfWidth, true);
        }

        private void AddHalf(VertexHelper helper, Rect bounds, Vector2 direction, float from, float to, bool fadesOut)
        {
            polygon[0] = bounds.min;
            polygon[1] = new Vector2(bounds.xMin, bounds.yMax);
            polygon[2] = bounds.max;
            polygon[3] = new Vector2(bounds.xMax, bounds.yMin);
            int count = Clip(polygon, 4, clipped, direction, from);
            count = Clip(clipped, count, polygon, -direction, -to);
            if (count < 3) { return; }

            int start = helper.currentVertCount;
            Color tint = settings.Tint;
            for (int index = 0; index < count; index++)
            {
                Vector2 point = polygon[index];
                float fraction = Mathf.InverseLerp(from, to, Vector2.Dot(point, direction));
                Color vertexColor = tint;
                vertexColor.a *= fadesOut ? 1 - fraction : fraction;
                helper.AddVert(point, vertexColor, Vector2.zero);
            }
            for (int index = 1; index < count - 1; index++) { helper.AddTriangle(start, start + index, start + index + 1); }
        }

        private static int Clip(Vector2[] input, int count, Vector2[] output, Vector2 normal, float boundary)
        {
            if (count == 0) { return 0; }
            int written = 0;
            Vector2 before = input[count - 1];
            float previousDistance = Vector2.Dot(before, normal) - boundary;
            for (int index = 0; index < count; index++)
            {
                Vector2 point = input[index];
                float distance = Vector2.Dot(point, normal) - boundary;
                if ((distance >= 0) != (previousDistance >= 0))
                {
                    output[written++] = Vector2.LerpUnclamped(before, point, previousDistance / (previousDistance - distance));
                }
                if (distance >= 0) { output[written++] = point; }
                before = point;
                previousDistance = distance;
            }
            return written;
        }
    }
}
