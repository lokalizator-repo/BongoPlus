using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BongoCatMod;

internal sealed class AppearancePreset
{
    public string Name { get; }
    public int Skin { get; }
    public int Hat { get; }

    public AppearancePreset(string name, int skin, int hat)
    {
        Name = name.Trim();
        Skin = skin;
        Hat = hat;
    }
}

internal static class CosmeticPreferences
{
    public static List<int> ReadIds(string value) => value.Split(',')
        .Select(part => int.TryParse(part.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : 0)
        .Where(id => id > 0).Distinct().ToList();

    public static string WriteIds(IEnumerable<int> values) => string.Join(",", values.Where(id => id > 0)
        .Distinct().Select(id => id.ToString(CultureInfo.InvariantCulture)));

    public static List<AppearancePreset> ReadPresets(string value)
    {
        var result = new List<AppearancePreset>();
        foreach (var row in value.Split('|'))
        {
            var parts = row.Split(':');
            if (parts.Length != 3 || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var skin) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hat) || skin < -2 || hat < -2)
                continue;
            string name;
            try { name = Uri.UnescapeDataString(parts[0]).Trim(); }
            catch (UriFormatException) { continue; }
            if (name.Length == 0 || name.Length > 40 || result.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                continue;
            result.Add(new AppearancePreset(name, skin, hat));
        }
        return result;
    }

    public static string WritePresets(IEnumerable<AppearancePreset> presets) => string.Join("|", presets.Select(p =>
        Uri.EscapeDataString(p.Name) + ":" + p.Skin.ToString(CultureInfo.InvariantCulture) + ":" + p.Hat.ToString(CultureInfo.InvariantCulture)));

    public static int[] Order(IEnumerable<int> available, IEnumerable<int> preferred)
    {
        var ids = available.Distinct().ToArray();
        var present = new HashSet<int>(ids);
        var ordered = preferred.Where(present.Contains).Distinct().ToList();
        var seen = new HashSet<int>(ordered);
        ordered.AddRange(ids.Where(id => !seen.Contains(id)));
        return ordered.ToArray();
    }

    public static int[] Move(IEnumerable<int> available, int id, int targetIndex)
    {
        var ids = available.Distinct().ToList();
        if (!ids.Remove(id)) return ids.ToArray();
        ids.Insert(Math.Max(0, Math.Min(targetIndex, ids.Count)), id);
        return ids.ToArray();
    }
}
