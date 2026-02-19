using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Persistent status effect for Nature's Shroud.
    /// While crouched near vegetation (bushes, shrubs, beech saplings),
    /// the player's stealth factor is massively boosted, making them
    /// virtually undetectable to nearby enemies.
    /// </summary>
    public class SE_NaturesShroud : StatusEffect
    {
        private const float VegetationCheckRadius = 4f;
        private const float CheckInterval = 0.5f;
        private float _checkTimer;
        private bool _nearVegetation;

        // Layer that small props/vegetation occupy in Valheim
        private static readonly int PieceMask = LayerMask.GetMask("piece", "piece_nonsolid", "Default_small");

        public override bool IsDone()
        {
            return false;
        }

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
            if (m_character == null) return;
            if (m_character.IsCrouching() && _nearVegetation)
            {
                // Massively boost stealth — make the player nearly invisible
                stealth -= baseStealth * 0.8f;
            }
        }

        public override string GetIconText()
        {
            return "";
        }

        public override string GetTooltipString()
        {
            return "Nature's Shroud: Nearly undetectable while crouched in vegetation";
        }

        private bool CheckForVegetation()
        {
            if (m_character == null) return false;

            var colliders = Physics.OverlapSphere(m_character.transform.position, VegetationCheckRadius, PieceMask);
            foreach (var col in colliders)
            {
                string name = col.gameObject.name;
                // Check for common vegetation prefab names in Valheim
                if (name.StartsWith("Bush") ||
                    name.StartsWith("Shrub") ||
                    name.StartsWith("bush") ||
                    name.StartsWith("shrub") ||
                    name.Contains("_bush") ||
                    name.Contains("vines") ||
                    name.StartsWith("Beech_Sapling") ||
                    name.StartsWith("FirTree_Sapling") ||
                    name.StartsWith("Pickable_Branch"))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
