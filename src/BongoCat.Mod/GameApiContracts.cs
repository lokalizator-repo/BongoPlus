using System;
using System.Collections;
using System.Collections.Generic;
using BongoCat;
using BongoCat.Multiplayer;
using Heathen.SteamworksIntegration;
using UnityEngine;
using UnityEngine.UI;

namespace BongoCatMod;

internal static class GameApiContracts
{
    public static void Check(FeatureCompatibility features)
    {
        features.Check(Feature.Catalog, Catalog);
        features.Check(Feature.Appearance, Appearance);
        features.Check(Feature.Sharing, Sharing);
        features.Check(Feature.Emotes, Emotes);
        features.Check(Feature.Wheel, Wheel);
        features.Check(Feature.Balance, Balance);
        features.Check(Feature.Automation, Automation);
        features.Check(Feature.Gifts, Gifts);
        features.Check(Feature.Previews, Previews);
        features.Check(Feature.Input, () => ApiContract.Property(typeof(Helper.InputFieldCache), "FocusingInputField", typeof(bool)));
    }

    private static void Catalog()
    {
        ApiContract.Field(typeof(CatInventory), "Instance", typeof(CatInventory), true);
        ApiContract.Field(typeof(CatInventory), "Items", typeof(List<SteamItemRuntime>));
        ApiContract.Field(typeof(CatInventory), "IsInitialized", typeof(bool));
        ApiContract.Field(typeof(SteamItemIdReference), "Inst", typeof(SteamItemIdReference), true);
        ApiContract.Field(typeof(SteamItemIdReference), "Ids", typeof(List<int>));
        ApiContract.Property(typeof(SteamItemIdReference), "Item", typeof(SteamItemRuntime), false, typeof(int));
        ApiContract.Property(typeof(SteamItemRuntime), "Id", typeof(int));
        ApiContract.Property(typeof(SteamItemRuntime), "ItemName", typeof(string));
        ApiContract.Property(typeof(SteamItemRuntime), "IsEmote", typeof(bool));
        ApiContract.Field(typeof(SteamItemRuntime), "ItemSlot", typeof(string));
        ApiContract.Field(typeof(SteamItemRuntime), "Icon", typeof(Sprite));
    }

    private static void Appearance()
    {
        ApiContract.Field(typeof(Cat), "Instance", typeof(Cat), true);
        ApiContract.Method(typeof(Cat), "SetItem", typeof(void), false, typeof(SteamItemRuntime), typeof(bool));
        ApiContract.Method(typeof(Cat), "ClearItemSlot", typeof(void), false, typeof(string));
        ApiContract.Field(typeof(CatCosmetics), "Instance", typeof(CatCosmetics), true);
        ApiContract.Field(typeof(CatCosmetics), "EquippedItems", typeof(List<SteamItemRuntime>));
        ApiContract.Method(typeof(MemoryImageCache), "GetSprite", typeof(Sprite), false, typeof(string));
    }

    private static void Sharing()
    {
        ApiContract.Field(typeof(LobbyData), "Current", typeof(LobbyData), true);
        ApiContract.Property(typeof(LobbyData), "IsValid", typeof(bool));
        ApiContract.Property(typeof(LobbyData), "Me", typeof(LobbyMemberData));
        ApiContract.Property(typeof(LobbyMemberData), "Item", typeof(string), true, typeof(string));
    }

    private static void Emotes()
    {
        ApiContract.Method(typeof(EmoteSpawner), "SpawnEmoteParticle", typeof(void), false, typeof(SteamItemRuntime));
        ApiContract.Method(typeof(SteamMultiplayer), "SendEmote", typeof(void), true, typeof(SteamItemRuntime));
        ApiContract.Field(typeof(Cat), "emoteSpawner", typeof(EmoteSpawner));
    }

    private static void Wheel()
    {
        ApiContract.Field(typeof(EmoteDonut), "IDtoEmoteEntry", typeof(Dictionary<int, EmoteDonutEntry>));
        ApiContract.Field(typeof(EmoteDonut), "maxEmoteCount", typeof(int));
        ApiContract.Field(typeof(EmoteDonut), "emoteEntryParent", typeof(Transform));
        ApiContract.Field(typeof(EmoteDonut), "radius", typeof(float));
        ApiContract.Field(typeof(EmoteDonut), "_isOpen", typeof(bool));
        ApiContract.Field(typeof(EmoteDonut), "_activelyClosed", typeof(bool));
        ApiContract.Field(typeof(EmoteDonut), "_showEmoteWheel", typeof(PlayerPrefsToggle));
        ApiContract.Field(typeof(EmoteDonut), "_menu", typeof(GameObject));
        foreach (var name in new[] { "Open", "Close", "RearrangeEmotes", "Update", "Validate" })
            ApiContract.Method(typeof(EmoteDonut), name, typeof(void), false);
        ApiContract.Method(typeof(EmoteDonut), "Start", typeof(IEnumerator), false);
        ApiContract.Method(typeof(EmoteDonut), "RemoveEmote", typeof(void), false, typeof(SteamItemRuntime));
        ApiContract.Method(typeof(EmoteDonut), "ToggleItem", typeof(void), false, typeof(SteamItemRuntime));
    }

    private static void Balance()
    {
        ApiContract.Field(typeof(Pets), "_currentGained", typeof(int));
        ApiContract.Field(typeof(Pets), "_totalSpent", typeof(int));
        ApiContract.Field(typeof(Pets), "_init", typeof(bool));
        ApiContract.Method(typeof(Pets), "UpdateStats", typeof(void), false);
    }

    private static void Automation()
    {
        ApiContract.Field(typeof(Shop), "_shopItem", typeof(ShopItem));
        ApiContract.Field(typeof(Shop), "_openingChest", typeof(bool));
        ApiContract.Method(typeof(Shop), "OnClick", typeof(void), false);
        ApiContract.Property(typeof(Shop), "CanGetChest", typeof(bool));
        ApiContract.Property(typeof(Shop), "CanAffordChest", typeof(bool));
        ApiContract.Field(typeof(ShopItem), "Price", typeof(int));
        ApiContract.Method(typeof(ShopItem), "Callback", typeof(void), false, typeof(int), typeof(bool));
    }

    private static void Gifts()
    {
        Sharing();
        ApiContract.Field(typeof(OpenChestForMember), "_targetUser", typeof(SteamUserData));
        ApiContract.Field(typeof(OpenChestForMember), "_chestType", typeof(ChestType));
        ApiContract.Field(typeof(OpenChestForMember), "_setChestActive", typeof(SetActiveBasedOnLobbyMemberValue));
        ApiContract.Field(typeof(OpenChestForMember), "_hasClicked", typeof(bool));
        ApiContract.Method(typeof(OpenChestForMember), "OnClicked", typeof(void), false);
        ApiContract.Method(typeof(OpenChestForMember), "Reset", typeof(void), false);
        ApiContract.Method(typeof(SetActiveBasedOnLobbyMemberValue), "Unfreeze", typeof(void), false);
        ApiContract.Method(typeof(LobbyMemberData), "Get", typeof(LobbyMemberData), true, typeof(LobbyData), typeof(UserData));
    }

    private static void Previews()
    {
        ApiContract.Method(typeof(MemoryImageCache), "GetSprite", typeof(Sprite), false, typeof(string));
        foreach (var name in new[] { "_bodyImage", "_rightPawImage", "_hatImage" }) ApiContract.Field(typeof(Cat), name, typeof(Image));
        foreach (var name in new[] { "_catNormalDefault", "_catNormalFrontDefault" }) ApiContract.Field(typeof(Cat), name, typeof(Sprite));
        ApiContract.Field(typeof(SteamItemRuntime), "FullImage", typeof(Sprite));
    }
}
