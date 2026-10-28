using UnityEngine;

namespace Coinbound
{
    /// <summary>Shared pixel placeholders. The caller owns the returned sprite and its texture.</summary>
    internal static class DungeonPlaceholderArt
    {
        public static Sprite Create(string label, Color color, bool chest)
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = label, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            Color32 outline = new Color32(15, 20, 34, 255);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int dx = x - 16, dy = y - 16;
                bool outer = chest ? x >= 3 && x <= 28 && y >= 6 && y <= 25 : dx * dx + dy * dy <= 144;
                bool inner = chest ? x >= 5 && x <= 26 && y >= 8 && y <= 23 : dx * dx + dy * dy <= 100;
                if (!outer) continue;
                pixels[y * size + x] = inner ? (Color32)color : outline;
                if (chest && inner && (y == 14 || (x >= 14 && x <= 17 && y >= 10 && y <= 16)))
                    pixels[y * size + x] = new Color32(255, 235, 163, 255);
                if (!chest && y >= 17 && y <= 19 && (x == 12 || x == 13 || x == 19 || x == 20))
                    pixels[y * size + x] = outline;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32, 0, SpriteMeshType.FullRect);
            sprite.name = label;
            return sprite;
        }
    }
}
