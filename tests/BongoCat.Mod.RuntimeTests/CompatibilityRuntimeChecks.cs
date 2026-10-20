using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BongoCat;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BongoCatMod.RuntimeTests;

[BepInPlugin("local.bongocat.compatibility.tests", "Compatibility runtime checks", "1.0.0")]
[BepInDependency(Plugin.Id)]
public sealed class CompatibilityRuntimeChecks : BaseUnityPlugin
{
    private readonly List<string> _results = new List<string>();
    private Plugin _plugin = null!;
    private string _report = "";
    private int _originalTab;
    private float[]? _originalScroll;

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
        if (window)
        {
            if (_originalScroll != null) AccessTools.Field(typeof(ModWindow), "_scrollPositions").SetValue(window, _originalScroll);
            AccessTools.Method(typeof(ModWindow), "ShowTab").Invoke(window, new object[] { _originalTab, false });
            AccessTools.Method(typeof(ModWindow), "SaveState").Invoke(window, null);
        }
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

        var window = _plugin.GetComponent<ModWindow>();
        var wait = Time.realtimeSinceStartup + 8;
        while (!window && Time.realtimeSinceStartup < wait) { yield return null; window = _plugin.GetComponent<ModWindow>(); }
        Check(window, "Mod window initialized for layout checks.");
        _originalScroll = (float[])((float[])AccessTools.Field(typeof(ModWindow), "_scrollPositions").GetValue(window)).Clone();
        var layoutChecks = VerifyLayout(window);
        while (layoutChecks.MoveNext()) yield return layoutChecks.Current;

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

        Check(window, "Mod window remains available when stats, sharing and wheel are incompatible.");
        var show = AccessTools.Method(typeof(ModWindow), "ShowTab");
        show.Invoke(window, new object[] { 0, false });
        yield return null;
        show.Invoke(window, new object[] { 2, false });
        yield return null;
        Check(window && _plugin.Available(Feature.Catalog) && _plugin.Available(Feature.Appearance),
            "General and Cosmetics render after independent compatibility failures.");
    }

    private IEnumerator VerifyLayout(ModWindow window)
    {
        var show = AccessTools.Method(typeof(ModWindow), "ShowTab");
        var visible = AccessTools.Method(typeof(ModWindow), "SetVisible");
        visible.Invoke(window, new object[] { true });
        var body = (RectTransform)AccessTools.Field(typeof(ModWindow), "_body").GetValue(window);
        for (var tab = 0; tab <= 1; tab++)
        {
            show.Invoke(window, new object[] { tab, false });
            yield return new WaitForSeconds(0.6f);
            Canvas.ForceUpdateCanvases();
            var summary = body.Find("Activity summary").GetComponent<TMP_Text>();
            var history = body.Find("Activity history").GetComponent<TMP_Text>();
            Check(summary.rectTransform.rect.height < 48 &&
                (!history.gameObject.activeSelf || history.rectTransform.rect.height < 160),
                "Activity text uses content height in " + (tab == 0 ? "General." : "Lobby."));
            history.text = "";
            NativeStyle.FitLabel(history, true);
            Check(!history.gameObject.activeSelf, "Empty activity history takes no layout space in tab " + tab + ".");
        }
        show.Invoke(window, new object[] { 0, false });
        yield return new WaitForSeconds(0.6f);
        var details = body.Find("Compatibility details").GetComponent<TMP_Text>();
        var toggle = body.GetComponentsInChildren<Button>().Single(button =>
            button.GetComponentInChildren<TMP_Text>()?.text == "Show compatibility details");
        Check(!details.gameObject.activeSelf, "Compatibility stays collapsed during periodic refresh.");
        toggle.onClick.Invoke();
        yield return new WaitForSeconds(0.6f);
        Canvas.ForceUpdateCanvases();
        var scroll = (ScrollRect)AccessTools.Field(typeof(ModWindow), "_scroll").GetValue(window);
        var top = scroll.viewport.InverseTransformPoint(details.transform.position).y + details.rectTransform.rect.height * (1 - details.rectTransform.pivot.y);
        Check(details.gameObject.activeSelf && top <= scroll.viewport.rect.yMax + 1 && top > scroll.viewport.rect.yMin + 30 &&
            toggle.GetComponentInChildren<TMP_Text>().text == "Hide compatibility details",
            "Expanding compatibility immediately reveals its first lines and changes the toggle state.");
        toggle.onClick.Invoke();
        yield return new WaitForSeconds(0.6f);
        Check(!details.gameObject.activeSelf, "Compatibility collapses without retaining an empty text block.");
    }
}
