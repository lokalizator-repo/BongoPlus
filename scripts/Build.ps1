[CmdletBinding()]
param([string]$GameDirectory, [string]$UnityEditorDirectory)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'Resolve-GameDirectory.ps1')
$GameDirectory = Resolve-BongoCatDirectory $GameDirectory
& (Join-Path $PSScriptRoot 'Get-Loader.ps1') -UnityEditorDirectory $UnityEditorDirectory
& dotnet build (Join-Path $projectRoot 'src/BongoCat.Mod/BongoCat.Mod.csproj') -c Release "-p:GameDirectory=$GameDirectory" -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
& dotnet run --project (Join-Path $projectRoot 'tests/BongoCat.Mod.Tests/BongoCat.Mod.Tests.csproj') -c Release "-p:GameDirectory=$GameDirectory" -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Regression checks failed.' }
$package = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts/BongoPlus-package'))
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
if (-not $package.StartsWith($artifactsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid package output path.' }
if (Test-Path -LiteralPath $package) { Remove-Item -LiteralPath $package -Recurse -Force }
New-Item -ItemType Directory -Path $package -Force | Out-Null
$loader = Join-Path $projectRoot 'vendor/BepInEx-5.4.23.5'
foreach ($name in @('winhttp.dll', 'doorstop_config.ini')) {
    Copy-Item -LiteralPath (Join-Path $loader $name) -Destination $package -Recurse -Force
}
$core = Join-Path $package 'BepInEx/core'
New-Item -ItemType Directory -Path $core -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $loader 'BepInEx/core') -File |
    Where-Object { $_.Extension -in @('.dll', '.xml') } | Copy-Item -Destination $core -Force
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
$legal = Join-Path $plugins 'legal'
New-Item -ItemType Directory -Path (Join-Path $legal 'sources') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination (Join-Path $legal 'LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD_PARTY_NOTICES.md') -Destination $legal -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'licenses') -Destination $legal -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'vendor/UnityDoorstop-source/UnityDoorstop-4.5.0-source.zip') -Destination (Join-Path $legal 'sources') -Force
$components = foreach ($file in Get-ChildItem -LiteralPath $package -File -Recurse -Filter '*.dll') {
    if ($file.FullName -eq (Join-Path $plugins 'BongoCat.Mod.dll')) { continue }
    $assemblyVersion = $null
    if ($file.Name -ne 'winhttp.dll') { $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($file.FullName).Version.ToString() }
    [pscustomobject]@{
        path = $file.FullName.Substring($package.Length + 1).Replace('\', '/')
        assemblyVersion = $assemblyVersion
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
[pscustomobject]@{ bepinex = '5.4.23.5'; monoProfile = 'Unity Editor 6000.5.6f1 / unityjit-win32'; components = @($components) } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $legal 'THIRD_PARTY_MANIFEST.json') -Encoding utf8
Compress-Archive -Path (Join-Path $package '*') -DestinationPath (Join-Path $projectRoot 'artifacts/BongoPlus-1.0.0.zip') -Force
Write-Output "Package ready: $package"
