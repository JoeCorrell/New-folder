using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Assassin active ability: Blade Dance.
    /// When activated, all dagger/knife attacks deal double damage for 15 seconds.
    /// 10 minute cooldown. Activated via Z key (ALT to switch abilities).
    /// Damage doubling is handled in AssassinPatches.Character_RPC_Damage_Patch.
    /// </summary>
    public static class BladeDance
    {
        private const float Duration = 15f;
        private const float Cooldown = 600f; // 10 minutes
        private const string CooldownKey = "StartingClassMod_BladeDance_CD";

        private static float _activeUntil;

        // Suppress flag — set before triggering gpower animation so the Harmony patch
        // can block the real guardian power from firing.
        internal static bool SuppressGuardianPower;

        // Cached reflection for non-public fields
        private static readonly FieldInfo ActivateEffectsField =
            AccessTools.Field(typeof(Player), "m_activatePowerEffects");
        private static readonly FieldInfo ActivateEffectsPlayerField =
            AccessTools.Field(typeof(Player), "m_activatePowerEffectsPlayer");
        private static readonly FieldInfo ZanimField =
            AccessTools.Field(typeof(Character), "m_zanim");

        /// <summary>Whether the damage buff is currently active.</summary>
        public static bool IsActive()
        {
            return Time.time < _activeUntil;
        }

        /// <summary>Seconds remaining on the buff, or 0 if inactive.</summary>
        public static float GetTimeRemaining()
        {
            return IsActive() ? _activeUntil - Time.time : 0f;
        }

        /// <summary>Seconds remaining on cooldown, or 0 if ready.</summary>
        public static float GetCooldownRemaining(Player player)
        {
            if (player == null) return 0f;
            if (!player.m_customData.TryGetValue(CooldownKey, out string val)) return 0f;
            if (!double.TryParse(val, out double cdEnd)) return 0f;
            if (ZNet.instance == null) return 0f;
            double remaining = cdEnd - ZNet.instance.GetTimeSeconds();
            return remaining > 0 ? (float)remaining : 0f;
        }

        /// <summary>Try to activate Blade Dance.</summary>
        public static void TryActivate(Player player)
        {
            if (player == null) return;

            string className = ClassPersistence.GetSelectedClassName(player);
            if (className != "Assassin") return;
            if (!AbilityManager.IsAbilityUnlocked(player, "Assassin", 5)) return;

            // Already active
            if (IsActive()) return;

            // Check cooldown
            if (GetCooldownRemaining(player) > 0f) return;

            // Activate
            _activeUntil = Time.time + Duration;

            // Set cooldown (using server time so it persists across sessions)
            if (ZNet.instance == null) return;
            double cdEnd = ZNet.instance.GetTimeSeconds() + Cooldown;
            player.m_customData[CooldownKey] = cdEnd.ToString("F0");

            // Play the forsaken power activation sound and animation
            PlayActivateEffects(player);

            StartingClassPlugin.Log("Blade Dance activated.");
        }

        /// <summary>Clear active state (for logout).</summary>
        public static void Reset()
        {
            _activeUntil = 0f;
        }

        private static void PlayActivateEffects(Player player)
        {
            // Set suppress flag so our Harmony patch blocks the real guardian power
            SuppressGuardianPower = true;

            // Trigger the raise-hands animation (same visual as forsaken power)
            var zanim = ZanimField?.GetValue(player) as ZSyncAnimation;
            if (zanim != null)
                zanim.SetTrigger("gpower");

            // Play world-space activation effects (particles)
            if (ActivateEffectsField != null)
            {
                var effects = ActivateEffectsField.GetValue(player) as EffectList;
                effects?.Create(player.transform.position, player.transform.rotation, player.transform);
            }

            // Play player-attached activation effects (sound + aura)
            if (ActivateEffectsPlayerField != null)
            {
                var effects = ActivateEffectsPlayerField.GetValue(player) as EffectList;
                effects?.Create(player.transform.position, player.transform.rotation, player.transform);
            }
        }
    }

    /// <summary>
    /// Blocks the real guardian power from activating when Blade Dance
    /// triggers the gpower animation.
    /// </summary>
    [HarmonyPatch(typeof(Player), "StartGuardianPower")]
    static class BladeDance_SuppressGuardianPower_Patch
    {
        static bool Prefix()
        {
            if (BladeDance.SuppressGuardianPower)
            {
                BladeDance.SuppressGuardianPower = false;
                return false; // skip real guardian power
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Player), "ActivateGuardianPower")]
    static class BladeDance_SuppressActivateGuardian_Patch
    {
        static bool Prefix()
        {
            if (BladeDance.SuppressGuardianPower)
            {
                BladeDance.SuppressGuardianPower = false;
                return false;
            }
            return true;
        }
    }
}
