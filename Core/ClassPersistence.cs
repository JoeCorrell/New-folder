namespace StartingClassMod
{
    /// <summary>
    /// Handles persisting class selection data to the character's custom data store.
    /// Uses Player.m_customData which is saved alongside the character profile.
    /// This ensures the data survives game restarts and is tied to the character.
    /// </summary>
    public static class ClassPersistence
    {
        private const string ClassDataKey = "StartingClassMod_SelectedClass";
        private const string ClassPendingKey = "StartingClassMod_Pending";

        /// <summary>
        /// Checks if this character has already selected a starting class.
        /// </summary>
        public static bool HasSelectedClass(Player player)
        {
            if (player == null) return false;
            return player.m_customData.ContainsKey(ClassDataKey);
        }

        /// <summary>
        /// Gets the name of the selected class, or null if none selected.
        /// </summary>
        public static string GetSelectedClassName(Player player)
        {
            if (player == null) return null;
            player.m_customData.TryGetValue(ClassDataKey, out string className);
            return className;
        }

        /// <summary>
        /// Saves the selected class name to the character's custom data.
        /// This will persist across saves and sessions.
        /// </summary>
        public static void SaveSelectedClass(Player player, string className)
        {
            if (player == null) return;
            player.m_customData[ClassDataKey] = className;
            ClearPending(player);
            StartingClassPlugin.Log($"Saved class '{className}' for character.");
        }

        /// <summary>
        /// Marks class selection as pending (started but not yet completed).
        /// Used to handle crash/disconnect during selection.
        /// </summary>
        public static void SetPending(Player player)
        {
            if (player == null) return;
            player.m_customData[ClassPendingKey] = "true";
        }

        /// <summary>
        /// Checks if a class selection was started but never completed.
        /// </summary>
        public static bool IsPending(Player player)
        {
            if (player == null) return false;
            return player.m_customData.ContainsKey(ClassPendingKey);
        }

        /// <summary>
        /// Clears the pending flag after successful class selection.
        /// </summary>
        public static void ClearPending(Player player)
        {
            if (player == null) return;
            player.m_customData.Remove(ClassPendingKey);
        }

        /// <summary>
        /// Removes all class data (for testing purposes only, triggered by command).
        /// </summary>
        public static void ClearAllData(Player player)
        {
            if (player == null) return;
            player.m_customData.Remove(ClassDataKey);
            player.m_customData.Remove(ClassPendingKey);
            StartingClassPlugin.Log("Cleared all starting class data for character.");
        }
    }
}
