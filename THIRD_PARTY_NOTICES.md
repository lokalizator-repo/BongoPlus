# Third-party components

BongoPlus source code is licensed under the MIT license in `LICENSE`. That license does not replace the licenses of the components listed below, or grant rights to Bongo Cat, Steam, Unity assets, or system fonts.

The portable executable embeds the same package as the manual installation ZIP. Notices and license texts are installed in `BepInEx/plugins/BongoCatMods/legal/`. Third-party DLLs are distributed without binary modifications. BongoPlus changes the loader configuration to select the Windows Mono class-library profile and disable Unity log interception.

## Redistributed components

| Component | Bundled files | Version / source | License text |
| --- | --- | --- | --- |
| BepInEx | `BepInEx.dll`, `BepInEx.Preloader.dll` and Harmony integration DLLs from the official distribution | [5.4.23.5](https://github.com/BepInEx/BepInEx/tree/v5.4.23.5) | [MIT](licenses/BepInEx-MIT.txt) |
| UnityDoorstop | `winhttp.dll` | [4.5.0, commit 33dab9a6733862eb81869ff08431d9478b28784b](https://github.com/NeighTools/UnityDoorstop/tree/33dab9a6733862eb81869ff08431d9478b28784b) | [LGPL 2.1](licenses/UnityDoorstop-LGPL-2.1.txt) |
| HarmonyX | `0Harmony.dll` | [2.9.0](https://github.com/BepInEx/HarmonyX/tree/v2.9.0), as identified by the bundled assembly | [MIT](licenses/HarmonyX-MIT.txt) |
| MonoMod | `MonoMod.RuntimeDetour.dll`, `MonoMod.Utils.dll` | [22.01.29.01](https://github.com/MonoMod/MonoMod/tree/v22.01.29.01) | [MIT](licenses/MonoMod-MIT.txt) |
| Mono.Cecil | `Mono.Cecil.dll`, `Mono.Cecil.Mdb.dll`, `Mono.Cecil.Pdb.dll`, `Mono.Cecil.Rocks.dll` | [0.10.4](https://github.com/jbevain/cecil/tree/0.10.4) | [MIT](licenses/Mono.Cecil-MIT.txt) |
| Mono class libraries | The 14 DLLs in `BepInEx/unstripped_corlib/` | Windows `unityjit-win32` profile from Unity Editor 6000.5.6f1 | [Mono license collection](licenses/Mono-LICENSE.txt), [third-party notices](licenses/Mono-Third-Party-Notices.txt), [Microsoft patent promise](licenses/Mono-Microsoft-Patent-Promise.txt) |

`BepInEx.Harmony.dll`, `0Harmony20.dll`, and `HarmonyXInterop.dll` are compatibility components supplied by the official BepInEx archive. The exact distributed DLL hashes and assembly versions are recorded in `THIRD_PARTY_MANIFEST.json`; this inventory is regenerated during packaging. In particular, the bundled HarmonyX and MonoMod assembly versions differ from the older dependency versions stated in the BepInEx tag's README.

## UnityDoorstop source

The package includes the complete upstream source archive at `sources/UnityDoorstop-4.5.0-source.zip` inside the legal directory, including its license and build scripts. It corresponds to the revision linked above. Its SHA-256 is `a71789ffcb359f6a37c6d2c7bbdd0527231e9db00bf46637693cff88ba7ffb80`.

The source archive is downloaded and verified by `scripts/Get-Loader.ps1`. UnityDoorstop is a separate replaceable DLL loaded by the game. Users may replace it with a compatible build. The original project includes Windows build instructions and scripts; BongoPlus does not modify its source.

## Mono notices

The Mono notices are preserved from the [Unity Mono repository at revision 11e940db4e086fc20c1526dc538d5782d81f8c5f](https://github.com/Unity-Technologies/mono/tree/11e940db4e086fc20c1526dc538d5782d81f8c5f). This revision identifies the notice snapshot, not the unverified source revision of the installed Editor's class-library binaries. Binary provenance is the local Editor profile and the hashes in the generated inventory. The aggregate notice files also describe components not included in BongoPlus. No Mono runtime, C# compiler, or Unity engine DLL is redistributed in this package.

## Referenced, not redistributed

Bongo Cat managed assemblies, Heathen Steamworks, Steamworks.NET, Newtonsoft.Json, TextMeshPro, and Unity UI/engine assemblies are read from the user's installed game for compilation or execution. They are not copied into the release. Game images and UI assets are accessed from the running game. Segoe UI is resolved through Windows and is not included as a font file. The installer uses the .NET Framework already installed on Windows.
