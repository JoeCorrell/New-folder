using HarmonyLib;
using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Harmony patches for Assassin class abilities:
    /// - Killing Edge: flat 20% backstab damage bonus
    /// - Blade Dance: double knife damage during active buff
    /// </summary>
    public static class AssassinPatches
    {
        /// <summary>
        /// Prefix on Character.RPC_Damage to enhance backstab for Assassins.
        /// </summary>
        [HarmonyPatch(typeof(Character), "RPC_Damage")]
        public static class Character_RPC_Damage_Patch
        {
            static void Prefix(Character __instance, HitData hit)
            {
                if (hit == null) return;

                Character attacker = hit.GetAttacker();
                if (attacker == null || !(attacker is Player player)) return;
                if (player != Player.m_localPlayer) return;

                string className = ClassPersistence.GetSelectedClassName(player);
                if (className != "Assassin") return;

                // Killing Edge (passive, index 0) — flat 20% backstab bonus
                if (AbilityManager.IsAbilityUnlocked(player, "Assassin", 0))
                {
                    var baseAI = __instance.GetComponent<BaseAI>();
                    if (baseAI != null && !baseAI.IsAlerted() && hit.m_backstabBonus > 1f)
                    {
                        hit.m_backstabBonus *= 1.2f;
                    }
                }

                // Blade Dance (index 5) — double knife damage when active
                if (AbilityManager.IsAbilityUnlocked(player, "Assassin", 5) && BladeDance.IsActive())
                {
                    var weapon = player.GetCurrentWeapon();
                    if (weapon != null && weapon.m_shared.m_skillType == Skills.SkillType.Knives)
                    {
                        hit.m_damage.Modify(2f);
                    }
                }
            }
        }
    }
}
