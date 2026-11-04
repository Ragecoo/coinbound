param([string]$Project = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assets = @(
    @{ Name = 'menu-crypt'; Width = 320; Height = 180 },
    @{ Name = 'coinbound-logo'; Width = 160; Height = 120 },
    @{ Name = 'swordsman-portrait'; Width = 72; Height = 96 },
    @{ Name = 'archer-portrait'; Width = 72; Height = 96 }
)
foreach ($asset in $assets) {
    $sourcePath = Join-Path $Project ('ArtSource/Interface/' + $asset.Name + '-source.png')
    $targetPath = Join-Path $Project ('Assets/Coinbound/Resources/InterfaceArt/' + $asset.Name + '.png')
    $source = [System.Drawing.Image]::FromFile($sourcePath)
    $pixels = [System.Drawing.Bitmap]::new($asset.Width, $asset.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($pixels)
    try {
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $graphics.DrawImage($source, [System.Drawing.Rectangle]::new(0, 0, $asset.Width, $asset.Height), 0, 0, $source.Width, $source.Height, [System.Drawing.GraphicsUnit]::Pixel)
        $pixels.Save($targetPath, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $pixels.Dispose(); $source.Dispose() }
    Write-Output ($asset.Name + ': ' + $asset.Width + 'x' + $asset.Height)
}
