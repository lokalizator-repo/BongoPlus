using System;

namespace BongoCatMod;

internal static class BalanceMath
{
    public const int Maximum = int.MaxValue - 1;

    public static (int Gained, int Spent) SetBalance(int gained, int spent, int target)
    {
        if (target < 0 || target > Maximum)
            throw new ArgumentOutOfRangeException(nameof(target));
        if (gained < 0 || spent < 0 || gained < spent)
            throw new ArgumentException("Steam counters are inconsistent. Reload the game before changing balance.");

        var nextGained = Math.Max(gained, (long)spent + target);
        if (nextGained > Maximum)
            throw new ArgumentOutOfRangeException(nameof(target),
                "Steam counters can only increase. Maximum available balance is " + Math.Max(0, Maximum - spent).ToString("N0") + ".");

        return ((int)nextGained, (int)nextGained - target);
    }
}
