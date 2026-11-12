using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DungeonMenuDust : MaskableGraphic
    {
        private struct Mote
        {
            public float x, y, phaseX, phaseY, phaseLight, driftX, driftY, opacity;
            public int speedX, speedY;
        }
        private Mote[] motes;
        private float clock, nextFrame;

        protected override void Awake()
        {
            base.Awake();
            var random = new System.Random(194728);
            float Unit() => (float)random.NextDouble();
            motes = new Mote[90];
            for (int i = 0; i < motes.Length; i++)
                motes[i] = new Mote {
                    x = Unit(), y = Unit(), phaseX = Unit() * Mathf.PI * 2,
                    phaseY = Unit() * Mathf.PI * 2, phaseLight = Unit() * Mathf.PI * 2,
                    driftX = 8 + Unit() * 25, driftY = 12 + Unit() * 36,
                    speedX = random.Next(1, 4), speedY = random.Next(1, 4), opacity = .035f + Unit() * .085f
                };
        }

        private void Update()
        {
            if (!DungeonSettings.ReducedMotion) clock = Mathf.Repeat(clock + Time.unscaledDeltaTime, 48);
            if (Time.unscaledTime < nextFrame) return;
            nextFrame = Time.unscaledTime + 1f / 12;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (motes == null || DungeonSettings.ReducedMotion) return;
            Rect r = rectTransform.rect;
            float turn = clock / 48 * Mathf.PI * 2;
            for (int i = 0; i < motes.Length; i++)
            {
                Mote p = motes[i];
                float x = r.xMin + p.x * r.width + Mathf.Sin(turn * p.speedX + p.phaseX) * p.driftX;
                float y = r.yMin + p.y * r.height + Mathf.Sin(turn * p.speedY + p.phaseY) * p.driftY;
                float edge = Mathf.Clamp01(Mathf.Min(Mathf.Min(x - r.xMin, r.xMax - x), Mathf.Min(y - r.yMin, r.yMax - y)) / 24);
                float alpha = p.opacity * (.55f + .45f * Mathf.Sin(turn * 2 + p.phaseLight)) * edge;
                Color tint = i % 4 == 0 ? new Color(.8f, .63f, .36f, alpha) : new Color(.43f, .61f, .75f, alpha);
                DungeonUiPixels.Rect(mesh, this, Mathf.Round(x / 4) * 4, Mathf.Round(y / 4) * 4, 4, 4, tint);
            }
        }
    }
}
