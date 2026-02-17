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

            // Grant starting items
            GrantItems(player, startingClass);

            // Apply skill bonuses
            ApplySkillBonuses(player, startingClass);

            // Save the class selection (also clears pending flag)
            ClassPersistence.SaveSelectedClass(player, startingClass.Name);

            // Show a message to the player
            player.Message(MessageHud.MessageType.Center, $"You begin your journey as a {startingClass.Name}!");

            StartingClassPlugin.Log($"Applied class '{startingClass.Name}' to player. " +
                                   $"Items: {startingClass.Items.Count}, Skill bonuses: {startingClass.SkillBonuses.Count}");
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

                if (addedItem != null)
                {
                    StartingClassPlugin.Log($"Granted {item.Quantity}x {item.PrefabName}.");
                }
                else
                {
                    // Inventory full - drop item on the ground
                    StartingClassPlugin.LogWarning($"Inventory full, dropping {item.PrefabName} at player position.");
                    var dropped = Object.Instantiate(prefab, player.transform.position + Vector3.up, Quaternion.identity);
                    var droppedItem = dropped.GetComponent<ItemDrop>();
                    if (droppedItem != null)
                    {
                        droppedItem.m_itemData.m_stack = item.Quantity;
                    }
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
                // Use reflection to access the internal GetSkill method,
                // or use the public API: RaiseSkill adds XP incrementally.
                // The most reliable approach is to use the m_skillData dictionary directly.
                float currentLevel = skills.GetSkillLevel(bonus.SkillType);
                if (currentLevel < bonus.BonusLevel)
                {
                    // RaiseSkill with a large delta to reach target level.
                    // Each level requires roughly (level+1)*10 XP, so we use a direct approach.
                    // We raise the skill incrementally to the target.
                    float needed = bonus.BonusLevel - currentLevel;
                    for (int i = 0; i < (int)(needed * 20); i++)
                    {
                        skills.RaiseSkill(bonus.SkillType, 1f);
                        if (skills.GetSkillLevel(bonus.SkillType) >= bonus.BonusLevel)
                            break;
                    }
                    StartingClassPlugin.Log($"Raised {bonus.SkillType} to level {skills.GetSkillLevel(bonus.SkillType):F1} (target: {bonus.BonusLevel}).");
                }
                else
                {
                    StartingClassPlugin.Log($"Skill {bonus.SkillType} already at level {currentLevel}, " +
                                           $"skipping bonus of {bonus.BonusLevel}.");
                }
            }
        }
    }
}
