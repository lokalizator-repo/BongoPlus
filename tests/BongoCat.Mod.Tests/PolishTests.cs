using BepInEx.Configuration;
using BongoCatMod;

internal static class PolishTests
{
    internal static void Run(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "BongoCatPolish-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.cfg");
            var settings = new ModSettings(new ConfigFile(path, false));
            settings.Favorites.Value = CosmeticPreferences.WriteIds(new[] { 451, 981, 451, 0, -2 });
            settings.Presets.Value = CosmeticPreferences.WritePresets(new[] {
                new AppearancePreset("Кот: море | 100%", 983, -2), new AppearancePreset("Zeus", 451, 453) });
            settings.WheelOrder.Value = CosmeticPreferences.WriteIds(new[] { 89, 42, 101 });
            settings.MenuTab.Value = 2;
            settings.MenuSlot.Value = "emote";
            settings.MenuSearch.Value = "Face Bonk";
            settings.MenuPage.Value = 3;
            settings.FavoritesOnly.Value = true;
            settings.CosmeticView.Value = 2;
            settings.SetScroll(new[] { 0f, 74.25f, 352.5f, 81f, 96f });
            settings.Appearance["hat"].Value = -2;
            settings.Save();
            var restored = new ModSettings(new ConfigFile(path, false));
            check(CosmeticPreferences.ReadIds(restored.Favorites.Value).SequenceEqual(new[] { 451, 981 }),
                "Favorites survive a real config reload independently of ownership and wheel selection");
            var presets = CosmeticPreferences.ReadPresets(restored.Presets.Value);
            check(presets.Count == 2 && presets[0].Name == "Кот: море | 100%" && presets[0].Skin == 983 && presets[0].Hat == -2,
                "Presets preserve Unicode, separators and an explicitly empty hat through the config file");
            check(restored.Appearance["hat"].Value == -2, "An explicitly cleared preset hat survives restart");
            check(CosmeticPreferences.ReadIds(restored.WheelOrder.Value).SequenceEqual(new[] { 89, 42, 101 }),
                "Wheel ordering survives reload without numeric sorting");
            check(restored.MenuTab.Value == 2 && restored.MenuSlot.Value == "emote" && restored.MenuSearch.Value == "Face Bonk" &&
                restored.MenuPage.Value == 3 && restored.FavoritesOnly.Value && restored.CosmeticView.Value == 2 &&
                restored.ReadScroll().SequenceEqual(new[] { 0f, 74.25f, 352.5f, 81f, 96f }),
                "Tab, category, search, page, favorites filter and independent scroll positions survive restart");
            restored.MenuScroll.Value = "NaN,Infinity,-40";
            check(restored.ReadScroll().All(value => value == 0), "Corrupt scroll positions cannot place content outside the viewport");
            restored.Presets.Value += "|broken|Bad:2147483648:2|Invalid:-4:1|Zeus:1:2";
            check(CosmeticPreferences.ReadPresets(restored.Presets.Value).Count == 2,
                "Malformed and duplicate preset records cannot corrupt valid saved appearances");
        }
        finally { Directory.Delete(directory, true); }

        var original = Enumerable.Range(1, 19).ToArray();
        var forward = CosmeticPreferences.Move(original, 2, 8);
        check(forward[8] == 2 && forward.Where(id => id != 2).SequenceEqual(original.Where(id => id != 2)),
            "Dragging forward across pages places the emote at the destination without scrambling other slots");
        var backward = CosmeticPreferences.Move(forward, 19, 0);
        check(backward[0] == 19 && backward[9] == 2 && backward.Distinct().Count() == 19,
            "Dragging back across pages preserves every emote exactly once");
        check(CosmeticPreferences.Order(new[] { 3, 8, 9 }, new[] { 8, 99, 3, 8 }).SequenceEqual(new[] { 8, 3, 9 }),
            "Native wheel entries reconcile saved order with removed and newly added emotes");
        var random = new Random(3419430);
        var valid = true;
        var ordered = original;
        for (var i = 0; i < 5000; i++)
        {
            var id = random.Next(1, 20);
            var index = random.Next(0, ordered.Length);
            ordered = CosmeticPreferences.Move(ordered, id, index);
            valid &= ordered[index] == id && ordered.OrderBy(value => value).SequenceEqual(original);
            var width = random.Next(160, 7680);
            var height = random.Next(160, 4320);
            var scale = MenuLayout.Scale(width, height, 1.2);
            valid &= 420 * scale + 24 <= width + 0.001 && 560 * scale + 24 <= height + 0.001;
        }
        check(valid, "5000 cross-page moves preserve the complete wheel and menu bounds fit small and large work areas");

        var activity = new AutomationActivity();
        check(activity.GiftSent("friend:skin") && !activity.GiftSent("friend:skin") && activity.GiftRequests == 1,
            "A gift observed by both the native hook and scanner is counted once");
        activity.GiftSent("friend:emote");
        activity.ReadyCleared("friend:skin");
        activity.ReadyCleared("friend:skin");
        check(activity.GiftReadyCleared == 1 && activity.PendingGifts == 1,
            "Ready changes are counted once independently for each chest type");
        activity.GiftSent("friend:skin");
        activity.LeaveLobby();
        check(activity.GiftRequests == 3 && activity.PendingGifts == 0 && activity.GiftReadyCleared == 1,
            "Leaving a lobby clears pending observations without reporting unconfirmed deliveries as successes");
    }
}
