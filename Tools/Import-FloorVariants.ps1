$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Generated artwork supplies the stone faces. Keep the pack's exact outer border,
# central mortar seams and palette; there is no interpolation in the game.
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public static class CoinboundFloorImport
{
    private const int Size = 32;

    private static int[] Medians(Color[] pixels)
    {
        var r = new List<int>(); var g = new List<int>(); var b = new List<int>();
        for (int y = 2; y < Size - 2; y++)
        for (int x = 2; x < Size - 2; x++)
        {
            Color c = pixels[y * Size + x];
            r.Add(c.R); g.Add(c.G); b.Add(c.B);
        }
        r.Sort(); g.Sort(); b.Sort();
        return new[] { r[r.Count / 2], g[g.Count / 2], b[b.Count / 2] };
    }

    private static Color PaletteColor(Color pixel, int[] sourceMedian, int[] targetMedian, List<Color> palette)
    {
        double r = Math.Min(255, pixel.R * (double)targetMedian[0] / Math.Max(1, sourceMedian[0]));
        double g = Math.Min(255, pixel.G * (double)targetMedian[1] / Math.Max(1, sourceMedian[1]));
        double b = Math.Min(255, pixel.B * (double)targetMedian[2] / Math.Max(1, sourceMedian[2]));
        double best = double.MaxValue;
        Color selected = palette[0];
        foreach (Color candidate in palette)
        {
            double dr = r - candidate.R, dg = g - candidate.G, db = b - candidate.B;
            double distance = dr * dr + dg * dg + db * db;
            if (distance >= best) continue;
            best = distance; selected = candidate;
        }
        return selected;
    }

    public static void Import(string sourceDirectory, string assetDirectory)
    {
        using (var reference = new Bitmap(Path.Combine(assetDirectory, "floor.room.0.png")))
        {
            var original = new Color[Size * Size];
            var palette = new List<Color>();
            var seen = new HashSet<int>();
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Color color = reference.GetPixel(x, y);
                original[y * Size + x] = color;
                if (seen.Add(color.ToArgb())) palette.Add(color);
            }
            int[] targetMedian = Medians(original);
            for (int id = 0; id < 6; id++)
            using (var source = new Bitmap(Path.Combine(sourceDirectory, "floor-" + id + "-source.png")))
            using (var output = new Bitmap(Size, Size, PixelFormat.Format32bppArgb))
            {
                var sampled = new Color[Size * Size];
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    sampled[y * Size + x] = source.GetPixel((int)((x + 0.5) * source.Width / Size),
                        (int)((y + 0.5) * source.Height / Size));
                int[] sourceMedian = Medians(sampled);
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    bool border = x < 2 || x >= Size - 2 || y < 2 || y >= Size - 2;
                    // The four slabs share the original thin, irregular mortar and edge highlights.
                    // Restore only the seam cross; keep the generated wear inside each slab intact.
                    bool centerSeam = (x >= 14 && x <= 17) || (y >= 14 && y <= 17);
                    Color color = border || centerSeam ? original[y * Size + x]
                        : PaletteColor(sampled[y * Size + x], sourceMedian, targetMedian, palette);
                    output.SetPixel(x, y, color);
                }
                output.Save(Path.Combine(assetDirectory, "floor.extra." + id + ".png"), ImageFormat.Png);
            }
        }
    }

    public static void Preview(string assetDirectory, string outputPath)
    {
        var tiles = new List<Bitmap>();
        try
        {
            for (int i = 0; i < 3; i++) tiles.Add(new Bitmap(Path.Combine(assetDirectory, "floor.room." + i + ".png")));
            for (int i = 0; i < 6; i++) tiles.Add(new Bitmap(Path.Combine(assetDirectory, "floor.extra." + i + ".png")));
            const int columns = 9, rows = 7, zoom = 3;
            var random = new Random(42);
            using (var preview = new Bitmap(columns * Size * zoom, rows * Size * zoom))
            {
                for (int row = 0; row < rows; row++)
                for (int col = 0; col < columns; col++)
                {
                    int id = row == 0 ? col : random.Next(100) < 55 ? random.Next(3) : 3 + random.Next(6);
                    for (int y = 0; y < Size * zoom; y++)
                    for (int x = 0; x < Size * zoom; x++)
                        preview.SetPixel(col * Size * zoom + x, row * Size * zoom + y,
                            tiles[id].GetPixel(x / zoom, y / zoom));
                }
                preview.Save(outputPath, ImageFormat.Png);
            }
        }
        finally { foreach (Bitmap tile in tiles) tile.Dispose(); }
    }
}
'@

$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceDirectory = Join-Path $projectRoot 'ArtSource/Floors'
$assetDirectory = Join-Path $projectRoot 'Assets/Coinbound/Resources/DungeonArt'
[CoinboundFloorImport]::Import($sourceDirectory, $assetDirectory)

$template = Get-Content -LiteralPath (Join-Path $assetDirectory 'floor.room.0.png.meta') -Raw -Encoding UTF8
for ($id = 0; $id -lt 6; $id++) {
    $metaPath = Join-Path $assetDirectory ('floor.extra.' + $id + '.png.meta')
    if (Test-Path -LiteralPath $metaPath) { continue }
    $meta = $template -replace '(?m)^guid:.*$', ('guid: ' + [guid]::NewGuid().ToString('N'))
    $meta = $meta -replace '(?m)^    spriteID:.*$', ('    spriteID: ' + [guid]::NewGuid().ToString('N'))
    $meta = $meta -replace 'spriteMeshType: 1', 'spriteMeshType: 0'
    [System.IO.File]::WriteAllText($metaPath, $meta, (New-Object System.Text.UTF8Encoding($false)))
}
[CoinboundFloorImport]::Preview($assetDirectory, (Join-Path $sourceDirectory 'tiling-preview.png'))
Write-Output 'Imported six 32 x 32 floor variants with matching borders and thin internal seams; saved the tiled preview.'
