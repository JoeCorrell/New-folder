using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace StartingClassMod
{
    /// <summary>
    /// Overrides the guardian power HUD to show the selected class ability
    /// when one is active. Uses a Harmony postfix on Hud.UpdateGuardianPower
    /// so our override runs AFTER vanilla sets the HUD, preventing flicker.
    /// </summary>
    public static class AbilityHud
    {
        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();
        private static Texture2D _placeholderTex;

        private static readonly Dictionary<string, Color> ClassColors = new Dictionary<string, Color>
        {
            { "Archer",   new Color(0.53f, 0.78f, 0.35f) },
            { "Assassin", new Color(0.90f, 0.15f, 0.10f) },
            { "Builder",  new Color(0.72f, 0.53f, 0.26f) },
            { "Explorer", new Color(0.40f, 0.70f, 0.90f) },
            { "Farmer",   new Color(0.45f, 0.72f, 0.25f) },
            { "Healer",   new Color(0.95f, 0.85f, 0.40f) },
            { "Hunter",   new Color(0.40f, 0.60f, 0.30f) },
            { "Miner",    new Color(0.60f, 0.60f, 0.65f) }
        };

        // Vanilla cooldown tint color (matches Hud.s_colorRedBlueZeroAlpha)
        private static readonly Color CooldownTint = new Color(1f, 0f, 1f, 0f);

        /// <summary>
        /// Called from Harmony postfix on Hud.UpdateGuardianPower.
        /// Runs AFTER vanilla sets the GP HUD, so our override always wins.
        /// </summary>
        public static void OverrideGuardianPowerHud(Player player)
        {
            if (player == null || Hud.instance == null) return;

            string activePower = ActivePowerManager.GetActivePower(player);
            if (activePower == ActivePowerManager.Forsaken) return;

            string className = ClassPersistence.GetSelectedClassName(player);
            if (string.IsNullOrEmpty(className)) return;

            var hud = Hud.instance;
            if (hud.m_gpRoot == null) return;

            // Ensure the GP HUD is visible (vanilla hides it when no forsaken power is set)
            if (!hud.m_gpRoot.gameObject.activeSelf)
                hud.m_gpRoot.gameObject.SetActive(true);

            EnsureIconsLoaded(className);

            // Override icon
            if (hud.m_gpIcon != null)
            {
                Sprite icon = GetAbilitySprite(activePower);
                if (icon != null)
                    hud.m_gpIcon.sprite = icon;
            }

            // Override name and cooldown/status
            switch (activePower)
            {
                case "MarkedByFate":
                    UpdateMarkedByFateHud(player, hud);
                    break;
                case "BladeDance":
                    UpdateBladeDanceHud(player, hud);
                    break;
                case "HuntersInstinct":
                    UpdateHuntersInstinctHud(player, hud);
                    break;
                case "Pathfinder":
                    UpdatePathfinderHud(player, hud);
                    break;
            }
        }

        private static void UpdateMarkedByFateHud(Player player, Hud hud)
        {
            if (hud.m_gpName != null)
                hud.m_gpName.text = "Marked by Fate";

            if (hud.m_gpCooldown != null)
            {
                hud.m_gpCooldown.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                hud.m_gpCooldown.overflowMode = TMPro.TextOverflowModes.Overflow;

                if (MarkedByFate.IsActive(player))
                {
                    float dur = MarkedByFate.GetDurationRemaining(player);
                    int marks = MarkedByFate.GetActiveMarkCount();
                    hud.m_gpCooldown.text = $"{StatusEffect.GetTimeString(dur)} ({marks})";
                    hud.m_gpCooldown.color = Color.white;
                    if (hud.m_gpIcon != null)
                        hud.m_gpIcon.color = Color.white;
                }
                else
                {
                    float cd = MarkedByFate.GetCooldownRemaining(player);
                    if (cd > 0f)
                    {
                        hud.m_gpCooldown.text = StatusEffect.GetTimeString(cd);
                        hud.m_gpCooldown.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                        if (hud.m_gpIcon != null)
                            hud.m_gpIcon.color = CooldownTint;
                    }
                    else
                    {
                        hud.m_gpCooldown.text = "Ready";
                        hud.m_gpCooldown.color = Color.white;
                        if (hud.m_gpIcon != null)
                            hud.m_gpIcon.color = Color.white;
                    }
                }
            }
        }

        private static void UpdateBladeDanceHud(Player player, Hud hud)
        {
            if (hud.m_gpName != null)
                hud.m_gpName.text = "Blade Dance";

            if (hud.m_gpCooldown != null)
            {
                hud.m_gpCooldown.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                hud.m_gpCooldown.overflowMode = TMPro.TextOverflowModes.Overflow;

                if (BladeDance.IsActive())
                {
                    float remaining = BladeDance.GetTimeRemaining();
                    hud.m_gpCooldown.text = $"{remaining:0}s";
                    hud.m_gpCooldown.color = Color.white;
                    if (hud.m_gpIcon != null)
                        hud.m_gpIcon.color = Color.white;
                }
                else
                {
                    float cd = BladeDance.GetCooldownRemaining(player);
                    if (cd > 0f)
                    {
                        hud.m_gpCooldown.text = StatusEffect.GetTimeString(cd);
                        hud.m_gpCooldown.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                        if (hud.m_gpIcon != null)
                            hud.m_gpIcon.color = CooldownTint;
                    }
                    else
                    {
                        hud.m_gpCooldown.text = "Ready";
                        hud.m_gpCooldown.color = Color.white;
                        if (hud.m_gpIcon != null)
                            hud.m_gpIcon.color = Color.white;
                    }
                }
            }
        }

        private static void UpdateHuntersInstinctHud(Player player, Hud hud)
        {
            if (hud.m_gpName != null)
                hud.m_gpName.text = "Hunter's Instinct";

            if (hud.m_gpCooldown != null)
            {
                hud.m_gpCooldown.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                hud.m_gpCooldown.overflowMode = TMPro.TextOverflowModes.Overflow;

                if (HuntersInstinct.IsActive(player))
                {
                    float dur = HuntersInstinct.GetDurationRemaining(player);
                    int marks = HuntersInstinct.GetActiveMarkCount();
                    hud.m_gpCooldown.text = $"{StatusEffect.GetTimeString(dur)} ({marks})";
                    hud.m_gpCooldown.color = Color.white;
                    if (hud.m_gpIcon != null)
                        hud.m_gpIcon.color = Color.white;
                }
                else
                {
                    float cd = HuntersInstinct.GetCooldownRemaining(player);
                    if (cd > 0f)
                    {
                        hud.m_gpCooldown.text = StatusEffect.GetTimeString(cd);
                        hud.m_gpCooldown.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                        if (hud.m_gpIcon != null)
                            hud.m_gpIcon.color = CooldownTint;
                    }
                    else
                    {
                        hud.m_gpCooldown.text = "Ready";
                        hud.m_gpCooldown.color = Color.white;
                        if (hud.m_gpIcon != null)
                            hud.m_gpIcon.color = Color.white;
                    }
                }
            }
        }

        private static void UpdatePathfinderHud(Player player, Hud hud)
        {
            if (hud.m_gpName != null)
                hud.m_gpName.text = "Pathfinder";

            if (hud.m_gpCooldown != null)
            {
                hud.m_gpCooldown.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                hud.m_gpCooldown.overflowMode = TMPro.TextOverflowModes.Overflow;

                if (Pathfinder.IsActive())
                {
                    float remaining = Pathfinder.GetTimeRemaining();
                    hud.m_gpCooldown.text = $"{remaining:0}s";
                    hud.m_gpCooldown.color = Color.white;
                    if (hud.m_gpIcon != null)
                        hud.m_gpIcon.color = Color.white;
                }
                else
                {
                    float cd = Pathfinder.GetCooldownRemaining(player);
                    if (cd > 0f)
                    {
                        hud.m_gpCooldown.text = StatusEffect.GetTimeString(cd);
                        hud.m_gpCooldown.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                        if (hud.m_gpIcon != null)
                            hud.m_gpIcon.color = CooldownTint;
                    }
                    else
                    {
                        hud.m_gpCooldown.text = "Ready";
                        hud.m_gpCooldown.color = Color.white;
                        if (hud.m_gpIcon != null)
                            hud.m_gpIcon.color = Color.white;
                    }
                }
            }
        }

        /// <summary>Clean up when logging out.</summary>
        public static void Destroy()
        {
            if (_placeholderTex != null)
                Object.Destroy(_placeholderTex);
            _placeholderTex = null;
            _spriteCache.Clear();
        }

        private static string _loadedClass;

        private static void EnsureIconsLoaded(string className)
        {
            // Reload if class changed (e.g., player reset and chose a different class)
            if (_loadedClass == className && _spriteCache.Count > 0) return;

            _spriteCache.Clear();
            if (_placeholderTex != null)
            {
                Object.Destroy(_placeholderTex);
                _placeholderTex = null;
            }
            _loadedClass = className;

            string[] iconNames;
            switch (className)
            {
                case "Assassin":
                    iconNames = new[] { "MarkedByFate", "BladeDance" };
                    break;
                case "Hunter":
                    iconNames = new[] { "HuntersInstinct", "Pathfinder" };
                    break;
                default:
                    iconNames = new string[0];
                    break;
            }

            foreach (string name in iconNames)
            {
                var sprite = TextureLoader.LoadAbilitySprite(name);
                if (sprite != null)
                    _spriteCache[name] = sprite;
            }

            if (_spriteCache.Count == 0)
            {
                Color iconColor = GetClassColor(className);
                _placeholderTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                Color transparent = new Color(0, 0, 0, 0);
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        int dx = Mathf.Abs(x - 32);
                        int dy = Mathf.Abs(y - 32);
                        _placeholderTex.SetPixel(x, y, (dx + dy <= 28) ? iconColor : transparent);
                    }
                _placeholderTex.Apply();
                _spriteCache["_fallback"] = Sprite.Create(
                    _placeholderTex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
            }
        }

        private static Sprite GetAbilitySprite(string abilityId)
        {
            if (_spriteCache.TryGetValue(abilityId, out Sprite s))
                return s;
            if (_spriteCache.TryGetValue("_fallback", out Sprite fb))
                return fb;
            return null;
        }

        private static Color GetClassColor(string className)
        {
            if (ClassColors.TryGetValue(className, out Color c))
                return c;
            return new Color(0.83f, 0.64f, 0.31f);
        }
    }

    /// <summary>
    /// Harmony postfix on Hud.UpdateGuardianPower to override the GP HUD
    /// after vanilla has set its values. This prevents race conditions.
    /// </summary>
    [HarmonyPatch(typeof(Hud), "UpdateGuardianPower")]
    public static class Hud_UpdateGuardianPower_Patch
    {
        static void Postfix(Player player)
        {
            AbilityHud.OverrideGuardianPowerHud(player);
        }
    }
}
