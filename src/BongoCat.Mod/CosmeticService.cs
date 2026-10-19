using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using BongoCat;
using BongoCat.Multiplayer;
using Heathen.SteamworksIntegration;
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
    public bool Publish { get; private set; } = true;
    public string Status { get; private set; } = "Using your normal equipped cosmetics.";
    public string EmoteStatus { get; private set; } = "Click an emote to send it.";

    public CosmeticService(ManualLogSource log, ModSettings settings)
    {
        _log = log;
        _settings = settings;
        Publish = settings.ShareAppearance.Value;
        _favorites = new HashSet<int>(CosmeticPreferences.ReadIds(settings.Favorites.Value));
        _presets = CosmeticPreferences.ReadPresets(settings.Presets.Value);
    }

    public void SetPublish(bool value)
    {
        if (Publish == value) return;
        Publish = value;
        _settings.ShareAppearance.Value = value;
        _settings.Save();
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

    public void SendEmote(SteamItemRuntime item)
    {
        if (!item.IsEmote) throw new ArgumentException("Choose a standard emote.", nameof(item));
        if (!Cat.Instance || !Cat.Instance.emoteSpawner || !Cat.Instance.emoteSpawner.gameObject.activeInHierarchy)
            throw new InvalidOperationException("Wait for your cat to load.");
        Cat.Instance.emoteSpawner.SpawnEmoteParticle(item);
        SteamMultiplayer.SendEmote(item);
        var inLobby = LobbyData.Current.IsValid;
        EmoteStatus = item.ItemName + (inLobby ? " — sent to lobby." : " — local emote; join a lobby to share.");
        _log.LogInfo("Emote requested: item=" + item.Id + ", lobby=" +
            (inLobby ? LobbyData.Current.SteamId.ToString() : "none") + ".");
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
        var lobby = LobbyData.Current;
        var synchronized = Publish && lobby.IsValid;
        foreach (var pair in _selected)
        {
            if (_dirty || force)
            {
                if (pair.Value == -2) Cat.Instance.ClearItemSlot(pair.Key);
                else Cat.Instance.SetItem(SteamItemIdReference.Inst[pair.Value], false);
            }
            if (Publish && lobby.IsValid)
            {
                var me = lobby.Me;
                var value = Math.Max(-1, pair.Value).ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (me[pair.Key] != value)
                {
                    synchronized = false;
                    me[pair.Key] = value;
                    _log.LogInfo("Cosmetic metadata submitted: lobby=" + lobby.SteamId +
                        ", slot=" + pair.Key + ", item=" + value + ".");
                }
            }
        }
        if ((_dirty || force) && OnDemandRenderHelper.Instance)
            OnDemandRenderHelper.Instance.ResumeRenderingForDuration(0.2f);
        if (restoredNow)
            _log.LogInfo("Saved appearance restored from local assets at " +
                Time.realtimeSinceStartup.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "s.");
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
        _restorePending = false;
        foreach (var entry in _settings.Appearance.Values) entry.Value = -1;
        _settings.Save();
        RestorePublished();
        foreach (var slot in _settings.Appearance.Keys) Cat.Instance.ClearItemSlot(slot);
        foreach (var item in CatCosmetics.Instance.EquippedItems.Where(i => _settings.Appearance.ContainsKey(i.ItemSlot)))
            Cat.Instance.SetItem(item, false);
        SetStatus("Restored your normal equipped cosmetics.");
    }

    public void RestorePublished()
    {
        if (!LobbyData.Current.IsValid || !CatCosmetics.Instance) return;
        foreach (var slot in _settings.Appearance.Keys)
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
