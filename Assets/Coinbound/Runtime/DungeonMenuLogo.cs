using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>Sample the existing logo on the same four-unit pixel grid as the crypt.</summary>
    public sealed class DungeonMenuLogo : RawImage
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (texture == null) return;
            Rect r = rectTransform.rect;
            int columns = Mathf.RoundToInt(r.width / 4), rows = Mathf.RoundToInt(r.height / 4);
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                Vector2 uv = new Vector2((x + .5f) / columns, (y + .5f) / rows);
                Vector2 min = new Vector2(r.xMin + x * 4, r.yMin + y * 4);
                Vector2 max = min + Vector2.one * 4;
                min = PixelAdjustPoint(min); max = PixelAdjustPoint(max);
                int n = mesh.currentVertCount;
                mesh.AddVert(new Vector3(min.x, min.y), color, uv);
                mesh.AddVert(new Vector3(min.x, max.y), color, uv);
                mesh.AddVert(new Vector3(max.x, max.y), color, uv);
                mesh.AddVert(new Vector3(max.x, min.y), color, uv);
                mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n, n + 2, n + 3);
            }
        }
    }
}
