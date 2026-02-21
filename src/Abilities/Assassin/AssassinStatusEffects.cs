using UnityEngine;

namespace StartingClassMod
{
    // ══════════════════════════════════════════
    //  Persistent passive status effects
    // ══════════════════════════════════════════

    /// <summary>
    /// Shadow Step: +25% sneak movement speed, -25% sneak stamina drain.
    /// </summary>
    public class SE_ShadowStep : StatusEffect
    {
        public override bool IsDone() => false;

        public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
        {
            if (character != null && character.IsCrouching())
                speed += baseSpeed * 0.25f;
        }

        public override void ModifySneakStaminaUsage(float baseStaminaUse, ref float staminaUse)
        {
            staminaUse -= baseStaminaUse * 0.25f;
        }

        public override string GetIconText() => "";
        public override string GetTooltipString() => "Shadow Step: +25% sneak speed, -25% sneak stamina drain";
    }

    /// <summary>
    /// Nature's Shroud: Nearly undetectable while crouched near vegetation.
    /// </summary>
    public class SE_NaturesShroud : StatusEffect
    {
        private const float VegetationCheckRadius = 4f;
        private const float CheckInterval = 0.5f;
        private float _checkTimer;
        private bool _nearVegetation;

        // Vegetation objects (bushes, shrubs, saplings) sit on Default_small/Default layers.
        // Exclude "piece"/"piece_nonsolid" (player-built structures) to avoid false positives.
        private static readonly int PieceMask = LayerMask.GetMask("Default_small", "Default");

        public override bool IsDone() => false;

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            _checkTimer -= dt;
            if (_checkTimer <= 0f)
            {
                _checkTimer = CheckInterval;
                _nearVegetation = CheckForVegetation();
            }
        }

        public override void ModifyStealth(float baseStealth, ref float stealth)
        {
            if (m_character != null && m_character.IsCrouching() && _nearVegetation)
                stealth -= baseStealth * 0.8f;
        }

        public override string GetIconText() => "";
        public override string GetTooltipString() => "Nature's Shroud: Nearly undetectable while crouched in vegetation";

        private bool CheckForVegetation()
        {
            if (m_character == null) return false;

            var colliders = Physics.OverlapSphere(m_character.transform.position, VegetationCheckRadius, PieceMask);
            foreach (var col in colliders)
            {
                string name = col.gameObject.name.ToLowerInvariant();
                if (name.StartsWith("bush") ||
                    name.StartsWith("shrub") ||
                    name.Contains("_bush") ||
                    name.Contains("vines") ||
                    name.StartsWith("beech_sapling") ||
                    name.StartsWith("firtree_sapling") ||
                    name.StartsWith("pickable_branch"))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Ghost Stride: 70% noise reduction while running.
    /// </summary>
    public class SE_GhostStride : StatusEffect
    {
        public override bool IsDone() => false;

        public override void ModifyNoise(float baseNoise, ref float noise)
        {
            noise -= baseNoise * 0.7f;
        }

        public override string GetIconText() => "";
        public override string GetTooltipString() => "Ghost Stride: Footsteps are significantly quieter while running";
    }

    // ══════════════════════════════════════════
    //  Temporary active ability status effects (HUD display)
    // ══════════════════════════════════════════

    /// <summary>
    /// HUD status effect for Marked by Fate — shows icon + duration timer.
    /// </summary>
    public class SE_MarkedByFate : StatusEffect
    {
        public const string SEName = "SE_MarkedByFate";

        public override bool IsDone()
        {
            var player = m_character as Player;
            if (player == null) return true;
            return MarkedByFate.GetDurationRemaining(player) <= 0f;
        }

        public override string GetIconText()
        {
            var player = m_character as Player;
            if (player == null) return "";
            float remaining = MarkedByFate.GetDurationRemaining(player);
            if (remaining > 0f)
                return StatusEffect.GetTimeString(remaining);
            return "";
        }

        public override string GetTooltipString() => "Marked by Fate: Enemy scan active";
    }

    /// <summary>
    /// HUD status effect for Blade Dance — shows icon + duration timer.
    /// </summary>
    public class SE_BladeDance : StatusEffect
    {
        public const string SEName = "SE_BladeDance";

        public override bool IsDone()
        {
            var player = m_character as Player;
            if (player == null) return true;
            return BladeDance.GetTimeRemaining(player) <= 0f;
        }

        public override string GetIconText()
        {
            var player = m_character as Player;
            if (player == null) return "";
            float remaining = BladeDance.GetTimeRemaining(player);
            if (remaining > 0f)
                return StatusEffect.GetTimeString(remaining);
            return "";
        }

        public override string GetTooltipString() => "Blade Dance: Knife damage doubled";
    }
}
