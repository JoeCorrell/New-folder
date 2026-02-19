using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Persistent status effect for Ghost Stride.
    /// Significantly reduces the noise the player makes while running,
    /// making enemies treat the player's footsteps as if they were sneaking.
    /// Uses the ModifyNoise virtual on StatusEffect.
    /// </summary>
    public class SE_GhostStride : StatusEffect
    {
        public override bool IsDone()
        {
            return false;
        }

        public override void ModifyNoise(float baseNoise, ref float noise)
        {
            // Reduce noise by 70% while running — enemies hear you as though sneaking
            noise -= baseNoise * 0.7f;
        }

        public override string GetIconText()
        {
            return "";
        }

        public override string GetTooltipString()
        {
            return "Ghost Stride: Footsteps are significantly quieter while running";
        }
    }
}
