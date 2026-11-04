param([string]$Project = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
# Copy the hand-sliced source PNGs byte-for-byte; no JPEG reconstruction or resampling.
$icons = @{ health='icon_1_2'; mana='icon_1_1'; damage='icon_1_5'; movement='icon_3_1'; 'attack-rate'='icon_1_7'; critical='icon_4_7'; luck='icon_6_2'; block='icon_2_3'; bow='icon_2_4'; roll='icon_5_2' }
$source = Join-Path $Project 'ура'
$dest = Join-Path $Project 'Assets/Coinbound/Resources/InterfaceArt'
New-Item -ItemType Directory -Force -Path (Join-Path $dest 'Stats'),(Join-Path $dest 'Gui') | Out-Null
foreach ($name in $icons.Keys) {
    Copy-Item -LiteralPath (Join-Path $source ('icons/' + $icons[$name] + '.png')) -Destination (Join-Path $dest ('Stats/' + $name + '.png'))
}
foreach ($name in @('panel','panel_outline','button','button_hover','button_pressed','button_disabled','divider')) {
    Copy-Item -LiteralPath (Join-Path $source ('gui/' + $name + '.png')) -Destination (Join-Path $dest ('Gui/' + $name + '.png'))
}
New-Item -ItemType Directory -Force -Path (Join-Path $dest 'Hud') | Out-Null
Get-ChildItem -LiteralPath (Join-Path $source 'hud') -Filter '*.png' | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $dest 'Hud')
}
