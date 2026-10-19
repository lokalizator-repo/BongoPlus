using System;

namespace BongoCatMod;

internal static class BalanceMath
{
    public const int Maximum = int.MaxValue - 1;

    public static (int Gained, int Spent) SetBalance(int gained, int spent, int target)
    {
        if (target < 0 || target > Maximum)
            throw new ArgumentOutOfRangeException(nameof(target));

        if (spent >= 0 && (long)spent + target <= Maximum)
            return (spent + target, spent);

        return (target, 0);
    }

    public static (int Gained, int Spent) MakeRoom(int gained, int spent, int amount)
    {
        if (amount <= 0 || (long)gained + amount <= Maximum || spent <= 0)
            return (gained, spent);

        var reclaimed = Math.Min(gained, spent);
        return (gained - reclaimed, spent - reclaimed);
    }
}
