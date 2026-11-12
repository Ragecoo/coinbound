using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>Ambient details authored in the background illustration's 320 x 180 pixel grid.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DungeonMenuAtmosphere : MaskableGraphic
    {
        private static readonly Vector2[] Candles = {
            new Vector2(30, 87), new Vector2(33, 87),
            new Vector2(73, 103), new Vector2(77, 106),
            new Vector2(267, 110), new Vector2(270, 107), new Vector2(273, 111)
        };
        private float clock, nextFrame;
        public const float LoopSeconds = 48;
        public float Phase { get; private set; }

        private void Update()
        {
            if (!DungeonSettings.ReducedMotion) clock = Mathf.Repeat(clock + Time.unscaledDeltaTime, LoopSeconds);
            if (Time.unscaledTime < nextFrame) return;
            nextFrame = Time.unscaledTime + 1f / 12;
            Phase = DungeonSettings.ReducedMotion ? 0 : clock / LoopSeconds;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float turn = Phase * Mathf.PI * 2;
            for (int i = 0; i < Candles.Length; i++)
            {
                Vector2 p = Candles[i];
                float flicker = .65f + .2f * Mathf.Sin(turn * 47 + i * 2.3f) + .15f * Mathf.Sin(turn * 79 + i);
                // Nested stepped silhouettes, never filtered circles or smooth particles.
                for (int radius = 10; radius >= 2; radius -= 2)
                {
                    float glow = (.008f + .009f * flicker) * (1 - radius / 14f);
                    Pixel(mesh, p.x - radius + 2, p.y - radius, radius * 2 - 4, radius * 2, new Color(1, .52f, .16f, glow));
                    Pixel(mesh, p.x - radius, p.y - radius + 2, radius * 2, radius * 2 - 4, new Color(1, .52f, .16f, glow));
                }
                int sway = Mathf.RoundToInt(Mathf.Sin(turn * 31 + i * 1.7f));
                int height = 2 + Mathf.RoundToInt(flicker * 2);
                Pixel(mesh, p.x - 1, p.y - 2, 3, 3, new Color(1, .34f, .06f, .35f));
                Pixel(mesh, p.x, p.y - height, 1, height, new Color(1, .65f, .23f, .85f));
                Pixel(mesh, p.x + sway, p.y - height, 1, 1, new Color(1, .76f, .35f, .65f));
                Pixel(mesh, p.x, p.y - 1, 1, 1, new Color(1, .9f, .57f, .9f));
            }

            // Water lies to the left of the central statue; no ripples on the masonry.
            for (int i = 0; i < 23; i++)
            {
                float life = Mathf.Repeat(Phase * 6 + i * .618034f, 1);
                float y = 108 + life * 20;
                float x = 183 + Mathf.Sin(i * 12.17f) * 12 + Mathf.Sin(turn * 6 + i) * 2;
                float width = 1 + life * (i % 4 + 2);
                float opacity = Mathf.Sin(life * Mathf.PI) * (.08f + .035f * (i % 3));
                Pixel(mesh, x, y, width, 1, new Color(.39f, .62f, .82f, opacity));
                if (i % 4 == 0) Pixel(mesh, x + 32, y - 1, width * .6f, 1, new Color(.32f, .5f, .7f, opacity * .55f));
            }

        }

        private void Pixel(VertexHelper mesh, float x, float y, float width, float height, Color tint)
        {
            Rect rect = rectTransform.rect;
            float sx = rect.width / 320, sy = rect.height / 180;
            DungeonUiPixels.Rect(mesh, this, rect.xMin + Mathf.Round(x) * sx,
                rect.yMax - (Mathf.Round(y) + Mathf.Max(1, Mathf.Round(height))) * sy,
                Mathf.Max(1, Mathf.Round(width)) * sx, Mathf.Max(1, Mathf.Round(height)) * sy, tint);
        }
    }
}
