using UnityEditor;
using UnityEngine;

namespace Coinbound.Editor
{
    /// <summary>Keep UI art crisp and free from platform compression artifacts.</summary>
    public sealed class InterfaceArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Coinbound/Resources/InterfaceArt/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            if (assetPath.Contains("/Hud/"))
                importer.isReadable = assetPath.EndsWith("/health_fill.png") || assetPath.EndsWith("/mana_fill.png");
            if (assetPath.Contains("/Gui/") && !assetPath.EndsWith("/divider.png"))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.spriteBorder = new Vector4(14, 14, 14, 14);
                importer.alphaIsTransparency = true;
            }
        }
    }
}
