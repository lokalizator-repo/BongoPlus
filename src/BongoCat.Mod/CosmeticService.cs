using System;
using System.Collections.Generic;
using System.Linq;
using BongoCat;
using Heathen.SteamworksIntegration;
using UnityEngine;

namespace BongoCatMod;

internal sealed class CosmeticService
{
    private readonly Dictionary<string, int> _selected = new Dictionary<string, int>();
    private float _nextRefresh;
    private bool _dirty;
    public bool Publish;
    public string Status { get; private set; } = "Using your normal equipped cosmetics.";

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
        Status = item.ItemName + (Publish ? " — lobby appearance requested." : " — local appearance.");
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
        foreach (var pair in _selected)
        {
            if (_dirty || force) Cat.Instance.SetItem(SteamItemIdReference.Inst[pair.Value], false);
            if (Publish && LobbyData.Current.IsValid)
            {
                var me = LobbyData.Current.Me;
                var value = pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (me[pair.Key] != value) me[pair.Key] = value;
            }
        }
        _dirty = false;
    }

    public void Restore()
    {
        if (!Cat.Instance || !CatCosmetics.Instance) return;
        _selected.Clear();
        RestorePublished();
        foreach (var slot in ItemSlots.Cosmetics) Cat.Instance.ClearItemSlot(slot);
        foreach (var item in CatCosmetics.Instance.EquippedItems) Cat.Instance.SetItem(item, false);
        Status = "Restored your normal equipped cosmetics.";
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
}
