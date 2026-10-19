using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BongoCatMod;

internal static class ModFonts
{
    private static TMP_FontAsset? _fallback;
    private static readonly HashSet<int> Prepared = new HashSet<int>();

    public static TMP_FontAsset Prepare(TMP_FontAsset font)
    {
        if (!font || !Prepared.Add(font.GetInstanceID())) return font;
        if (!_fallback)
        {
            _fallback = TMP_FontAsset.CreateFontAsset("Segoe UI", "Regular", 48);
            _fallback.name = "Mods Unicode fallback";
            _fallback.atlasPopulationMode = AtlasPopulationMode.DynamicOS;
            _fallback.isMultiAtlasTexturesEnabled = true;
            Object.DontDestroyOnLoad(_fallback);
        }
        font.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
        if (!font.fallbackFontAssetTable.Contains(_fallback)) font.fallbackFontAssetTable.Add(_fallback);
        return font;
    }
}
