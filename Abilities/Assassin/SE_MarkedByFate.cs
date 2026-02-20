using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Temporary status effect for Marked by Fate.
    /// Shows on the HUD with the ability icon and remaining duration timer.
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

        public override string GetTooltipString()
        {
            return "Marked by Fate: Enemy scan active";
        }
    }
}
