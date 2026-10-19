[CmdletBinding()]
param([string]$GameDirectory = 'D:\Gaming\Launchers\Steam\steamapps\common\BongoCat')

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Get-Loader.ps1')
& dotnet build (Join-Path $projectRoot 'src/BongoCat.Mod/BongoCat.Mod.csproj') -c Release "-p:GameDirectory=$GameDirectory" -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
& dotnet run --project (Join-Path $projectRoot 'tests/BongoCat.Mod.Tests/BongoCat.Mod.Tests.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Regression checks failed.' }
$package = Join-Path $projectRoot 'artifacts/BongoCatMods'
New-Item -ItemType Directory -Path $package -Force | Out-Null
$loader = Join-Path $projectRoot 'vendor/BepInEx-5.4.23.5'
foreach ($name in @('winhttp.dll', 'doorstop_config.ini', 'BepInEx')) {
    Copy-Item -LiteralPath (Join-Path $loader $name) -Destination $package -Recurse -Force
}
$plugins = Join-Path $package 'BepInEx/plugins/BongoCatMods'
New-Item -ItemType Directory -Path $plugins -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'src/BongoCat.Mod/bin/Release/net472/BongoCat.Mod.dll') -Destination $plugins -Force
Compress-Archive -Path (Join-Path $package '*') -DestinationPath (Join-Path $projectRoot 'artifacts/BongoCatMods-0.1.0.zip') -Force
Write-Output "Package ready: $package"
