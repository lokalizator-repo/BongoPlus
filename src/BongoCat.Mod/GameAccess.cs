using System;
using System.Reflection;
using System.Collections.Generic;
using BongoCat;
using HarmonyLib;

namespace BongoCatMod;

internal static class GameAccess
{
    private static readonly Dictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>();
    private static readonly Dictionary<string, MethodInfo> Methods = new Dictionary<string, MethodInfo>();
    public static FieldInfo Gained => RequiredField(typeof(Pets), "_currentGained");
    public static FieldInfo Spent => RequiredField(typeof(Pets), "_totalSpent");
    public static FieldInfo PetsInitialized => RequiredField(typeof(Pets), "_init");
    public static MethodInfo RefreshStats => RequiredMethod(typeof(Pets), "UpdateStats");
    public static FieldInfo ShopItem => RequiredField(typeof(Shop), "_shopItem");
    public static FieldInfo OpeningChest => RequiredField(typeof(Shop), "_openingChest");

    public static bool Ready => (Plugin.Instance?.Compatibility.Available(Feature.Balance) ?? false) && Pets.Instance && (bool)PetsInitialized.GetValue(Pets.Instance);
    public static int Balance => Ready ? Math.Max(0, (int)Gained.GetValue(Pets.Instance) - (int)Spent.GetValue(Pets.Instance)) : 0;

    public static FieldInfo RequiredField(Type type, string name)
    {
        var key = type.AssemblyQualifiedName + ":" + name;
        if (Fields.TryGetValue(key, out var field)) return field;
        field = AccessTools.Field(type, name) ?? throw new MissingFieldException(type.FullName, name);
        Fields.Add(key, field);
        return field;
    }

    public static MethodInfo RequiredMethod(Type type, string name)
    {
        var key = type.AssemblyQualifiedName + ":" + name;
        if (Methods.TryGetValue(key, out var method)) return method;
        method = AccessTools.Method(type, name, Type.EmptyTypes) ?? throw new MissingMethodException(type.FullName, name);
        Methods.Add(key, method);
        return method;
    }

    public static ShopItem Item(Shop shop) => (ShopItem)ShopItem.GetValue(shop);
    public static bool IsOpening(Shop shop) => shop && (bool)OpeningChest.GetValue(shop);
}
