using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Shared helper for playing ability activation effects (animation + VFX/SFX).
    /// Used by all active abilities (MarkedByFate, BladeDance, HuntersInstinct, Pathfinder).
    /// Falls back to Eikthyr's guardian power effects when the player has no forsaken power equipped.
    /// </summary>
    public static class AbilityEffects
    {
        private static readonly FieldInfo ZanimField =
            AccessTools.Field(typeof(Character), "m_zanim");
        private static readonly FieldInfo GuardianSEField =
            AccessTools.Field(typeof(Player), "m_guardianSE");

        // Cached fallback effects from ObjectDB (resolved once, used for all abilities)
        private static EffectList _fallbackEffects;
        private static bool _fallbackResolved;

        /// <summary>
        /// Play the guardian power activation animation and VFX/SFX.
        /// If the player has a forsaken power equipped, uses its effects.
        /// Otherwise falls back to GP_Eikthyr's effects from ObjectDB.
        /// </summary>
        public static void PlayActivation(Player player)
        {
            if (player == null) return;

            // Play the raise-hands gpower animation
            var zanim = ZanimField?.GetValue(player) as ZSyncAnimation;
            if (zanim != null)
                zanim.SetTrigger("gpower");

            // Try the player's own forsaken power effects first
            var guardianSE = GuardianSEField?.GetValue(player) as StatusEffect;
            EffectList effects = guardianSE?.m_startEffects;

            // Fall back to Eikthyr's effects if no forsaken power is equipped
            if (effects == null)
                effects = GetFallbackEffects();

            effects?.Create(player.GetCenterPoint(), player.transform.rotation, player.transform);
        }

        private static EffectList GetFallbackEffects()
        {
            if (_fallbackResolved) return _fallbackEffects;

            if (ObjectDB.instance != null)
            {
                var se = ObjectDB.instance.GetStatusEffect("GP_Eikthyr".GetStableHashCode());
                if (se != null)
                {
                    _fallbackEffects = se.m_startEffects;
                    _fallbackResolved = true;
                }
            }
            return _fallbackEffects;
        }
    }
}
