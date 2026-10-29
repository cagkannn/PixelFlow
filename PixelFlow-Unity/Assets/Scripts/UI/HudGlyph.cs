using UnityEngine;
using UnityEngine.UI;

namespace PixelFlow.UI
{
    /// <summary>Small, resolution-independent interface marks. No emoji or downloaded images.</summary>
    public sealed class HudGlyph : MaskableGraphic
    {
        public enum Kind { Pause, Play, Restart, Previous, Next, ChevronDown, Help }
        public Kind Symbol = Kind.Pause;
        public float Stroke = 2.1f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float unit = Mathf.Min(r.width, r.height) / 24f;
            Vector2 center = r.center;
            if (Symbol == Kind.Pause)
            {
                Segment(vh, center, unit, new Vector2(-4, -7), new Vector2(-4, 7), Stroke + 1f);
                Segment(vh, center, unit, new Vector2(4, -7), new Vector2(4, 7), Stroke + 1f);
            }
            else if (Symbol == Kind.Play)
            {
                Triangle(vh, center + new Vector2(-4, -7) * unit,
                    center + new Vector2(7, 0) * unit, center + new Vector2(-4, 7) * unit);
            }
            else if (Symbol == Kind.Previous || Symbol == Kind.Next)
            {
                float sign = Symbol == Kind.Next ? 1f : -1f;
                Segment(vh, center, unit, new Vector2(-3 * sign, 7), new Vector2(4 * sign, 0), Stroke);
                Segment(vh, center, unit, new Vector2(4 * sign, 0), new Vector2(-3 * sign, -7), Stroke);
            }
            else if (Symbol == Kind.ChevronDown)
            {
                Segment(vh, center, unit, new Vector2(-5, 2), new Vector2(0, -3), Stroke);
                Segment(vh, center, unit, new Vector2(0, -3), new Vector2(5, 2), Stroke);
            }
            else if (Symbol == Kind.Restart)
            {
                const int steps = 18;
                Vector2 previous = Vector2.zero;
                for (int i = 0; i <= steps; i++)
                {
                    float angle = Mathf.Lerp(135f, -170f, i / (float)steps) * Mathf.Deg2Rad;
                    Vector2 point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 7.2f;
                    if (i > 0) Segment(vh, center, unit, previous, point, Stroke);
                    previous = point;
                }
                Segment(vh, center, unit, new Vector2(-5.2f, 5.1f), new Vector2(-5.2f, 10f), Stroke);
                Segment(vh, center, unit, new Vector2(-5.2f, 5.1f), new Vector2(-.3f, 5.1f), Stroke);
            }
            else if (Symbol == Kind.Help)
            {
                const int circleSteps = 30;
                for (int i = 0; i < circleSteps; i++)
                {
                    float a = i * Mathf.PI * 2f / circleSteps;
                    float b = (i + 1) * Mathf.PI * 2f / circleSteps;
                    Segment(vh, center, unit, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 9f,
                        new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 9f, Stroke * .75f);
                }
                Vector2[] mark = { new Vector2(-2.5f, 3.1f), new Vector2(-1.5f, 5f),
                    new Vector2(1.1f, 5.1f), new Vector2(3f, 3.7f), new Vector2(2.8f, 1.6f),
                    new Vector2(.1f, -.2f), new Vector2(.1f, -2.1f) };
                for (int i = 1; i < mark.Length; i++) Segment(vh, center, unit, mark[i - 1], mark[i], Stroke * .8f);
                Segment(vh, center, unit, new Vector2(.1f, -5.3f), new Vector2(.1f, -5.5f), Stroke * .9f);
            }
        }

        private void Segment(VertexHelper vh, Vector2 center, float scale, Vector2 from, Vector2 to, float thickness)
        {
            from = center + from * scale;
            to = center + to * scale;
            Vector2 direction = to - from;
            if (direction.sqrMagnitude < .000001f) return;
            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * thickness * scale * .5f;
            int first = vh.currentVertCount;
            vh.AddVert(from - normal, color, Vector2.zero);
            vh.AddVert(from + normal, color, Vector2.zero);
            vh.AddVert(to + normal, color, Vector2.zero);
            vh.AddVert(to - normal, color, Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2);
            vh.AddTriangle(first, first + 2, first + 3);
        }

        private void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c)
        {
            int first = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero); vh.AddVert(c, color, Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2);
        }
    }
}
