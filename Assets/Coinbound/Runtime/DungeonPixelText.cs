using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>Rasterize at the font's native size, then enlarge by whole screen pixels.</summary>
    public sealed class DungeonPixelText : Text
    {
        private readonly UIVertex[] quad = new UIVertex[4];
        public int ExtraPixelSteps = 1;
        public float MeasureHeight(float width)
        {
            if (font == null) return 0;
            float screenScale = canvas != null ? Mathf.Max(.01f, canvas.scaleFactor) : 1;
            float zoom = (Mathf.Max(1, Mathf.Round(fontSize * screenScale / 16)) + ExtraPixelSteps) / screenScale;
            var settings = GetGenerationSettings(new Vector2(width / zoom, 0));
            settings.fontSize = 16; settings.fontStyle = FontStyle.Normal; settings.scaleFactor = 1;
            settings.resizeTextForBestFit = false; settings.verticalOverflow = VerticalWrapMode.Overflow;
            return cachedTextGeneratorForLayout.GetPreferredHeight(text, settings) * zoom;
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (font == null) return;
            const int nativeSize = 16;
            float screenScale = canvas != null ? Mathf.Max(.01f, canvas.scaleFactor) : 1;
            // One additional whole-pixel step makes every label larger without filtering.
            float zoom = (Mathf.Max(1, Mathf.Round(fontSize * screenScale / nativeSize)) + ExtraPixelSteps) / screenScale;
            var settings = GetGenerationSettings(rectTransform.rect.size / zoom);
            settings.fontSize = nativeSize;
            settings.fontStyle = FontStyle.Normal;
            settings.scaleFactor = 1;
            settings.resizeTextForBestFit = false;
            // Native font line metrics exceed the visible glyph height. Keep compact HUD rows
            // and button labels visible instead of truncating them at those invisible margins.
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            m_DisableFontTextureRebuiltCallback = true;
            try
            {
                cachedTextGenerator.PopulateWithErrors(text, settings, gameObject);
                if (font.material.mainTexture != null) font.material.mainTexture.filterMode = FilterMode.Point;
                var vertices = cachedTextGenerator.verts;
                Vector2 offset = vertices.Count > 0 ? (Vector2)vertices[0].position * zoom : Vector2.zero;
                offset = PixelAdjustPoint(offset) - offset;
                for (int i = 0; i < vertices.Count; i++)
                {
                    int corner = i & 3;
                    quad[corner] = vertices[i];
                    quad[corner].position = vertices[i].position * zoom + (Vector3)offset;
                    if (corner == 3) mesh.AddUIVertexQuad(quad);
                }
            }
            finally { m_DisableFontTextureRebuiltCallback = false; }
        }
    }
}
