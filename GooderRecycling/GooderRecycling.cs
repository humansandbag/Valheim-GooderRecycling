using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static Version;
using Object = UnityEngine.Object;

namespace GooderRecycling
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    //[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    //[BepInIncompatibility("")]
    internal class GooderRecycling : BaseUnityPlugin
    {
        public const string PluginGUID = "MainStreetGaming.GooderRecycling";
        public const string PluginName = "GooderRecycling";
        public const string PluginVersion = "1.0.1";

        public static ConfigEntry<bool> _enableDebug;
        public static ConfigEntry<KeyCode> recycleHotKey;
        public static ConfigEntry<bool> returnUnknownRes;

        // Use this class to add your own localization to the game
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        private void Awake()
        {
            // Array of 10 funny messages
            string[] messages = {
                "In the game of life, you either adapt or you install {0}. Choose wisely.",
                "Hold your breath, gamers! {0} is here to either fix everything or create chaos – we're not sure which yet.",
                "Just when you thought things couldn't get crazier, along comes {0} to prove you wrong.",
                "We're not saying {0} is the answer to all your problems, but it's definitely the answer to some of them.",
                "They say curiosity killed the cat, but in our case, it just installed {0}.",
                "Someone fetch the confetti cannon! {0} has arrived, and we're throwing a virtual party!",
                "We're not saying {0} is the key to happiness, but it's a pretty good start!",
                "Welcome to the age of enlightenment, where {0} has replaced logic and reason.",
                "Breaking news: {0} just took a wrong turn and ended up here. Let the chaos begin!",
                "Buckle up, buttercups! {0} just strolled in, and it's ready to rewrite the rules."
            };

            // Generate a random index for the message array
            System.Random random = new System.Random();
            int messageIndex = random.Next(messages.Length);

            // Use String.Format to insert the plugin name into the message
            string message = String.Format(messages[messageIndex], PluginName);

            // Jotunn comes with its own Logger class to provide a consistent Log style for all mods using it
            Jotunn.Logger.LogInfo(message);

            // To learn more about Jotunn's features, go to
            // https://valheim-modding.github.io/Jotunn/tutorials/overview.html
            CreateConfigValues();
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        private void CreateConfigValues()
        {
            ConfigurationManagerAttributes isAdminOnly = new ConfigurationManagerAttributes { IsAdminOnly = true };

            _enableDebug = Config.Bind(
                "Client config",
                "Enable Debug",
                false,
                new ConfigDescription("Enable Debug Mode?")
            );

            recycleHotKey = Config.Bind(
                "Client config",
                "Recycle Hotkey",
                KeyCode.Delete,
                new ConfigDescription("Hotkey to recylce items.")
            );

            returnUnknownRes = Config.Bind(
                "Server config",
                "Return Unknown Resources",
                true,
                new ConfigDescription(
                    "Return resources for unknown recipe?",
                    null,
                    isAdminOnly
                )
            );
        }

        public static void DebugLog(string data)
        {
            if (_enableDebug.Value)
                Jotunn.Logger.LogInfo(PluginName + ": " + data);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateItemDrag")]
    public static class UpdateItemDrag_Patch
    {
        private static void Postfix(
            InventoryGui __instance,
            ItemDrop.ItemData ___m_dragItem,
            Inventory ___m_dragInventory,
            int ___m_dragAmount,
            ref GameObject ___m_dragGo)
        {
            if (!Input.GetKeyDown(GooderRecycling.recycleHotKey.Value) ||
                ___m_dragItem == null ||
                !___m_dragInventory.ContainsItem(___m_dragItem))
            {
                return;
            }

            GooderRecycling.DebugLog(
                string.Format(
                    "Initiating Recycling Process for {0}/{1} {2}",
                    ___m_dragAmount,
                    ___m_dragItem.m_stack,
                    ___m_dragItem.m_dropPrefab.name
                )
            );

            // Attempt to retrieve the crafting recipe for the item being dragged
            Recipe recipe = ObjectDB.instance.GetRecipe(___m_dragItem);

            // Check if a valid recipe was found and if the player knows the recipe
            // or allows recycling unknown resources
            if (recipe != null &&
                (GooderRecycling.returnUnknownRes.Value ||
                 Player.m_localPlayer.IsRecipeKnown(___m_dragItem.m_shared.m_name)))
            {
                // Create a list to store the crafting requirements from the recipe
                List<Piece.Requirement> requirementsList =
                    recipe.m_resources.ToList<Piece.Requirement>();

                bool isRecyclingAllowed = false;

                // Check if recycling is allowed and if there are items to recycle
                if (!isRecyclingAllowed &&
                    ___m_dragAmount / recipe.m_amount > 0)
                {
                    // Iterate through the recycling process for each stack of items
                    for (int i = 0;
                         i < ___m_dragAmount / recipe.m_amount;
                         i++)
                    {
                        // Iterate through the crafting requirements
                        using (List<Piece.Requirement>.Enumerator reqEnumerator =
                               requirementsList.GetEnumerator())
                        {
                            while (reqEnumerator.MoveNext())
                            {
                                Piece.Requirement requirement = reqEnumerator.Current;

                                // Initialize a function predicate to match items by name
                                Func<GameObject, bool> itemPredicate =
                                (GameObject item) =>
                                {
                                    if (item == null)
                                        return false;

                                    ItemDrop itemDrop = item.GetComponent<ItemDrop>();

                                    if (itemDrop == null ||
                                        itemDrop.m_itemData == null ||
                                        itemDrop.m_itemData.m_shared == null)
                                    {
                                        return false;
                                    }

                                    return itemDrop.m_itemData.m_shared.m_name ==
                                            requirement.m_resItem.m_itemData.m_shared.m_name;
                                };

                                // Iterate through item qualities for recycling
                                for (int quality = ___m_dragItem.m_quality;
                                     quality > 0;
                                     quality--)
                                {
                                    IEnumerable<GameObject> availableItems = ObjectDB.instance.m_items;

                                    // Find the matching item in the database
                                    GameObject matchingItem =
                                        availableItems.FirstOrDefault(itemPredicate);

                                    if (matchingItem == null)
                                    {
                                        GooderRecycling.DebugLog(
                                            "Could not find matching item for recycled resource: " +
                                            requirement.m_resItem.m_itemData.m_shared.m_name
                                        );

                                        continue;
                                    }

                                    ItemDrop matchingItemDrop =
                                        matchingItem.GetComponent<ItemDrop>();

                                    if (matchingItemDrop == null ||
                                        matchingItemDrop.m_itemData == null)
                                    {
                                        GooderRecycling.DebugLog(
                                            "Matching item has invalid ItemData: " +
                                            requirement.m_resItem.m_itemData.m_shared.m_name
                                        );

                                        continue;
                                    }

                                    // Clone the item data for recycling
                                    ItemDrop.ItemData clonedItemData =
                                        matchingItemDrop.m_itemData.Clone();

                                    // Preserve the quality and variant from the original
                                    // AddItem overload.
                                    clonedItemData.m_quality =
                                        requirement.m_resItem.m_itemData.m_quality;

                                    clonedItemData.m_variant =
                                        requirement.m_resItem.m_itemData.m_variant;

                                    // Upgrader-only resources (Protection Idol, etc.) aren't part of the base
                                    // craft, but GetAmount(1) still returns m_amount for them in 1.0 — exclude
                                    // them explicitly at the base quality level.
                                    int itemCountToRecycle =
                                        (quality <= 1 && requirement.m_upgraderResource)
                                            ? 0
                                            : requirement.GetAmount(quality);

                                    // Recycle items
                                    while (itemCountToRecycle > 0)
                                    {
                                        int stackSize =
                                            Mathf.Min(
                                                requirement.m_resItem
                                                    .m_itemData
                                                    .m_shared
                                                    .m_maxStackSize,
                                                itemCountToRecycle
                                            );

                                        itemCountToRecycle -= stackSize;

                                        // Create a new ItemData representing the
                                        // resources we are trying to add.
                                        ItemDrop.ItemData recycledItemData =
                                            clonedItemData.Clone();

                                        recycledItemData.m_stack = stackSize;

                                        Inventory inventory =
                                            Player.m_localPlayer.GetInventory();

                                        // Continue attempting to place the remaining
                                        // items until they are either completely
                                        // added or there is no room left.
                                        while (recycledItemData.m_stack > 0)
                                        {
                                            bool addedSomething = false;

                                            // -------------------------------------------------
                                            // First, look for an existing compatible stack.
                                            // -------------------------------------------------
                                            ItemDrop.ItemData existingStack =
                                                inventory.GetAllItems()
                                                    .FirstOrDefault(item =>
                                                        item.IsSameType(
                                                            recycledItemData
                                                        ) &&
                                                        item.m_stack <
                                                        item.m_shared.m_maxStackSize
                                                    );

                                            if (existingStack != null)
                                            {
                                                Vector2i gridPos =
                                                    existingStack.m_gridPos;

                                                int amountBefore =
                                                    recycledItemData.m_stack;

                                                inventory.AddItem(
                                                    recycledItemData,
                                                    recycledItemData.m_stack,
                                                    gridPos.x,
                                                    gridPos.y
                                                );

                                                // AddItem reduces recycledItemData.m_stack
                                                // by the amount it successfully added.
                                                addedSomething =
                                                    recycledItemData.m_stack <
                                                    amountBefore;
                                            }

                                            // -------------------------------------------------
                                            // If there wasn't a stack with room, find an
                                            // empty inventory slot.
                                            // -------------------------------------------------
                                            if (!addedSomething)
                                            {
                                                for (int y = 0;
                                                     y < inventory.GetHeight() &&
                                                     !addedSomething;
                                                     y++)
                                                {
                                                    for (int x = 0;
                                                         x < inventory.GetWidth();
                                                         x++)
                                                    {
                                                        if (inventory.GetItemAt(x, y) != null)
                                                        {
                                                            continue;
                                                        }

                                                        int amountBefore =
                                                            recycledItemData.m_stack;

                                                        inventory.AddItem(
                                                            recycledItemData,
                                                            recycledItemData.m_stack,
                                                            x,
                                                            y
                                                        );

                                                        addedSomething =
                                                            recycledItemData.m_stack <
                                                            amountBefore;

                                                        if (addedSomething)
                                                        {
                                                            break;
                                                        }
                                                    }
                                                }
                                            }

                                            // -------------------------------------------------
                                            // No inventory space remains. Drop whatever
                                            // is still left into the world.
                                            // -------------------------------------------------
                                            if (!addedSomething)
                                            {
                                                Transform transform;

                                                ItemDrop recycledItem =
                                                    Object.Instantiate<GameObject>(
                                                        matchingItem,
                                                        (
                                                            transform =
                                                                Player.m_localPlayer
                                                                    .transform
                                                        ).position +
                                                        transform.forward +
                                                        transform.up,
                                                        transform.rotation
                                                    ).GetComponent<ItemDrop>();

                                                recycledItem.m_itemData =
                                                    recycledItemData;

                                                recycledItem.m_itemData.m_dropPrefab =
                                                    matchingItem;

                                                recycledItem.m_itemData.m_stack =
                                                    recycledItemData.m_stack;

                                                recycledItem.Save();

                                                // Everything remaining has now been
                                                // transferred to the world.
                                                recycledItemData.m_stack = 0;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Check if the item is an equipped weapon and unequip it if needed
                    if (IsEquippedWeapon(___m_dragItem))
                    {
                        GooderRecycling.DebugLog(
                            "Item is equipped. Unequipping..."
                        );

                        UnequipWeapon(___m_dragItem);
                    }

                    // Remove the recycled item from the inventory
                    ___m_dragInventory.RemoveItem(
                        ___m_dragItem,
                        ___m_dragAmount
                    );
                }
            }

            // Destroy the object being dragged
            Object.Destroy(___m_dragGo);
            ___m_dragGo = null;

            // Update the crafting panel in the GUI
            __instance.UpdateCraftingPanel(false);
        }

        private static bool IsEquippedWeapon(ItemDrop.ItemData itemData)
        {
            // Check if the item's name is in any of the equipment slots
            string itemName = itemData.m_shared.m_name;

            return Player.m_localPlayer
                .GetInventory()
                .GetEquippedItems()
                .Any(
                    equippedItem =>
                        equippedItem.m_shared.m_name == itemName
                );
        }

        private static void UnequipWeapon(ItemDrop.ItemData itemData)
        {
            // Unequip the item from the player's equipment slots
            Player.m_localPlayer.RemoveEquipAction(itemData);
            Player.m_localPlayer.UnequipItem(itemData, false);
        }
    }
}