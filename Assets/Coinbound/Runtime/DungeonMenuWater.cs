using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>Pixel-sized horizontal refraction confined to the painted pool, below the statue.</summary>
    public sealed class DungeonMenuWater : RawImage
    {
        private float clock, nextFrame;
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
            if (texture == null || DungeonSettings.ReducedMotion) return;
            Rect r = rectTransform.rect;
            float sx = r.width / 320, sy = r.height / 180;
            float phase = clock / 48 * Mathf.PI * 2;
            for (int y = 110; y < 128; y++)
            {
                int left = 180 - (y - 110) / 3;
                int right = 203 - Mathf.Max(0, y - 121);
                int shift = Mathf.RoundToInt(Mathf.Sin(phase * 8 + y * .8f));
                for (int x = left; x < right; x++)
                {
                    float edge = Mathf.Min(Mathf.Min(x - left, right - 1 - x), Mathf.Min(y - 110, 127 - y));
                    Color tint = new Color(1, 1, 1, Mathf.Clamp01(edge / 3) * .8f);
                    float px = r.xMin + x * sx, py = r.yMax - (y + 1) * sy;
                    float u = (x + shift) / 320f, v = 1 - (y + 1) / 180f;
                    int n = mesh.currentVertCount;
                    mesh.AddVert(new Vector3(px, py), tint, new Vector2(u, v));
                    mesh.AddVert(new Vector3(px, py + sy), tint, new Vector2(u, v + 1f / 180));
                    mesh.AddVert(new Vector3(px + sx, py + sy), tint, new Vector2(u + 1f / 320, v + 1f / 180));
                    mesh.AddVert(new Vector3(px + sx, py), tint, new Vector2(u + 1f / 320, v));
                    mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n, n + 2, n + 3);
                }
            }
        }
    }
}
