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

}
