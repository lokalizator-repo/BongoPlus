using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;

namespace BongoCatMod;

internal sealed class ModSettings
{
    private readonly ConfigFile _config;
    public readonly ConfigEntry<bool> BuyOwn;
    public readonly ConfigEntry<bool> Gift;
    public readonly ConfigEntry<bool> CosmeticChests;
    public readonly ConfigEntry<bool> EmoteChests;
    public readonly ConfigEntry<bool> AllPlayers;
    public readonly ConfigEntry<string> Recipients;
    public readonly ConfigEntry<bool> ShareAppearance;
    public readonly ConfigEntry<string> WheelEmotes;
    public readonly ConfigEntry<string> WheelOrder;
    public readonly ConfigEntry<string> Favorites;
    public readonly ConfigEntry<string> Presets;
    public readonly ConfigEntry<int> MenuTab;
    public readonly ConfigEntry<string> MenuSlot;
    public readonly ConfigEntry<string> MenuSearch;
    public readonly ConfigEntry<int> MenuPage;
    public readonly ConfigEntry<int> CosmeticView;
    public readonly ConfigEntry<bool> FavoritesOnly;
    public readonly ConfigEntry<string> MenuScroll;
    public readonly Dictionary<string, ConfigEntry<int>> Appearance = new Dictionary<string, ConfigEntry<int>>();

    public ModSettings(ConfigFile config)
    {
        _config = config;
        config.SaveOnConfigSet = false;
        BuyOwn = config.Bind("Automation", "BuyOwnChests", false);
        Gift = config.Bind("Automation", "GiftChests", false);
        CosmeticChests = config.Bind("Automation", "CosmeticChests", true);
        EmoteChests = config.Bind("Automation", "EmoteChests", true);
        AllPlayers = config.Bind("Automation", "AllPlayers", false);
        Recipients = config.Bind("Automation", "Recipients", "");
        ShareAppearance = config.Bind("Appearance", "ShareInLobby", true);
        WheelEmotes = config.Bind("Emotes", "WheelItems", "");
        WheelOrder = config.Bind("Emotes", "WheelOrder", "");
        Favorites = config.Bind("Cosmetics", "Favorites", "");
        Presets = config.Bind("Cosmetics", "Presets", "");
        MenuTab = config.Bind("Menu", "Tab", 0);
        MenuSlot = config.Bind("Menu", "Slot", "skin");
        MenuSearch = config.Bind("Menu", "Search", "");
        MenuPage = config.Bind("Menu", "Page", 0);
        CosmeticView = config.Bind("Menu", "CosmeticView", 0);
        FavoritesOnly = config.Bind("Menu", "FavoritesOnly", false);
        MenuScroll = config.Bind("Menu", "Scroll", "0,0,0");
        foreach (var slot in new[] { "skin", "hat" })
            Appearance.Add(slot, config.Bind("Appearance", slot, -1));
        var obsolete = config.Bind("Appearance", "bongos", -1);
        config.Remove(obsolete.Definition);
        config.Save();
    }

    public HashSet<ulong> ReadRecipients()
    {
        var result = new HashSet<ulong>();
        foreach (var value in Recipients.Value.Split(','))
            if (ulong.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id != 0)
                result.Add(id);
        return result;
    }

    public void SetRecipients(IEnumerable<ulong> recipients) => Recipients.Value = string.Join(",",
        recipients.Where(id => id != 0).Distinct().OrderBy(id => id)
            .Select(id => id.ToString(CultureInfo.InvariantCulture)));

    public void Save() => _config.Save();

    public float[] ReadScroll()
    {
        var result = new float[5];
        var values = MenuScroll.Value.Split(',');
        for (var i = 0; i < result.Length && i < values.Length; i++)
            if (float.TryParse(values[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
                !float.IsNaN(value) && !float.IsInfinity(value)) result[i] = System.Math.Max(0, value);
        return result;
    }

    public void SetScroll(IEnumerable<float> values) => MenuScroll.Value = string.Join(",",
        values.Take(5).Select(value => value.ToString("R", CultureInfo.InvariantCulture)));

    public HashSet<int> ReadWheelEmotes()
    {
        var result = new HashSet<int>();
        foreach (var value in WheelEmotes.Value.Split(','))
            if (int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0)
                result.Add(id);
        return result;
    }

    public void SetWheelEmotes(IEnumerable<int> emotes) => WheelEmotes.Value = string.Join(",",
        emotes.Where(id => id > 0).Distinct().OrderBy(id => id)
            .Select(id => id.ToString(CultureInfo.InvariantCulture)));
}
