param(
    [ValidateSet('all', '01', '02')][string]$Hero = 'all',
    [ValidateSet('all', 'down', 'left', 'right', 'up')][string]$Direction = 'all',
    [ValidateRange(0.75, 1.0)][double]$Scale01 = 1.0,
    [ValidateRange(0.75, 1.0)][double]$Scale02 = 1.0,
    [ValidateRange(1, 6)][int]$PreviewScale = 3,
    [switch]$NoGif
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Authored imagegen frames only: this tool samples, palette-matches, anchors and packs
# the existing art. Every frame keeps the same scale; rolled poses are never enlarged.
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

public static class CoinboundHeroAnimationImport
{
    private const int Width = 32, Height = 64, Columns = 8, Rows = 5;
    private static readonly string[] Directions = { "down", "left", "right", "up" };
    private static readonly string[] States = { "Idle", "Run", "Attack", "Roll", "Block" };
    private static readonly Color BackgroundA = Color.FromArgb(255, 20, 20, 27);
    private static readonly Color BackgroundB = Color.FromArgb(255, 27, 27, 36);

    private static List<Color> ReadPalette(string referenceDirectory)
    {
        var colors = new List<Color>();
        var seen = new HashSet<int>();
        foreach (string direction in Directions)
        using (var original = new Bitmap(Path.Combine(referenceDirectory, direction + ".png")))
        for (int y = 0; y < original.Height; y++)
        for (int x = 0; x < original.Width; x++)
        {
            Color c = original.GetPixel(x, y);
            if (c.A < 128) continue;
            c = Color.FromArgb(255, c.R, c.G, c.B);
            if (seen.Add(c.ToArgb())) colors.Add(c);
        }
        if (colors.Count == 0) throw new InvalidOperationException("Original hero palette is empty.");
        return colors;
    }

    private static Color Match(Color color, List<Color> palette, Dictionary<int, Color> memo)
    {
        if (color.A < 128) return Color.Transparent;
        int key = color.ToArgb();
        Color result;
        if (memo.TryGetValue(key, out result)) return result;
        int best = int.MaxValue;
        result = palette[0];
        foreach (Color candidate in palette)
        {
            int r = color.R - candidate.R, g = color.G - candidate.G, b = color.B - candidate.B;
            int difference = 2 * r * r + 4 * g * g + b * b;
            if (difference >= best) continue;
            best = difference;
            result = candidate;
        }
        memo.Add(key, result);
        return result;
    }

    private static int FootBottom(Color[] frame)
    {
        // Ignore the outer cape/bow/sword silhouette when locating the ground contact.
        // At least two central pixels exclude a detached tip or isolated alpha noise.
        for (int y = Height - 1; y >= 0; y--)
        {
            int count = 0;
            for (int x = 4; x < 28; x++) if (frame[y * Width + x].A >= 128) count++;
            if (count >= 2) return y;
        }
        for (int y = Height - 1; y >= 0; y--)
        for (int x = 0; x < Width; x++) if (frame[y * Width + x].A >= 128) return y;
        throw new InvalidOperationException("A sampled animation cell is empty.");
    }

    private sealed class SourceCell
    {
        public readonly int Width, Height;
        public readonly Color[] Pixels;
        public readonly int[] Component;
        public readonly int BodyComponent;

        public SourceCell(Bitmap source, int row, int column)
        {
            int left = column * source.Width / Columns, top = row * source.Height / Rows;
            Width = (column + 1) * source.Width / Columns - left;
            Height = (row + 1) * source.Height / Rows - top;
            Pixels = new Color[Width * Height];
            Component = new int[Pixels.Length];
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++) Pixels[y * Width + x] = source.GetPixel(left + x, top + y);

            int[] queue = new int[Pixels.Length];
            int nextId = 0, largest = 0;
            for (int seed = 0; seed < Pixels.Length; seed++)
            {
                if (Pixels[seed].A < 128 || Component[seed] != 0) continue;
                int id = ++nextId, head = 0, tail = 0;
                queue[tail++] = seed;
                Component[seed] = id;
                while (head < tail)
                {
                    int position = queue[head++], x = position % Width, y = position / Width;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= Width || ny < 0 || ny >= Height) continue;
                        int neighbor = ny * Width + nx;
                        if (Component[neighbor] != 0 || Pixels[neighbor].A < 128) continue;
                        Component[neighbor] = id;
                        queue[tail++] = neighbor;
                    }
                }
                if (tail > largest) { largest = tail; BodyComponent = id; }
            }
            if (largest == 0) throw new InvalidOperationException("Source animation cell is empty.");
        }

        public Color Sample(double x, double y)
        {
            int sx = Math.Min(Width - 1, (int)(x * Width / CoinboundHeroAnimationImport.Width));
            int sy = Math.Min(Height - 1, (int)(y * Height / CoinboundHeroAnimationImport.Height));
            int index = sy * Width + sx;
            return Component[index] == BodyComponent ? Pixels[index] : Color.Transparent;
        }
    }

    public static string Import(string sourcePath, string referenceDirectory, string outputPath,
        string previewStem, double scale, int zoom, bool makeGif)
    {
        List<Color> palette = ReadPalette(referenceDirectory);
        var memo = new Dictionary<int, Color>();
        var frames = new Color[Columns * Rows][];
        var sourceBottom = new int[frames.Length];
        var sourceOpaque = new int[frames.Length];
        var report = new StringBuilder();
        using (var source = new Bitmap(sourcePath))
        {
            // Proportional boundaries accommodate imagegen sizes such as 1122 x 1402.
            // All sampling is bounded to its cell, so neighboring poses never bleed in.
            for (int row = 0; row < Rows; row++)
            for (int column = 0; column < Columns; column++)
            {
                int index = row * Columns + column;
                // Some authored weapon tips cross a row boundary. A bounded high-resolution
                // component keeps the hero intact and rejects tips belonging to the next row.
                var cell = new SourceCell(source, row, column);
                frames[index] = new Color[Width * Height];
                for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    double localX = (x + 0.5 - Width * 0.5) / scale + Width * 0.5;
                    double localY = (y + 0.5 - 56) / scale + 56;
                    if (localX < 0 || localY < 0 || localX >= Width || localY >= Height) continue;
                    Color sampled = cell.Sample(localX, localY);
                    if (sampled.A >= 128) sourceOpaque[index]++;
                    frames[index][y * Width + x] = Match(sampled, palette, memo);
                }
                sourceBottom[index] = FootBottom(frames[index]);
            }

            int[] runBottoms = new int[Columns];
            Array.Copy(sourceBottom, Columns, runBottoms, 0, Columns);
            Array.Sort(runBottoms);
            int runGround = runBottoms[Columns / 2];

            report.Append("{\n  \"source_size\": [").Append(source.Width).Append(", ").Append(source.Height)
                .Append("],\n  \"sheet_size\": [256, 320],\n  \"palette_colors\": ").Append(palette.Count)
                .Append(",\n  \"scale\": ").Append(scale.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Append(",\n  \"ground_line_from_top\": 56,\n  \"frames\": [\n");

            using (var output = new Bitmap(Width * Columns, Height * Rows, PixelFormat.Format32bppArgb))
            {
                for (int row = 0; row < Rows; row++)
                for (int column = 0; column < Columns; column++)
                {
                    int index = row * Columns + column;
                    // Keep at most one authored pixel of running lift; never align by cape bounds.
                    int lift = row == 1 && sourceBottom[index] < runGround ? 1 : 0;
                    int shift = 55 - lift - sourceBottom[index];
                    Color[] anchored = new Color[Width * Height];
                    int clipped = 0, edge = 0, top = Height, bottom = -1;
                    for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                    {
                        Color color = frames[index][y * Width + x];
                        if (color.A < 128) continue;
                        int destinationY = y + shift;
                        if (destinationY < 0 || destinationY >= Height) { clipped++; continue; }
                        if (x == 0 || x == Width - 1) edge++;
                        top = Math.Min(top, destinationY); bottom = Math.Max(bottom, destinationY);
                        anchored[destinationY * Width + x] = color;
                        output.SetPixel(column * Width + x, row * Height + destinationY, color);
                    }
                    frames[index] = anchored;
                    if (index > 0) report.Append(",\n");
                    report.Append("    {\"state\": \"").Append(States[row]).Append("\", \"frame\": ").Append(column)
                        .Append(", \"sampled_opaque\": ").Append(sourceOpaque[index])
                        .Append(", \"source_foot_y\": ").Append(sourceBottom[index])
                        .Append(", \"shift_y\": ").Append(shift)
                        .Append(", \"run_lift\": ").Append(lift)
                        .Append(", \"top_y\": ").Append(top).Append(", \"bottom_y\": ").Append(bottom)
                        .Append(", \"shift_clipped_pixels\": ").Append(clipped)
                        .Append(", \"side_edge_pixels\": ").Append(edge).Append("}");
                }
                output.Save(outputPath, ImageFormat.Png);
                SaveContact(output, previewStem + "-contact.png", zoom);
            }
        }
        if (makeGif) SaveGif(frames, palette, previewStem + ".gif", zoom);
        return report.Append("\n  ]\n}\n").ToString();
    }

    private static Color PreviewPixel(Color color, int x, int y)
    {
        return color.A >= 128 ? color : ((x / 8 + y / 8) & 1) == 0 ? BackgroundA : BackgroundB;
    }

    private static void SaveContact(Bitmap sheet, string path, int zoom)
    {
        using (var preview = new Bitmap(sheet.Width * zoom, sheet.Height * zoom, PixelFormat.Format32bppArgb))
        {
            for (int y = 0; y < preview.Height; y++)
            for (int x = 0; x < preview.Width; x++)
                preview.SetPixel(x, y, PreviewPixel(sheet.GetPixel(x / zoom, y / zoom), x / zoom, y / zoom));
            preview.Save(path, ImageFormat.Png);
        }
    }

    private static void SaveGif(Color[][] frames, List<Color> heroPalette, string path, int zoom)
    {
        // A slow review GIF shows all five states side by side; runtime timings remain in C#.
        var palette = new List<Color> { BackgroundA, BackgroundB };
        foreach (Color color in heroPalette) if (!palette.Contains(color)) palette.Add(color);
        if (palette.Count > 256) throw new InvalidOperationException("Preview GIF palette exceeds 256 colors.");
        var index = new Dictionary<int, byte>();
        for (int i = 0; i < palette.Count; i++) index[palette[i].ToArgb()] = (byte)i;
        int width = Width * Rows * zoom, height = Height * zoom;
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(Encoding.ASCII.GetBytes("GIF89a"));
            writer.Write((ushort)width); writer.Write((ushort)height);
            writer.Write((byte)0xF7); writer.Write((byte)0); writer.Write((byte)0);
            for (int i = 0; i < 256; i++)
            {
                Color color = i < palette.Count ? palette[i] : Color.Black;
                writer.Write(color.R); writer.Write(color.G); writer.Write(color.B);
            }
            writer.Write(new byte[] { 0x21, 0xFF, 0x0B });
            writer.Write(Encoding.ASCII.GetBytes("NETSCAPE2.0"));
            writer.Write(new byte[] { 3, 1, 0, 0, 0 });
            for (int frame = 0; frame < Columns; frame++)
            {
                writer.Write(new byte[] { 0x21, 0xF9, 4, 0, 12, 0, 0, 0 });
                writer.Write((byte)0x2C); writer.Write((ushort)0); writer.Write((ushort)0);
                writer.Write((ushort)width); writer.Write((ushort)height); writer.Write((byte)0);
                writer.Write((byte)8);
                byte[] pixels = new byte[width * height];
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int px = x / zoom, py = y / zoom, state = px / Width;
                    Color color = PreviewPixel(frames[state * Columns + frame][py * Width + px % Width], px, py);
                    pixels[y * width + x] = index[color.ToArgb()];
                }
                byte[] compressed = LiteralGifCodes(pixels);
                for (int offset = 0; offset < compressed.Length; offset += 255)
                {
                    int count = Math.Min(255, compressed.Length - offset);
                    writer.Write((byte)count); writer.Write(compressed, offset, count);
                }
                writer.Write((byte)0);
            }
            writer.Write((byte)0x3B);
        }
    }

    private static byte[] LiteralGifCodes(byte[] pixels)
    {
        // Frequent clear codes keep every LZW code nine bits; this needs no external encoder.
        // Preview size is small, and exact original colors matter more than GIF compression.
        var bytes = new List<byte>();
        int bits = 0, buffer = 0;
        Action<int> writeCode = code =>
        {
            buffer |= code << bits;
            bits += 9;
            while (bits >= 8) { bytes.Add((byte)(buffer & 255)); buffer >>= 8; bits -= 8; }
        };
        writeCode(256);
        for (int i = 0; i < pixels.Length; i++)
        {
            if (i > 0 && i % 250 == 0) writeCode(256);
            writeCode(pixels[i]);
        }
        writeCode(257);
        if (bits > 0) bytes.Add((byte)(buffer & 255));
        return bytes.ToArray();
    }
}
'@

$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $projectRoot 'ArtSource/Heroes'
$assetRoot = Join-Path $projectRoot 'Assets/Coinbound/Resources/HeroArt'
# Build the source directory name without a locale-sensitive script encoding.
$referenceRoot = Join-Path $projectRoot (-join @([char]0x0442, [char]0x0443, [char]0x0442, [char]0x0430))
$previewRoot = Join-Path $sourceRoot 'Previews'
$heroes = if ($Hero -eq 'all') { @('01', '02') } else { @($Hero) }
$directions = if ($Direction -eq 'all') { @('down', 'left', 'right', 'up') } else { @($Direction) }

foreach ($id in $heroes) {
    foreach ($facing in $directions) {
        $sourcePath = Join-Path (Join-Path $sourceRoot $id) ($facing + '-source.png')
        if (-not (Test-Path -LiteralPath $sourcePath)) { throw ('Missing generated source: ' + $sourcePath) }
    }
}
[System.IO.Directory]::CreateDirectory($previewRoot) | Out-Null

foreach ($id in $heroes) {
    $assetDirectory = Join-Path $assetRoot $id
    [System.IO.Directory]::CreateDirectory($assetDirectory) | Out-Null
    $scale = if ($id -eq '01') { $Scale01 } else { $Scale02 }
    foreach ($facing in $directions) {
        $sourcePath = Join-Path (Join-Path $sourceRoot $id) ($facing + '-source.png')
        $outputPath = Join-Path $assetDirectory ($facing + '.png')
        $previewStem = Join-Path $previewRoot ($id + '-' + $facing)
        $metrics = [CoinboundHeroAnimationImport]::Import($sourcePath,
            (Join-Path $referenceRoot $id), $outputPath, $previewStem, $scale, $PreviewScale, (-not $NoGif))
        [System.IO.File]::WriteAllText(($previewStem + '-metrics.json'), $metrics, (New-Object System.Text.UTF8Encoding($false)))

        $metaPath = $outputPath + '.meta'
        $guid = [guid]::NewGuid().ToString('N')
        if (Test-Path -LiteralPath $metaPath) {
            $existingMeta = Get-Content -LiteralPath $metaPath -Raw
            if ($existingMeta -match '(?m)^guid:\s*([a-f0-9]+)') { $guid = $matches[1] }
        }
        $meta = @"
fileFormatVersion: 2
guid: $guid
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 1
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  spriteMode: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 0
  textureShape: 1
  maxTextureSize: 2048
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 100
    crunchedCompression: 0
    overridden: 0
  userData: 8 columns x 5 rows; Idle Run Attack Roll Block; frame32x64; footline56; 32PPU
  assetBundleName:
  assetBundleVariant:
"@
        # Unity's YAML parser expects the final empty mapping value to end with a newline.
        [System.IO.File]::WriteAllText($metaPath, ($meta.TrimEnd() + [Environment]::NewLine), (New-Object System.Text.UTF8Encoding($false)))
        $summary = $metrics | ConvertFrom-Json
        $opaque = ($summary.frames | Measure-Object -Property sampled_opaque -Minimum -Maximum)
        $clipped = ($summary.frames | Measure-Object -Property shift_clipped_pixels -Sum).Sum
        Write-Output ($id + '/' + $facing + ': 256x320; palette=' + $summary.palette_colors +
            '; opaque/cell=' + $opaque.Minimum + '..' + $opaque.Maximum + '; shifted-clipped=' + $clipped)
    }
}
Write-Output ('Saved native sheets, contact sheets, frame metrics' + $(if ($NoGif) { '.' } else { ' and eight-frame review GIFs.' }))
