using BepInEx.Configuration;
using BongoCatMod;

internal static class SettingsTests
{
    internal static void Run(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "BongoCatModTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.cfg");
            var settings = new ModSettings(new ConfigFile(path, false));
            check(!settings.BuyOwn.Value && !settings.Gift.Value && settings.ShareAppearance.Value,
                "Fresh settings keep automation off and lobby appearance sharing on");
            settings.BuyOwn.Value = true;
            settings.Gift.Value = true;
            settings.EmoteChests.Value = false;
            settings.AllPlayers.Value = true;
            settings.ShareAppearance.Value = false;
            settings.Appearance["skin"].Value = 42;
            settings.SetRecipients(new ulong[] { 76561198000000002, 0, 76561198000000001, 76561198000000002 });
            settings.SetWheelEmotes(new[] { 42, 89, 42, 0, -1 });
            settings.Save();

            var restored = new ModSettings(new ConfigFile(path, false));
            check(restored.BuyOwn.Value && restored.Gift.Value && !restored.EmoteChests.Value && restored.AllPlayers.Value,
                "Automation preferences survive a new settings instance");
            check(!restored.ShareAppearance.Value && restored.Appearance["skin"].Value == 42 &&
                restored.Appearance["hat"].Value == -1,
                "Appearance sharing and selected slots survive a restart");
            check(restored.ReadRecipients().SetEquals(new ulong[] { 76561198000000001, 76561198000000002 }),
                "Recipient SteamIDs survive serialization without duplicates or zero IDs");
            check(restored.ReadWheelEmotes().SetEquals(new[] { 42, 89 }),
                "Native wheel choices survive restart without duplicate or invalid item IDs");
            restored.WheelEmotes.Value += ",invalid,0,-1,2147483648";
            check(restored.ReadWheelEmotes().SetEquals(new[] { 42, 89 }),
                "Malformed saved wheel choices are ignored");

            restored.Recipients.Value += ",invalid,0,-1,18446744073709551616";
            check(restored.ReadRecipients().Count == 2, "Malformed saved recipients are ignored");
            restored.BuyOwn.Value = false;
            restored.Gift.Value = false;
            foreach (var entry in restored.Appearance.Values) entry.Value = -1;
            restored.SetWheelEmotes(Array.Empty<int>());
            restored.Save();
            var reset = new ModSettings(new ConfigFile(path, false));
            check(!reset.BuyOwn.Value && !reset.Gift.Value && reset.Appearance.Values.All(entry => entry.Value == -1),
                "Stopping automation and restoring appearance remain effective after restart");
            check(reset.ReadWheelEmotes().Count == 0, "Removed wheel choices stay removed after restart");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
