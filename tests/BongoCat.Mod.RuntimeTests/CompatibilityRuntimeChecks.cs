using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BongoCat;
using BongoCat.PawPass;
using BongoCat.UiThemes;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
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
        while ((!EmoteDonut.Instance || !EmoteDonut.Instance.GetComponent<WheelPager>() || !CatInventory.Instance || !CatInventory.Instance.IsInitialized || CatInventory.Instance.FetchingItems)
            && Time.realtimeSinceStartup < until) yield return null;
        Check(EmoteDonut.Instance && EmoteDonut.Instance.GetComponent<WheelPager>() && CatInventory.Instance && CatInventory.Instance.IsInitialized,
            "Native inventory and modified wheel initialized.");

        var window = _plugin.GetComponent<ModWindow>();
        var wait = Time.realtimeSinceStartup + 8;
        while (!window && Time.realtimeSinceStartup < wait) { yield return null; window = _plugin.GetComponent<ModWindow>(); }
        Check(window, "Mod window initialized for layout checks.");
        VerifyOwnedEmotes();
        var clickUntil = Time.realtimeSinceStartup + 15;
        while (!_plugin.Balance.CanAddClicks && Time.realtimeSinceStartup < clickUntil) yield return null;
        Check(_plugin.Balance.CanAddClicks, "Click controls wait for initialized Steam stats and Paw Pass.");
        var added = (UnityEvent<int>)AccessTools.Field(typeof(Pets), "_onPetsAdded").GetValue(Pets.Instance);
        var persistent = AccessTools.Field(typeof(UnityEventBase), "m_PersistentCalls").GetValue(added);
        var calls = (IEnumerable)AccessTools.Field(persistent.GetType(), "m_Calls").GetValue(persistent);
        Check(calls.Cast<object>().Any(call =>
            (UnityEngine.Object)AccessTools.Field(call.GetType(), "m_Target").GetValue(call) == PawPassManager.Instance &&
            (string)AccessTools.Field(call.GetType(), "m_MethodName").GetValue(call) == "AddTaps"),
            "Native click event is connected to Paw Pass AddTaps in the loaded game scene.");
        var gainedBefore = (int)GameAccess.Gained.GetValue(Pets.Instance);
        var spentBefore = (int)GameAccess.Spent.GetValue(Pets.Instance);
        foreach (var amount in new[] { 0, -1, int.MaxValue })
        {
            var rejectedClicks = false;
            try { _plugin.Balance.AddClicks(amount); }
            catch (ArgumentOutOfRangeException) { rejectedClicks = true; }
            Check(rejectedClicks && !_plugin.Balance.Pending && gainedBefore == (int)GameAccess.Gained.GetValue(Pets.Instance) &&
                spentBefore == (int)GameAccess.Spent.GetValue(Pets.Instance),
                "Invalid click amount is rejected without changing counters or starting a save: " + amount + ".");
        }
        VerifyUpdatedUi(window);
        _originalScroll = (float[])((float[])AccessTools.Field(typeof(ModWindow), "_scrollPositions").GetValue(window)).Clone();
        var cosmeticChecks = VerifyCosmeticsUi(window);
        while (cosmeticChecks.MoveNext()) yield return cosmeticChecks.Current;
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

        _plugin.Cosmetics.Tick();
        _plugin.Compatibility.Disable(Feature.Balance, "Runtime test: stat fields changed type.");
        Check(!_plugin.Available(Feature.Automation) && !_plugin.Available(Feature.Gifts) && !_plugin.Available(Feature.Clicks) &&
            !Patched(typeof(ShopItem), "Callback", Feature.Automation), "Incompatible stats disabled and unpatched dependent spending paths.");
        var rejected = false;
        try { _plugin.Balance.Set(0); }
        catch (NotSupportedException) { rejected = true; }
        Check(rejected, "Disabled balance controls reject writes before accessing Steam stats.");

        Check(window, "Mod window remains available when stats and wheel are incompatible.");
        var show = AccessTools.Method(typeof(ModWindow), "ShowTab");
        show.Invoke(window, new object[] { 0, false });
        yield return null;
        show.Invoke(window, new object[] { 2, false });
        yield return null;
        Check(window && _plugin.Available(Feature.Catalog) && _plugin.Available(Feature.Appearance),
            "General and Cosmetics render after independent compatibility failures.");
    }

    private void VerifyOwnedEmotes()
    {
        AccessTools.Field(typeof(EmoteWheelService), "_nextRefresh").SetValue(_plugin.Wheel, 0f);
        _plugin.Wheel.Tick();
        Check(_plugin.Cosmetics.Items("emote", "").All(item => item.IsEmote && item.IsOwned),
            "Emote catalog contains only owned Steam emotes.");
        Check(_plugin.Settings.ReadWheelEmotes().All(id => SteamItemIdReference.Inst[id].IsOwned) &&
            _plugin.Wheel.ItemIds.All(id => SteamItemIdReference.Inst[id].IsOwned),
            "Saved wheel choices and native entries contain only owned items after migration.");
        var unowned = CatInventory.Instance.Items.FirstOrDefault(item => item.IsEmote && !item.IsOwned);
        Check(unowned != null, "Unowned emote is available to verify wheel rejection.");
        var before = _plugin.Wheel.ItemIds;
        var saved = _plugin.Settings.WheelEmotes.Value;
        var rejected = false;
        try { _plugin.Wheel.Toggle(unowned!); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected && before.SequenceEqual(_plugin.Wheel.ItemIds) && saved == _plugin.Settings.WheelEmotes.Value,
            "Adding an unowned emote is rejected without changing the wheel or saved choices.");
    }

    private IEnumerator VerifyCosmeticsUi(ModWindow window)
    {
        var names = new[] { "_slot", "_cosmeticView", "_query", "_page", "_favoritesOnly" };
        var fields = names.Select(name => AccessTools.Field(typeof(ModWindow), name)).ToArray();
        var values = fields.Select(field => field.GetValue(window)).ToArray();
        var show = AccessTools.Method(typeof(ModWindow), "ShowTab");
        var body = (RectTransform)AccessTools.Field(typeof(ModWindow), "_body").GetValue(window);
        try
        {
            fields[0].SetValue(window, "skin");
            fields[1].SetValue(window, 0);
            show.Invoke(window, new object[] { 2, false });
            yield return null;
            Check(body.Find("Local preview notice") && body.Find("Cosmetics views").GetComponentsInChildren<Button>().Length == 4,
                "Cosmetics clearly labels local previews and has no lobby sharing control.");
            fields[0].SetValue(window, "emote");
            show.Invoke(window, new object[] { 2, false });
            yield return null;
            Check(body.Find("Owned emotes notice") && !body.Find("Local preview notice") &&
                body.Find("Cosmetics views").GetComponentsInChildren<Button>().Length == 3,
                "Owned emote catalog replaces appearance controls with wheel instructions.");
            fields[1].SetValue(window, 2);
            show.Invoke(window, new object[] { 2, false });
            yield return null;
            var editor = (RectTransform)AccessTools.Field(typeof(ModWindow), "_wheelEditor").GetValue(window);
            var add = editor.GetChild(0).GetComponentInChildren<Button>();
            Check(add && add.interactable, "Expanded wheel has an accessible add-emotes button even when its page is full.");
            add.onClick.Invoke();
            yield return null;
            Check((string)fields[0].GetValue(window) == "emote" && (int)fields[1].GetValue(window) == 0 &&
                body.Find("Owned emotes notice"), "Wheel add button opens the owned emote selector.");
        }
        finally
        {
            for (var i = 0; i < fields.Length; i++) fields[i].SetValue(window, values[i]);
        }
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
            if (tab == 0)
            {
                var clicks = body.GetComponentsInChildren<Button>().Single(button =>
                    button.GetComponentInChildren<TMP_Text>()?.text == "Add clicks");
                Check(clicks.interactable, "General exposes the enabled native click action without executing it during tests.");
                var count = clicks.transform.parent.GetComponentInChildren<TMP_InputField>();
                Check(count && count.text == "1000000" && count.contentType == TMP_InputField.ContentType.IntegerNumber,
                    "Click action has an editable integer count with a one-million default.");
            }
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

    private void VerifyUpdatedUi(ModWindow window)
    {
        var button = (Button)AccessTools.Field(typeof(ModWindow), "_menuButton").GetValue(window);
        Check(button && button.gameObject.scene.IsValid(), "Mods menu button is attached to the live game menu.");
        UiThemeManager.Instance.Redraw(button.GetComponent<NativeMenuButton>());
        Check(button.GetComponent<Image>().sprite && button.GetComponentInChildren<TMP_Text>(true).font,
            "Mods menu button has its native sprite and font after the theme API update.");
        Check(ThemeRoles.Sprite("PopupBackground") && ThemeRoles.Sprite("FooterButtonDefault") &&
            ThemeRoles.Color("NormalText") && ThemeRoles.Color("TabButtonIconDefault") && ThemeRoles.Font("Body"),
            "Required ScriptableObject theme roles resolve from loaded game assets.");
        button.onClick.Invoke();
        var panel = (RectTransform)AccessTools.Field(typeof(ModWindow), "_panel").GetValue(window);
        Check(panel.gameObject.activeSelf, "Clicking Mods opens the mod window.");
        button.onClick.Invoke();
        Check(!panel.gameObject.activeSelf, "Clicking Mods again closes the mod window.");

        if (_plugin.Wheel.ItemIds.Length == 0)
        {
            _results.Add("SKIP Wheel open-state checks require at least one configured emote.");
            return;
        }
        var wheel = EmoteDonut.Instance;
        var pager = wheel.GetComponent<WheelPager>();
        var buttonMode = AccessTools.Field(typeof(EmoteDonut), "_openWithButton");
        var closed = AccessTools.Field(typeof(EmoteDonut), "_activelyClosed");
        var counter = (GameObject)AccessTools.Field(typeof(EmoteDonut), "_counterBar").GetValue(wheel);
        var originalMode = (bool)buttonMode.GetValue(wheel);
        var originalOpen = wheel.IsOpen;
        var originalClosed = (bool)closed.GetValue(wheel);
        var originalCounter = counter.activeSelf;
        try
        {
            pager.Close();
            buttonMode.SetValue(wheel, true);
            closed.SetValue(wheel, true);
            wheel.Open();
            Check(wheel.IsOpen && !counter.activeSelf, "Button-opened paged wheel replaces the counter even after an explicit close.");
            AccessTools.Method(typeof(EmoteDonut), "Close").Invoke(wheel, null);
            Check(!wheel.IsOpen && counter.activeSelf, "Closing the paged wheel restores the counter.");
            buttonMode.SetValue(wheel, false);
            wheel.Open();
            Check(wheel.IsOpen && counter.activeSelf, "Hover-opened paged wheel keeps the counter visible.");
        }
        finally
        {
            pager.Close();
            buttonMode.SetValue(wheel, originalMode);
            if (originalOpen) pager.Open();
            closed.SetValue(wheel, originalClosed);
            counter.SetActive(originalCounter);
        }
    }
}
