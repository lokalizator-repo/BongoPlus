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
        foreach (var slot in new[] { "skin", "hat", "bongos" })
            Appearance.Add(slot, config.Bind("Appearance", slot, -1));
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
