[CmdletBinding()]
param(
    [string]$GameDirectory = 'D:\Gaming\Launchers\Steam\steamapps\common\BongoCat',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') -GameDirectory $GameDirectory }
$iconPath = Join-Path $projectRoot 'src/BongoPlus.Installer/app.ico'
if (-not (Test-Path -LiteralPath $iconPath)) {
    Add-Type -AssemblyName System.Drawing
    $bitmap = [Drawing.Bitmap]::new(64, 64)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $dark = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(26, 33, 36))
    $mint = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(158, 214, 191))
    $eyes = [Drawing.Pen]::new($dark.Color, 3)
    $plus = [Drawing.Pen]::new($mint.Color, 4)
    $memory = [IO.MemoryStream]::new()
    try {
        $graphics.FillEllipse($dark, 1, 1, 62, 62)
        $points = [Drawing.PointF[]]@([Drawing.PointF]::new(10,43),[Drawing.PointF]::new(13,16),[Drawing.PointF]::new(25,24),[Drawing.PointF]::new(36,24),[Drawing.PointF]::new(47,16),[Drawing.PointF]::new(50,43))
        $graphics.FillPolygon($mint, $points)
        $graphics.DrawLine($eyes, 22, 32, 22, 36)
        $graphics.DrawLine($eyes, 37, 32, 37, 36)
        $graphics.DrawLine($plus, 49, 46, 49, 60)
        $graphics.DrawLine($plus, 42, 53, 56, 53)
        $bitmap.Save($memory, [Drawing.Imaging.ImageFormat]::Png)
        $imageBytes = $memory.ToArray()
        $writer = [IO.BinaryWriter]::new([IO.File]::Create($iconPath))
        try {
            $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
            $writer.Write([byte]64); $writer.Write([byte]64); $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$imageBytes.Length); $writer.Write([uint32]22)
            $writer.Write($imageBytes)
        } finally { $writer.Dispose() }
    } finally {
        $memory.Dispose(); $plus.Dispose(); $eyes.Dispose(); $mint.Dispose(); $dark.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
    }
}
& dotnet build (Join-Path $projectRoot 'tests/BongoPlus.Installer.Tests/BongoPlus.Installer.Tests.csproj') --framework net472 -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Installer checks could not be built.' }
& dotnet build (Join-Path $projectRoot 'src/BongoPlus.Installer/BongoPlus.Installer.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
$destination = Join-Path $projectRoot 'artifacts/BongoPlus.exe'
Copy-Item -LiteralPath (Join-Path $projectRoot 'src/BongoPlus.Installer/bin/Release/net472/BongoPlus.exe') -Destination $destination -Force
& (Join-Path $projectRoot 'tests/BongoPlus.Installer.Tests/bin/Release/net472/BongoPlus.Installer.Tests.exe') $destination
if ($LASTEXITCODE -ne 0) { throw 'Installer checks failed.' }
Write-Output "Portable installer ready: $destination"
