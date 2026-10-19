# Cosmetic catalog audit

Checked on 2026-10-20 against installed Steam build 25936063.

The installed catalog contains 242 skin definitions. All 242 pass the native inventory population filter and the mod's cosmetic list filter. Each has a cached icon and all four animation sprites (`Left`, `LeftPunch`, `Right`, `RightPunch`). No skin definitions were excluded.

The audit resolved all 990 `SteamItemIdReference.AllItemData` pointers to exactly the 990 parsed `SteamItemBasic` assets, with no missing assets or duplicate item IDs. Asset hashes and per-item results are in [cosmetic-catalog-audit.json](cosmetic-catalog-audit.json).

The native list requires `!Hidden`, `!GameOnly`, and a nonempty item slot. The mod additionally requires a matching slot and an icon; an active search narrows results by name. Neither list population nor the mod's list filter requires ownership. All 21 skin definitions with `PriceCategory`, all 50 with `HideUntilReceived`, and all nine with an `owns:` promotion are included.

## DLC comparison

The [official Steam DLC catalog](https://store.steampowered.com/dlc/3419430/Bongo_Cat/?l=english) lists five DLC packs. Their 15 advertised skins all have matching local definitions and pass the mod filter.

| DLC | Local skin names and item IDs | Included |
| --- | --- | --- |
| [God Pack](https://store.steampowered.com/app/3929900/?l=english) | Hades (450), Zeus (451), Poseidon (452) | 3/3 |
| [Mecha Pack](https://store.steampowered.com/app/4600310/?l=english) | Attack Mecha S.L.A.S.H. (786), Heal Mecha P.U.R.R. (787), Tank Mecha B.O.N.K. (789) | 3/3 |
| [Squirrel Pack](https://store.steampowered.com/app/4600320/?l=english) | Eurasian Red Squirrel (585), Eastern Grey Squirrel (586), Calabrian Black Squirrel (587) | 3/3 |
| [Wrestler Pack](https://store.steampowered.com/app/4600330/?l=english) | Wrestler Cutie Punch (928), Wrestler Ribbit Rampage (929), Wrestler Six Seven Lives (930) | 3/3 |
| [Wild Wonders](https://store.steampowered.com/app/4856340/?l=english) | Bumble Kitten (981), Meowxolotl (982), Sea Purrtle (983) | 3/3 |

## Scope and reproduction

This is a static audit of the installed asset catalog and the current list-building code. It does not claim that every skin was clicked in a running game, that ownership was granted in Steam, or that future builds will remain compatible. Files absent from this installed build cannot be listed by the mod.

Run from the project root:

```powershell
python analysis/tools/audit_cosmetic_assets.py 'D:\Gaming\Launchers\Steam\steamapps\common\BongoCat\BongoCat_Data' analysis/cosmetic-catalog-audit.json
```

The parser targets this build's Unity serialized format and fails on unexpected layouts rather than silently accepting them. No plugin or game files were changed by this audit.
