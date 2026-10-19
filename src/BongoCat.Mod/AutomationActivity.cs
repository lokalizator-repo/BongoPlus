using System.Collections.Generic;

namespace BongoCatMod;

internal sealed class AutomationActivity
{
    private readonly HashSet<string> _pendingGifts = new HashSet<string>();
    public int OwnOpened { get; private set; }
    public int GiftRequests { get; private set; }
    public int GiftReadyCleared { get; private set; }
    public int PendingGifts => _pendingGifts.Count;

    public void OwnCompleted() => OwnOpened++;

    public bool GiftSent(string key)
    {
        if (!_pendingGifts.Add(key)) return false;
        GiftRequests++;
        return true;
    }

    public void ReadyCleared(string key)
    {
        if (_pendingGifts.Remove(key)) GiftReadyCleared++;
    }

    public void LeaveLobby() => _pendingGifts.Clear();
}
