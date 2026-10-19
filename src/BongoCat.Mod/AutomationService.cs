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
    private static readonly System.Reflection.FieldInfo Clicked = GameAccess.RequiredField(typeof(OpenChestForMember), "_hasClicked");
    private readonly ManualLogSource _log;
    private readonly BalanceService _balance;
    private readonly AutomationPolicy _policy = new AutomationPolicy();
    private ShopItem? _pendingOwn;
    private float _pendingAt;
    private float _nextAction;
    private ulong _lobby;

    public bool BuyOwn;
    public bool Gift;
    public bool Cosmetics = true;
    public bool Emotes;
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
        if (Time.realtimeSinceStartup < _nextAction) return;
        _nextAction = Time.realtimeSinceStartup + 1f;
        var lobby = LobbyData.Current.IsValid ? LobbyData.Current.SteamId.m_SteamID : 0;
        if (_lobby != lobby)
        {
            _lobby = lobby;
            _policy.NewLobby();
            SelectedPlayers.Clear();
            Gift = false;
        }
        if (_pendingOwn)
        {
            if (Time.realtimeSinceStartup - _pendingAt > 45f)
                Stop("Chest exchange timed out. No automatic retry.");
            return;
        }
        if ((!BuyOwn && !Gift) || !GameAccess.Ready || _balance.Pending) return;
        if (!_balance.Healthy) { Stop("Steam stats save failed. Reload stats before using automation."); return; }
        if (GameAccess.IsOpening(Shop.NormalShop) || GameAccess.IsOpening(Shop.EmoteShop)) return;

        if (BuyOwn)
        {
            foreach (var shop in new[] { Shop.NormalShop, Shop.EmoteShop })
            {
                if (!shop || !Enabled(shop.ChestType) || !shop.CanGetChest) continue;
                var item = GameAccess.Item(shop);
                if (!item.gameObject.activeInHierarchy) continue;
                _pendingOwn = item;
                _pendingAt = Time.realtimeSinceStartup;
                Status = "Opening your " + shop.ChestType.ToString().ToLowerInvariant() + " chest…";
                shop.OnClick();
                return;
            }
        }

        if (!Gift || lobby == 0) return;
        foreach (var chest in Object.FindObjectsByType<OpenChestForMember>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var user = ((SteamUserData)Target.GetValue(chest)).Data;
            var type = (ChestType)Chest.GetValue(chest);
            if (user.IsMe || !Enabled(type)) continue;
            var key = user.SteamId + ":" + type;
            var member = LobbyMemberData.Get(LobbyData.Current, user);
            var ready = member[type == ChestType.Emote ? "emote_chest_ready" : "chest_ready"] == "true";
            if (!_policy.Observe(key, ready, Time.realtimeSinceStartup) || !chest.isActiveAndEnabled || (bool)Clicked.GetValue(chest)) continue;
            if (!AllPlayers && !SelectedPlayers.Contains(user.SteamId)) continue;
            var shop = type == ChestType.Emote ? Shop.EmoteShop : Shop.NormalShop;
            if (!shop || !shop.CanAffordChest) continue;
            chest.OnClicked();
            if (!(bool)Clicked.GetValue(chest)) continue;
            _policy.Record(key);
            Status = "Gift request sent to " + user.Name + ".";
            _log.LogInfo(Status);
            _nextAction = Time.realtimeSinceStartup + 3f;
            return;
        }
    }

    private bool Enabled(ChestType type) => type == ChestType.Emote ? Emotes : Cosmetics;

    public void OnPurchaseFinished(ShopItem item, int itemId)
    {
        if (item != _pendingOwn) return;
        _pendingOwn = null;
        if (itemId < 0)
        {
            Stop("Steam chest exchange failed. No automatic retry.");
            return;
        }
        Status = "Chest opened. Item " + itemId + ".";
        _nextAction = Time.realtimeSinceStartup + 3f;
    }

    public IEnumerable<UserData> Players() => !LobbyData.Current.IsValid
        ? Enumerable.Empty<UserData>()
        : MultiplayerLobbyReferences.GameplayInstances.Keys.Where(user => !user.IsMe);
}
