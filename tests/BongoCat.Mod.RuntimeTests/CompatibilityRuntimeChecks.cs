using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BongoCat;
using HarmonyLib;
using UnityEngine;

namespace BongoCatMod.RuntimeTests;

[BepInPlugin("local.bongocat.compatibility.tests", "Compatibility runtime checks", "1.0.0")]
[BepInDependency(Plugin.Id)]
public sealed class CompatibilityRuntimeChecks : BaseUnityPlugin
{
    private readonly List<string> _results = new List<string>();
    private Plugin _plugin = null!;
    private string _report = "";
    private int _originalTab;

    private void Awake()
    {
        var args = Environment.GetCommandLineArgs();
        if (!args.Contains("--bongocat-compatibility-check")) { enabled = false; return; }
        _report = args.First(argument => argument.StartsWith("--compat-report=", StringComparison.Ordinal)).Substring("--compat-report=".Length);
        _plugin = Plugin.Instance!;
        _originalTab = _plugin.Settings.MenuTab.Value;
        _plugin.Automation.Stop("Runtime verification: purchases and gifts disabled for this test launch.");
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        var routine = Verify();
        while (true)
        {
            object? step;
            try { if (!routine.MoveNext()) break; step = routine.Current; }
            catch (Exception error) { _results.Add("FAIL " + error); break; }
            yield return step;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(_report)!);
        File.WriteAllLines(_report, _results);
        var window = _plugin.GetComponent<ModWindow>();
        if (window) AccessTools.Method(typeof(ModWindow), "ShowTab").Invoke(window, new object[] { _originalTab, false });
        Application.Quit();
    }

    private void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
        _results.Add("PASS " + message);
    }

    private static bool Patched(Type owner, string name, Feature feature) => Harmony.GetPatchInfo(AccessTools.Method(owner, name))?.Owners
        .Contains(Plugin.Id + "." + feature) ?? false;

    private IEnumerator Verify()
    {
        Check(((Feature[])Enum.GetValues(typeof(Feature))).All(_plugin.Available), "All startup API contracts passed under the game's Mono runtime.");
        Check(Patched(typeof(EmoteDonut), "Open", Feature.Wheel) && Patched(typeof(ShopItem), "Callback", Feature.Automation),
            "Independent wheel and automation Harmony groups installed.");
        var until = Time.realtimeSinceStartup + 90;
        while ((!EmoteDonut.Instance || !EmoteDonut.Instance.GetComponent<WheelPager>() || !CatInventory.Instance || !CatInventory.Instance.IsInitialized)
            && Time.realtimeSinceStartup < until) yield return null;
        Check(EmoteDonut.Instance && EmoteDonut.Instance.GetComponent<WheelPager>() && CatInventory.Instance && CatInventory.Instance.IsInitialized,
            "Native inventory and modified wheel initialized.");

        _plugin.Compatibility.KnownBuild = false;
        Check(_plugin.Available(Feature.Appearance) && _plugin.Available(Feature.Wheel), "An unrecognized build does not disable compatible features.");
        _plugin.Compatibility.Disable(Feature.Wheel, "Runtime test: wheel API changed.");
        yield return null;
        Check(!Patched(typeof(EmoteDonut), "Open", Feature.Wheel) && !EmoteDonut.Instance.GetComponent<WheelPager>(),
            "Wheel failure removed only its patches and restored native entry parents.");
        Check(_plugin.Available(Feature.Appearance) && _plugin.Available(Feature.Automation) &&
            Patched(typeof(ShopItem), "Callback", Feature.Automation), "Appearance and purchase hooks survived wheel failure.");

        _plugin.Compatibility.Disable(Feature.Sharing, "Runtime test: lobby metadata API changed.");
        Check(_plugin.Available(Feature.Appearance), "Local appearance survived a sharing API failure.");
        _plugin.Cosmetics.Tick();
        _plugin.Compatibility.Disable(Feature.Balance, "Runtime test: stat fields changed type.");
        Check(!_plugin.Available(Feature.Automation) && !_plugin.Available(Feature.Gifts) &&
            !Patched(typeof(ShopItem), "Callback", Feature.Automation), "Incompatible stats disabled and unpatched dependent spending paths.");
        var rejected = false;
        try { _plugin.Balance.Set(0); }
        catch (NotSupportedException) { rejected = true; }
        Check(rejected, "Disabled balance controls reject writes before accessing Steam stats.");

        var window = _plugin.GetComponent<ModWindow>();
        var wait = Time.realtimeSinceStartup + 8;
        while (!window && Time.realtimeSinceStartup < wait) { yield return null; window = _plugin.GetComponent<ModWindow>(); }
        Check(window, "Mod window remains available when stats, sharing and wheel are incompatible.");
        var show = AccessTools.Method(typeof(ModWindow), "ShowTab");
        show.Invoke(window, new object[] { 0, false });
        yield return null;
        show.Invoke(window, new object[] { 2, false });
        yield return null;
        Check(window && _plugin.Available(Feature.Catalog) && _plugin.Available(Feature.Appearance),
            "General and Cosmetics render after independent compatibility failures.");
    }
}
