using HarmonyLib;

namespace StartingClassMod
{
    /// <summary>
    /// Harmony patches for Hunter class passive abilities:
    /// - Predator's Mark (idx 0): +15% damage to creatures
    /// - Keen Eye (idx 2): +15% bow/spear damage
    /// - Thick Hide (idx 4): -15% damage from creature attacks (not players/bosses)
    /// </summary>
    public static class HunterPatches
    {
        [HarmonyPatch(typeof(Character), "RPC_Damage")]
        public static class Character_RPC_Damage_Hunter_Patch
        {
            static void Prefix(Character __instance, HitData hit)
            {
                if (hit == null) return;

                Player localPlayer = Player.m_localPlayer;
                if (localPlayer == null) return;

                string className = ClassPersistence.GetSelectedClassName(localPlayer);
                if (className != "Hunter") return;

                Character attacker = hit.GetAttacker();

                // === Offensive passives: attacker is the Hunter player ===
                if (attacker == localPlayer)
                {
                    // Predator's Mark (idx 0): +15% damage to creatures
                    if (!__instance.IsPlayer() && AbilityManager.IsAbilityUnlocked(localPlayer, "Hunter", 0))
                    {
                        hit.m_damage.Modify(1.15f);
                    }

                    // Keen Eye (idx 2): +15% bow and spear damage
                    if (AbilityManager.IsAbilityUnlocked(localPlayer, "Hunter", 2))
                    {
                        var skill = localPlayer.GetCurrentWeapon()?.m_shared?.m_skillType;
                        if (skill == Skills.SkillType.Bows || skill == Skills.SkillType.Spears)
                            hit.m_damage.Modify(1.15f);
                    }
                }

                // === Defensive passive: target is the Hunter player ===
                if (__instance == localPlayer && attacker != null)
                {
                    // Thick Hide (idx 4): -15% damage from creatures (not players or bosses)
                    if (!attacker.IsPlayer() && !attacker.m_boss &&
                        AbilityManager.IsAbilityUnlocked(localPlayer, "Hunter", 4))
                    {
                        hit.m_damage.Modify(0.85f);
                    }
                }
            }
        }
    }
}
