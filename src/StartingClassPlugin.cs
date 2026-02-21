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

        private void Awake()
        {
            Instance = this;
            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll();

            // Register active abilities with the central registry
            MarkedByFate.Register();
            BladeDance.Register();
            HuntersInstinct.Register();
            Pathfinder.Register();

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

            // Z — Toggle class UI (or RB+X on controller)
            // Raw Input for X button since ZInput suppresses JoyButtonX when RB is held
            bool keyboardToggle = Input.GetKeyDown(KeyCode.Z);
            bool controllerToggle = ZInput.GetButton("JoyTabRight") && Input.GetKeyDown(KeyCode.JoystickButton2);

            if (keyboardToggle || controllerToggle)
            {
                if (IsClassMenuOpen)
                    HideClassSelection();
                else
                    ShowClassSelection(true);
            }

            // Update all registered active abilities each frame
            ActiveAbilityRegistry.UpdateAll();
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
