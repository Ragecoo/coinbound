#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace Coinbound.EnemyPack.Editor
{
    public sealed class EnemyTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.Contains("/CoinboundEnemyPack/Sprites/"))return;
            var t=(TextureImporter)assetImporter;
            t.textureType=TextureImporterType.Sprite;
            t.spriteImportMode=SpriteImportMode.Single;
            t.spritePixelsPerUnit=32;
            t.filterMode=FilterMode.Point;
            t.textureCompression=TextureImporterCompression.Uncompressed;
            t.mipmapEnabled=false;
            t.alphaIsTransparency=true;
            t.npotScale=TextureImporterNPOTScale.None;
            t.maxTextureSize=2048;
        }
    }
}
#endif
