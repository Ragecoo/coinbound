$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Image generation supplies the new artwork. This step only packs it into the exact
# game pixel dimensions with nearest-neighbor sampling and a hard pixel-art alpha edge.
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;

public static class CoinboundLandmarkImport
{
    private static Rectangle OpaqueBounds(Bitmap bitmap)
    {
        int left = bitmap.Width, top = bitmap.Height, right = -1, bottom = -1;
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            if (bitmap.GetPixel(x, y).A < 128) continue;
            left = Math.Min(left, x); top = Math.Min(top, y);
            right = Math.Max(right, x); bottom = Math.Max(bottom, y);
        }
        if (right < left) throw new InvalidOperationException("Sprite has no opaque pixels.");
        return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    public static string Import(string generatedPath, string referencePath, string outputPath, int size)
    {
        using (var generated = new Bitmap(generatedPath))
        using (var reference = new Bitmap(referencePath))
        using (var output = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        {
            Rectangle source = OpaqueBounds(generated);
            Rectangle original = OpaqueBounds(reference);
            Rectangle target = Rectangle.FromLTRB(
                (int)Math.Round((double)original.Left * size / reference.Width),
                (int)Math.Round((double)original.Top * size / reference.Height),
                (int)Math.Round((double)original.Right * size / reference.Width),
                (int)Math.Round((double)original.Bottom * size / reference.Height));
            for (int y = 0; y < target.Height; y++)
            for (int x = 0; x < target.Width; x++)
            {
                int sx = source.X + Math.Min(source.Width - 1, (int)((x + 0.5) * source.Width / target.Width));
                int sy = source.Y + Math.Min(source.Height - 1, (int)((y + 0.5) * source.Height / target.Height));
                Color pixel = generated.GetPixel(sx, sy);
                if (pixel.A >= 128)
                    output.SetPixel(target.X + x, target.Y + y, Color.FromArgb(255, pixel.R, pixel.G, pixel.B));
            }
            output.Save(outputPath, ImageFormat.Png);
            return size + " x " + size + "; drawing bounds " + target;
        }
    }
}
'@

$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceDirectory = Join-Path $projectRoot 'ArtSource/Landmarks'
$referenceDirectory = Join-Path $projectRoot 'вот тут/indigo-crypt-32-v14/objects'
$assetDirectory = Join-Path $projectRoot 'Assets/Coinbound/Resources/DungeonArt'
foreach ($asset in @(@{Name='entrance';Size=144}, @{Name='fountain';Size=120})) {
    $name = $asset.Name
    $description = [CoinboundLandmarkImport]::Import(
        (Join-Path $sourceDirectory ($name + '-redraw-source.png')),
        (Join-Path $referenceDirectory ('object.' + $name + '.png')),
        (Join-Path $assetDirectory ('object.' + $name + '.png')),
        $asset.Size)
    Write-Output ($name + ': ' + $description)
}
