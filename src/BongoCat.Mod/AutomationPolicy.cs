using System.Collections.Generic;

namespace BongoCatMod;

internal sealed class AutomationPolicy
{
    private readonly HashSet<string> _sent = new HashSet<string>();
    private readonly Dictionary<string, double> _firstReady = new Dictionary<string, double>();

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

    public void Record(string key)
    {
        _sent.Add(key);
    }

    public void NewLobby()
    {
        _sent.Clear();
        _firstReady.Clear();
    }
}
