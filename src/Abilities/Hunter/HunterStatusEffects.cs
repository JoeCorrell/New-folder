using UnityEngine;

namespace StartingClassMod
{
    // ══════════════════════════════════════════
    //  Hunter persistent passive status effects
    // ══════════════════════════════════════════

    /// <summary>
    /// Survivalist: +10% move speed outdoors, +15% stamina regen in forest/meadows.
    /// </summary>
    public class SE_Survivalist : StatusEffect
    {
        private const float BiomeCheckInterval = 0.5f;

        private float _biomeCheckTimer;
        private bool _inBonusBiome;

        public override bool IsDone() => false;

        public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
        {
            var player = character as Player;
            if (player != null && !player.InShelter())
                speed += baseSpeed * 0.1f;
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            if (m_character == null) return;

            // Cache biome check every 0.5s to avoid per-frame Heightmap lookup
            _biomeCheckTimer -= dt;
            if (_biomeCheckTimer <= 0f)
            {
                _biomeCheckTimer = BiomeCheckInterval;
                Heightmap.Biome biome = Heightmap.FindBiome(m_character.transform.position);
                _inBonusBiome = biome == Heightmap.Biome.Meadows || biome == Heightmap.Biome.BlackForest;
            }
        }

        // Hooks into Player's stamina regen calculation: staminaRegen starts at 1f,
        // regen is multiplied by this value, so *1.15 = +15%.
        public override void ModifyStaminaRegen(ref float staminaRegen)
        {
            if (_inBonusBiome)
                staminaRegen *= 1.15f;
        }

        public override string GetIconText() => "";
        public override string GetTooltipString() => "Survivalist: +10% speed outdoors, +15% stamina regen in meadows/forest";
    }

    // ══════════════════════════════════════════
    //  Hunter temporary active ability status effects (HUD display)
    // ══════════════════════════════════════════

    /// <summary>
    /// HUD status effect for Hunter's Instinct — shows icon + duration timer.
    /// </summary>
    public class SE_HuntersInstinct : StatusEffect
    {
        public const string SEName = "SE_HuntersInstinct";

        public override bool IsDone()
        {
            var player = m_character as Player;
            if (player == null) return true;
            return HuntersInstinct.GetDurationRemaining(player) <= 0f;
        }

        public override string GetIconText()
        {
            var player = m_character as Player;
            if (player == null) return "";
            float remaining = HuntersInstinct.GetDurationRemaining(player);
            if (remaining > 0f)
                return StatusEffect.GetTimeString(remaining);
            return "";
        }

        public override string GetTooltipString() => "Hunter's Instinct: Enemy scan active";
    }

    /// <summary>
    /// HUD status effect for Pathfinder — shows icon + duration timer.
    /// </summary>
    public class SE_Pathfinder : StatusEffect
    {
        public const string SEName = "SE_Pathfinder";

        public override bool IsDone()
        {
            var player = m_character as Player;
            if (player == null) return true;
            return Pathfinder.GetTimeRemaining(player) <= 0f;
        }

        public override string GetIconText()
        {
            var player = m_character as Player;
            if (player == null) return "";
            float remaining = Pathfinder.GetTimeRemaining(player);
            if (remaining > 0f)
                return StatusEffect.GetTimeString(remaining);
            return "";
        }

        public override string GetTooltipString() => "Pathfinder: Creature trails active";
    }
}
