using System;
using System.IO;
using System.Security.Cryptography;
using BepInEx;
using BongoCat;
using HarmonyLib;
using UnityEngine;

namespace BongoCatMod;

[BepInPlugin(Id, "Bongo Cat Mods", Version)]
[BepInProcess("BongoCat.exe")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Id = "local.bongocat.mods";
    public const string Version = "0.1.3";
    private const string ExpectedAssembly = "96bbcbb2733988fd31ce261891eb3027888389e03b6a23dbd63e9e7f518adfb8";
    internal static Plugin? Instance;
    internal BalanceService Balance = null!;
    internal AutomationService Automation = null!;
    internal CosmeticService Cosmetics = null!;
    private Harmony? _harmony;
    private ModWindow _window = null!;
    private bool _failed;
    private float _nextInit;

    private void Awake()
    {
        Instance = this;
        try
        {
            var assembly = Path.Combine(Paths.ManagedPath, "Assembly-CSharp.dll");
            using var sha = SHA256.Create();
            using var input = File.OpenRead(assembly);
            var actual = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
            if (actual != ExpectedAssembly)
                throw new InvalidOperationException("Game build changed. This plugin requires a compatibility update.");

            Balance = new BalanceService(Logger);
            Automation = new AutomationService(Logger, Balance);
            Cosmetics = new CosmeticService();
            _harmony = new Harmony(Id);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo("Bongo Cat Mods " + Version + " loaded. Build verified; all automation starts off.");
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
            if (!_window && Time.realtimeSinceStartup >= _nextInit)
            {
                _nextInit = Time.realtimeSinceStartup + 2f;
                if (GameAccess.Ready && CatInventory.Instance && CatInventory.Instance.IsInitialized && BongoCat.UiThemes.UiThemeManager.Instance)
                {
                    _window = gameObject.AddComponent<ModWindow>();
                    _window.Initialize(this);
                    Logger.LogInfo("Native mod window created. Press F9 to open.");
                }
            }
            Balance.Tick();
            Automation.Tick();
            Cosmetics.Tick();
        }
        catch (Exception error)
        {
            Automation?.Stop("Automation stopped after an error. See the BepInEx log.");
            Logger.LogError(error);
            _failed = true;
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

    private void OnDestroy()
    {
        if (Instance != this) return;
        Automation?.Stop();
        if (_window) Destroy(_window);
        _harmony?.UnpatchSelf();
        Balance?.Dispose();
        Instance = null;
    }

    [HarmonyPatch(typeof(ShopItem), nameof(ShopItem.Callback))]
    private static class PurchaseFinished
    {
        private static void Postfix(ShopItem __instance, int itemId) => Instance?.Automation?.OnPurchaseFinished(__instance, itemId);
    }

    [HarmonyPatch(typeof(CatCosmetics), nameof(CatCosmetics.Validate))]
    private static class RefreshAppearance
    {
        private static void Postfix() => Instance?.Cosmetics?.Apply(true);
    }
}
