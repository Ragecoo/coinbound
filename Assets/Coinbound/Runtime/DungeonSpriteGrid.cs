using UnityEngine;

namespace Coinbound
{
    /// <summary>Maps axis-aligned landmark art onto the dungeon's world pixel grid after scaling.</summary>
    internal static class DungeonSpriteGrid
    {
        private static readonly int WorldRectId = Shader.PropertyToID("_PixelWorldRect");
        private static readonly int UvRectId = Shader.PropertyToID("_PixelUVRect");
        private static readonly int TexelSizeId = Shader.PropertyToID("_PixelTexelSize");

        public static void Apply(SpriteRenderer renderer, MaterialPropertyBlock properties)
        {
            Bounds bounds = renderer.bounds;
            Sprite sprite = renderer.sprite;
            Rect rect = sprite.textureRect;
            Texture2D texture = sprite.texture;
            properties.SetVector(WorldRectId, new Vector4(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y));
            properties.SetVector(UvRectId, new Vector4(rect.x / texture.width, rect.y / texture.height,
                rect.width / texture.width, rect.height / texture.height));
            properties.SetVector(TexelSizeId, new Vector4(1f / texture.width, 1f / texture.height, texture.width, texture.height));
            renderer.SetPropertyBlock(properties);
        }
    }
}
