using System;

namespace BongoCatMod;

internal enum ControlIcon { Add, Remove, Previous, Next, Favorite, Catalog, Presets, Wheel, Skin, Hat, Emote, Share, Reset, Close, Save, Trash, Settings, Lobby, Palette }

internal static class ControlButtonArt
{
    public const int Resolution = 4;
    private static readonly System.Collections.Generic.Dictionary<ControlIcon, double[][]> Paths = new System.Collections.Generic.Dictionary<ControlIcon, double[][]>
    {
        [ControlIcon.Catalog] = new[] { new double[] { -6,-6,-1,-6,-1,-1,-6,-1,-6,-6 }, new double[] { 2,-6,7,-6,7,-1,2,-1,2,-6 }, new double[] { -6,2,-1,2,-1,7,-6,7,-6,2 }, new double[] { 2,2,7,2,7,7,2,7,2,2 } },
        [ControlIcon.Presets] = new[] { new double[] { -6,-6,3,-6,3,3,-6,3,-6,-6 }, new double[] { -3,6,6,6,6,-3 } },
        [ControlIcon.Skin] = new[] { new double[] { -7,-3,-7,7,-2,4,2,4,7,7,7,-3,4,-6,-4,-6,-7,-3 }, new double[] { -3,0,-3,-1 }, new double[] { 3,0,3,-1 }, new double[] { -2,-3,0,-4,2,-3 } },
        [ControlIcon.Hat] = new[] { new double[] { -8,-5,8,-5 }, new double[] { -5,-5,-4,6,4,6,5,-5 }, new double[] { -5,-2,5,-2 } },
        [ControlIcon.Emote] = new[] { new double[] { -4,-1,-2,-3,2,-3,4,-1 }, new double[] { -3,3,-3,2 }, new double[] { 3,3,3,2 } },
        [ControlIcon.Share] = new[] { new double[] { -5,0,5,5 }, new double[] { -5,0,5,-5 } },
        [ControlIcon.Reset] = new[] { new double[] { -6,2,-6,7,-1,7 } },
        [ControlIcon.Close] = new[] { new double[] { -4,-4,4,4 }, new double[] { -4,4,4,-4 } },
        [ControlIcon.Save] = new[] { new double[] { -6,-6,6,-6,6,3,3,6,-6,6,-6,-6 }, new double[] { -3,6,-3,1,2,1,2,6 }, new double[] { -3,-6,-3,-2,3,-2,3,-6 } },
        [ControlIcon.Trash] = new[] { new double[] { -6,4,6,4 }, new double[] { -4,4,-3,-6,3,-6,4,4 }, new double[] { -2,4,-2,7,2,7,2,4 }, new double[] { -1,1,-1,-3 }, new double[] { 1,1,1,-3 } },
        [ControlIcon.Lobby] = new[] { new double[] { -7,-6,-7,-3,-5,-1,-1,-1,1,-3,1,-6 }, new double[] { 3,-1,6,-1,8,-3,8,-6 } },
        [ControlIcon.Palette] = new[] { new double[] { -5,3,-5,3 }, new double[] { -1,5,-1,5 }, new double[] { 3,4,3,4 }, new double[] { 5,0,5,0 }, new double[] { 2,-6,-1,-3,2,-1 } }
    };

    public static byte[] Render(ControlIcon icon, int width, int height, bool background = true)
    {
        var pixels = new byte[width * height * Resolution * Resolution * 4];
        var radius = Math.Min(6, Math.Min(width, height) * 0.23);
        var glyph = Math.Min(width, height) / 24d;
        for (var y = 0; y < height * Resolution; y++)
        for (var x = 0; x < width * Resolution; x++)
        {
            var px = (x + 0.5) / Resolution - width / 2d;
            var py = (y + 0.5) / Resolution - height / 2d;
            var outer = background ? Coverage(RoundedBox(px, py, width / 2d - 0.75, height / 2d - 0.75, radius)) : 0;
            var inner = Coverage(RoundedBox(px, py, width / 2d - 2, height / 2d - 2, radius - 1.25));
            var line = icon == ControlIcon.Add || icon == ControlIcon.Remove
                ? Segment(px, py, -4 * glyph, 0, 4 * glyph, 0) - 0.9 * glyph
                : Chevron(px, py, icon == ControlIcon.Previous ? -1 : 1, glyph);
            if (icon == ControlIcon.Add)
                line = Math.Min(line, Segment(px, py, 0, -4 * glyph, 0, 4 * glyph) - 0.9 * glyph);
            if (icon == ControlIcon.Favorite)
            {
                line = double.PositiveInfinity;
                for (var point = 0; point < 10; point++)
                {
                    var angle = Math.PI / 2 + point * Math.PI / 5;
                    var next = angle + Math.PI / 5;
                    var radiusA = (point % 2 == 0 ? 6 : 2.8) * glyph;
                    var radiusB = (point % 2 == 0 ? 2.8 : 6) * glyph;
                    line = Math.Min(line, Segment(px, py, Math.Cos(angle) * radiusA, Math.Sin(angle) * radiusA,
                        Math.Cos(next) * radiusB, Math.Sin(next) * radiusB) - 0.7 * glyph);
                }
            }
            if (icon > ControlIcon.Favorite) line = Symbol(icon, px / glyph, py / glyph) * glyph;
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

    private static double Symbol(ControlIcon icon, double x, double y)
    {
        var distance = double.PositiveInfinity;
        if (Paths.TryGetValue(icon, out var paths))
            foreach (var path in paths)
                for (var i = 0; i < path.Length - 2; i += 2)
                    distance = Math.Min(distance, Segment(x, y, path[i], path[i + 1], path[i + 2], path[i + 3]) - 0.7);
        double Circle(double cx, double cy, double radius) => Math.Abs(Math.Sqrt((x-cx)*(x-cx)+(y-cy)*(y-cy)) - radius) - 0.7;
        if (icon == ControlIcon.Emote) distance = Math.Min(distance, Circle(0, 0, 7));
        if (icon == ControlIcon.Share)
            distance = Math.Min(distance, Math.Min(Circle(-5,0,1.8), Math.Min(Circle(5,5,1.8), Circle(5,-5,1.8))));
        if (icon == ControlIcon.Lobby) distance = Math.Min(distance, Math.Min(Circle(-3,3,2.6), Circle(5,3,2)));
        if (icon == ControlIcon.Wheel || icon == ControlIcon.Settings)
        {
            distance = Math.Min(distance, Circle(0, 0, icon == ControlIcon.Wheel ? 2 : 2.5));
            for (var i = 0; i < 8; i++)
            {
                var angle = i * Math.PI / 4;
                var cx = Math.Cos(angle);
                var cy = Math.Sin(angle);
                distance = Math.Min(distance, icon == ControlIcon.Wheel ? Circle(cx * 6, cy * 6, 1.3)
                    : Segment(x,y,cx*5,cy*5,cx*7,cy*7)-1.1);
            }
            if (icon == ControlIcon.Settings) distance = Math.Min(distance, Circle(0,0,5));
        }
        if (icon == ControlIcon.Reset)
        {
            var angle = Math.Atan2(y,x);
            if (angle < 2.6) distance = Math.Min(distance, Circle(0,0,6));
        }
        if (icon == ControlIcon.Palette) distance = Math.Min(distance, Circle(0,0,7));
        return distance;
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
        var length = dx * dx + dy * dy;
        var t = length == 0 ? 0 : Math.Max(0, Math.Min(1, ((x - ax) * dx + (y - ay) * dy) / length));
        var px = x - ax - t * dx;
        var py = y - ay - t * dy;
        return Math.Sqrt(px * px + py * py);
    }
}
