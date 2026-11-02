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
    private static FieldInfo Entries => GameAccess.RequiredField(typeof(EmoteDonut), "IDtoEmoteEntry");
    private static FieldInfo Capacity => GameAccess.RequiredField(typeof(EmoteDonut), "maxEmoteCount");
    private readonly ModSettings _settings;
    private readonly ManualLogSource _log;
    private readonly HashSet<int> _pinned;
    private int[] _order;
    private EmoteDonut? _readyWheel;
    private float _nextRefresh;
    private int _originalCapacity;

    public string Status { get; private set; } = "Add your owned emotes to the expanded native wheel.";

    public EmoteWheelService(ModSettings settings, ManualLogSource log)
    {
        _settings = settings;
        _log = log;
        _pinned = settings.ReadWheelEmotes();
        _order = CosmeticPreferences.ReadIds(settings.WheelOrder.Value).ToArray();
    }

    public void OnReady(EmoteDonut wheel)
    {
        if (!(Plugin.Instance?.Available(Feature.Wheel) ?? false)) return;
        _readyWheel = wheel;
        _originalCapacity = (int)Capacity.GetValue(wheel);
        Capacity.SetValue(wheel, int.MaxValue);
        var pager = wheel.GetComponent<WheelPager>() ?? wheel.gameObject.AddComponent<WheelPager>();
        pager.Initialize(wheel);
    }

    public bool Contains(EmoteDonut wheel, int id) => GetEntries(wheel).ContainsKey(id);
    public bool Contains(int id) => (Plugin.Instance?.Available(Feature.Wheel) ?? false) && EmoteDonut.Instance && Contains(EmoteDonut.Instance, id);

    public int[] OrderIds(IEnumerable<int> available) => CosmeticPreferences.Order(available, _order);

    public int[] ItemIds => (Plugin.Instance?.Available(Feature.Wheel) ?? false) && EmoteDonut.Instance ? OrderIds(GetEntries(EmoteDonut.Instance).Keys) : Array.Empty<int>();

    public void Move(int id, int targetIndex)
    {
        Plugin.Instance!.Require(Feature.Wheel);
        _order = CosmeticPreferences.Move(ItemIds, id, targetIndex);
        _settings.WheelOrder.Value = CosmeticPreferences.WriteIds(_order);
        _settings.Save();
        EmoteDonut.Instance?.GetComponent<WheelPager>()?.Refresh();
        Status = "Wheel order saved.";
    }

    public void ObserveToggle(SteamItemRuntime item, bool wasInWheel)
    {
        if (!wasInWheel || Contains(item.Id)) return;
        _pinned.Remove(item.Id);
        _order = _order.Where(id => id != item.Id).ToArray();
        Save();
        Status = item.ItemName + " — removed from wheel.";
    }

    public void Toggle(SteamItemRuntime item)
    {
        Plugin.Instance!.Require(Feature.Wheel);
        if (!item.IsEmote && !Contains(item.Id)) throw new ArgumentException("Choose a standard emote.", nameof(item));
        var wheel = EmoteDonut.Instance;
        if (!wheel || _readyWheel != wheel || !CatInventory.Instance || !CatInventory.Instance.IsInitialized)
            throw new InvalidOperationException("Wait for your emote wheel and inventory to load.");
        if (Contains(wheel, item.Id))
        {
            wheel.ToggleItem(item);
            Status = item.ItemName + " — removed from wheel.";
            return;
        }
        if (CatInventory.Instance.FetchingItems || !item.IsOwned)
            throw new InvalidOperationException("Only emotes in your loaded Steam inventory can be added to the wheel.");

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
        EmoteDonut.Instance?.GetComponent<WheelPager>()?.Refresh();
        Status = item.ItemName + " — added to native wheel.";
        _log.LogInfo("Native wheel emote added: item=" + item.Id + ".");
    }

    public void Tick()
    {
        if (!(Plugin.Instance?.Available(Feature.Wheel) ?? false)) return;
        if (Time.realtimeSinceStartup < _nextRefresh) return;
        _nextRefresh = Time.realtimeSinceStartup + 2f;
        var wheel = EmoteDonut.Instance;
        if (!wheel || _readyWheel != wheel || !CatInventory.Instance || !CatInventory.Instance.IsInitialized || CatInventory.Instance.FetchingItems) return;
        var owned = CatInventory.Instance.Items.Where(item => item.IsEmote && item.IsOwned).ToDictionary(item => item.Id);
        var removed = _pinned.RemoveWhere(id => !owned.ContainsKey(id));
        foreach (var id in GetEntries(wheel).Keys.ToArray())
        {
            var item = SteamItemIdReference.Inst[id];
            if (item.IsOwned) continue;
            wheel.ToggleItem(item);
            removed++;
        }
        foreach (var id in _pinned.OrderBy(id => id).ToArray())
        {
            if (Contains(wheel, id)) continue;
            var item = owned[id];
            if (!item.Icon) continue;
            wheel.ToggleItem(item);
            _log.LogInfo("Native wheel emote restored: item=" + id + ".");
        }
        var order = ItemIds;
        if (removed > 0 || !_order.SequenceEqual(order))
        {
            _order = order;
            Save();
            wheel.GetComponent<WheelPager>()?.Refresh();
        }
        if (removed > 0)
        {
            Status = "Wheel updated to match your owned Steam emotes.";
            _log.LogInfo("Removed unavailable saved wheel items: " + removed + ".");
        }
    }

    private static Dictionary<int, EmoteDonutEntry> GetEntries(EmoteDonut wheel) =>
        (Dictionary<int, EmoteDonutEntry>)Entries.GetValue(wheel);

    public void Deactivate()
    {
        if (!_readyWheel) return;
        _readyWheel!.GetComponent<WheelPager>()?.RestoreNative();
        Capacity.SetValue(_readyWheel, _originalCapacity);
        _readyWheel = null;
    }

    private void Save()
    {
        _settings.SetWheelEmotes(_pinned);
        _settings.WheelOrder.Value = CosmeticPreferences.WriteIds(_order);
        _settings.Save();
    }
}
