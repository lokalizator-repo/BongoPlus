using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using BongoCat;
using Newtonsoft.Json;
using Steamworks;

namespace BongoCatMod;

internal sealed class BalanceService : IDisposable
{
    private readonly ManualLogSource _log;
    private Callback<UserStatsStored_t>? _stored;
    private Snapshot? _original;
    private bool _waiting;
    private float _requestedAt;

    public string Status { get; private set; } = "No balance changes this session.";
    public bool Pending => _waiting;
    public bool Healthy { get; private set; } = true;
    public bool HasBackup => _original != null;

    public BalanceService(ManualLogSource log)
    {
        _log = log;
    }

    public void Set(int target)
    {
        Plugin.Instance!.Require(Feature.Balance);
        if (!GameAccess.Ready || _waiting)
            throw new InvalidOperationException("Wait for Steam stats to finish loading or saving.");

        if (!SteamUserStats.GetStat("BongoTap", out int oldGained) || !SteamUserStats.GetStat("BongoMinus", out int oldSpent))
            throw new InvalidOperationException("Steam could not read BongoTap/BongoMinus. Wait for stats to load.");
        var next = BalanceMath.SetBalance(oldGained, oldSpent, target);
        SaveBackup(oldGained, oldSpent);
        Write(next.Gained, next.Spent, oldGained, oldSpent);
    }

    public void Restore()
    {
        Plugin.Instance!.Require(Feature.Balance);
        if (_original == null)
            throw new InvalidOperationException("No balance backup in this session.");
        if (!GameAccess.Ready || _waiting)
            throw new InvalidOperationException("Wait for Steam stats to finish loading or saving.");

        Set(Math.Max(0, _original.Gained - _original.Spent));
    }

    private void SaveBackup(int gained, int spent)
    {
        if (_original != null) return;
        var snapshot = new Snapshot { Gained = gained, Spent = spent, SteamId = SteamUser.GetSteamID().m_SteamID };
        var folder = Path.Combine(Paths.ConfigPath, Plugin.Id, "backups");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json"),
            JsonConvert.SerializeObject(snapshot));
        _original = snapshot;
    }

    private void Write(int gained, int spent, int oldGained, int oldSpent)
    {
        var changeGained = gained != oldGained;
        var changeSpent = spent != oldSpent;
        if (gained < oldGained || spent < oldSpent || (changeGained && changeSpent))
            throw new InvalidOperationException("Balance changes must update one increasing Steam counter.");

        var stat = changeGained ? "BongoTap" : "BongoMinus";
        var value = changeGained ? gained : spent;
        _log.LogInfo($"Balance request: BongoTap {oldGained} -> {gained}, BongoMinus {oldSpent} -> {spent}.");
        if ((changeGained || changeSpent) && !SteamUserStats.SetStat(stat, value))
        {
            _log.LogWarning($"SetStat rejected {stat}={value}. Both counters are increment-only in the installed schema.");
            throw new InvalidOperationException("Steam refused " + stat + " = " + value + ". See the log for counter values.");
        }

        GameAccess.Gained.SetValue(Pets.Instance, gained);
        GameAccess.Spent.SetValue(Pets.Instance, spent);
        GameAccess.RefreshStats.Invoke(Pets.Instance, null);
        _waiting = true;
        Healthy = true;
        _requestedAt = UnityEngine.Time.realtimeSinceStartup;
        Status = SteamUserStats.StoreStats()
            ? "Balance updated; waiting for Steam save."
            : "Balance cached; Steam save deferred. Waiting for the game's next save.";
        _log.LogInfo(Status);
    }

    private void OnStored(UserStatsStored_t result)
    {
        if (!(Plugin.Instance?.Available(Feature.Balance) ?? false)) return;
        if (!_waiting || result.m_nGameID != 3419430UL) return;
        _waiting = false;
        Healthy = result.m_eResult == EResult.k_EResultOK;
        if (!Healthy && SteamUserStats.GetStat("BongoTap", out int gained) && SteamUserStats.GetStat("BongoMinus", out int spent))
        {
            GameAccess.Gained.SetValue(Pets.Instance, gained);
            GameAccess.Spent.SetValue(Pets.Instance, spent);
            GameAccess.RefreshStats.Invoke(Pets.Instance, null);
        }
        Status = result.m_eResult == EResult.k_EResultOK
            ? "Steam confirmed the stats save."
            : "Steam save failed: " + result.m_eResult + ". Stop automation and reload stats.";
        _log.LogInfo(Status);
    }

    public void Tick()
    {
        if (_stored == null && SteamTools.Interface.IsReady)
            _stored = Callback<UserStatsStored_t>.Create(OnStored);
        if (_waiting && UnityEngine.Time.realtimeSinceStartup - _requestedAt > 30f)
            Status = "Steam save is still pending. Balance controls are paused.";
    }

    public void Dispose() => _stored?.Dispose();

    private sealed class Snapshot
    {
        public ulong SteamId;
        public int Gained;
        public int Spent;
    }
}
