using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>Blends the illustration into the scene without a visible rectangular image edge.</summary>
    // RawImage provides the renderer before the first overlay pass, and dirties the material when texture changes.
    public sealed class DungeonVignetteImage : RawImage
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = rectTransform.rect;
            const int columns = 24, rows = 16;
            for (int y = 0; y <= rows; y++)
            for (int x = 0; x <= columns; x++)
            {
                float u = (float)x / columns, v = (float)y / rows;
                Color tint = color;
                tint.a *= Mathf.SmoothStep(0, 1, Mathf.Min(u, 1 - u) / .14f)
                    * Mathf.SmoothStep(0, 1, Mathf.Min(v, 1 - v) / .18f);
                mesh.AddVert(new Vector3(r.xMin + u * r.width, r.yMin + v * r.height), tint, new Vector2(u, v));
                if (x == columns || y == rows) continue;
                int a = y * (columns + 1) + x;
                mesh.AddTriangle(a, a + columns + 1, a + columns + 2);
                mesh.AddTriangle(a, a + columns + 2, a + 1);
            }
        }
    }
}
