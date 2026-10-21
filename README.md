<div align="center">

<img src="docs/images/logo.png" width="64" height="64" alt="BongoPlus">

# BongoPlus

**Cosmetics, emotes, and chest automation for Bongo Cat.**

![Version 1.0.0](docs/images/version.svg)
![Windows x64](docs/images/windows.svg)
[![MIT license](docs/images/license.svg)](LICENSE)

<a href="docs/images/cosmetics.png"><img src="docs/images/cosmetics.png" width="500" alt="BongoPlus cosmetics catalog inside Bongo Cat"></a>

</div>

## Features

| Feature | What you get |
| --- | --- |
| **Cosmetics** | Browse the game's skin and hat catalog, search, and save favorites. Share your appearance in lobbies with players who do not have BongoPlus. |
| **Presets** | Save skin-and-hat combinations with visual previews and switch between them. |
| **Emote wheel** | Use more than eight emotes across compact pages. Drag slots to reorder them. |
| **Chest automation** | Open your ready chests and send chest gifts to selected players or everyone in your lobby. Choose cosmetic chests, emote chests, or both. |
| **Point controls** | Adjust available points within the limits shown by the mod. |

Settings are saved between launches. The mod runs inside the game; no background companion app is required.

## Screenshots

<p align="center">
  <a href="docs/images/presets.png"><img src="docs/images/presets.png" width="500" alt="Saved appearance presets with cat and hat previews"></a>
</p>
<p align="center">
  <a href="docs/images/emote-wheel.png"><img src="docs/images/emote-wheel.png" width="500" alt="BongoPlus emote wheel editor with paged slots"></a>
</p>

## Installation

Release installers are distributed through GitHub Releases.

1. Close Bongo Cat and open **BongoPlus.exe**.
2. Click **Install BongoPlus**. It finds the game across your Steam libraries; use **Browse** if needed.
3. Launch Bongo Cat through Steam and open **Mods** in the game menu.

<p align="center">
  <a href="docs/images/installer.png"><img src="docs/images/installer.png" width="640" alt="BongoPlus portable installer with automatic Steam game detection"></a>
</p>

The portable installer includes the plugin and loader. You can close it after installation.

**Requirements:** Steam's Windows x64 Mono version of Bongo Cat, Windows 10 or 11, and .NET Framework 4.7.2 or newer.

## Usage

Open **Mods** from Bongo Cat's game menu. Click outside the window or press Escape to close it. A keyboard shortcut can be assigned under **General → Open Mods shortcut**; none is assigned by default.

To update or uninstall, close the game and run the installer. **Remove mod** keeps your preferences, stored in `BepInEx/config/local.bongocat.mods.cfg`.

## Compatibility and limits

<details>
<summary><strong>Will other players see my cosmetics?</strong></summary>

Enable lobby appearance sharing to show your selected cosmetics to other players. Their game must contain the corresponding assets. Custom images are not shared, and selecting a cosmetic does not add it to Steam Inventory.

</details>

<details>
<summary><strong>Are point changes unlimited? Who pays for gifts?</strong></summary>

Available points depend on Steam's earned and spent counters. BongoPlus shows the maximum it can set; it cannot reset exhausted counters or provide unlimited points.

Gifts spend your points and use the game's normal exchange flow. A sent request does not guarantee item delivery.

</details>

<details>
<summary><strong>What happens after a game update?</strong></summary>

The mod checks feature compatibility and disables incompatible features with an explanation under **General → Compatibility details**. The cosmetic catalog is read from the installed game.

Game updates can require a BongoPlus update. Version 1.0.0 was tested with Unity Mono `6000.2.8f1`, Steam build `25936063`.

</details>

## Help and development

Found a problem? [Open an issue](https://github.com/lokalizator-repo/BongoPlus/issues) with your mod version and steps to reproduce it. Logs are in `BepInEx/LogOutput.log`; remove personal information before sharing them.

For builds and tests, see **[BUILDING.md](BUILDING.md)**.

## License

BongoPlus code is available under the **[MIT license](LICENSE)**. Bundled dependencies retain their own licenses; see **[third-party notices](THIRD_PARTY_NOTICES.md)**.

BongoPlus is an independent project and is not affiliated with the developers of Bongo Cat, Valve, or Unity.
