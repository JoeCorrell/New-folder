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
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        public static class Player_OnSpawned_Patch
        {
            static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer) return;
                if (StartingClassPlugin.Instance == null) return;

                if (!ClassPersistence.HasSelectedClass(__instance) || ClassPersistence.IsPending(__instance))
                {
                    StartingClassPlugin.Instance.ShowClassSelection(false);
                }
                else
                {
                    // Re-apply equipped passive effects on login
                    string className = ClassPersistence.GetSelectedClassName(__instance);
                    if (!string.IsNullOrEmpty(className))
                        AbilityManager.InitializeAbilities(__instance, className);
                }
            }
        }

        /// <summary>
        /// Patch Terminal.InputText to handle console commands.
        /// </summary>
        [HarmonyPatch(typeof(Terminal), "InputText")]
        public static class Terminal_InputText_Patch
        {
            static bool Prefix(Terminal __instance)
            {
                string text = __instance.m_input.text;
                if (string.IsNullOrEmpty(text)) return true;

                string trimmed = text.Trim();

                if (trimmed.Equals("/OpenClassMenu", System.StringComparison.OrdinalIgnoreCase))
                {
                    HandleOpenClassMenu(__instance, false);
                    return false;
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

                return true;
            }

            private static void HandleOpenClassMenu(Terminal terminal, bool resetData)
            {
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
                        seman.RemoveStatusEffect("SE_Survivalist".GetStableHashCode());
                    }
                    ClassPersistence.ClearAllData(player);
                    terminal.AddString("StartingClass: Cleared class data. Opening selection menu...");
                }
                else
                {
                    string existing = ClassPersistence.GetSelectedClassName(player);
                    if (existing != null)
                    {
                        terminal.AddString($"StartingClass: Current class is '{existing}'. " +
                                          "Class is locked — use /ClassReset to change class.");
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

                // Remove all active status effects granted by the mod
                var seman = player.GetSEMan();
                if (seman != null)
                {
                    seman.RemoveStatusEffect("SE_ShadowStep".GetStableHashCode());
                    seman.RemoveStatusEffect("SE_NaturesShroud".GetStableHashCode());
                    seman.RemoveStatusEffect("SE_GhostStride".GetStableHashCode());
                    seman.RemoveStatusEffect("SE_Survivalist".GetStableHashCode());
                }

                // Refund ability points then snapshot total before ClearAllData wipes them
                string className = ClassPersistence.GetSelectedClassName(player);
                if (!string.IsNullOrEmpty(className))
                    AbilityManager.ResetAbilities(player, className);
                int savedPoints = SkillPointSystem.GetPoints(player);

                // Clear all class mod data (class, abilities, equip slots, XP, skill points)
                ClassPersistence.ClearAllData(player);

                // Restore the refunded point balance
                if (savedPoints > 0)
                    SkillPointSystem.SetPoints(player, savedPoints);

                terminal.AddString("StartingClass: Class fully reset. Opening class selection menu...");
                StartingClassPlugin.Instance.ShowClassSelection(false);
            }
        }

        /// <summary>
        /// Patch Game.Logout to clean up mod state.
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
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Make InventoryGui.IsVisible() return true when our class menu is open.
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
        /// Also closes the class selection menu when the local player dies.
        /// </summary>
        [HarmonyPatch(typeof(Character), "OnDeath")]
        public static class Character_OnDeath_Patch
        {
            private static readonly FieldInfo LastHitField =
                AccessTools.Field(typeof(Character), "m_lastHit");

            static void Postfix(Character __instance)
            {
                if (__instance == null) return;

                if (__instance.IsPlayer())
                {
                    if (__instance == Player.m_localPlayer)
                        StartingClassPlugin.Instance?.HideClassSelection();
                    return;
                }

                if (LastHitField == null) return;

                var lastHit = LastHitField.GetValue(__instance) as HitData;
                if (lastHit == null) return;

                var attacker = lastHit.GetAttacker();
                if (attacker == null || attacker != Player.m_localPlayer) return;

                SkillPointSystem.OnEnemyKilled(__instance, Player.m_localPlayer);
            }
        }

        /// <summary>
        /// Enhance armor piece protection based on the equipped set's enhancement level.
        /// </summary>
        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetArmor), new System.Type[] { typeof(int), typeof(float) })]
        public static class ItemData_GetArmor_Patch
        {
            static void Postfix(ItemDrop.ItemData __instance, ref float __result)
            {
                var player = Player.m_localPlayer;
                if (player == null || !__instance.m_equipped) return;
                __result += ArmorUpgradeSystem.GetItemSetBonus(player, __instance);
            }
        }

        /// <summary>
        /// Suppress game actions tied to the X face button so vanilla doesn't
        /// process them while our class menu is open or when RB is held.
        /// </summary>
        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButtonDown))]
        public static class ZInput_GetButtonDown_Patch
        {
            private static readonly System.Collections.Generic.HashSet<string> SuppressXActions =
                new System.Collections.Generic.HashSet<string>
                {
                    "JoySit", "JoyUse", "JoyButtonX"
                };

            static bool Prefix(string name, ref bool __result)
            {
                if (!SuppressXActions.Contains(name)) return true;

                if (StartingClassPlugin.Instance != null &&
                    StartingClassPlugin.Instance.IsClassMenuOpen)
                {
                    __result = false;
                    return false;
                }

                if (ZInput.GetButton("JoyTabRight"))
                {
                    __result = false;
                    return false;
                }

                return true;
            }
        }
    }
}
