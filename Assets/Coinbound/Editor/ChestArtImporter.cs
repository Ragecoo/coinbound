using UnityEditor;
using UnityEngine;

namespace Coinbound.Editor
{
    public sealed class ChestArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            bool indigo=assetPath.StartsWith("Assets/Coinbound/Resources/ChestArtIndigo/");
            if (!indigo && !assetPath.StartsWith("Assets/Coinbound/Resources/ChestArt/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            // Indigo base center is (16,40) from the top, identical in every 32x48 frame.
            importer.spritePivot = indigo ? new Vector2(.5f,8f/48) : new Vector2(.5f, 6f / 40);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
        }
    }
}
