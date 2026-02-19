using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Persistent status effect for the Assassin's Shadow Step ability.
    /// Increases sneak movement speed by 25% and reduces sneak stamina drain by 25%.
    /// </summary>
    public class SE_ShadowStep : StatusEffect
    {
        public override bool IsDone()
        {
            return false;
        }

        public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
        {
            if (character == null) return;
            if (character.IsCrouching())
                speed += baseSpeed * 0.25f;
        }

        public override void ModifySneakStaminaUsage(float baseStaminaUse, ref float staminaUse)
        {
            staminaUse -= baseStaminaUse * 0.25f;
        }

        public override string GetIconText()
        {
            return "";
        }

        public override string GetTooltipString()
        {
            return "Shadow Step: +25% sneak speed, -25% sneak stamina drain";
        }
    }
}
