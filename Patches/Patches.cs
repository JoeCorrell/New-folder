using System.Reflection;
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
                if (__instance != Player.m_localPlayer) return;
                if (StartingClassPlugin.Instance == null) return;

                // Only show for characters that haven't selected a class yet
                // (or whose selection was interrupted by a crash)
                if (!ClassPersistence.HasSelectedClass(__instance) || ClassPersistence.IsPending(__instance))
                {
                    StartingClassPlugin.Instance.ShowClassSelection(false);
                }
                else
                {
                    // Re-apply ability status effects on login
                    string className = ClassPersistence.GetSelectedClassName(__instance);
                    if (!string.IsNullOrEmpty(className))
                        AbilityManager.InitializeAbilities(__instance, className);
                }
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

                if (trimmed.StartsWith("/SetSkillPoints", System.StringComparison.OrdinalIgnoreCase))
                {
                    HandleSetSkillPoints(__instance, trimmed);
                    return false;
                }

                if (trimmed.Equals("/ClassReset", System.StringComparison.OrdinalIgnoreCase))
                {
                    HandleClassReset(__instance);
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
                    // Remove mod-applied status effects before clearing data
                    var seman = player.GetSEMan();
                    if (seman != null)
                    {
                        seman.RemoveStatusEffect("SE_ShadowStep".GetStableHashCode());
                        seman.RemoveStatusEffect("SE_NaturesShroud".GetStableHashCode());
                        seman.RemoveStatusEffect("SE_GhostStride".GetStableHashCode());
                    }
                    MarkedByFate.ClearAllMarks();
                    BladeDance.Reset();
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

            private static void HandleSetSkillPoints(Terminal terminal, string input)
            {
                terminal.m_input.text = "";

                var player = Player.m_localPlayer;
                if (player == null)
                {
                    terminal.AddString("StartingClass: No local player found. Must be in-game.");
                    return;
                }

                // Parse amount from "/SetSkillPoints 100"
                string[] parts = input.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || !int.TryParse(parts[1], out int amount) || amount < 0)
                {
                    terminal.AddString("Usage: /SetSkillPoints <amount>  (e.g. /SetSkillPoints 100)");
                    return;
                }

                SkillPointSystem.SetPoints(player, amount);
                terminal.AddString($"StartingClass: Skill points set to {amount}.");
            }

            private static void HandleClassReset(Terminal terminal)
            {
                terminal.m_input.text = "";

                var player = Player.m_localPlayer;
                if (player == null)
                {
                    terminal.AddString("StartingClass: No local player found. Must be in-game.");
                    return;
                }

                string className = ClassPersistence.GetSelectedClassName(player);
                if (string.IsNullOrEmpty(className))
                {
                    terminal.AddString("StartingClass: No class selected. Nothing to reset.");
                    return;
                }

                int refunded = AbilityManager.ResetAbilities(player, className);

                // Remove active SEs
                var seman = player.GetSEMan();
                if (seman != null)
                {
                    seman.RemoveStatusEffect("SE_ShadowStep".GetStableHashCode());
                    seman.RemoveStatusEffect("SE_NaturesShroud".GetStableHashCode());
                    seman.RemoveStatusEffect("SE_GhostStride".GetStableHashCode());
                }
                MarkedByFate.ClearAllMarks();
                BladeDance.Reset();

                terminal.AddString($"StartingClass: Reset all abilities for {className}. Refunded {refunded} skill points.");
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

        /// <summary>
        /// Close the class selection UI when the inventory is opened.
        /// Patches InventoryGui.Show so pressing Tab/inventory key dismisses our menu.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
        public static class InventoryGui_Show_Patch
        {
            static void Postfix()
            {
                StartingClassPlugin.Instance?.HideClassSelection();
            }
        }

        /// <summary>
        /// Award skill points when a creature dies.
        /// Character.OnDeath is protected virtual — use string-based patch.
        /// </summary>
        [HarmonyPatch(typeof(Character), "OnDeath")]
        public static class Character_OnDeath_Patch
        {
            private static readonly FieldInfo LastHitField =
                AccessTools.Field(typeof(Character), "m_lastHit");

            static void Postfix(Character __instance)
            {
                if (__instance == null || __instance.IsPlayer()) return;
                if (LastHitField == null) return;

                var lastHit = LastHitField.GetValue(__instance) as HitData;
                if (lastHit == null) return;

                var attacker = lastHit.GetAttacker();
                if (attacker == null || attacker != Player.m_localPlayer) return;

                SkillPointSystem.OnEnemyKilled(__instance, Player.m_localPlayer);
            }
        }

        /// <summary>
        /// Award bonus skill points when a class-relevant skill levels up.
        /// Hooks Player.OnSkillLevelup which is called from Skills.RaiseSkill on level change.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.OnSkillLevelup))]
        public static class Player_OnSkillLevelup_Patch
        {
            static void Postfix(Player __instance, Skills.SkillType skill)
            {
                if (__instance != Player.m_localPlayer) return;
                SkillPointSystem.OnSkillLevelup(__instance, skill);
            }
        }

        /// <summary>
        /// Clean up marked enemies and ability HUD when logging out.
        /// </summary>
        [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
        public static class Game_Logout_MarkedByFate_Patch
        {
            static void Prefix()
            {
                MarkedByFate.ClearAllMarks();
                BladeDance.Reset();
                AbilityHud.Destroy();
            }
        }
    }
}
