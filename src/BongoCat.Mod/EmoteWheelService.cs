using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using BongoCat;
using UnityEngine;

namespace BongoCatMod;

internal sealed class EmoteWheelService
{
    private static readonly FieldInfo Entries = GameAccess.RequiredField(typeof(EmoteDonut), "IDtoEmoteEntry");
    private static readonly FieldInfo Capacity = GameAccess.RequiredField(typeof(EmoteDonut), "maxEmoteCount");
    private readonly ModSettings _settings;
    private readonly ManualLogSource _log;
    private readonly HashSet<int> _pinned;
    private EmoteDonut? _readyWheel;
    private int _validationDepth;
    private float _nextRefresh;

    public string Status { get; private set; } = "Select emotes to add to your native wheel.";

    public EmoteWheelService(ModSettings settings, ManualLogSource log)
    {
        _settings = settings;
        _log = log;
        _pinned = settings.ReadWheelEmotes();
    }

    public void OnReady(EmoteDonut wheel)
    {
        _readyWheel = wheel;
        Capacity.SetValue(wheel, int.MaxValue);
        var pager = wheel.GetComponent<WheelPager>() ?? wheel.gameObject.AddComponent<WheelPager>();
        pager.Initialize(wheel);
    }
    public void BeginValidation() => _validationDepth++;
    public void EndValidation() => _validationDepth = Math.Max(0, _validationDepth - 1);
    public bool KeepDuringValidation(SteamItemRuntime item) =>
        _validationDepth > 0 && item.IsEmote && _pinned.Contains(item.Id);

    public bool Contains(EmoteDonut wheel, int id) => GetEntries(wheel).ContainsKey(id);
    public bool Contains(int id) => EmoteDonut.Instance && Contains(EmoteDonut.Instance, id);

    public void ObserveToggle(SteamItemRuntime item, bool wasInWheel)
    {
        if (!wasInWheel || Contains(item.Id) || !_pinned.Remove(item.Id)) return;
        Save();
        Status = item.ItemName + " — removed from wheel.";
    }

    public void Toggle(SteamItemRuntime item)
    {
        if (!item.IsEmote) throw new ArgumentException("Choose a standard emote.", nameof(item));
        var wheel = EmoteDonut.Instance;
        if (!wheel || _readyWheel != wheel || !CatInventory.Instance || !CatInventory.Instance.IsInitialized)
            throw new InvalidOperationException("Wait for your emote wheel and inventory to load.");
        if (Contains(wheel, item.Id))
        {
            wheel.ToggleItem(item);
            Status = item.ItemName + " — removed from wheel.";
            return;
        }

        var added = _pinned.Add(item.Id);
        try
        {
            wheel.ToggleItem(item);
            if (!Contains(wheel, item.Id)) throw new InvalidOperationException("The game could not add this emote.");
        }
        catch
        {
            if (added) _pinned.Remove(item.Id);
            throw;
        }
        Save();
        Status = item.ItemName + " — added to native wheel.";
        _log.LogInfo("Native wheel emote added: item=" + item.Id + ".");
    }

    public void Tick()
    {
        if (Time.realtimeSinceStartup < _nextRefresh) return;
        _nextRefresh = Time.realtimeSinceStartup + 2f;
        var wheel = EmoteDonut.Instance;
        if (!wheel || _readyWheel != wheel || !CatInventory.Instance || !CatInventory.Instance.IsInitialized) return;
        foreach (var id in _pinned.OrderBy(id => id).ToArray())
        {
            if (Contains(wheel, id)) continue;
            var item = CatInventory.Instance.Items.FirstOrDefault(i => i.Id == id && i.IsEmote);
            if (item == null || !item.Icon) continue;
            wheel.ToggleItem(item);
            _log.LogInfo("Native wheel emote restored: item=" + id + ".");
        }
    }

    private static Dictionary<int, EmoteDonutEntry> GetEntries(EmoteDonut wheel) =>
        (Dictionary<int, EmoteDonutEntry>)Entries.GetValue(wheel);

    private void Save()
    {
        _settings.SetWheelEmotes(_pinned);
        _settings.Save();
    }
}
