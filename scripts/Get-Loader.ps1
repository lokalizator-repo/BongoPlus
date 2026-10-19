[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$version = '5.4.23.5'
$name = "BepInEx_win_x64_$version.zip"
$expectedHash = '82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4'
$vendor = Join-Path $projectRoot 'vendor'
$archive = Join-Path $vendor $name
$destination = Join-Path $vendor "BepInEx-$version"
New-Item -ItemType Directory -Path $vendor -Force | Out-Null
if (-not (Test-Path -LiteralPath $archive)) {
    Invoke-WebRequest -Uri "https://github.com/BepInEx/BepInEx/releases/download/v$version/$name" -OutFile $archive
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash) {
    throw 'The official loader archive failed its SHA-256 check.'
}
if (-not (Test-Path -LiteralPath $destination)) {
    Expand-Archive -LiteralPath $archive -DestinationPath $destination
}
Write-Output "Verified BepInEx $version in $destination"
