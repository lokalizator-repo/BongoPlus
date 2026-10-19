using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using BongoCat;
using BongoCat.Multiplayer;
using Heathen.SteamworksIntegration;
using Steamworks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BongoCatMod;

internal sealed class AutomationService
{
    private static readonly System.Reflection.FieldInfo Target = GameAccess.RequiredField(typeof(OpenChestForMember), "_targetUser");
    private static readonly System.Reflection.FieldInfo Chest = GameAccess.RequiredField(typeof(OpenChestForMember), "_chestType");
    private static readonly System.Reflection.FieldInfo ChestActive = GameAccess.RequiredField(typeof(OpenChestForMember), "_setChestActive");
    private static readonly System.Reflection.FieldInfo Clicked = GameAccess.RequiredField(typeof(OpenChestForMember), "_hasClicked");
    private readonly ManualLogSource _log;
    private readonly BalanceService _balance;
    private readonly AutomationPolicy _policy = new AutomationPolicy();
    private ShopItem? _pendingOwn;
    private int _pendingOwnCost;
    private float _pendingAt;
    private float _nextScan;
    private float _nextOwn;
    private float _nextGift;
    private float _nextViewRefresh;
    private OpenChestForMember[] _chestViews = Array.Empty<OpenChestForMember>();
    private readonly Dictionary<string, string> _giftStates = new Dictionary<string, string>();
    private readonly Dictionary<string, float> _sentAt = new Dictionary<string, float>();
    private ulong _lobby;

    public bool BuyOwn;
    public bool Gift;
    public bool Cosmetics = true;
    public bool Emotes = true;
    public bool AllPlayers;
    public readonly HashSet<ulong> SelectedPlayers = new HashSet<ulong>();
    public string Status { get; private set; } = "Automation is off.";

    public AutomationService(ManualLogSource log, BalanceService balance)
    {
        _log = log;
        _balance = balance;
    }

    public void Stop(string reason = "Automation stopped.")
    {
        BuyOwn = false;
        Gift = false;
        Status = reason;
        _log.LogInfo(reason);
    }

    public void Tick()
    {
        if (!SteamTools.Interface.IsReady) return;
        var now = Time.realtimeSinceStartup;
        if (now < _nextScan) return;
        _nextScan = now + 0.25f;
        var lobby = LobbyData.Current.IsValid ? LobbyData.Current.SteamId.m_SteamID : 0;
        if (_lobby != lobby)
        {
            _lobby = lobby;
            _policy.NewLobby();
            _giftStates.Clear();
            _sentAt.Clear();
            _chestViews = Array.Empty<OpenChestForMember>();
            _nextViewRefresh = 0;
            SelectedPlayers.Clear();
            Gift = false;
        }
        if ((!BuyOwn && !Gift) || !GameAccess.Ready || _balance.Pending) return;
        if (!_balance.Healthy) { Stop("Steam stats save failed. Reload stats before using automation."); return; }

        if (Gift && lobby != 0) TickGifts(now);

        if (_pendingOwn)
        {
            if (Time.realtimeSinceStartup - _pendingAt > 45f)
                Stop("Chest exchange timed out. No automatic retry.");
            return;
        }
        if (GameAccess.IsOpening(Shop.NormalShop) || GameAccess.IsOpening(Shop.EmoteShop)) return;

        if (BuyOwn && now >= _nextOwn)
        {
            foreach (var shop in new[] { Shop.NormalShop, Shop.EmoteShop })
            {
                if (!shop || !Enabled(shop.ChestType) || !shop.CanGetChest) continue;
                var item = GameAccess.Item(shop);
                if (!item.gameObject.activeInHierarchy) continue;
                _pendingOwn = item;
                _pendingOwnCost = item.Price;
                _pendingAt = Time.realtimeSinceStartup;
                Status = "Opening your " + shop.ChestType.ToString().ToLowerInvariant() + " chest…";
                shop.OnClick();
                return;
            }
        }

    }

    private void TickGifts(float now)
    {
        if (now >= _nextViewRefresh)
        {
            _chestViews = Object.FindObjectsByType<OpenChestForMember>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _nextViewRefresh = now + 1f;
        }
        foreach (var chest in _chestViews)
        {
            if (!chest) continue;
            var userData = (SteamUserData)Target.GetValue(chest);
            if (!userData) continue;
            var user = userData.Data;
            var type = (ChestType)Chest.GetValue(chest);
            if (user.IsMe || !user.IsValid || !MultiplayerLobbyReferences.GameplayInstances.ContainsKey(user)) continue;
            var key = user.SteamId + ":" + type;
            var member = LobbyMemberData.Get(LobbyData.Current, user);
            if (!bool.TryParse(member[type == ChestType.Emote ? "emote_chest_ready" : "chest_ready"], out var ready)) continue;
            var eligible = _policy.Observe(key, ready);
            if (!ready)
            {
                if (_sentAt.Remove(key)) SetGiftState(key, user, type, "recipient no longer advertises this chest as ready");
                continue;
            }
            if (!eligible)
            {
                if (_sentAt.TryGetValue(key, out var sentAt) && now - sentAt > 20f)
                    SetGiftState(key, user, type, "request sent, still ready; no paid retry without a new chest cycle");
                continue;
            }
            if (!Enabled(type)) continue;
            if (!AllPlayers && !SelectedPlayers.Contains(user.SteamId)) continue;
            if (_policy.NeedsUiReset(key))
            {
                chest.Reset();
                ((SetActiveBasedOnLobbyMemberValue)ChestActive.GetValue(chest)).Unfreeze();
            }
            if ((bool)Clicked.GetValue(chest))
            {
                SetGiftState(key, user, type, "already clicked; waiting for the recipient's ready flag to clear");
                continue;
            }
            var shop = type == ChestType.Emote ? Shop.EmoteShop : Shop.NormalShop;
            if (!shop || !shop.CanAffordChest || (long)GameAccess.Balance < GameAccess.Item(shop).Price + (long)_pendingOwnCost)
            {
                SetGiftState(key, user, type, "waiting for enough points");
                continue;
            }
            if (now < _nextGift) continue;
            chest.OnClicked();
            if (!(bool)Clicked.GetValue(chest)) continue;
            _policy.Record(key);
            _sentAt[key] = now;
            SetGiftState(key, user, type, "gift request sent");
            _nextGift = now + 0.5f;
        }
    }

    private void SetGiftState(string key, UserData user, ChestType type, string state)
    {
        if (_giftStates.TryGetValue(key, out var previous) && previous == state) return;
        _giftStates[key] = state;
        Status = type + " · " + user.Name + ": " + state + ".";
        _log.LogInfo(Status);
    }

    public void OnGiftClicked(OpenChestForMember chest, bool wasClicked)
    {
        if (wasClicked || !(bool)Clicked.GetValue(chest)) return;
        var user = ((SteamUserData)Target.GetValue(chest)).Data;
        var type = (ChestType)Chest.GetValue(chest);
        var key = user.SteamId + ":" + type;
        _policy.Record(key);
        _sentAt[key] = Time.realtimeSinceStartup;
        SetGiftState(key, user, type, "gift request sent");
    }

    public static bool WasGiftClicked(OpenChestForMember chest) => (bool)Clicked.GetValue(chest);

    private bool Enabled(ChestType type) => type == ChestType.Emote ? Emotes : Cosmetics;

    public void OnPurchaseFinished(ShopItem item, int itemId)
    {
        if (item != _pendingOwn) return;
        _pendingOwn = null;
        _pendingOwnCost = 0;
        if (itemId < 0)
        {
            Stop("Steam chest exchange failed. No automatic retry.");
            return;
        }
        Status = "Chest opened. Item " + itemId + ".";
        _nextOwn = Time.realtimeSinceStartup + 0.25f;
    }

    public IEnumerable<UserData> Players() => !LobbyData.Current.IsValid
        ? Enumerable.Empty<UserData>()
        : MultiplayerLobbyReferences.GameplayInstances.Keys.Where(user => !user.IsMe);
}
