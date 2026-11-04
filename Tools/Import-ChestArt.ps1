param([string]$Project = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$source = Join-Path $Project 'ура/chest'
$destination = Join-Path $Project 'Assets/Coinbound/Resources/ChestArt'
foreach ($facing in @('south', 'west', 'north', 'east')) {
    $target = Join-Path $destination $facing
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    for ($frame = 0; $frame -le 8; $frame++) {
        $name = 'chest_{0:00}.png' -f $frame
        Copy-Item -LiteralPath (Join-Path (Join-Path $source $facing) $name) -Destination (Join-Path $target $name)
    }
}
