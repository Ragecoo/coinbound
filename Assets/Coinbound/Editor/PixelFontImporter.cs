using UnityEditor;
using UnityEngine;

namespace Coinbound.Editor
{
    public sealed class PixelFontImporter : AssetPostprocessor
    {
        private void OnPreprocessAsset()
        {
            if (!assetPath.Contains("/InterfaceFonts/") || !(assetPath.EndsWith(".ttf") || assetPath.EndsWith(".otf"))) return;
            if (assetImporter is TrueTypeFontImporter font)
                font.fontRenderingMode = FontRenderingMode.HintedRaster;
        }
    }
}
