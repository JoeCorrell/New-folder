using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Manages skill point earning and spending.
    /// Points are earned from kills (scaled by enemy health) and class skill levelups.
    /// Balance is persisted in Player.m_customData.
    /// </summary>
    public static class SkillPointSystem
    {
        private const string PointsKey = "StartingClassMod_SkillPoints";

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
            if (player == null || cost <= 0) return false;
            int current = GetPoints(player);
            if (current < cost) return false;
            player.m_customData[PointsKey] = (current - cost).ToString();
            return true;
        }

        /// <summary>
        /// Called when a creature dies. Awards points to the killer based on enemy max health.
        /// </summary>
        public static void OnEnemyKilled(Character enemy, Player killer)
        {
            if (enemy == null || killer == null) return;
            if (!ClassPersistence.HasSelectedClass(killer)) return;

            float maxHp = enemy.GetMaxHealth();
            int points = Mathf.Max(1, Mathf.RoundToInt(maxHp / 50f));

            // Boss kills grant a large bonus
            if (enemy.IsBoss())
                points += 10;

            AddPoints(killer, points);
            killer.Message(MessageHud.MessageType.TopLeft, $"+ {points} Skill Points");
        }

        /// <summary>
        /// Called when a skill levels up. Awards bonus points for class-relevant skills.
        /// </summary>
        public static void OnSkillLevelup(Player player, Skills.SkillType skill)
        {
            if (player == null) return;
            string className = ClassPersistence.GetSelectedClassName(player);
            if (string.IsNullOrEmpty(className)) return;

            if (!IsClassSkill(className, skill)) return;

            int bonus = 2;
            AddPoints(player, bonus);
            player.Message(MessageHud.MessageType.TopLeft, $"+ {bonus} Skill Points (skill levelup)");
        }

        /// <summary>Returns true if the given skill type is relevant to the player's class.</summary>
        private static bool IsClassSkill(string className, Skills.SkillType skill)
        {
            switch (className)
            {
                case "Assassin":
                    return skill == Skills.SkillType.Sneak || skill == Skills.SkillType.Knives;
                case "Archer":
                    return skill == Skills.SkillType.Bows;
                case "Builder":
                    return skill == Skills.SkillType.WoodCutting || skill == Skills.SkillType.Axes;
                case "Explorer":
                    return skill == Skills.SkillType.Run;
                case "Farmer":
                    return skill == Skills.SkillType.Blocking || skill == Skills.SkillType.Run;
                case "Healer":
                    return skill == Skills.SkillType.BloodMagic;
                case "Hunter":
                    return skill == Skills.SkillType.Bows || skill == Skills.SkillType.Sneak;
                case "Miner":
                    return skill == Skills.SkillType.Pickaxes || skill == Skills.SkillType.Blocking;
                default:
                    return false;
            }
        }
    }
}
