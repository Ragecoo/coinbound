using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>Clips the authored fill row by row, translating its angled right edge in whole pixels.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DungeonHudFill : MaskableGraphic
    {
        private Texture2D texture;
        private int[] rightEdges;
        private Color32[] sourcePixels;
        private int travel;
        private int shift = -1;
        private bool empty;
        private float targetFraction = 1;
        public float DisplayedFraction { get; private set; } = 1;
        public event System.Action<Vector2, int> PixelsLost;
        public event System.Action<Vector2, Color32> PixelDetached;
        public override Texture mainTexture => texture != null ? texture : base.mainTexture;

        public void Initialize(Texture2D source)
        {
            texture = source;
            Color32[] pixels = sourcePixels = source.GetPixels32();
            rightEdges = new int[source.height];
            for (int y = 0; y < source.height; y++)
                for (int x = 0; x < source.width; x++)
                    if (pixels[y * source.width + x].a > 0) rightEdges[y] = x + 1;
            int left = 0;
            while (left < source.width && pixels[(source.height - 1) * source.width + left].a == 0) left++;
            travel = rightEdges[source.height - 1] - left;
            SetFraction(1);
        }

        public void SetFraction(float fraction)
        {
            targetFraction = DisplayedFraction = Mathf.Clamp01(fraction);
            ApplyFraction(DisplayedFraction);
        }

        public void AnimateTo(float fraction) => targetFraction = Mathf.Clamp01(fraction);

        private void Update()
        {
            if (DisplayedFraction == targetFraction) return;
            int previousShift = shift;
            // Full-width changes finish in 0.25 seconds; mesh edges still advance in whole source pixels.
            DisplayedFraction = Mathf.MoveTowards(DisplayedFraction, targetFraction, Time.deltaTime * 4);
            ApplyFraction(DisplayedFraction);
            if (shift > previousShift && texture != null)
            {
                // Emit each texel removed by this exact clipping step, preserving its color.
                Rect rect = rectTransform.rect;
                for (int y = 0; y < texture.height; y++)
                for (int x = empty ? 0 : Mathf.Max(0, rightEdges[y] - shift); x < Mathf.Max(0, rightEdges[y] - previousShift); x++)
                {
                    Color32 tint = sourcePixels[y * texture.width + x];
                    if (tint.a == 0) continue;
                    PixelDetached?.Invoke(new Vector2(rect.xMin + x * rect.width / texture.width,
                        rect.yMin + y * rect.height / texture.height), tint);
                }
                int row = texture.height / 2;
                Vector2 edge = new Vector2(Mathf.Max(0, rightEdges[row] - (shift + previousShift) / 2),
                    rectTransform.rect.yMin + row);
                PixelsLost?.Invoke(edge, shift - previousShift);
            }
        }

        private void ApplyFraction(float fraction)
        {
            int next = Mathf.RoundToInt((1 - fraction) * travel);
            bool hide = fraction <= 0;
            if (next == shift && hide == empty) return;
            shift = next; empty = hide; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (texture == null || empty) return;
            Rect rect = rectTransform.rect;
            for (int y = 0; y < texture.height; y++)
            {
                int right = Mathf.Max(0, rightEdges[y] - shift);
                if (right == 0) continue;
                float u = right / (float)texture.width;
                float v0 = y / (float)texture.height, v1 = (y + 1f) / texture.height;
                int n = mesh.currentVertCount;
                mesh.AddVert(new Vector3(rect.xMin, rect.yMin + rect.height * v0), color, new Vector2(0, v0));
                mesh.AddVert(new Vector3(rect.xMin, rect.yMin + rect.height * v1), color, new Vector2(0, v1));
                mesh.AddVert(new Vector3(rect.xMin + rect.width * u, rect.yMin + rect.height * v1), color, new Vector2(u, v1));
                mesh.AddVert(new Vector3(rect.xMin + rect.width * u, rect.yMin + rect.height * v0), color, new Vector2(u, v0));
                mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n + 2, n + 3, n);
            }
        }
    }
}
