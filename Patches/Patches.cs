using HarmonyLib;

namespace StartingClassMod
{
    /// <summary>
    /// Harmony patches for detecting first-time character world entry
    /// and handling edge cases.
    /// </summary>
    public static class Patches
    {
        /// <summary>
        /// Patch Player.OnSpawned to detect when a character enters a world.
        /// OnSpawned is called once after the player is fully loaded and placed in the world.
        /// This is the ideal hook point because:
        /// - The player object is fully initialized
        /// - Inventory and skills systems are ready
        /// - ZNetScene is available for item prefab lookups
        /// - Custom data has been loaded from the save
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        public static class Player_OnSpawned_Patch
        {
            static void Postfix(Player __instance)
            {
                // Only process the local player
                if (__instance != Player.m_localPlayer) return;

                // Always open the class selection UI on world start (for testing)
                StartingClassPlugin.Log("Opening class selection UI.");
                StartingClassPlugin.Instance.ShowClassSelection(false);
            }
        }

        /// <summary>
        /// Patch Terminal.InputText to handle the /OpenClassMenu console command.
        /// </summary>
        [HarmonyPatch(typeof(Terminal), "InputText")]
        public static class Terminal_InputText_Patch
        {
            static bool Prefix(Terminal __instance)
            {
                string text = __instance.m_input.text;
                if (string.IsNullOrEmpty(text)) return true;

                // Check for our command (case-insensitive)
                string trimmed = text.Trim();

                if (trimmed.Equals("/OpenClassMenu", System.StringComparison.OrdinalIgnoreCase))
                {
                    HandleOpenClassMenu(__instance, false);
                    return false; // Skip original method
                }

                if (trimmed.Equals("/OpenClassMenu reset", System.StringComparison.OrdinalIgnoreCase))
                {
                    HandleOpenClassMenu(__instance, true);
                    return false;
                }

                return true; // Let other commands through
            }

            private static void HandleOpenClassMenu(Terminal terminal, bool resetData)
            {
                // Clear the input
                terminal.m_input.text = "";

                var player = Player.m_localPlayer;
                if (player == null)
                {
                    terminal.AddString("StartingClass: No local player found. Must be in-game.");
                    return;
                }

                if (resetData)
                {
                    ClassPersistence.ClearAllData(player);
                    terminal.AddString("StartingClass: Cleared class data. Opening selection menu...");
                }
                else
                {
                    string existing = ClassPersistence.GetSelectedClassName(player);
                    if (existing != null)
                    {
                        terminal.AddString($"StartingClass: Current class is '{existing}'. " +
                                          "Opening menu (will overwrite on new selection).");
                    }
                    else
                    {
                        terminal.AddString("StartingClass: No class selected yet. Opening selection menu...");
                    }
                }

                StartingClassPlugin.Instance.ShowClassSelection(true);
            }
        }

        /// <summary>
        /// Patch Player.OnDeath to ensure the class data is not lost on death.
        /// This is a safety measure - m_customData persists through death already,
        /// but we log to confirm.
        /// </summary>
        [HarmonyPatch(typeof(Player), "OnDeath")]
        public static class Player_OnDeath_Patch
        {
            static void Prefix(Player __instance)
            {
                if (__instance != Player.m_localPlayer) return;

                if (ClassPersistence.HasSelectedClass(__instance))
                {
                    StartingClassPlugin.Log("Player died. Class data is preserved in custom data.");
                }
            }
        }

        /// <summary>
        /// Patch Game.Logout to close the class selection UI if open during logout.
        /// Prevents lingering UI or state corruption.
        /// </summary>
        [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
        public static class Game_Logout_Patch
        {
            static void Prefix()
            {
                StartingClassPlugin.Instance?.HideClassSelection();
            }
        }

        /// <summary>
        /// Block player input while the class selection UI is open.
        /// Patches Player.TakeInput to return false when our UI is visible,
        /// which prevents movement, attacks, and other actions.
        /// </summary>
        [HarmonyPatch(typeof(Player), "TakeInput")]
        public static class Player_TakeInput_Patch
        {
            static bool Prefix(ref bool __result, Player __instance)
            {
                if (__instance != Player.m_localPlayer) return true;
                if (StartingClassPlugin.Instance != null && StartingClassPlugin.Instance.IsClassMenuOpen)
                {
                    __result = false;
                    return false; // Skip original
                }
                return true;
            }
        }

        /// <summary>
        /// Make InventoryGui.IsVisible() return true when our class menu is open.
        /// This tells GameCamera, Hud, and all other game systems that a menu is active,
        /// which unlocks the cursor, stops camera rotation, and blocks all game input.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.IsVisible))]
        public static class InventoryGui_IsVisible_Patch
        {
            static void Postfix(ref bool __result)
            {
                if (!__result && StartingClassPlugin.Instance != null && StartingClassPlugin.Instance.IsClassMenuOpen)
                    __result = true;
            }
        }
    }
}
