using UnityEngine;
using UnityEngine.UI;

namespace BeastSoccer.UI
{
    // Resolution-independent rings and thumb-following arcs, without a filled glow disc.
    public class ArcGraphic : MaskableGraphic
    {
        public float thickness = 5f;
        public float sweep = 360f;
        public float angle;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
            float inner = Mathf.Max(0f, radius - thickness);
            int segments = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(sweep) / 4f));
            for (int i = 0; i <= segments; i++)
            {
                float a = (angle - sweep * .5f + sweep * i / segments) * Mathf.Deg2Rad;
                Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(v * inner, color, Vector2.zero);
                vh.AddVert(v * radius, color, Vector2.zero);
                if (i == 0) continue;
                int n = i * 2;
                vh.AddTriangle(n - 2, n - 1, n);
                vh.AddTriangle(n, n - 1, n + 1);
            }
        }
        public void SetArc(float facing, float extent, Color tint)
        {
            angle = facing; sweep = extent; color = tint; SetVerticesDirty();
        }
    }
}
