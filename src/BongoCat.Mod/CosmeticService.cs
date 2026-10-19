using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using BongoCat;
using Heathen.SteamworksIntegration;
using UnityEngine;

namespace BongoCatMod;

internal sealed class CosmeticService
{
    private readonly Dictionary<string, int> _selected = new Dictionary<string, int>();
    private readonly ManualLogSource _log;
    private float _nextRefresh;
    private bool _dirty;
    public bool Publish { get; private set; } = true;
    public string Status { get; private set; } = "Using your normal equipped cosmetics.";

    public CosmeticService(ManualLogSource log) => _log = log;

    public void SetPublish(bool value)
    {
        if (Publish == value) return;
        Publish = value;
        if (!value) RestorePublished();
        Apply();
    }

    public IEnumerable<SteamItemRuntime> Items(string slot, string search) => CatInventory.Instance
        ? CatInventory.Instance.Items.Where(item => item.ItemSlot == slot && item.Icon &&
            (string.IsNullOrWhiteSpace(search) || item.ItemName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
            .OrderBy(item => item.ItemName)
        : Enumerable.Empty<SteamItemRuntime>();

    public void Select(SteamItemRuntime item)
    {
        if (!Cat.Instance) throw new InvalidOperationException("Wait for your cat to load.");
        _selected[item.ItemSlot] = item.Id;
        _dirty = true;
        Apply();
    }

    public bool Selected(SteamItemRuntime item) => _selected.TryGetValue(item.ItemSlot, out var id) && id == item.Id;

    public void Tick()
    {
        if (Time.realtimeSinceStartup < _nextRefresh) return;
        _nextRefresh = Time.realtimeSinceStartup + 2f;
        Apply();
    }

    public void Apply(bool force = false)
    {
        if (!Cat.Instance || !SteamItemIdReference.Inst) return;
        if (_selected.Count == 0) return;
        var lobby = LobbyData.Current;
        var synchronized = Publish && lobby.IsValid;
        foreach (var pair in _selected)
        {
            if (_dirty || force) Cat.Instance.SetItem(SteamItemIdReference.Inst[pair.Value], false);
            if (Publish && lobby.IsValid)
            {
                var me = lobby.Me;
                var value = pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (me[pair.Key] != value)
                {
                    synchronized = false;
                    me[pair.Key] = value;
                    _log.LogInfo("Cosmetic metadata submitted: lobby=" + lobby.SteamId +
                        ", slot=" + pair.Key + ", item=" + value + ".");
                }
            }
        }
        _dirty = false;
        SetStatus(!Publish ? "Local appearance only. Lobby sharing is off."
            : !lobby.IsValid ? "Appearance selected. Join a lobby to share it."
            : synchronized ? "Appearance matches your Steam lobby data."
            : "Appearance sent to Steam. Waiting for lobby data.");
    }

    public void Restore()
    {
        if (!Cat.Instance || !CatCosmetics.Instance) return;
        _selected.Clear();
        RestorePublished();
        foreach (var slot in ItemSlots.Cosmetics) Cat.Instance.ClearItemSlot(slot);
        foreach (var item in CatCosmetics.Instance.EquippedItems) Cat.Instance.SetItem(item, false);
        SetStatus("Restored your normal equipped cosmetics.");
    }

    public void RestorePublished()
    {
        if (!LobbyData.Current.IsValid || !CatCosmetics.Instance) return;
        foreach (var slot in ItemSlots.Cosmetics)
        {
            var item = CatCosmetics.Instance.EquippedItems.FirstOrDefault(i => i.ItemSlot == slot);
            var me = LobbyData.Current.Me;
            me[slot] = (item?.Id ?? -1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private void SetStatus(string value)
    {
        if (Status == value) return;
        Status = value;
        _log.LogInfo("Cosmetics: " + value);
    }
}
