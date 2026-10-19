using System;

namespace BongoCatMod;

internal static class MenuLayout
{
    public static double Scale(double width, double height, double preferred) =>
        Math.Max(0.01, Math.Min(preferred, Math.Min(Math.Max(1, width - 24) / 420, Math.Max(1, height - 24) / 560)));
}
