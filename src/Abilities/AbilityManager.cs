using System.Collections.Generic;
using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Manages ability unlock state per character.
    /// Ability data is persisted in Player.m_customData with keys like
    /// "StartingClassMod_Ability_Assassin_1" = "1".
    /// Passive abilities (index 0) are always unlocked for the chosen class.
    /// </summary>
    public static class AbilityManager
    {
        private const string AbilityKeyPrefix = "StartingClassMod_Ability_";
        private const int MaxAbilityIndex = 5; // 6 abilities: indices 0-5

        private static string GetKey(string className, int abilityIndex)
        {
            return $"{AbilityKeyPrefix}{className}_{abilityIndex}";
        }

        /// <summary>Check if a specific ability is unlocked.</summary>
        public static bool IsAbilityUnlocked(Player player, string className, int abilityIndex)
        {
            if (player == null || string.IsNullOrEmpty(className)) return false;
            // Passive (index 0) is always unlocked for the selected class
            if (abilityIndex == 0)
            {
                string selected = ClassPersistence.GetSelectedClassName(player);
                return selected == className;
            }
            return player.m_customData.ContainsKey(GetKey(className, abilityIndex));
        }

        /// <summary>
        /// Unlock an ability if the player has enough skill points.
        /// Returns true on success.
        /// </summary>
        public static bool UnlockAbility(Player player, string className, int abilityIndex, int pointCost)
        {
            if (player == null || abilityIndex <= 0) return false;
            if (IsAbilityUnlocked(player, className, abilityIndex)) return false;
            if (!SkillPointSystem.SpendPoints(player, pointCost)) return false;

            player.m_customData[GetKey(className, abilityIndex)] = "1";

            // Apply the newly unlocked ability's effect
            ApplyAbilityEffect(player, className, abilityIndex);

            StartingClassPlugin.Log($"Unlocked {className} ability index {abilityIndex}.");
            return true;
        }

        /// <summary>Get indices of all unlocked abilities for a class.</summary>
        public static List<int> GetUnlockedAbilities(Player player, string className)
        {
            var result = new List<int>();
            if (player == null || string.IsNullOrEmpty(className)) return result;

            string selected = ClassPersistence.GetSelectedClassName(player);
            if (selected != className) return result;

            // Passive is always unlocked
            result.Add(0);

            for (int i = 1; i <= MaxAbilityIndex; i++)
            {
                if (player.m_customData.ContainsKey(GetKey(className, i)))
                    result.Add(i);
            }
            return result;
        }

        /// <summary>
        /// Reset all non-passive abilities for a class. Removes unlock keys
        /// and refunds the total point cost. Returns the number of points refunded.
        /// </summary>
        public static int ResetAbilities(Player player, string className)
        {
            if (player == null || string.IsNullOrEmpty(className)) return 0;

            int refunded = 0;
            for (int i = 1; i <= MaxAbilityIndex; i++)
            {
                string key = GetKey(className, i);
                if (player.m_customData.ContainsKey(key))
                {
                    player.m_customData.Remove(key);
                    int cost = GetAbilityPointCost(className, i);
                    refunded += cost;
                }
            }

            if (refunded > 0)
                SkillPointSystem.AddPoints(player, refunded);

            return refunded;
        }

        /// <summary>Look up the point cost for a specific ability index from the class data.</summary>
        private static int GetAbilityPointCost(string className, int abilityIndex)
        {
            var classes = ClassDefinitions.GetAll();
            foreach (var cls in classes)
            {
                if (cls.Name != className) continue;
                if (cls.Abilities != null && abilityIndex < cls.Abilities.Count)
                    return cls.Abilities[abilityIndex].PointCost;
            }
            return 0;
        }

        /// <summary>
        /// Initialize all ability effects for the player's current class.
        /// Called on class selection and on login (OnSpawned).
        /// Auto-unlocks any abilities that have zero point cost.
        /// </summary>
        public static void InitializeAbilities(Player player, string className)
        {
            if (player == null || string.IsNullOrEmpty(className)) return;

            // Auto-unlock abilities with zero point cost so players don't need
            // to click "Unlock" for free abilities.
            AutoUnlockFreeAbilities(player, className);

            var unlocked = GetUnlockedAbilities(player, className);
            foreach (int idx in unlocked)
                ApplyAbilityEffect(player, className, idx);
        }

        private static void AutoUnlockFreeAbilities(Player player, string className)
        {
            var classes = ClassDefinitions.GetAll();
            StartingClass cls = null;
            foreach (var c in classes)
            {
                if (c.Name == className) { cls = c; break; }
            }
            if (cls?.Abilities == null) return;

            for (int i = 1; i < cls.Abilities.Count && i <= MaxAbilityIndex; i++)
            {
                if (cls.Abilities[i].PointCost == 0 && !IsAbilityUnlocked(player, className, i))
                {
                    player.m_customData[GetKey(className, i)] = "1";
                    StartingClassPlugin.Log($"Auto-unlocked free {className} ability index {i}.");
                }
            }
        }

        /// <summary>Apply the gameplay effect for a specific ability.</summary>
        private static void ApplyAbilityEffect(Player player, string className, int abilityIndex)
        {
            if (player == null) return;

            switch (className)
            {
                case "Assassin":
                    ApplyAssassinAbility(player, abilityIndex);
                    break;
                case "Hunter":
                    ApplyHunterAbility(player, abilityIndex);
                    break;
            }
        }

        private static void ApplyAssassinAbility(Player player, int abilityIndex)
        {
            var seman = player.GetSEMan();
            if (seman == null) return;

            switch (abilityIndex)
            {
                case 0:
                    // Killing Edge (passive) — handled via Harmony patch, no SE needed
                    break;
                case 1:
                    // Marked by Fate — restore HUD SE if still active from previous session
                    MarkedByFate.RestoreIfActive(player);
                    break;
                case 2:
                    // Shadow Step — apply persistent SE
                    ApplySE<SE_ShadowStep>(seman, "SE_ShadowStep");
                    break;
                case 3:
                    // Nature's Shroud — apply persistent SE
                    ApplySE<SE_NaturesShroud>(seman, "SE_NaturesShroud");
                    break;
                case 4:
                    // Ghost Stride — apply persistent SE
                    ApplySE<SE_GhostStride>(seman, "SE_GhostStride");
                    break;
                case 5:
                    // Blade Dance — restore HUD SE if still active from previous session
                    BladeDance.RestoreIfActive(player);
                    break;
            }
        }

        private static void ApplyHunterAbility(Player player, int abilityIndex)
        {
            var seman = player.GetSEMan();
            if (seman == null) return;

            switch (abilityIndex)
            {
                case 0:
                    // Predator's Mark (passive) — handled via Harmony patch, no SE needed
                    break;
                case 1:
                    // Hunter's Instinct — restore HUD SE if still active from previous session
                    HuntersInstinct.RestoreIfActive(player);
                    break;
                case 2:
                    // Keen Eye (passive) — handled via Harmony patch, no SE needed
                    break;
                case 3:
                    // Survivalist — apply persistent SE
                    ApplySE<SE_Survivalist>(seman, "SE_Survivalist");
                    break;
                case 4:
                    // Thick Hide (passive) — handled via Harmony patch, no SE needed
                    break;
                case 5:
                    // Pathfinder — restore HUD SE if still active from previous session
                    Pathfinder.RestoreIfActive(player);
                    break;
            }
        }

        /// <summary>Helper to apply a persistent SE if not already present.</summary>
        private static void ApplySE<T>(SEMan seman, string seName) where T : StatusEffect
        {
            int hash = seName.GetStableHashCode();
            if (seman.HaveStatusEffect(hash)) return;

            var se = ScriptableObject.CreateInstance<T>();
            if (se == null)
            {
                StartingClassPlugin.LogWarning($"Failed to create StatusEffect instance for '{seName}'.");
                return;
            }
            se.name = seName;
            se.m_name = seName;
            se.m_ttl = 0f;
            seman.AddStatusEffect(se);
        }
    }
}
