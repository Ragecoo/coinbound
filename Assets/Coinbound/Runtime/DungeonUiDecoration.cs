using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>Grid-aligned bronze pixel filigree and restrained ambient particles.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DungeonUiDecoration : MaskableGraphic
    {
        public enum Kind { Frame, Rule, Motes, Shade, Coin, Aura }
        public Kind kind;
        public bool falling;
        public float emphasis = 1;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = rectTransform.rect;
            Color c = color;
            if (kind == Kind.Aura)
            {
                for (int i = 16; i > 0; i--)
                {
                    float t = i / 16f;
                    Color tint = c; tint.a *= .025f;
                    Quad(mesh, -r.width * t / 2, -r.height * t / 2, r.width * t, r.height * t, tint);
                }
            }
            else if (kind == Kind.Frame)
            {
                Quad(mesh, r.xMin, r.yMin, r.width, 1, c);
                Quad(mesh, r.xMin, r.yMax - 2, r.width, 2, c);
                Quad(mesh, r.xMin, r.yMin, 1, r.height, c);
                Quad(mesh, r.xMax - 2, r.yMin, 2, r.height, c);
                Color inner = c; inner.a *= .24f;
                Quad(mesh, r.xMin + 4, r.yMin + 4, r.width - 8, 1, inner);
                Quad(mesh, r.xMin + 4, r.yMax - 6, r.width - 8, 1, inner);
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                {
                    Vector2 p = new Vector2(x < 0 ? r.xMin : r.xMax, y < 0 ? r.yMin : r.yMax);
                    for (int i = 0; i < 4; i++)
                    {
                        float inset = 4 + i * 4;
                        Diamond(mesh, p + new Vector2(-x * inset, -y * 4), i == 0 ? 4 : 2, c);
                        Diamond(mesh, p + new Vector2(-x * 4, -y * inset), i == 0 ? 4 : 2, c);
                    }
                    Quad(mesh, p.x - x * 10 - 2, p.y - y * 10 - 2, 4, 4, c);
                }
            }
            else if (kind == Kind.Rule)
            {
                Color faint = c; faint.a *= .22f;
                Quad(mesh, r.xMin, -0.5f, r.width, 1, faint);
                Quad(mesh, -r.width * .18f, -0.5f, r.width * .36f, 1, c);
                Diamond(mesh, Vector2.zero, 4, c);
                Diamond(mesh, new Vector2(-13, 0), 2, c);
                Diamond(mesh, new Vector2(13, 0), 2, c);
            }
            else if (kind == Kind.Shade)
            {
                const int slices = 24;
                for (int i = 0; i < slices; i++)
                {
                    float t = (i + .5f) / slices;
                    Color shade = c;
                    shade.a *= Mathf.Pow(Mathf.Abs(t * 2 - 1), 1.4f);
                    Quad(mesh, r.xMin, r.yMin + r.height * i / slices, r.width, r.height / slices + 1, shade);
                }
            }
            else if (kind == Kind.Coin)
            {
                float t = DungeonSettings.ReducedMotion ? 0 : Time.unscaledTime * 1.8f;
                float width = Mathf.Max(.14f, Mathf.Abs(Mathf.Cos(t)));
                for (int i = 0; i < 8; i++)
                {
                    float y = (i - 3.5f) * r.height / 10;
                    float w = (i == 0 || i == 7 ? .46f : i == 1 || i == 6 ? .72f : .88f) * r.width * width;
                    Color band = c * (i % 3 == 0 ? .72f : 1); band.a = c.a;
                    Quad(mesh, -w / 2, y, w, r.height / 10 + .5f, band);
                }
                Quad(mesh, -width * 2, -r.height * .2f, width * 4, r.height * .4f, new Color(.15f, .11f, .08f, c.a));
            }
            else if (!DungeonSettings.ReducedMotion)
            {
                float t = Time.unscaledTime;
                for (int i = 0; i < 32; i++)
                {
                    float phase = Mathf.Repeat(i * .618034f + t * (falling ? -.12f : .012f), 1);
                    float x = Mathf.Repeat(i * .381966f + Mathf.Sin(t * .35f + i) * .009f, 1);
                    Color particle = c; particle.a *= Mathf.Sin(phase * Mathf.PI) * (.2f + (i % 4) * .16f);
                    float size = i % 5 == 0 ? 3 : 2;
                    Quad(mesh, Mathf.Round(r.xMin + x * r.width), Mathf.Round(r.yMin + phase * r.height), size, size, particle);
                }
            }
        }

        private void Update()
        {
            if (kind == Kind.Motes || kind == Kind.Coin) SetVerticesDirty();
        }
        private void Quad(VertexHelper m, float x, float y, float w, float h, Color c)
        {
            DungeonUiPixels.Rect(m, this, x, y, w, h, c);
        }
        private void Diamond(VertexHelper m, Vector2 p, float size, Color c)
        {
            // Stepped rather than antialiased ornament, matching the game art.
            Quad(m, p.x - size, p.y - 1, size * 2, 2, c);
            Quad(m, p.x - 1, p.y - size, 2, size * 2, c);
        }
    }
}
