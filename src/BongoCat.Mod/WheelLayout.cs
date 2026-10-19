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
        var spread = 20d * slots * Math.PI / 180;
        var step = slots > 1 ? spread / (slots - 1) : 0;
        radius = Math.Max(radius, 1);
        var minX = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var minY = double.PositiveInfinity;
        var maxY = double.NegativeInfinity;
        for (var i = 0; i < slots; i++)
        {
            var angle = slots == 1 ? 0 : -spread / 2 + i * step;
            points[i] = (Math.Sin(angle) * radius, Math.Cos(angle) * radius);
            minX = Math.Min(minX, points[i].X - half);
            maxX = Math.Max(maxX, points[i].X + half);
            minY = Math.Min(minY, points[i].Y - half);
            maxY = Math.Max(maxY, points[i].Y + half);
        }
        if (slots == 0) return new Plan(points, (originX, originY), (originX, originY), (originX, originY), 1);
        var previous = (X: points[0].X - half - 20 * uiScale, Y: points[0].Y);
        var next = (X: points[slots - 1].X + half + 20 * uiScale, Y: points[slots - 1].Y);
        var counter = (X: 0d, Y: maxY + 18 * uiScale);
        if (PageCount(count) > 1)
        {
            minX = Math.Min(previous.X - 14 * uiScale, counter.X - 28 * uiScale);
            maxX = Math.Max(next.X + 14 * uiScale, counter.X + 28 * uiScale);
            minY = Math.Min(minY, previous.Y - 14 * uiScale);
            maxY = counter.Y + 12 * uiScale;
        }
        var margin = Math.Min(12, Math.Min(width, height) / 4);
        var scale = Math.Min(1, Math.Min((width - margin * 2) / (maxX - minX),
            (height - margin * 2) / (maxY - minY)));
        var x = Math.Max(margin - minX * scale, Math.Min(width - margin - maxX * scale, originX));
        var y = Math.Max(margin - minY * scale, Math.Min(height - margin - maxY * scale, originY));
        for (var i = 0; i < slots; i++) points[i] = (x + points[i].X * scale, y + points[i].Y * scale);
        (double X, double Y) ToScreen((double X, double Y) point) => (x + point.X * scale, y + point.Y * scale);
        return new Plan(points, ToScreen(previous), ToScreen(next), ToScreen(counter), scale);
    }

    internal sealed class Plan
    {
        public readonly (double X, double Y)[] Slots;
        public readonly (double X, double Y) Previous;
        public readonly (double X, double Y) Next;
        public readonly (double X, double Y) Counter;
        public readonly double Scale;

        public Plan((double X, double Y)[] slots, (double X, double Y) previous,
            (double X, double Y) next, (double X, double Y) counter, double scale)
        {
            Slots = slots;
            Previous = previous;
            Next = next;
            Counter = counter;
            Scale = scale;
        }
    }
}
