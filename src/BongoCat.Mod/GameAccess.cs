using System;
using System.Reflection;
using BongoCat;
using HarmonyLib;

namespace BongoCatMod;

internal static class GameAccess
{
    public static readonly FieldInfo Gained = RequiredField(typeof(Pets), "_currentGained");
    public static readonly FieldInfo Spent = RequiredField(typeof(Pets), "_totalSpent");
    public static readonly FieldInfo PetsInitialized = RequiredField(typeof(Pets), "_init");
    public static readonly MethodInfo RefreshStats = RequiredMethod(typeof(Pets), "UpdateStats");
    public static readonly FieldInfo ShopItem = RequiredField(typeof(Shop), "_shopItem");
    public static readonly FieldInfo OpeningChest = RequiredField(typeof(Shop), "_openingChest");

    public static bool Ready => Pets.Instance && (bool)PetsInitialized.GetValue(Pets.Instance);
    public static int Balance => Ready ? Math.Max(0, (int)Gained.GetValue(Pets.Instance) - (int)Spent.GetValue(Pets.Instance)) : 0;

    public static FieldInfo RequiredField(Type type, string name) =>
        AccessTools.Field(type, name) ?? throw new MissingFieldException(type.FullName, name);

    public static MethodInfo RequiredMethod(Type type, string name) =>
        AccessTools.Method(type, name) ?? throw new MissingMethodException(type.FullName, name);

    public static ShopItem Item(Shop shop) => (ShopItem)ShopItem.GetValue(shop);
    public static bool IsOpening(Shop shop) => shop && (bool)OpeningChest.GetValue(shop);
}
