using HarmonyLib;
using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Harmony patches for Assassin class passive skills:
    /// - Killing Edge: flat 20% backstab damage bonus (when equipped)
    /// </summary>
    public static class AssassinPatches
    {
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

                // Killing Edge (persistence index 0) — flat 20% backstab bonus
                if (AbilityManager.IsSkillEquipped(player, "Assassin", 0))
                {
                    var baseAI = __instance.GetComponent<BaseAI>();
                    if (baseAI != null && !baseAI.IsAlerted() && hit.m_backstabBonus > 1f)
                    {
                        hit.m_backstabBonus *= 1.2f;
                    }
                }
            }
        }
    }
}
