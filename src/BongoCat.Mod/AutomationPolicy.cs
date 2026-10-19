using System.Collections.Generic;

namespace BongoCatMod;

internal sealed class AutomationPolicy
{
    private readonly HashSet<string> _sent = new HashSet<string>();
    private readonly HashSet<string> _newCycle = new HashSet<string>();

    public bool Observe(string key, bool ready)
    {
        if (!ready)
        {
            _sent.Remove(key);
            _newCycle.Add(key);
            return false;
        }
        return !_sent.Contains(key);
    }

    public bool NeedsUiReset(string key) => _newCycle.Contains(key);

    public void Record(string key)
    {
        _sent.Add(key);
        _newCycle.Remove(key);
    }

    public void NewLobby()
    {
        _sent.Clear();
        _newCycle.Clear();
    }
}
