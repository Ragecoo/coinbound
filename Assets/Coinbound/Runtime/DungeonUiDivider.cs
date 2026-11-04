using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>Authored three-part divider: only the plain ends stretch, never the central ornament.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DungeonUiDivider : MaskableGraphic
    {
        private Texture2D texture;
        public override Texture mainTexture => texture != null ? texture : base.mainTexture;

        protected override void Awake()
        {
            base.Awake();
            texture = Resources.Load<Texture2D>("InterfaceArt/Gui/divider");
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = GetPixelAdjustedRect();
            float scale = canvas != null ? canvas.scaleFactor : 1;
            float artScale = Mathf.Max(1, Mathf.Round(scale)) / scale;
            float half = Mathf.Min(r.width / 2, 30.5f * artScale);
            float height = 9 * artScale;
            Part(mesh, r.xMin, r.center.x - half, r.center.y, height, 0, 50f / 161);
            Part(mesh, r.center.x - half, r.center.x + half, r.center.y, height, 50f / 161, 111f / 161);
            Part(mesh, r.center.x + half, r.xMax, r.center.y, height, 111f / 161, 1);
        }

        private void Part(VertexHelper mesh, float left, float right, float y, float height, float u0, float u1)
        {
            if (right <= left) return;
            Vector2 min = new Vector2(left, y - height / 2);
            Vector2 max = new Vector2(right, y + height / 2);
            if (canvas != null)
            {
                min = RectTransformUtility.PixelAdjustPoint(min, rectTransform, canvas);
                max = RectTransformUtility.PixelAdjustPoint(max, rectTransform, canvas);
            }
            int n = mesh.currentVertCount;
            mesh.AddVert(new Vector3(min.x, min.y), color, new Vector2(u0, 0));
            mesh.AddVert(new Vector3(min.x, max.y), color, new Vector2(u0, 1));
            mesh.AddVert(new Vector3(max.x, max.y), color, new Vector2(u1, 1));
            mesh.AddVert(new Vector3(max.x, min.y), color, new Vector2(u1, 0));
            mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n + 2, n + 3, n);
        }
    }
}
