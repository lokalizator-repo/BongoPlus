using System;
using System.Collections.Generic;
using System.Linq;

namespace BongoCatMod;

internal enum Feature { Catalog, Appearance, Sharing, Emotes, Wheel, Balance, Automation, Gifts, Previews, Input }

internal sealed class FeatureCompatibility
{
    private readonly Dictionary<Feature, string> _failures = new Dictionary<Feature, string>();
    private readonly Dictionary<Feature, Feature[]> _dependencies = new Dictionary<Feature, Feature[]>
    {
        [Feature.Appearance] = new[] { Feature.Catalog },
        [Feature.Sharing] = new[] { Feature.Appearance },
        [Feature.Emotes] = new[] { Feature.Catalog },
        [Feature.Wheel] = new[] { Feature.Catalog },
        [Feature.Automation] = new[] { Feature.Balance },
        [Feature.Gifts] = new[] { Feature.Automation },
        [Feature.Previews] = new[] { Feature.Catalog }
    };

    public bool KnownBuild { get; set; }
    public string BuildHash { get; set; } = "unavailable";
    public Action<Feature, string>? Disabled;

    public bool Available(Feature feature) => Reason(feature) == null;

    public string? Reason(Feature feature)
    {
        if (_failures.TryGetValue(feature, out var error)) return error;
        if (_dependencies.TryGetValue(feature, out var dependencies))
            foreach (var dependency in dependencies)
            {
                var reason = Reason(dependency);
                if (reason != null) return dependency + ": " + reason;
            }
        return null;
    }

    public void Require(Feature feature)
    {
        var reason = Reason(feature);
        if (reason != null) throw new NotSupportedException(feature + " is unavailable: " + reason);
    }

    public void Disable(Feature feature, string reason)
    {
        if (_failures.ContainsKey(feature)) return;
        _failures.Add(feature, reason);
        Disabled?.Invoke(feature, reason);
    }

    public void Check(Feature feature, Action contract)
    {
        try { contract(); }
        catch (Exception error) { Disable(feature, error.GetBaseException().Message); }
    }

    public bool Execute(Feature feature, Action action)
    {
        if (!Available(feature)) return false;
        try { action(); return true; }
        catch (Exception error) { Disable(feature, error.GetBaseException().Message); return false; }
    }

    public string Summary => (KnownBuild ? "Known game build" : "Updated game build · API checks enabled") + "\n" +
        string.Join("\n", ((Feature[])Enum.GetValues(typeof(Feature))).Select(feature => feature + ": " +
            (Reason(feature) is string reason ? "unavailable — " + reason : "compatible")));
}
