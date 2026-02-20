using System.Collections.Generic;
using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Applies the selected class to the player: grants items, applies skill bonuses,
    /// and persists the selection.
    /// </summary>
    public static class ClassApplicator
    {
        /// <summary>
        /// Applies a starting class to the player.
        /// </summary>
        /// <param name="player">The local player.</param>
        /// <param name="startingClass">The class to apply.</param>
        /// <param name="isFromCommand">If true, items/skills are still applied but existing
        /// class data is overwritten (command-based re-selection).</param>
        public static void ApplyClass(Player player, StartingClass startingClass, bool isFromCommand)
        {
            if (player == null || startingClass == null) return;

            // Mark as pending before we start applying (crash safety)
            ClassPersistence.SetPending(player);

            try
            {
                // On command-based re-selection, clear inventory to prevent item duplication
                if (isFromCommand)
                {
                    var inventory = player.GetInventory();
                    if (inventory != null)
                    {
                        var items = new List<ItemDrop.ItemData>(inventory.GetAllItems());
                        foreach (var item in items)
                            inventory.RemoveItem(item);
                    }
                }

                // Grant starting items
                GrantItems(player, startingClass);

                // Apply skill bonuses
                ApplySkillBonuses(player, startingClass);
            }
            finally
            {
                // Save the class selection (also clears pending flag) — always runs even on error
                ClassPersistence.SaveSelectedClass(player, startingClass.Name);
            }

            // Initialize ability effects (passive SE, any previously unlocked SEs)
            AbilityManager.InitializeAbilities(player, startingClass.Name);

            // Play skill level-up sound effect
            player.m_skillLevelupEffects.Create(player.GetHeadPoint(), player.transform.rotation, player.transform);

            // Show a message to the player
            player.Message(MessageHud.MessageType.Center, $"You begin your journey as a {startingClass.Name}!");
            StartingClassPlugin.Log($"Applied class '{startingClass.Name}'.");
        }

        private static void GrantItems(Player player, StartingClass startingClass)
        {
            var inventory = player.GetInventory();
            if (inventory == null)
            {
                StartingClassPlugin.LogError("Player inventory is null, cannot grant items.");
                return;
            }

            foreach (var item in startingClass.Items)
            {
                var prefab = ZNetScene.instance?.GetPrefab(item.PrefabName);
                if (prefab == null)
                {
                    StartingClassPlugin.LogWarning($"Item prefab '{item.PrefabName}' not found. Skipping.");
                    continue;
                }

                var itemDrop = prefab.GetComponent<ItemDrop>();
                if (itemDrop == null)
                {
                    StartingClassPlugin.LogWarning($"Prefab '{item.PrefabName}' has no ItemDrop component. Skipping.");
                    continue;
                }

                // Add item to inventory - AddItem returns ItemDrop.ItemData or null on failure
                var addedItem = inventory.AddItem(prefab.name, item.Quantity,
                    itemDrop.m_itemData.m_quality,
                    itemDrop.m_itemData.m_variant,
                    player.GetPlayerID(),
                    player.GetPlayerName());

                if (addedItem == null)
                {
                    // Inventory full - drop item using the player's drop method (network-safe)
                    StartingClassPlugin.LogWarning($"Inventory full, dropping {item.PrefabName} at player position.");
                    // Create a temporary ItemData to drop
                    var tempItem = itemDrop.m_itemData?.Clone();
                    if (tempItem == null) continue;
                    tempItem.m_stack = item.Quantity;
                    ItemDrop.DropItem(tempItem, item.Quantity,
                        player.transform.position + player.transform.forward + Vector3.up,
                        player.transform.rotation);
                }
            }
        }

        private static void ApplySkillBonuses(Player player, StartingClass startingClass)
        {
            var skills = player.GetSkills();
            if (skills == null)
            {
                StartingClassPlugin.LogError("Player skills are null, cannot apply bonuses.");
                return;
            }

            foreach (var bonus in startingClass.SkillBonuses)
            {
                float currentLevel = skills.GetSkillLevel(bonus.SkillType);
                if (currentLevel >= bonus.BonusLevel) continue;

                // RaiseSkill adds XP incrementally. Cap iterations to prevent runaway loops.
                const int maxIterations = 500;
                bool reached = false;
                for (int i = 0; i < maxIterations; i++)
                {
                    skills.RaiseSkill(bonus.SkillType, 1f);
                    if (skills.GetSkillLevel(bonus.SkillType) >= bonus.BonusLevel)
                    { reached = true; break; }
                }
                if (!reached)
                    StartingClassPlugin.LogWarning($"Skill {bonus.SkillType} did not reach target level {bonus.BonusLevel} after {maxIterations} iterations.");
            }
        }
    }
}
