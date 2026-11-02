using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using BongoCat;
using UnityEngine;

namespace BongoCatMod;

internal sealed class CosmeticService
{
    private readonly Dictionary<string, int> _selected = new Dictionary<string, int>();
    private readonly ManualLogSource _log;
    private readonly ModSettings _settings;
    private bool _restorePending = true;
    private float _nextRefresh;
    private bool _dirty;
    private readonly HashSet<int> _favorites;
    private readonly List<AppearancePreset> _presets;
    public IReadOnlyList<AppearancePreset> Presets => _presets;
    public string Status { get; private set; } = "Local fitting room. Other players see your equipped Steam items.";

    public CosmeticService(ManualLogSource log, ModSettings settings)
    {
        _log = log;
        _settings = settings;
        _favorites = new HashSet<int>(CosmeticPreferences.ReadIds(settings.Favorites.Value));
        _presets = CosmeticPreferences.ReadPresets(settings.Presets.Value);
    }

    public IEnumerable<SteamItemRuntime> Items(string slot, string search) => CatInventory.Instance
        ? CatInventory.Instance.Items.Where(item => item.ItemSlot == slot && item.Icon &&
            (slot != "emote" || item.IsOwned) &&
            (string.IsNullOrWhiteSpace(search) || item.ItemName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
            .OrderBy(item => item.ItemName)
        : Enumerable.Empty<SteamItemRuntime>();

    public void Select(SteamItemRuntime item)
    {
        Plugin.Instance!.Require(Feature.Appearance);
        if (!Cat.Instance) throw new InvalidOperationException("Wait for your cat to load.");
        if (!_settings.Appearance.ContainsKey(item.ItemSlot))
            throw new ArgumentException("Choose a skin or hat.", nameof(item));
        _selected[item.ItemSlot] = item.Id;
        _settings.Appearance[item.ItemSlot].Value = item.Id;
        _settings.Save();
        _dirty = true;
        Apply();
    }

    public bool IsFavorite(int id) => _favorites.Contains(id);

    private int CurrentItemId(string slot) => _selected.TryGetValue(slot, out var id) ? id :
        CatCosmetics.Instance ? CatCosmetics.Instance.EquippedItems.FirstOrDefault(item => item.ItemSlot == slot)?.Id ?? -2 : -2;

    public bool Selected(AppearancePreset preset) => CurrentItemId("skin") == preset.Skin && CurrentItemId("hat") == preset.Hat;

    public void ToggleFavorite(int id)
    {
        if (!_favorites.Add(id)) _favorites.Remove(id);
        _settings.Favorites.Value = CosmeticPreferences.WriteIds(_favorites.OrderBy(value => value));
        _settings.Save();
    }

    public void SavePreset(string name)
    {
        name = name.Trim();
        if (name.Length == 0 || name.Length > 40) throw new ArgumentException("Use a preset name with 1–40 characters.");
        if (!Cat.Instance || !CatCosmetics.Instance) throw new InvalidOperationException("Wait for your cat to load.");
        var preset = new AppearancePreset(name, CurrentItemId("skin"), CurrentItemId("hat"));
        var index = _presets.FindIndex(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) _presets[index] = preset;
        else _presets.Add(preset);
        SavePresets();
        SetStatus("Preset saved: " + name + ".");
    }

    public void DeletePreset(AppearancePreset preset)
    {
        _presets.Remove(preset);
        SavePresets();
        SetStatus("Preset deleted: " + preset.Name + ".");
    }

    private void SavePresets()
    {
        _settings.Presets.Value = CosmeticPreferences.WritePresets(_presets);
        _settings.Save();
    }

    public void ApplyPreset(AppearancePreset preset)
    {
        Plugin.Instance!.Require(Feature.Appearance);
        if (!Cat.Instance || !SteamItemIdReference.Inst) throw new InvalidOperationException("Wait for your cat to load.");
        var values = new Dictionary<string, int> { ["skin"] = preset.Skin, ["hat"] = preset.Hat };
        foreach (var pair in values)
            if (pair.Value >= 0 && (!SteamItemIdReference.Inst.Ids.Contains(pair.Value) ||
                SteamItemIdReference.Inst[pair.Value].ItemSlot != pair.Key || !SteamItemIdReference.Inst[pair.Value].Icon))
                throw new InvalidOperationException("This preset contains an unavailable " + pair.Key + ". Save it again with current items.");
        foreach (var pair in values)
        {
            _selected[pair.Key] = pair.Value < 0 ? -2 : pair.Value;
            _settings.Appearance[pair.Key].Value = _selected[pair.Key];
        }
        _restorePending = false;
        _settings.Save();
        _dirty = true;
        Apply();
    }

    public bool Selected(SteamItemRuntime item) => _selected.TryGetValue(item.ItemSlot, out var id)
        ? id == item.Id
        : CatCosmetics.Instance && CatCosmetics.Instance.EquippedItems.Any(equipped => equipped.Id == item.Id);

    public void Tick()
    {
        if (Time.realtimeSinceStartup < _nextRefresh) return;
        _nextRefresh = Time.realtimeSinceStartup + (_restorePending ? 0.1f : 2f);
        Apply();
    }

    public void Apply(bool force = false)
    {
        if (!(Plugin.Instance?.Available(Feature.Appearance) ?? false)) return;
        if (!Cat.Instance || !SteamItemIdReference.Inst || !MemoryImageCache.Instance) return;
        var restoredNow = false;
        if (_restorePending)
        {
            foreach (var entry in _settings.Appearance.Values)
                if (SteamItemIdReference.Inst.Ids.Contains(entry.Value) && !SteamItemIdReference.Inst[entry.Value].Icon)
                    return;
            foreach (var pair in _settings.Appearance)
            {
                var id = pair.Value.Value;
                if (id == -1 || _selected.ContainsKey(pair.Key)) continue;
                if (id != -2 && (!SteamItemIdReference.Inst.Ids.Contains(id) || SteamItemIdReference.Inst[id].ItemSlot != pair.Key))
                {
                    _log.LogWarning("Ignoring saved cosmetic: slot=" + pair.Key + ", item=" + id + ".");
                    continue;
                }
                _selected[pair.Key] = id;
            }
            _restorePending = false;
            _dirty = true;
            restoredNow = true;
        }
        if (_selected.Count == 0) return;
        foreach (var pair in _selected)
        {
            if (_dirty || force)
            {
                if (pair.Value == -2) Cat.Instance.ClearItemSlot(pair.Key);
                else Cat.Instance.SetItem(SteamItemIdReference.Inst[pair.Value], false);
            }
        }
        if ((_dirty || force) && OnDemandRenderHelper.Instance)
            OnDemandRenderHelper.Instance.ResumeRenderingForDuration(0.2f);
        if (restoredNow)
            _log.LogInfo("Saved appearance restored from local assets at " +
                Time.realtimeSinceStartup.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "s.");
        _dirty = false;
        SetStatus("Local preview saved. Other players do not see this appearance.");
    }

    public void Restore()
    {
        Plugin.Instance!.Require(Feature.Appearance);
        if (!Cat.Instance || !CatCosmetics.Instance) return;
        _selected.Clear();
        _restorePending = false;
        foreach (var entry in _settings.Appearance.Values) entry.Value = -1;
        _settings.Save();
        foreach (var slot in _settings.Appearance.Keys) Cat.Instance.ClearItemSlot(slot);
        foreach (var item in CatCosmetics.Instance.EquippedItems.Where(i => _settings.Appearance.ContainsKey(i.ItemSlot)))
            Cat.Instance.SetItem(item, false);
        SetStatus("Restored your normal equipped cosmetics.");
    }

    private void SetStatus(string value)
    {
        if (Status == value) return;
        Status = value;
        _log.LogInfo("Cosmetics: " + value);
    }

}
