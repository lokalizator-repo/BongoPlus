[CmdletBinding()]
param([string]$UnityEditorDirectory = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor')

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

$profile = Join-Path $UnityEditorDirectory 'Data/MonoBleedingEdge/lib/mono/unityjit-win32'
$corlibDestination = Join-Path $vendor 'Unity-corlibs-win32'
$corlibHash = 'f7698bd46af58d3ee9dac59a286cfedfd28e51f71c0fe3940671a3ccc7af5711'
if (-not (Test-Path -LiteralPath (Join-Path $profile 'mscorlib.dll'))) {
    throw 'The tested Windows Mono profile is missing. Pass the Unity 6000.5.6f1 Editor directory.'
}
if ((Get-FileHash -LiteralPath (Join-Path $profile 'mscorlib.dll') -Algorithm SHA256).Hash.ToLowerInvariant() -ne $corlibHash) {
    throw 'The Windows Mono profile differs from the tested profile.'
}
New-Item -ItemType Directory -Path $corlibDestination -Force | Out-Null
$coreNames = @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Configuration.dll',
    'System.Data.dll', 'System.Net.Http.dll', 'System.Numerics.dll', 'System.Runtime.Serialization.dll',
    'System.Security.dll', 'System.Xml.dll', 'System.Xml.Linq.dll', 'Mono.Security.dll', 'Microsoft.CSharp.dll')
foreach ($coreName in $coreNames) {
    Copy-Item -LiteralPath (Join-Path $profile $coreName) -Destination $corlibDestination -Force
}
Copy-Item -LiteralPath (Join-Path $profile 'Facades/netstandard.dll') -Destination $corlibDestination -Force
Write-Output 'Verified Windows Mono core libraries from the installed Unity 6000.5.6f1 Editor.'
