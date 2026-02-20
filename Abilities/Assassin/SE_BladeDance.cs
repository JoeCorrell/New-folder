using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Temporary status effect for Blade Dance.
    /// Shows on the HUD with the ability icon and remaining duration timer.
    /// </summary>
    public class SE_BladeDance : StatusEffect
    {
        public const string SEName = "SE_BladeDance";

        public override bool IsDone()
        {
            return !BladeDance.IsActive();
        }

        public override string GetIconText()
        {
            float remaining = BladeDance.GetTimeRemaining();
            if (remaining > 0f)
                return StatusEffect.GetTimeString(remaining);
            return "";
        }

        public override string GetTooltipString()
        {
            return "Blade Dance: Knife damage doubled";
        }
    }
}
