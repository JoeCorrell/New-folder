using System;
using System.Collections.Generic;
using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Manages skill point earning via class-specific XP accumulation.
    /// Assassin: XP from killing enemies while wearing full troll leather armor.
    /// Hunter: XP from killing animals.
    /// Boss first-kills award 2 skill points directly (once per boss).
    /// </summary>
    public static class SkillPointSystem
    {
        private const string PointsKey = "StartingClassMod_SkillPoints";
        private const string XPKey = "StartingClassMod_SkillXP";
        private const string BossKeyPrefix = "StartingClassMod_Boss_";
        private const int XPThreshold = 100;

        // ── Troll leather set (Assassin XP requirement) ──────────────────────

        private static readonly HashSet<string> TrollLeatherSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "HelmetTrollLeather", "ArmorTrollLeatherChest",
            "ArmorTrollLeatherLegs", "CapeTrollHide"
        };

        // ── Animal XP values (Hunter) ────────────────────────────────────────

        private static readonly Dictionary<string, int> AnimalXP = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Boar", 1 }, { "Boar_piggy", 1 }, { "Neck", 1 },
            { "Hare", 1 }, { "Hen", 1 }, { "Chicken", 1 },
            { "Deer", 2 }, { "Wolf_cub", 2 }, { "Tick", 2 },
            { "Wolf", 3 }, { "Lox_Calf", 3 },
            { "Serpent", 5 }, { "Lox", 5 }
        };

        // ── Points API (unchanged) ───────────────────────────────────────────

        public static int GetPoints(Player player)
        {
            if (player == null) return 0;
            if (player.m_customData.TryGetValue(PointsKey, out string val) && int.TryParse(val, out int pts))
                return pts;
            return 0;
        }

        public static void AddPoints(Player player, int amount)
        {
            if (player == null || amount <= 0) return;
            int current = GetPoints(player);
            player.m_customData[PointsKey] = (current + amount).ToString();
        }

        /// <summary>Set points to an exact value (for console/debug commands).</summary>
        public static void SetPoints(Player player, int amount)
        {
            if (player == null) return;
            player.m_customData[PointsKey] = amount.ToString();
        }

        /// <summary>Spend points. Returns true if successful, false if insufficient.</summary>
        public static bool SpendPoints(Player player, int cost)
        {
            if (player == null || cost < 0) return false;
            if (cost == 0) return true;
            int current = GetPoints(player);
            if (current < cost) return false;
            player.m_customData[PointsKey] = (current - cost).ToString();
            return true;
        }

        // ── XP API ───────────────────────────────────────────────────────────

        public static int GetXP(Player player)
        {
            if (player == null) return 0;
            if (player.m_customData.TryGetValue(XPKey, out string val) && int.TryParse(val, out int xp))
                return xp;
            return 0;
        }

        /// <summary>
        /// Add XP and auto-convert to skill points at threshold.
        /// Shows notification messages for XP gains and skill point rewards.
        /// </summary>
        public static void AddXP(Player player, int xp, string className)
        {
            if (player == null || xp <= 0) return;

            int current = GetXP(player);
            int newXP = current + xp;

            player.Message(MessageHud.MessageType.Center, $"+{xp} XP ({className})");

            while (newXP >= XPThreshold)
            {
                newXP -= XPThreshold;
                AddPoints(player, 1);
                player.Message(MessageHud.MessageType.Center, "Skill Point earned!");
            }

            player.m_customData[XPKey] = newXP.ToString();
        }

        // ── Kill handler ─────────────────────────────────────────────────────

        /// <summary>
        /// Called when a creature dies. Awards class-specific XP and boss first-kill bonuses.
        /// </summary>
        public static void OnEnemyKilled(Character enemy, Player killer)
        {
            if (enemy == null || killer == null) return;
            if (!ClassPersistence.HasSelectedClass(killer)) return;

            string className = ClassPersistence.GetSelectedClassName(killer);

            // Boss first-kill: 2 skill points (any class, once per boss)
            if (enemy.IsBoss())
            {
                string bossName = Utils.GetPrefabName(enemy.gameObject.name);
                string bossKey = BossKeyPrefix + bossName;
                if (!killer.m_customData.ContainsKey(bossKey))
                {
                    killer.m_customData[bossKey] = "1";
                    AddPoints(killer, 2);
                    killer.Message(MessageHud.MessageType.Center, "+2 Skill Points (Boss defeated!)");
                }
            }

            // Class-specific XP
            int xp = 0;

            if (className == "Assassin")
            {
                if (IsWearingFullTrollLeather(killer))
                    xp = Mathf.Max(1, Mathf.CeilToInt(enemy.GetMaxHealth() / 200f));
            }
            else if (className == "Hunter")
            {
                string prefabName = Utils.GetPrefabName(enemy.gameObject.name);
                if (AnimalXP.TryGetValue(prefabName, out int animalXp))
                    xp = animalXp;
            }

            if (xp > 0)
                AddXP(killer, xp, className);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static bool IsWearingFullTrollLeather(Player player)
        {
            if (player == null) return false;

            var equipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in player.GetInventory().GetEquippedItems())
            {
                if (item.m_dropPrefab != null)
                    equipped.Add(item.m_dropPrefab.name);
            }

            foreach (string piece in TrollLeatherSet)
            {
                if (!equipped.Contains(piece))
                    return false;
            }
            return true;
        }
    }
}
