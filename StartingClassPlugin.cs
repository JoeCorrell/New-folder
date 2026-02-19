using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace StartingClassMod
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class StartingClassPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.shadow.startingclass";
        public const string PluginName = "Starting Class Mod";
        public const string PluginVersion = "1.0.0";

        public static StartingClassPlugin Instance { get; private set; }
        private Harmony _harmony;
        private ClassSelectionUI _classSelectionUI;

        // Ability slot selection — ALT cycles, Z activates
        private static int _selectedAbilitySlot;

        // Frame number when a controller combo (RB+X/Y) was consumed.
        // Used by TakeInput patch to block game input for that frame only.
        internal static int ComboConsumedFrame = -1;

        /// <summary>Currently selected ability slot (0-based among unlocked active abilities).</summary>
        public static int SelectedAbilitySlot => _selectedAbilitySlot;

        private void Awake()
        {
            Instance = this;
            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll();
            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded.");
        }

        private void Update()
        {
            if (Player.m_localPlayer == null) return;

            // Don't process while typing in chat, console, or other text fields
            if (Console.IsVisible() || Chat.instance?.HasFocus() == true)
                return;
            if (TextInput.IsVisible())
                return;

            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // Shift+Z — Toggle class UI (or RB+X on controller)
            bool keyboardToggle = shiftHeld && Input.GetKeyDown(KeyCode.Z);
            bool controllerToggle = ZInput.GetButton("JoyTabRight") && ZInput.GetButtonDown("JoyButtonX");

            if (keyboardToggle || controllerToggle)
            {
                if (controllerToggle) ComboConsumedFrame = Time.frameCount;
                if (IsClassMenuOpen)
                    HideClassSelection();
                else
                    ShowClassSelection(true);
            }

            // ALT or RB+B on controller — Cycle selected ability slot
            bool keyboardCycle = Input.GetKeyDown(KeyCode.LeftAlt);
            bool controllerCycle = ZInput.GetButton("JoyTabRight") && ZInput.GetButtonDown("JoyButtonB");

            if ((keyboardCycle || controllerCycle) && !IsClassMenuOpen)
            {
                if (controllerCycle) ComboConsumedFrame = Time.frameCount;
                string cls = ClassPersistence.GetSelectedClassName(Player.m_localPlayer);
                if (!string.IsNullOrEmpty(cls))
                    CycleAbilitySlot(Player.m_localPlayer, cls);
            }

            // Z (no shift) or RB+Y on controller — Activate selected ability
            bool keyboardAbility = Input.GetKeyDown(KeyCode.Z) && !shiftHeld;
            bool controllerAbility = ZInput.GetButton("JoyTabRight") && ZInput.GetButtonDown("JoyButtonY");

            if ((keyboardAbility || controllerAbility) && !IsClassMenuOpen)
            {
                if (controllerAbility) ComboConsumedFrame = Time.frameCount;
                string cls = ClassPersistence.GetSelectedClassName(Player.m_localPlayer);
                if (!string.IsNullOrEmpty(cls))
                    ActivateAbilityAtSlot(Player.m_localPlayer, cls);
            }

            // Update tracked enemy marks and ability HUD each frame
            MarkedByFate.UpdateMarks();
            AbilityHud.UpdateHud(Player.m_localPlayer);
        }

        private static void CycleAbilitySlot(Player player, string className)
        {
            int count = CountUnlockedActiveAbilities(player, className);
            if (count <= 1) return;
            _selectedAbilitySlot = (_selectedAbilitySlot + 1) % count;
        }

        private static int CountUnlockedActiveAbilities(Player player, string className)
        {
            switch (className)
            {
                case "Assassin":
                    int c = 0;
                    if (AbilityManager.IsAbilityUnlocked(player, "Assassin", 1)) c++;
                    if (AbilityManager.IsAbilityUnlocked(player, "Assassin", 5)) c++;
                    return c;
                default:
                    return 0;
            }
        }

        private static void ActivateAbilityAtSlot(Player player, string className)
        {
            // Clamp slot to valid range
            int count = CountUnlockedActiveAbilities(player, className);
            if (count == 0) return;
            if (_selectedAbilitySlot >= count) _selectedAbilitySlot = 0;

            switch (className)
            {
                case "Assassin":
                    int current = 0;
                    if (AbilityManager.IsAbilityUnlocked(player, "Assassin", 1))
                    {
                        if (current == _selectedAbilitySlot) { MarkedByFate.TryActivate(player); return; }
                        current++;
                    }
                    if (AbilityManager.IsAbilityUnlocked(player, "Assassin", 5))
                    {
                        if (current == _selectedAbilitySlot) { BladeDance.TryActivate(player); return; }
                    }
                    break;
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        /// <summary>
        /// Opens the class selection UI. Called by the first-time detection patch
        /// or the /OpenClassMenu console command.
        /// </summary>
        public void ShowClassSelection(bool isFromCommand = false)
        {
            // Prevent double-open (e.g. death/respawn triggering OnSpawned again)
            if (IsClassMenuOpen && !isFromCommand) return;

            if (_classSelectionUI == null)
            {
                var go = new GameObject("StartingClassUI");
                DontDestroyOnLoad(go);
                _classSelectionUI = go.AddComponent<ClassSelectionUI>();
            }

            _classSelectionUI.Open(isFromCommand);
        }

        /// <summary>
        /// Returns true if the class selection menu is currently open.
        /// Used by the input blocking patch.
        /// </summary>
        public bool IsClassMenuOpen => _classSelectionUI != null && _classSelectionUI.IsVisible;

        /// <summary>
        /// Closes the class selection UI.
        /// </summary>
        public void HideClassSelection()
        {
            if (_classSelectionUI != null)
            {
                _classSelectionUI.Close();
            }
        }

        public static void Log(string message)
        {
            Instance?.Logger.LogInfo(message);
        }

        public static void LogWarning(string message)
        {
            Instance?.Logger.LogWarning(message);
        }

        public static void LogError(string message)
        {
            Instance?.Logger.LogError(message);
        }
    }
}
