function Resolve-BongoCatDirectory {
    param([string]$GameDirectory)

    if ([string]::IsNullOrWhiteSpace($GameDirectory)) { $GameDirectory = $env:BONGO_CAT_DIRECTORY }
    if (-not [string]::IsNullOrWhiteSpace($GameDirectory)) {
        $resolved = (Resolve-Path -LiteralPath $GameDirectory -ErrorAction Stop).Path
        if (-not (Test-Path -LiteralPath (Join-Path $resolved 'BongoCat_Data/Managed/Assembly-CSharp.dll'))) {
            throw 'The selected folder does not contain the Windows Mono edition of Bongo Cat.'
        }
        return $resolved
    }

    $roots = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($registryPath in @('HKCU:\Software\Valve\Steam', 'HKLM:\Software\Valve\Steam', 'HKLM:\Software\WOW6432Node\Valve\Steam')) {
        $steam = Get-ItemProperty -LiteralPath $registryPath -ErrorAction SilentlyContinue
        foreach ($path in @($steam.SteamPath, $steam.InstallPath)) {
            if ($path -and (Test-Path -LiteralPath $path -PathType Container)) { $null = $roots.Add([IO.Path]::GetFullPath($path)) }
        }
    }
    $defaultSteam = Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Steam'
    if (Test-Path -LiteralPath $defaultSteam) { $null = $roots.Add($defaultSteam) }
    $libraries = [Collections.Generic.HashSet[string]]::new($roots, [StringComparer]::OrdinalIgnoreCase)
    foreach ($root in $roots) {
        $vdf = Join-Path $root 'steamapps/libraryfolders.vdf'
        if (-not (Test-Path -LiteralPath $vdf)) { continue }
        foreach ($match in [regex]::Matches([IO.File]::ReadAllText($vdf), '"(?:path|[0-9]+)"\s*"((?:\\.|[^"\\])*)"', 'IgnoreCase')) {
            $null = $libraries.Add($match.Groups[1].Value.Replace('\\', '\').Replace('\"', '"'))
        }
    }
    foreach ($library in $libraries) {
        $apps = Join-Path $library 'steamapps'
        $folder = 'BongoCat'
        $manifest = Join-Path $apps 'appmanifest_3419430.acf'
        if (Test-Path -LiteralPath $manifest) {
            $match = [regex]::Match([IO.File]::ReadAllText($manifest), '"installdir"\s*"([^"\\/]+)"', 'IgnoreCase')
            if ($match.Success -and $match.Groups[1].Value -notin @('.', '..')) { $folder = $match.Groups[1].Value }
        }
        $candidate = Join-Path $apps ('common/' + $folder)
        if (Test-Path -LiteralPath (Join-Path $candidate 'BongoCat_Data/Managed/Assembly-CSharp.dll')) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    throw 'Bongo Cat was not found. Pass -GameDirectory or set BONGO_CAT_DIRECTORY.'
}
