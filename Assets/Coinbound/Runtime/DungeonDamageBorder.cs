using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>Authored 320x180 pixel vignette, with discrete fading and point sampling.</summary>
    public sealed class DungeonDamageBorder : RawImage
    {
        private float remaining;
        private const float Duration = .48f;
        public void Pulse() => Show("vignette-damage");
        public void Heal() => Show("vignette-heal");
        private void Show(string asset)
        {
            texture = Resources.Load<Texture2D>("InterfaceArt/Vignette/" + asset);
            remaining = Duration;
            color = Color.white;
        }
        public void Clear() { remaining = 0; color = Color.clear; }
        protected override void Awake() { base.Awake(); Clear(); }
        private void Update()
        {
            if (remaining <= 0) return;
            remaining = Mathf.Max(0, remaining - Time.deltaTime);
            color = new Color(1, 1, 1, DungeonSettings.DamageFlash ? Mathf.Ceil(remaining / Duration * 16) / 16 : 0);
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            if (remaining <= 0 || !DungeonSettings.DamageFlash) { mesh.Clear(); return; }
            PopulatePixelVignette(this, mesh);
        }
        internal static void PopulatePixelVignette(RawImage image, VertexHelper mesh)
        {
            mesh.Clear();
            if (image.texture == null) return;
            Rect rect = image.rectTransform.rect;
            float scale = image.canvas != null ? Mathf.Max(.01f, image.canvas.scaleFactor) : 1;
            float zoom = Mathf.Max(1, Mathf.Ceil(Mathf.Max(rect.width * scale / image.texture.width,
                rect.height * scale / image.texture.height))) / scale;
            // Crop symmetrically on other aspect ratios instead of stretching pixels.
            float u = rect.width / (image.texture.width * zoom), v = rect.height / (image.texture.height * zoom);
            mesh.AddVert(new Vector3(rect.xMin, rect.yMin), image.color, new Vector2((1-u)/2, (1-v)/2));
            mesh.AddVert(new Vector3(rect.xMin, rect.yMax), image.color, new Vector2((1-u)/2, (1+v)/2));
            mesh.AddVert(new Vector3(rect.xMax, rect.yMax), image.color, new Vector2((1+u)/2, (1+v)/2));
            mesh.AddVert(new Vector3(rect.xMax, rect.yMin), image.color, new Vector2((1+u)/2, (1-v)/2));
            mesh.AddTriangle(0,1,2); mesh.AddTriangle(2,3,0);
        }
    }
}
