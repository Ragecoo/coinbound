using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>Shared two-unit art grid. Every emitted edge is also aligned to a physical screen pixel.</summary>
    internal static class DungeonUiPixels
    {
        public const float Size = 2;
        public static float Snap(float value) => Mathf.Round(value / Size) * Size;

        public static void Rect(VertexHelper mesh, Graphic graphic, float x, float y, float width, float height, Color color)
        {
            if (width <= 0 || height <= 0) return;
            float left = Snap(x), bottom = Snap(y);
            float right = left + Mathf.Max(Size, Snap(width));
            float top = bottom + Mathf.Max(Size, Snap(height));
            Add(mesh, graphic, new Vector2(left, bottom), new Vector2(right, top), color);
        }

        private static void Add(VertexHelper mesh, Graphic graphic, Vector2 min, Vector2 max, Color color)
        {
            // CanvasScaler may have a fractional scale at arbitrary window sizes.
            if (graphic.canvas != null)
            {
                min = RectTransformUtility.PixelAdjustPoint(min, graphic.rectTransform, graphic.canvas);
                max = RectTransformUtility.PixelAdjustPoint(max, graphic.rectTransform, graphic.canvas);
            }
            int n = mesh.currentVertCount;
            mesh.AddVert(new Vector3(min.x, min.y), color, Vector2.zero);
            mesh.AddVert(new Vector3(min.x, max.y), color, Vector2.zero);
            mesh.AddVert(new Vector3(max.x, max.y), color, Vector2.zero);
            mesh.AddVert(new Vector3(max.x, min.y), color, Vector2.zero);
            mesh.AddTriangle(n, n + 1, n + 2);
            mesh.AddTriangle(n + 2, n + 3, n);
        }

        public static void Line(VertexHelper mesh, Graphic graphic, Vector2 from, Vector2 to, float width, Color color)
        {
            int x = Mathf.RoundToInt(from.x / Size), y = Mathf.RoundToInt(from.y / Size);
            int endX = Mathf.RoundToInt(to.x / Size), endY = Mathf.RoundToInt(to.y / Size);
            int dx = Mathf.Abs(endX - x), dy = -Mathf.Abs(endY - y);
            int sx = x < endX ? 1 : -1, sy = y < endY ? 1 : -1;
            int error = dx + dy;
            int cells = Mathf.Max(1, Mathf.RoundToInt(width / Size));
            while (true)
            {
                Rect(mesh, graphic, (x - cells / 2) * Size, (y - cells / 2) * Size,
                    cells * Size, cells * Size, color);
                if (x == endX && y == endY) break;
                int twice = error * 2;
                if (twice >= dy) { error += dy; x += sx; }
                if (twice <= dx) { error += dx; y += sy; }
            }
        }
    }
}
