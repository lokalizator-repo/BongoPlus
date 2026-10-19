using System;

namespace BongoCatMod;

internal static class WheelLayout
{
    public const int PageSize = 8;
    public static int PageCount(int count) => count <= 0 ? 1 : (count - 1) / PageSize + 1;

    public static Plan Arrange(int count, double originX, double originY, double width, double height,
        double iconSize, double radius, double uiScale, int page = 0)
    {
        page = Math.Max(0, Math.Min(page, PageCount(count) - 1));
        var slots = Math.Min(PageSize, Math.Max(0, count - page * PageSize));
        var points = new (double X, double Y)[slots];
        var half = iconSize / 2;
        var step = slots > 1 ? 140d / (slots - 1) * Math.PI / 180 : 0;
        radius = Math.Max(radius, iconSize);
        for (var i = 1; i < slots; i++)
        {
            var a = -70 * Math.PI / 180 + (i - 1) * step;
            var b = a + step;
            var separation = Math.Max(Math.Abs(Math.Sin(b) - Math.Sin(a)), Math.Abs(Math.Cos(b) - Math.Cos(a)));
            radius = Math.Max(radius, (iconSize + 8 * uiScale) / separation);
        }
        var minX = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var minY = double.PositiveInfinity;
        var maxY = double.NegativeInfinity;
        for (var i = 0; i < slots; i++)
        {
            var angle = slots == 1 ? 0 : -70 * Math.PI / 180 + i * step;
            points[i] = (Math.Sin(angle) * radius, Math.Cos(angle) * radius);
            minX = Math.Min(minX, points[i].X - half);
            maxX = Math.Max(maxX, points[i].X + half);
            minY = Math.Min(minY, points[i].Y - half);
            maxY = Math.Max(maxY, points[i].Y + half);
        }
        if (slots == 0) return new Plan(points, originX, originY, 1);
        var navY = minY - 24 * uiScale;
        if (PageCount(count) > 1)
        {
            minX = Math.Min(minX, -72 * uiScale);
            maxX = Math.Max(maxX, 72 * uiScale);
            minY = navY - 14 * uiScale;
        }
        var margin = Math.Min(12, Math.Min(width, height) / 4);
        var scale = Math.Min(1, Math.Min((width - margin * 2) / (maxX - minX),
            (height - margin * 2) / (maxY - minY)));
        var x = Math.Max(margin - minX * scale, Math.Min(width - margin - maxX * scale, originX));
        var y = Math.Max(margin - minY * scale, Math.Min(height - margin - maxY * scale, originY));
        for (var i = 0; i < slots; i++) points[i] = (x + points[i].X * scale, y + points[i].Y * scale);
        return new Plan(points, x, y + navY * scale, scale);
    }

    internal sealed class Plan
    {
        public readonly (double X, double Y)[] Slots;
        public readonly double NavigationX;
        public readonly double NavigationY;
        public readonly double Scale;

        public Plan((double X, double Y)[] slots, double navigationX, double navigationY, double scale)
        {
            Slots = slots;
            NavigationX = navigationX;
            NavigationY = navigationY;
            Scale = scale;
        }
    }
}
