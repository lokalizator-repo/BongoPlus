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
$corlibs = Join-Path $package 'BepInEx/unstripped_corlib'
New-Item -ItemType Directory -Path $corlibs -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'vendor/Unity-corlibs-win32') -File -Filter '*.dll' |
    Copy-Item -Destination $corlibs -Force
$doorstop = Join-Path $package 'doorstop_config.ini'
$doorstopText = [IO.File]::ReadAllText($doorstop) -replace '(?m)^dll_search_path_override\s*=.*$', 'dll_search_path_override = BepInEx\unstripped_corlib'
[IO.File]::WriteAllText($doorstop, $doorstopText, [Text.UTF8Encoding]::new($false))
$loaderConfig = Join-Path $package 'BepInEx/config'
New-Item -ItemType Directory -Path $loaderConfig -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $loaderConfig 'BepInEx.cfg'), "[Logging]`nUnityLogListening = false`n", [Text.UTF8Encoding]::new($false))
$plugins = Join-Path $package 'BepInEx/plugins/BongoCatMods'
New-Item -ItemType Directory -Path $plugins -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'src/BongoCat.Mod/bin/Release/net472/BongoCat.Mod.dll') -Destination $plugins -Force
Compress-Archive -Path (Join-Path $package '*') -DestinationPath (Join-Path $projectRoot 'artifacts/BongoCatMods-0.1.3.zip') -Force
Write-Output "Package ready: $package"
