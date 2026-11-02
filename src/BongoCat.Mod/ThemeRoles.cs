using System;
using System.Collections.Generic;
using System.Linq;
using BongoCat.UiThemes;
using UnityEngine;

namespace BongoCatMod;

internal static class ThemeRoles
{
    public static SpriteRole Sprite(string name) => Find<SpriteRole>(name);
    public static ColorRole Color(string name) => Find<ColorRole>(name);
    public static FontRole Font(string name) => Find<FontRole>(name);

    private static T Find<T>(string name) where T : ThemeRole
    {
        if (Cache<T>.Roles.TryGetValue(name, out var role) && role) return role;
        role = Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(value => value.name == name);
        if (!role) throw new InvalidOperationException("Game theme role is unavailable: " + typeof(T).Name + "." + name);
        Cache<T>.Roles[name] = role;
        return role;
    }

    private static class Cache<T> where T : ThemeRole
    {
        internal static readonly Dictionary<string, T> Roles = new Dictionary<string, T>(StringComparer.Ordinal);
    }
}
