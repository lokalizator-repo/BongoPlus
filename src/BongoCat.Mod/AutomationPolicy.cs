using System;
using System.Collections.Generic;

namespace BongoCatMod;

internal sealed class AutomationPolicy
{
    private readonly HashSet<string> _sent = new HashSet<string>();
    private readonly Dictionary<string, double> _firstReady = new Dictionary<string, double>();

    public int Spent { get; private set; }
    public int Limit { get; set; } = 10000;
    public bool CanSpend(int price) => price > 0 && price <= Limit - Spent;

    public bool Observe(string key, bool ready, double now)
    {
        if (!ready)
        {
            _sent.Remove(key);
            _firstReady.Remove(key);
            return false;
        }
        if (!_firstReady.TryGetValue(key, out var since))
        {
            _firstReady[key] = now;
            return false;
        }
        return !_sent.Contains(key) && now - since >= 2d;
    }

    public void Record(string key, int price)
    {
        if (!CanSpend(price)) throw new InvalidOperationException("Session spending limit reached.");
        _sent.Add(key);
        Spent += price;
    }

    public void NewLobby()
    {
        _sent.Clear();
        _firstReady.Clear();
    }
}
