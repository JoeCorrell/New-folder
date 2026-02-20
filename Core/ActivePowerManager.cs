namespace StartingClassMod
{
    /// <summary>
    /// Manages which power (forsaken or class ability) is currently selected
    /// as the player's active power, triggered by the GP button.
    /// </summary>
    public static class ActivePowerManager
    {
        private const string DataKey = "StartingClassMod_ActivePower";
        public const string Forsaken = "forsaken";

        /// <summary>Get the currently selected active power ID.</summary>
        public static string GetActivePower(Player player)
        {
            if (player == null) return Forsaken;
            if (player.m_customData.TryGetValue(DataKey, out string value) && !string.IsNullOrEmpty(value))
                return value;
            return Forsaken;
        }

        /// <summary>Set the active power. Use "forsaken" for the default guardian power.</summary>
        public static void SetActivePower(Player player, string powerId)
        {
            if (player == null) return;
            player.m_customData[DataKey] = powerId ?? Forsaken;
        }

        /// <summary>True if a class ability (not forsaken power) is currently selected.</summary>
        public static bool IsClassAbilityActive(Player player)
        {
            return GetActivePower(player) != Forsaken;
        }
    }
}
