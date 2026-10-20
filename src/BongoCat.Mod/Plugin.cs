using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Linq;
using BepInEx;
using BongoCat;
using HarmonyLib;
using UnityEngine;

namespace BongoCatMod;

[BepInPlugin(Id, "BongoPlus", Version)]
[BepInProcess("BongoCat.exe")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Id = "local.bongocat.mods";
    public const string Version = "1.0.0";
    private const string ExpectedAssembly = "96bbcbb2733988fd31ce261891eb3027888389e03b6a23dbd63e9e7f518adfb8";
    internal static Plugin? Instance;
    internal BalanceService Balance = null!;
    internal AutomationService Automation = null!;
    internal CosmeticService Cosmetics = null!;
    internal EmoteWheelService Wheel = null!;
    private ModSettings _settings = null!;
    internal ModSettings Settings => _settings;
    private readonly Dictionary<Feature, Harmony> _harmonies = new Dictionary<Feature, Harmony>();
    internal FeatureCompatibility Compatibility { get; } = new FeatureCompatibility();
    private ModWindow _window = null!;
    private bool _failed;
    private float _nextInit;
    private bool _uiFailed;

    internal bool Available(Feature feature) => Compatibility.Available(feature);
    internal void Require(Feature feature) => Compatibility.Require(feature);
    internal bool Execute(Feature feature, Action action) => Compatibility.Execute(feature, action);

    [HarmonyPatch(typeof(Helper.InputFieldCache), "get_FocusingInputField")]
    private static class ModInputFocused
    {
        private static bool Prefix(ref bool __result)
        {
            if (!(Instance?.Available(Feature.Input) ?? false) || !(Instance?._window?.IsTyping ?? false)) return true;
            __result = true;
            return false;
        }
    }

    private void Awake()
    {
        Instance = this;
        try
        {
            ReadBuildFingerprint();

            _settings = new ModSettings(Config);
            Compatibility.Disabled = OnFeatureDisabled;
            GameApiContracts.Check(Compatibility);
            Balance = new BalanceService(Logger);
            Automation = new AutomationService(Logger, Balance, _settings);
            Cosmetics = new CosmeticService(Logger, _settings);
            Wheel = new EmoteWheelService(_settings, Logger);
            Patch(Feature.Input, typeof(ModInputFocused));
            Patch(Feature.Automation, typeof(PurchaseFinished));
            Patch(Feature.Gifts, typeof(GiftClicked));
            if (RefreshAppearance.HasTargets) Patch(Feature.Appearance, typeof(RefreshAppearance));
            Patch(Feature.Wheel, typeof(WheelStarted), typeof(WheelValidated), typeof(WheelRearranged), typeof(WheelOpened),
                typeof(WheelClosed), typeof(WheelHovered), typeof(WheelEmoteRemoved), typeof(WheelEmoteToggled));
            Logger.LogInfo("BongoPlus " + Version + " loaded. " + Compatibility.Summary);
        }
        catch (Exception error)
        {
            _failed = true;
            Logger.LogError(error);
        }
    }

    private void Update()
    {
        if (_failed) return;
        try
        {
            if (!_window && !_uiFailed && Time.realtimeSinceStartup >= _nextInit)
            {
                _nextInit = Time.realtimeSinceStartup + 2f;
                if (BongoCat.UiThemes.UiThemeManager.Instance && NativeUiReady())
                {
                    _window = gameObject.AddComponent<ModWindow>();
                    _window.Initialize(this);
                    Logger.LogInfo("Native mod window created. Open it from Mods in the game menu.");
                }
            }
        }
        catch (Exception error)
        {
            Logger.LogError(error);
            if (_window) Destroy(_window);
            _uiFailed = true;
        }
        Execute(Feature.Balance, Balance.Tick);
        Execute(Feature.Automation, Automation.Tick);
        Execute(Feature.Appearance, Cosmetics.Tick);
        Execute(Feature.Wheel, Wheel.Tick);
    }

    private bool NativeUiReady() => !Available(Feature.Catalog) || CatalogReady();

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool CatalogReady() => CatInventory.Instance && CatInventory.Instance.IsInitialized;

    private void ReadBuildFingerprint()
    {
        try
        {
            using var sha = SHA256.Create();
            using var input = File.OpenRead(Path.Combine(Paths.ManagedPath, "Assembly-CSharp.dll"));
            Compatibility.BuildHash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
            Compatibility.KnownBuild = Compatibility.BuildHash == ExpectedAssembly;
        }
        catch (Exception error) { Logger.LogWarning("Build fingerprint unavailable: " + error.GetBaseException().Message); }
        if (!Compatibility.KnownBuild) Logger.LogWarning("Unrecognized game assembly (" + Compatibility.BuildHash + "). Checking each feature's API contract.");
    }

    private void Patch(Feature feature, params Type[] patches)
    {
        if (!Available(feature)) return;
        var harmony = new Harmony(Id + "." + feature);
        _harmonies.Add(feature, harmony);
        Execute(feature, () => { foreach (var patch in patches) harmony.CreateClassProcessor(patch).Patch(); });
    }

    private void OnFeatureDisabled(Feature feature, string reason)
    {
        Logger.LogWarning(feature + " disabled: " + reason);
        foreach (var pair in _harmonies.Where(pair => !Available(pair.Key)).ToArray())
        {
            try { pair.Value.UnpatchSelf(); }
            catch (Exception error) { Logger.LogWarning(error); }
            _harmonies.Remove(pair.Key);
        }
        if (!Available(Feature.Automation)) Automation?.Stop("Automation is unavailable: " + Compatibility.Reason(Feature.Automation));
        else if (!Available(Feature.Gifts) && Automation != null) Automation.Gift = false;
        if (!Available(Feature.Wheel) && Wheel != null)
        {
            try { Wheel.Deactivate(); }
            catch (Exception error) { Logger.LogWarning("Native wheel restore: " + error.GetBaseException().Message); }
        }
    }

    internal void Run(Action action)
    {
        try { action(); }
        catch (Exception error)
        {
            Logger.LogWarning(error);
            if (_window) _window.ShowError(error.GetBaseException().Message);
        }
    }

    internal void SaveAutomation()
    {
        _settings.BuyOwn.Value = Automation.BuyOwn;
        _settings.Gift.Value = Automation.Gift;
        _settings.CosmeticChests.Value = Automation.Cosmetics;
        _settings.EmoteChests.Value = Automation.Emotes;
        _settings.AllPlayers.Value = Automation.AllPlayers;
        _settings.SetRecipients(Automation.SelectedPlayers);
        _settings.Save();
    }

    internal void StopAutomation()
    {
        Automation.Stop();
        SaveAutomation();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Automation?.Stop();
        if (_window) Destroy(_window);
        foreach (var harmony in _harmonies.Values) harmony.UnpatchSelf();
        Balance?.Dispose();
        SpriteContentBounds.Dispose();
        Instance = null;
    }

    [HarmonyPatch(typeof(ShopItem), nameof(ShopItem.Callback))]
    private static class PurchaseFinished
    {
        private static void Postfix(ShopItem __instance, int itemId) =>
            Instance?.Execute(Feature.Automation, () => Instance.Automation.OnPurchaseFinished(__instance, itemId));
    }

    [HarmonyPatch(typeof(EmoteDonut), "Start")]
    private static class WheelStarted
    {
        private static void Postfix(EmoteDonut __instance, ref IEnumerator __result) =>
            __result = WaitForStart(__instance, __result);

        private static IEnumerator WaitForStart(EmoteDonut wheel, IEnumerator routine)
        {
            try
            {
                while (routine.MoveNext()) yield return routine.Current;
            }
            finally
            {
                (routine as IDisposable)?.Dispose();
            }
            Instance?.Execute(Feature.Wheel, () => Instance.Wheel.OnReady(wheel));
        }
    }

    [HarmonyPatch(typeof(EmoteDonut), nameof(EmoteDonut.Validate))]
    private static class WheelValidated
    {
        private static void Prefix() => Instance?.Execute(Feature.Wheel, () => Instance.Wheel.BeginValidation());

        private static Exception? Finalizer(Exception? __exception)
        {
            Instance?.Execute(Feature.Wheel, () => Instance.Wheel.EndValidation());
            return __exception;
        }
    }

    [HarmonyPatch(typeof(EmoteDonut), "RearrangeEmotes")]
    private static class WheelRearranged
    {
        private static bool Prefix(EmoteDonut __instance)
        {
            var pager = __instance.GetComponent<WheelPager>();
            if (!pager || !(Instance?.Available(Feature.Wheel) ?? false)) return true;
            return !Instance!.Execute(Feature.Wheel, pager.Refresh);
        }
    }

    [HarmonyPatch(typeof(EmoteDonut), "Open")]
    private static class WheelOpened
    {
        private static bool Prefix(EmoteDonut __instance)
        {
            var pager = __instance.GetComponent<WheelPager>();
            if (!pager || !(Instance?.Available(Feature.Wheel) ?? false)) return true;
            return !Instance!.Execute(Feature.Wheel, pager.Open);
        }
    }

    [HarmonyPatch(typeof(EmoteDonut), "Close")]
    private static class WheelClosed
    {
        private static bool Prefix(EmoteDonut __instance)
        {
            var pager = __instance.GetComponent<WheelPager>();
            if (!pager || !(Instance?.Available(Feature.Wheel) ?? false)) return true;
            return !Instance!.Execute(Feature.Wheel, pager.Close);
        }
    }

    [HarmonyPatch(typeof(EmoteDonut), "Update")]
    private static class WheelHovered
    {
        private static bool Prefix(EmoteDonut __instance)
        {
            var keep = false;
            Instance?.Execute(Feature.Wheel, () => keep = __instance.GetComponent<WheelPager>()?.KeepOpenOnHover() ?? false);
            return !keep;
        }
    }

    [HarmonyPatch(typeof(EmoteDonut), "RemoveEmote")]
    private static class WheelEmoteRemoved
    {
        private static bool Prefix(SteamItemRuntime steamItem) =>
            !(Instance?.Available(Feature.Wheel) == true && (Instance.Wheel?.KeepDuringValidation(steamItem) ?? false));
    }

    [HarmonyPatch(typeof(EmoteDonut), nameof(EmoteDonut.ToggleItem))]
    private static class WheelEmoteToggled
    {
        private static void Prefix(EmoteDonut __instance, SteamItemRuntime steamItem, out bool __state)
        {
            var contains = false;
            Instance?.Execute(Feature.Wheel, () => contains = Instance.Wheel.Contains(__instance, steamItem.Id));
            __state = contains;
        }

        private static void Postfix(SteamItemRuntime steamItem, bool __state) =>
            Instance?.Execute(Feature.Wheel, () => Instance.Wheel.ObserveToggle(steamItem, __state));
    }

    [HarmonyPatch]
    private static class RefreshAppearance
    {
        private static readonly string[] Names = { nameof(CatCosmetics.Validate), "Equip", "Unequip",
            nameof(CatCosmetics.RestoreActuallyEquippedItems), "OnLobbyEnter" };
        public static bool HasTargets => TargetMethods().Any();
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var name in Names)
                foreach (var method in typeof(CatCosmetics).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(method => method.Name == name && method.ReturnType == typeof(void) && !method.ContainsGenericParameters))
                    yield return method;
        }

        private static void Postfix() => Instance?.Execute(Feature.Appearance, () => Instance.Cosmetics.Apply(true));
    }

    [HarmonyPatch(typeof(BongoCat.Multiplayer.OpenChestForMember), nameof(BongoCat.Multiplayer.OpenChestForMember.OnClicked))]
    private static class GiftClicked
    {
        private static void Prefix(BongoCat.Multiplayer.OpenChestForMember __instance, out bool __state)
        {
            var clicked = true;
            Instance?.Execute(Feature.Gifts, () => clicked = AutomationService.WasGiftClicked(__instance));
            __state = clicked;
        }

        private static void Postfix(BongoCat.Multiplayer.OpenChestForMember __instance, bool __state) =>
            Instance?.Execute(Feature.Gifts, () => Instance.Automation.OnGiftClicked(__instance, __state));
    }
}
