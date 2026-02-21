using System.Collections.Generic;
using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Manages passive skill unlock and equip state per character.
    /// Players unlock skills with skill points, then equip up to 2 at a time.
    /// Only equipped skills provide their passive bonuses.
    ///
    /// Persistence keys:
    ///   "StartingClassMod_Ability_{className}_{persistenceIndex}" = "1" (unlocked)
    ///   "StartingClassMod_Equipped_0" = persistenceIndex (slot 0)
    ///   "StartingClassMod_Equipped_1" = persistenceIndex (slot 1)
    /// </summary>
    public static class AbilityManager
    {
        private const string AbilityKeyPrefix = "StartingClassMod_Ability_";
        private const string EquipSlot0Key = "StartingClassMod_Equipped_0";
        private const string EquipSlot1Key = "StartingClassMod_Equipped_1";
        private const int MaxAbilityIndex = 5; // legacy: indices 0-5
        private const int MaxEquippedSkills = 2;

        private static string GetKey(string className, int persistenceIndex)
        {
            return $"{AbilityKeyPrefix}{className}_{persistenceIndex}";
        }

        // ── Unlock API ───────────────────────────────────────────────────────

        /// <summary>Check if a specific ability is unlocked.</summary>
        public static bool IsAbilityUnlocked(Player player, string className, int persistenceIndex)
        {
            if (player == null || string.IsNullOrEmpty(className)) return false;
            // Index 0 (free passive) is always unlocked for the selected class
            if (persistenceIndex == 0)
            {
                string selected = ClassPersistence.GetSelectedClassName(player);
                return selected == className;
            }
            return player.m_customData.ContainsKey(GetKey(className, persistenceIndex));
        }

        /// <summary>
        /// Unlock an ability using the ability's own PointCost. Returns true on success.
        /// Does NOT auto-equip — player must equip manually.
        /// </summary>
        public static bool UnlockAbility(Player player, string className, int persistenceIndex)
        {
            if (player == null || persistenceIndex <= 0) return false;
            if (IsAbilityUnlocked(player, className, persistenceIndex)) return false;

            int cost = GetAbilityPointCost(className, persistenceIndex);
            if (cost <= 0) return false;
            if (!SkillPointSystem.SpendPoints(player, cost)) return false;

            player.m_customData[GetKey(className, persistenceIndex)] = "1";
            StartingClassPlugin.Log($"Unlocked {className} skill (persistence index {persistenceIndex}).");
            return true;
        }

        /// <summary>Get persistence indices of all unlocked abilities for a class.</summary>
        public static List<int> GetUnlockedAbilities(Player player, string className)
        {
            var result = new List<int>();
            if (player == null || string.IsNullOrEmpty(className)) return result;

            string selected = ClassPersistence.GetSelectedClassName(player);
            if (selected != className) return result;

            // Index 0 is always unlocked
            result.Add(0);

            for (int i = 1; i <= MaxAbilityIndex; i++)
            {
                if (player.m_customData.ContainsKey(GetKey(className, i)))
                    result.Add(i);
            }
            return result;
        }

        // ── Equip API ────────────────────────────────────────────────────────

        /// <summary>Check if a skill is currently equipped (in one of 2 slots).</summary>
        public static bool IsSkillEquipped(Player player, string className, int persistenceIndex)
        {
            if (player == null) return false;
            string piStr = persistenceIndex.ToString();
            if (player.m_customData.TryGetValue(EquipSlot0Key, out string v0) && v0 == piStr) return true;
            if (player.m_customData.TryGetValue(EquipSlot1Key, out string v1) && v1 == piStr) return true;
            return false;
        }

        /// <summary>Equip a skill into the first available slot. Returns true on success.</summary>
        public static bool EquipSkill(Player player, string className, int persistenceIndex)
        {
            if (player == null) return false;
            if (!IsAbilityUnlocked(player, className, persistenceIndex)) return false;
            if (IsSkillEquipped(player, className, persistenceIndex)) return false;

            string v0 = player.m_customData.TryGetValue(EquipSlot0Key, out string s0) ? s0 : "";
            string v1 = player.m_customData.TryGetValue(EquipSlot1Key, out string s1) ? s1 : "";

            if (string.IsNullOrEmpty(v0))
                player.m_customData[EquipSlot0Key] = persistenceIndex.ToString();
            else if (string.IsNullOrEmpty(v1))
                player.m_customData[EquipSlot1Key] = persistenceIndex.ToString();
            else
                return false; // Both slots full

            ApplyPassiveAbility(player, className, persistenceIndex);
            return true;
        }

        /// <summary>Unequip a skill from its slot. Returns true if it was equipped.</summary>
        public static bool UnequipSkill(Player player, string className, int persistenceIndex)
        {
            if (player == null) return false;
            string piStr = persistenceIndex.ToString();
            bool removed = false;

            if (player.m_customData.TryGetValue(EquipSlot0Key, out string v0) && v0 == piStr)
            {
                player.m_customData[EquipSlot0Key] = "";
                removed = true;
            }
            if (player.m_customData.TryGetValue(EquipSlot1Key, out string v1) && v1 == piStr)
            {
                player.m_customData[EquipSlot1Key] = "";
                removed = true;
            }

            if (removed)
                RemovePassiveAbility(player, className, persistenceIndex);
            return removed;
        }

        /// <summary>Get list of equipped skill persistence indices.</summary>
        public static List<int> GetEquippedSkills(Player player)
        {
            var result = new List<int>();
            if (player == null) return result;
            if (player.m_customData.TryGetValue(EquipSlot0Key, out string v0) &&
                !string.IsNullOrEmpty(v0) && int.TryParse(v0, out int i0))
                result.Add(i0);
            if (player.m_customData.TryGetValue(EquipSlot1Key, out string v1) &&
                !string.IsNullOrEmpty(v1) && int.TryParse(v1, out int i1))
                result.Add(i1);
            return result;
        }

        /// <summary>Get number of currently equipped skills (0-2).</summary>
        public static int GetEquippedCount(Player player)
        {
            int count = 0;
            if (player == null) return count;
            if (player.m_customData.TryGetValue(EquipSlot0Key, out string v0) && !string.IsNullOrEmpty(v0))
                count++;
            if (player.m_customData.TryGetValue(EquipSlot1Key, out string v1) && !string.IsNullOrEmpty(v1))
                count++;
            return count;
        }

        // ── Reset ────────────────────────────────────────────────────────────

        /// <summary>
        /// Reset all non-free abilities for a class. Removes unlock keys,
        /// clears equip slots, removes SEs, and refunds point costs.
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
                    int designCost = GetAbilityPointCost(className, i);
                    if (designCost > 0) refunded += designCost;
                }
            }

            // Clear equip slots
            player.m_customData.Remove(EquipSlot0Key);
            player.m_customData.Remove(EquipSlot1Key);

            // Remove all passive SEs
            RemoveAllPassiveSEs(player);

            if (refunded > 0)
                SkillPointSystem.AddPoints(player, refunded);

            return refunded;
        }

        // ── Initialization ───────────────────────────────────────────────────

        /// <summary>
        /// Initialize equipped passive effects on login or class selection.
        /// If no equip slots are set (first time), auto-equips the free passive (index 0).
        /// </summary>
        public static void InitializeAbilities(Player player, string className)
        {
            if (player == null || string.IsNullOrEmpty(className)) return;

            var equipped = GetEquippedSkills(player);
            if (equipped.Count == 0)
            {
                // First login after class selection: auto-equip the free passive
                EquipSkill(player, className, 0);
            }
            else
            {
                // Re-apply SEs for equipped skills
                foreach (int pi in equipped)
                    ApplyPassiveAbility(player, className, pi);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>Resolve PersistenceIndex for a ClassAbility at a given list index.</summary>
        public static int GetPersistenceIndex(StartingClass cls, int listIndex)
        {
            if (cls?.Abilities == null || listIndex < 0 || listIndex >= cls.Abilities.Count)
                return listIndex;
            int pi = cls.Abilities[listIndex].PersistenceIndex;
            return pi >= 0 ? pi : listIndex;
        }

        /// <summary>Look up the point cost for a specific persistence index from the class data.</summary>
        private static int GetAbilityPointCost(string className, int persistenceIndex)
        {
            var classes = ClassDefinitions.GetAll();
            foreach (var cls in classes)
            {
                if (cls.Name != className || cls.Abilities == null) continue;
                for (int i = 0; i < cls.Abilities.Count; i++)
                {
                    int pi = cls.Abilities[i].PersistenceIndex >= 0
                        ? cls.Abilities[i].PersistenceIndex : i;
                    if (pi == persistenceIndex)
                        return cls.Abilities[i].PointCost;
                }
            }
            return 0;
        }

        /// <summary>Apply a passive SE for a specific ability.</summary>
        private static void ApplyPassiveAbility(Player player, string className, int persistenceIndex)
        {
            var seman = player?.GetSEMan();
            if (seman == null) return;

            if (className == "Assassin")
            {
                switch (persistenceIndex)
                {
                    case 2: ApplySE<SE_ShadowStep>(seman, "SE_ShadowStep"); break;
                    case 3: ApplySE<SE_NaturesShroud>(seman, "SE_NaturesShroud"); break;
                    case 4: ApplySE<SE_GhostStride>(seman, "SE_GhostStride"); break;
                }
            }
            else if (className == "Hunter")
            {
                switch (persistenceIndex)
                {
                    case 3: ApplySE<SE_Survivalist>(seman, "SE_Survivalist"); break;
                }
            }
        }

        /// <summary>Remove a passive SE for a specific ability.</summary>
        private static void RemovePassiveAbility(Player player, string className, int persistenceIndex)
        {
            var seman = player?.GetSEMan();
            if (seman == null) return;

            if (className == "Assassin")
            {
                switch (persistenceIndex)
                {
                    case 2: seman.RemoveStatusEffect("SE_ShadowStep".GetStableHashCode()); break;
                    case 3: seman.RemoveStatusEffect("SE_NaturesShroud".GetStableHashCode()); break;
                    case 4: seman.RemoveStatusEffect("SE_GhostStride".GetStableHashCode()); break;
                }
            }
            else if (className == "Hunter")
            {
                switch (persistenceIndex)
                {
                    case 3: seman.RemoveStatusEffect("SE_Survivalist".GetStableHashCode()); break;
                }
            }
        }

        /// <summary>Remove all mod-applied passive SEs.</summary>
        private static void RemoveAllPassiveSEs(Player player)
        {
            var seman = player?.GetSEMan();
            if (seman == null) return;
            seman.RemoveStatusEffect("SE_ShadowStep".GetStableHashCode());
            seman.RemoveStatusEffect("SE_NaturesShroud".GetStableHashCode());
            seman.RemoveStatusEffect("SE_GhostStride".GetStableHashCode());
            seman.RemoveStatusEffect("SE_Survivalist".GetStableHashCode());
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
