using BongoCatMod;

internal static class CompatibilityTests
{
    private sealed class ContractFixture
    {
        private int _counter = 1;
        public static string Global = "fixture";
        public int Counter => _counter;
        public void Set(int counter) => _counter = counter;
        public void Set(string counter) => _counter = int.Parse(counter);
        public int Changed() => _counter;
    }

    private static bool Rejects(Action action)
    {
        try { action(); return false; }
        catch (MissingMemberException) { return true; }
        catch (NotSupportedException) { return true; }
    }

    internal static void Run(Action<bool, string> check)
    {
        var fixture = typeof(ContractFixture);
        check(ApiContract.Field(fixture, "_counter", typeof(int)).IsPrivate &&
            ApiContract.Method(fixture, "Set", typeof(void), false, typeof(int)).GetParameters()[0].ParameterType == typeof(int),
            "Compatibility checks resolve private fields and exact overload signatures");
        check(Rejects(() => ApiContract.Field(fixture, "_renamedCounter", typeof(int))) &&
            Rejects(() => ApiContract.Field(fixture, "_counter", typeof(long))) &&
            Rejects(() => ApiContract.Field(fixture, "Global", typeof(string))),
            "Renamed fields, changed widths and static/instance changes are rejected before access");
        check(Rejects(() => ApiContract.Method(fixture, "Changed", typeof(void), false)) &&
            Rejects(() => ApiContract.Property(fixture, "Counter", typeof(int), true)),
            "Changed return types and missing write accessors cannot satisfy an API contract");

        var features = new FeatureCompatibility { KnownBuild = false, BuildHash = "new-game-build" };
        check(Enum.GetValues<Feature>().All(features.Available), "A different assembly fingerprint alone does not disable any feature");
        var disabledEvents = 0;
        features.Disabled = (_, _) => disabledEvents++;
        features.Check(Feature.Wheel, () => ApiContract.Field(fixture, "oldWheelEntries", typeof(Dictionary<int, int>)));
        features.Disable(Feature.Wheel, "second error");
        var calls = 0;
        check(!features.Execute(Feature.Wheel, () => calls++) && calls == 0 && disabledEvents == 1 &&
            features.Available(Feature.Appearance) && features.Available(Feature.Automation),
            "A broken wheel is skipped without stopping appearance or purchases and emits one failure notification");
        features.Execute(Feature.Sharing, () => throw new MissingMethodException("Lobby metadata setter changed"));
        check(!features.Available(Feature.Sharing) && features.Available(Feature.Appearance) &&
            features.Execute(Feature.Appearance, () => calls++),
            "A runtime lobby error disables sharing while local cosmetics continue");

        var payments = new FeatureCompatibility();
        payments.Disable(Feature.Balance, "Counter storage changed to Int64");
        check(!payments.Available(Feature.Automation) && !payments.Available(Feature.Gifts) &&
            payments.Available(Feature.Catalog) && payments.Available(Feature.Wheel),
            "Incompatible stats block dependent spending paths while cosmetic features remain available");
        check(Rejects(() => payments.Require(Feature.Gifts)), "UI actions cannot bypass a failed dependency contract");

        var liveContracts = new FeatureCompatibility();
        GameApiContracts.Check(liveContracts);
        check(Enum.GetValues<Feature>().All(liveContracts.Available),
            "All feature contracts match the installed game assemblies: " +
            string.Join("; ", Enum.GetValues<Feature>().Where(f => !liveContracts.Available(f)).Select(f => f + "=" + liveContracts.Reason(f))));
        check(ContractFixture.Global == "fixture", "Metadata verification leaves game-like state untouched");
    }
}
