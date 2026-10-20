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
    private static System.Reflection.FieldInfo Target => GameAccess.RequiredField(typeof(OpenChestForMember), "_targetUser");
    private static System.Reflection.FieldInfo Chest => GameAccess.RequiredField(typeof(OpenChestForMember), "_chestType");
    private static System.Reflection.FieldInfo ChestActive => GameAccess.RequiredField(typeof(OpenChestForMember), "_setChestActive");
    private static System.Reflection.FieldInfo Clicked => GameAccess.RequiredField(typeof(OpenChestForMember), "_hasClicked");
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
    private readonly ModSettings _settings;
    private bool _resumeOwn;
    private bool _resumeGift;
    private readonly Queue<string> _recent = new Queue<string>();
    public AutomationActivity Activity { get; } = new AutomationActivity();
    public string? Error { get; private set; }
    public bool CanRetry => (Plugin.Instance?.Available(Feature.Automation) ?? false) && Error != null && !_pendingOwn && _balance.Healthy && !_balance.Pending &&
        GameAccess.Ready && !GameAccess.IsOpening(Shop.NormalShop) && !GameAccess.IsOpening(Shop.EmoteShop);
    public string Recent => string.Join("\n", _recent);
    public string Summary => "This session · Opened: " + Activity.OwnOpened + " · Gifts sent: " + Activity.GiftRequests +
        "\nGift ready cleared: " + Activity.GiftReadyCleared + " · Awaiting ready change: " + Activity.PendingGifts;

    public bool BuyOwn;
    public bool Gift;
    public bool Cosmetics = true;
    public bool Emotes = true;
    public bool AllPlayers;
    public readonly HashSet<ulong> SelectedPlayers = new HashSet<ulong>();
    public string Status { get; private set; } = "Automation is off.";

    public AutomationService(ManualLogSource log, BalanceService balance, ModSettings settings)
    {
        _log = log;
        _balance = balance;
        _settings = settings;
        BuyOwn = settings.BuyOwn.Value && (Plugin.Instance?.Available(Feature.Automation) ?? false);
        Gift = settings.Gift.Value && (Plugin.Instance?.Available(Feature.Gifts) ?? false);
        Cosmetics = settings.CosmeticChests.Value;
        Emotes = settings.EmoteChests.Value;
        AllPlayers = settings.AllPlayers.Value;
        SelectedPlayers.UnionWith(settings.ReadRecipients());
    }

    public void Stop(string reason = "Automation stopped.")
    {
        BuyOwn = false;
        Gift = false;
        Error = null;
        _resumeOwn = _resumeGift = false;
        Status = reason;
        _log.LogInfo(reason);
    }

    private void Fail(string reason)
    {
        if (Error == null) { _resumeOwn = BuyOwn; _resumeGift = Gift; }
        BuyOwn = Gift = false;
        _settings.BuyOwn.Value = _settings.Gift.Value = false;
        _settings.Save();
        Error = reason;
        Record(reason);
    }

    public void Retry()
    {
        Plugin.Instance!.Require(Feature.Automation);
        if (!CanRetry) throw new InvalidOperationException("Wait for the pending exchange and Steam stats to finish. Reload the game if Steam stats failed.");
        BuyOwn = _resumeOwn;
        Gift = _resumeGift && Plugin.Instance!.Available(Feature.Gifts);
        _settings.BuyOwn.Value = BuyOwn;
        _settings.Gift.Value = Gift;
        _settings.Save();
        Error = null;
        _nextScan = _nextOwn = _nextGift = 0;
        Record("Automation resumed. Existing gift requests will not be paid again.");
    }

    private void Record(string message)
    {
        Status = message;
        if (_recent.Count == 5) _recent.Dequeue();
        _recent.Enqueue(DateTime.Now.ToString("HH:mm:ss") + "  " + message);
        _log.LogInfo(message);
    }

    public void Tick()
    {
        if (!(Plugin.Instance?.Available(Feature.Automation) ?? false)) return;
        if (!SteamTools.Interface.IsReady) return;
        var now = Time.realtimeSinceStartup;
        if (now < _nextScan) return;
        _nextScan = now + 0.25f;
        Plugin.Instance!.Execute(Feature.Gifts, () => TickLobby(now));
        var lobby = _lobby;
        if (_pendingOwn && now - _pendingAt > 45f && Error == null)
            Fail("Chest exchange timed out. Waiting for its final result before retry is available.");
        if (Error != null) return;
        if ((!BuyOwn && !Gift) || !GameAccess.Ready || _balance.Pending) return;
        if (!_balance.Healthy) { Fail("Steam stats save failed. Restart the game to reload stats."); return; }

        if (_pendingOwn) return;
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
        Status = !Cosmetics && !Emotes ? "Choose at least one chest type."
            : Gift && !BuyOwn && lobby == 0 ? "Join a lobby to send gifts."
            : Gift && !BuyOwn && !AllPlayers && SelectedPlayers.Count == 0 ? "Choose recipients or enable everyone in the lobby."
            : BuyOwn && new[] { Shop.NormalShop, Shop.EmoteShop }.Any(shop => shop && Enabled(shop.ChestType) && shop.ChestIsReady && !shop.CanAffordChest)
                ? "Waiting for enough points." : "Watching for ready chests.";
    }

    private void TickLobby(float now)
    {
        var lobby = LobbyData.Current.IsValid ? LobbyData.Current.SteamId.m_SteamID : 0;
        if (_lobby != lobby)
        {
            _lobby = lobby;
            _policy.NewLobby();
            _giftStates.Clear();
            _sentAt.Clear();
            Activity.LeaveLobby();
            _chestViews = Array.Empty<OpenChestForMember>();
            _nextViewRefresh = 0;
        }
        if (lobby != 0 && (Gift || _sentAt.Count > 0))
            TickGifts(now, Gift && Error == null && GameAccess.Ready && !_balance.Pending && _balance.Healthy);
    }

    private void TickGifts(float now, bool allowSend)
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
                if (_sentAt.Remove(key))
                {
                    Activity.ReadyCleared(key);
                    SetGiftState(key, user, type, "recipient no longer advertises this chest as ready");
                }
                continue;
            }
            if (!eligible)
            {
                if (_sentAt.TryGetValue(key, out var sentAt) && now - sentAt > 20f)
                    SetGiftState(key, user, type, "request sent, still ready; no paid retry without a new chest cycle");
                continue;
            }
            if (!allowSend || !Enabled(type)) continue;
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
            Activity.GiftSent(key);
            SetGiftState(key, user, type, "gift request sent");
            _nextGift = now + 0.5f;
        }
    }

    private void SetGiftState(string key, UserData user, ChestType type, string state)
    {
        if (_giftStates.TryGetValue(key, out var previous) && previous == state) return;
        _giftStates[key] = state;
        Record(type + " · " + user.Name + ": " + state + ".");
    }

    public void OnGiftClicked(OpenChestForMember chest, bool wasClicked)
    {
        if (wasClicked || !(bool)Clicked.GetValue(chest)) return;
        var user = ((SteamUserData)Target.GetValue(chest)).Data;
        var type = (ChestType)Chest.GetValue(chest);
        var key = user.SteamId + ":" + type;
        _policy.Record(key);
        _sentAt[key] = Time.realtimeSinceStartup;
        Activity.GiftSent(key);
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
            Fail("Steam chest exchange failed after the game's retries. Resume to watch for the next ready chest.");
            return;
        }
        Activity.OwnCompleted();
        Record("Chest opened. Item " + itemId + ".");
        if (Error != null) Error = "The previous exchange completed. You can resume automation.";
        _nextOwn = Time.realtimeSinceStartup + 0.25f;
    }

    public IEnumerable<UserData> Players() => (Plugin.Instance?.Available(Feature.Gifts) ?? false)
        ? PlayersFromLobby() : Enumerable.Empty<UserData>();

    private static IEnumerable<UserData> PlayersFromLobby() => !LobbyData.Current.IsValid
        ? Enumerable.Empty<UserData>()
        : MultiplayerLobbyReferences.GameplayInstances.Keys.Where(user => !user.IsMe);
}
