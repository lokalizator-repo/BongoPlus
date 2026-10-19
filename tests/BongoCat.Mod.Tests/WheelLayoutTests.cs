using BongoCatMod;

internal static class WheelLayoutTests
{
    internal static void Run(Action<bool, string> check)
    {
        check(WheelLayout.PageCount(8) == 1 && WheelLayout.PageCount(9) == 2 &&
            WheelLayout.PageCount(10000) == 1250, "Wheel pagination imposes no eight-item selection limit");
        var random = new Random(3419430);
        var bounded = true;
        var separated = true;
        var centered = true;
        for (var i = 0; i < 10000; i++)
        {
            var width = random.Next(320, 3841);
            var height = random.Next(240, 2161);
            var uiScale = random.NextDouble() * 2.5 + 0.5;
            var icon = 64 * uiScale;
            var count = random.Next(1, 10001);
            var page = random.Next(WheelLayout.PageCount(count));
            var plan = WheelLayout.Arrange(count, random.Next(-500, width + 500),
                random.Next(-500, height + 500), width, height, icon, 100 * uiScale, uiScale, page);
            var half = icon * plan.Scale / 2;
            bounded &= plan.Slots.Length == Math.Min(8, count - page * 8) && plan.Scale > 0 && plan.Scale <= 1;
            foreach (var p in plan.Slots)
                bounded &= p.X - half >= 12 - 1e-6 && p.X + half <= width - 12 + 1e-6 &&
                    p.Y - half >= 12 - 1e-6 && p.Y + half <= height - 12 + 1e-6;
            if (WheelLayout.PageCount(count) > 1)
                bounded &= plan.NavigationX - 72 * uiScale * plan.Scale >= 12 - 1e-6 &&
                    plan.NavigationX + 72 * uiScale * plan.Scale <= width - 12 + 1e-6 &&
                    plan.NavigationY - 14 * uiScale * plan.Scale >= 12 - 1e-6 &&
                    plan.NavigationY + 14 * uiScale * plan.Scale <= height - 12 + 1e-6;
            for (var a = 0; a < plan.Slots.Length; a++)
                for (var b = a + 1; b < plan.Slots.Length; b++)
                    separated &= Math.Max(Math.Abs(plan.Slots[a].X - plan.Slots[b].X),
                        Math.Abs(plan.Slots[a].Y - plan.Slots[b].Y)) >= icon * plan.Scale - 1e-6;
            if (plan.Slots.Length > 1)
                centered &= Math.Abs(plan.Slots[0].Y - plan.Slots[^1].Y) < 1e-6;
        }
        check(bounded, "10000 wheel layouts keep every icon and page control within screen margins");
        check(separated, "Wheel icons do not overlap across UI scales and screen sizes");
        check(centered, "Partial pages retain a centered arc");
        check(WheelLayout.Arrange(0, 0, 0, 1920, 1080, 64, 100, 1).Slots.Length == 0,
            "An empty wheel needs no visible slots");
    }
}
