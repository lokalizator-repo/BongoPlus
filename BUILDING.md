# Building BongoPlus

Build on Windows with .NET SDK 9, .NET Framework 4.7.2 targeting assemblies, a local Windows Mono installation of Bongo Cat, and the tested Unity Editor 6000.5.6f1 Windows Mono profile. Game assemblies and Unity Editor files are not included in the repository.

```powershell
./scripts/Build-Installer.ps1 -GameDirectory 'E:\Steam Library\steamapps\common\BongoCat' -UnityEditorDirectory 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor'
```

The game path is resolved from the explicit parameter, `BONGO_CAT_DIRECTORY`, or Steam's registered libraries. The Unity path is resolved from the explicit parameter, `UNITY_EDITOR_DIRECTORY`, or its standard Unity Hub location. Both parameters are forwarded through the build pipeline. The tested Mono profile hash is checked before copying its libraries.

All game-dependent projects share `Directory.Build.props`. For direct MSBuild or test commands, pass `-p:GameDirectory=...` or set the environment variable:

```powershell
$env:BONGO_CAT_DIRECTORY = 'E:\Steam Library\steamapps\common\BongoCat'
dotnet run --project tests/BongoCat.Mod.Tests -c Release
dotnet build tests/BongoCat.Mod.RuntimeTests -c Release
```

Without an explicit path or environment variable, direct project builds use the standard Steam folder under Program Files (x86). Missing references produce a diagnostic explaining how to set the path. The PowerShell build scripts also discover custom Steam library locations automatically.

`scripts/Build.ps1` builds the plugin, runs its regression checks, and creates `artifacts/BongoPlus-1.0.0.zip`. `scripts/Build-Installer.ps1` embeds that ZIP in `artifacts/BongoPlus.exe` and runs installation checks against its actual embedded payload. `-SkipBuild` reuses an already prepared ZIP; use it only when the plugin and package are current.

The release contains license texts, component hashes, and UnityDoorstop's corresponding upstream source archive. First-time dependency setup downloads only the pinned official loader and source archives and verifies their SHA-256 hashes. Runtime layout checks require a separate Steam launch as described in `analysis/COMPATIBILITY.md`; the runtime test plugin is never part of a release.
