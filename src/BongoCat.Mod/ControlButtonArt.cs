using System;

namespace BongoCatMod;

internal enum ControlIcon { Add, Remove, Previous, Next }

internal static class ControlButtonArt
{
    public const int Resolution = 4;

    public static byte[] Render(ControlIcon icon, int width, int height)
    {
        var pixels = new byte[width * height * Resolution * Resolution * 4];
        var radius = Math.Min(6, Math.Min(width, height) * 0.23);
        var glyph = Math.Min(width, height) / 24d;
        for (var y = 0; y < height * Resolution; y++)
        for (var x = 0; x < width * Resolution; x++)
        {
            var px = (x + 0.5) / Resolution - width / 2d;
            var py = (y + 0.5) / Resolution - height / 2d;
            var outer = Coverage(RoundedBox(px, py, width / 2d - 0.75, height / 2d - 0.75, radius));
            var inner = Coverage(RoundedBox(px, py, width / 2d - 2, height / 2d - 2, radius - 1.25));
            var line = icon == ControlIcon.Add || icon == ControlIcon.Remove
                ? Segment(px, py, -4 * glyph, 0, 4 * glyph, 0) - 0.9 * glyph
                : Chevron(px, py, icon == ControlIcon.Previous ? -1 : 1, glyph);
            if (icon == ControlIcon.Add)
                line = Math.Min(line, Segment(px, py, 0, -4 * glyph, 0, 4 * glyph) - 0.9 * glyph);
            var ink = Coverage(line);
            var alpha = ink + outer * (1 - ink);
            if (alpha <= 0) continue;
            var fill = outer > 0 ? Math.Min(1, inner / outer) : 0;
            var offset = (y * width * Resolution + x) * 4;
            pixels[offset] = Channel(27 + 45 * fill, 239, ink, outer, alpha);
            pixels[offset + 1] = Channel(34 + 48 * fill, 243, ink, outer, alpha);
            pixels[offset + 2] = Channel(39 + 52 * fill, 245, ink, outer, alpha);
            pixels[offset + 3] = (byte)Math.Round(alpha * 255);
        }
        return pixels;
    }

    private static byte Channel(double background, double foreground, double ink, double outer, double alpha) =>
        (byte)Math.Round((foreground * ink + background * outer * (1 - ink)) / alpha);

    public static byte[] RenderSelection(int width, int height)
    {
        var pixels = new byte[width * height * Resolution * Resolution * 4];
        for (var y = 0; y < height * Resolution; y++)
        for (var x = 0; x < width * Resolution; x++)
        {
            var px = (x + 0.5) / Resolution - width / 2d;
            var py = (y + 0.5) / Resolution - height / 2d;
            var outer = Coverage(RoundedBox(px, py, width / 2d - 0.8, height / 2d - 0.8, 8));
            var inner = Coverage(RoundedBox(px, py, width / 2d - 2.8, height / 2d - 2.8, 6));
            var offset = (y * width * Resolution + x) * 4;
            pixels[offset] = 158;
            pixels[offset + 1] = 201;
            pixels[offset + 2] = 185;
            pixels[offset + 3] = (byte)Math.Round(Math.Max(0, outer - inner) * 255);
        }
        return pixels;
    }

    private static double Coverage(double distance) => Math.Max(0, Math.Min(1, 0.5 - distance * Resolution));

    private static double RoundedBox(double x, double y, double halfWidth, double halfHeight, double radius)
    {
        var dx = Math.Abs(x) - (halfWidth - radius);
        var dy = Math.Abs(y) - (halfHeight - radius);
        return Math.Sqrt(Math.Max(dx, 0) * Math.Max(dx, 0) + Math.Max(dy, 0) * Math.Max(dy, 0)) +
            Math.Min(Math.Max(dx, dy), 0) - radius;
    }

    private static double Chevron(double x, double y, int direction, double scale) =>
        Math.Min(Segment(x, y, -direction * 2 * scale, -4 * scale, direction * 2 * scale, 0),
            Segment(x, y, direction * 2 * scale, 0, -direction * 2 * scale, 4 * scale)) - 0.9 * scale;

    private static double Segment(double x, double y, double ax, double ay, double bx, double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var t = Math.Max(0, Math.Min(1, ((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy)));
        var px = x - ax - t * dx;
        var py = y - ay - t * dy;
        return Math.Sqrt(px * px + py * py);
    }
}
