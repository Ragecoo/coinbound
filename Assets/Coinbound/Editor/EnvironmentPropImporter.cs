using System.IO;
using UnityEditor;
using UnityEngine;
namespace Coinbound.Editor
{
    public sealed class EnvironmentPropImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if(!assetPath.Contains("/Resources/EnvironmentProps52V5/"))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.npotScale=TextureImporterNPOTScale.None;
            importer.wrapMode=TextureWrapMode.Clamp;importer.alphaIsTransparency=true;importer.isReadable=false;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteGenerateFallbackPhysicsShape=false;settings.spriteAlignment=(int)SpriteAlignment.Custom;
            settings.spritePivot=new Vector2(.5f,.25f);
            if(assetPath.Contains("/sprites/"))
            {
                int id=int.Parse(Path.GetFileName(assetPath).Substring(0,2));
                var catalog=JsonUtility.FromJson<DungeonPropCatalog>(File.ReadAllText("Assets/Coinbound/Resources/EnvironmentProps52V5/catalog.json"));
                var item=catalog.Get(id);settings.spritePivot=new Vector2(item.pivot[0]/(float)item.width,item.pivot[1]/(float)item.height);
            }
            importer.SetTextureSettings(settings);
        }
    }
}
