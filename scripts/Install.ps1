[CmdletBinding()]
param([string]$GameDirectory = 'D:\Gaming\Launchers\Steam\steamapps\common\BongoCat')

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
$assembly = Join-Path $game 'BongoCat_Data/Managed/Assembly-CSharp.dll'
if ((Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash.ToLowerInvariant() -ne '96bbcbb2733988fd31ce261891eb3027888389e03b6a23dbd63e9e7f518adfb8') {
    throw 'The installed game build differs from the analyzed build.'
}
if (Get-Process -Name BongoCat -ErrorAction SilentlyContinue) {
    throw 'Close Bongo Cat before installing or updating the plugin.'
}
$package = Join-Path $projectRoot 'artifacts/BongoCatMods'
if (-not (Test-Path -LiteralPath (Join-Path $package 'BepInEx/plugins/BongoCatMods/BongoCat.Mod.dll'))) {
    throw 'Run scripts/Build.ps1 first.'
}
$marker = Join-Path $game 'BepInEx/config/local.bongocat.mods.install.json'
if ((Test-Path -LiteralPath (Join-Path $game 'winhttp.dll')) -and -not (Test-Path -LiteralPath $marker)) {
    throw 'An existing loader is installed. Review it before replacing any loader files.'
}
$records = [System.Collections.Generic.List[object]]::new()
foreach ($file in Get-ChildItem -LiteralPath $package -File -Recurse) {
    $relative = $file.FullName.Substring($package.Length + 1)
    $target = [IO.Path]::GetFullPath((Join-Path $game $relative))
    if ($relative.Replace('\', '/') -eq 'BepInEx/config/BepInEx.cfg' -and (Test-Path -LiteralPath $target)) { continue }
    if (-not $target.StartsWith($game + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Package file escapes the game directory.'
    }
    $parent = Split-Path $target -Parent
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target -Force
    $records.Add([pscustomobject]@{ path = $relative; sha256 = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() })
}
$loaderConfig = Join-Path $game 'BepInEx/config/BepInEx.cfg'
$loaderConfigText = [IO.File]::ReadAllText($loaderConfig)
if ($loaderConfigText -match '(?m)^UnityLogListening\s*=') {
    $loaderConfigText = $loaderConfigText -replace '(?m)^UnityLogListening\s*=.*$', 'UnityLogListening = false'
} elseif ($loaderConfigText -match '(?m)^\[Logging\]\s*$') {
    $loaderConfigText = $loaderConfigText -replace '(?m)^\[Logging\]\s*$', "[Logging]`nUnityLogListening = false"
} else {
    $loaderConfigText += "`n[Logging]`nUnityLogListening = false`n"
}
[IO.File]::WriteAllText($loaderConfig, $loaderConfigText, [Text.UTF8Encoding]::new($false))
New-Item -ItemType Directory -Path (Split-Path $marker -Parent) -Force | Out-Null
[pscustomobject]@{ loader = 'BepInEx 5.4.23.5'; plugin = '0.1.13'; installedUtc = [DateTime]::UtcNow.ToString('o'); files = $records } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $marker -Encoding utf8
Write-Output 'Installed. Launch Bongo Cat normally through Steam, then press F9.'
