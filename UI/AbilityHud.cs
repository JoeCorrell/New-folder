using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StartingClassMod
{
    /// <summary>
    /// HUD elements displayed beside the forsaken power icon.
    /// Shows one panel per unlocked active ability, each with its own icon,
    /// name, and status text. Panels are positioned side by side.
    /// </summary>
    public static class AbilityHud
    {
        private class AbilityPanel
        {
            public GameObject Root;
            public Image Icon;
            public TMP_Text NameText;
            public TMP_Text StatusText;
        }

        private static readonly List<AbilityPanel> _panels = new List<AbilityPanel>();
        private static string _builtForClass;
        private static int _builtAbilityCount;

        // Cached sprites for each ability icon (loaded from embedded resources)
        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();
        private static Texture2D _placeholderTex;

        // Class → icon/accent color
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

        /// <summary>Called every frame from plugin Update.</summary>
        public static void UpdateHud(Player player)
        {
            if (player == null || Hud.instance == null)
            {
                HidePanels();
                return;
            }

            string className = ClassPersistence.GetSelectedClassName(player);
            if (string.IsNullOrEmpty(className))
            {
                HidePanels();
                return;
            }

            if (!HasActiveAbility(player, className))
            {
                HidePanels();
                return;
            }

            // Rebuild if class changed or new abilities were unlocked
            int activeCount = CountActiveAbilities(player, className);
            if (_panels.Count > 0 && (_builtForClass != className || activeCount != _builtAbilityCount))
                Destroy();

            // Lazy-create
            if (_panels.Count == 0)
            {
                if (!CreateHud(player, className)) return;
            }

            // Update each panel
            UpdatePanels(player, className);
        }

        /// <summary>Clean up when logging out.</summary>
        public static void Destroy()
        {
            foreach (var panel in _panels)
            {
                if (panel.Root != null)
                    Object.Destroy(panel.Root);
            }
            _panels.Clear();
            if (_placeholderTex != null)
                Object.Destroy(_placeholderTex);
            _placeholderTex = null;
            _builtForClass = null;
            _builtAbilityCount = 0;
            _spriteCache.Clear();
        }

        private static void HidePanels()
        {
            foreach (var panel in _panels)
            {
                if (panel.Root != null) panel.Root.SetActive(false);
            }
        }

        private static void UpdatePanels(Player player, string className)
        {
            Color accent = GetClassColor(className);
            string accentHex = ColorUtility.ToHtmlStringRGB(accent);

            switch (className)
            {
                case "Assassin":
                    UpdateAssassinPanels(player, accentHex);
                    break;
            }
        }

        private static void UpdateAssassinPanels(Player player, string accentHex)
        {
            int panelIdx = 0;
            int selectedSlot = StartingClassPlugin.SelectedAbilitySlot;

            // Marked by Fate panel
            if (AbilityManager.IsAbilityUnlocked(player, "Assassin", 1) && panelIdx < _panels.Count)
            {
                var p = _panels[panelIdx];
                bool isSelected = (panelIdx == selectedSlot);
                p.Root.SetActive(isSelected);

                if (isSelected)
                {
                    p.NameText.text = "Marked by Fate";
                    if (p.Icon != null) p.Icon.color = Color.white;

                    int charges = MarkedByFate.GetChargesRemaining();
                    int active = MarkedByFate.GetActiveMarkCount();
                    if (active > 0)
                        p.StatusText.text = $"<color=#{accentHex}>{active} Marked</color> | {charges} Left";
                    else if (charges >= 3)
                        p.StatusText.text = $"<color=#{accentHex}>Ready</color>";
                    else
                        p.StatusText.text = $"<color=#{accentHex}>{charges} Charges</color>";
                }

                panelIdx++;
            }

            // Blade Dance panel
            if (AbilityManager.IsAbilityUnlocked(player, "Assassin", 5) && panelIdx < _panels.Count)
            {
                var p = _panels[panelIdx];
                bool isSelected = (panelIdx == selectedSlot);
                p.Root.SetActive(isSelected);

                if (isSelected)
                {
                    p.NameText.text = "Blade Dance";
                    if (p.Icon != null) p.Icon.color = Color.white;

                    if (BladeDance.IsActive())
                    {
                        float remaining = BladeDance.GetTimeRemaining();
                        p.StatusText.text = $"<color=#{accentHex}>Active {remaining:0}s</color>";
                    }
                    else
                    {
                        float cd = BladeDance.GetCooldownRemaining(player);
                        if (cd > 0f)
                        {
                            int mins = (int)(cd / 60f);
                            int secs = (int)(cd % 60f);
                            p.StatusText.text = $"<color=#999999>{mins}:{secs:D2}</color>";
                        }
                        else
                        {
                            p.StatusText.text = $"<color=#{accentHex}>Ready</color>";
                        }
                    }
                }

                panelIdx++;
            }

            // Hide unused panels
            for (int i = panelIdx; i < _panels.Count; i++)
            {
                if (_panels[i].Root != null) _panels[i].Root.SetActive(false);
            }
        }

        private static bool HasActiveAbility(Player player, string className)
        {
            return CountActiveAbilities(player, className) > 0;
        }

        private static int CountActiveAbilities(Player player, string className)
        {
            int count = 0;
            switch (className)
            {
                case "Assassin":
                    if (AbilityManager.IsAbilityUnlocked(player, "Assassin", 1)) count++;
                    if (AbilityManager.IsAbilityUnlocked(player, "Assassin", 5)) count++;
                    break;
            }
            return count;
        }

        private static Color GetClassColor(string className)
        {
            if (ClassColors.TryGetValue(className, out Color c))
                return c;
            return new Color(0.83f, 0.64f, 0.31f);
        }

        private static bool CreateHud(Player player, string className)
        {
            var hud = Hud.instance;
            if (hud == null || hud.m_gpRoot == null) return false;

            PreloadAbilityIcons(className);

            // Determine which abilities are unlocked to know how many panels to create
            var abilityKeys = new List<string>();
            switch (className)
            {
                case "Assassin":
                    if (AbilityManager.IsAbilityUnlocked(player, "Assassin", 1))
                        abilityKeys.Add("MarkedByFate");
                    if (AbilityManager.IsAbilityUnlocked(player, "Assassin", 5))
                        abilityKeys.Add("BladeDance");
                    break;
            }

            if (abilityKeys.Count == 0) return false;

            var gpRt = hud.m_gpRoot;
            float panelWidth = gpRt.sizeDelta.x;

            for (int i = 0; i < abilityKeys.Count; i++)
            {
                var go = Object.Instantiate(gpRt.gameObject, gpRt.parent);
                go.name = $"ClassAbilityHud_{abilityKeys[i]}";

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = gpRt.anchorMin;
                rt.anchorMax = gpRt.anchorMax;
                rt.pivot = gpRt.pivot;
                // All panels at same position — only one is visible at a time (ALT to switch)
                rt.anchoredPosition = gpRt.anchoredPosition + new Vector2(panelWidth + 10f, 0f);
                rt.sizeDelta = gpRt.sizeDelta;

                var icon = FindChild<Image>(go, "Icon");
                var nameText = FindChild<TMP_Text>(go, "Name");
                var statusText = FindChild<TMP_Text>(go, "TimeText");

                // Fallback: find by component order
                if (icon == null || nameText == null || statusText == null)
                {
                    var images = go.GetComponentsInChildren<Image>(true);
                    var texts = go.GetComponentsInChildren<TMP_Text>(true);
                    if (icon == null && images.Length >= 2) icon = images[1];
                    if (texts.Length >= 2)
                    {
                        if (nameText == null) nameText = texts[0];
                        if (statusText == null) statusText = texts[texts.Length - 1];
                    }
                }

                if (icon == null || nameText == null || statusText == null)
                {
                    StartingClassPlugin.LogWarning($"AbilityHud: Could not find child components for panel {i}.");
                    Object.Destroy(go);
                    continue;
                }

                nameText.richText = true;
                statusText.richText = true;

                icon.type = Image.Type.Simple;
                icon.preserveAspect = true;

                var iconRt = icon.GetComponent<RectTransform>();
                if (iconRt != null)
                    iconRt.localScale = new Vector3(1.5f, 1.5f, 1f);

                // Set icon sprite
                if (_spriteCache.TryGetValue(abilityKeys[i], out Sprite sprite))
                    icon.sprite = sprite;
                else if (_spriteCache.TryGetValue("_fallback", out Sprite fallback))
                    icon.sprite = fallback;

                icon.color = Color.white;
                go.SetActive(true);

                _panels.Add(new AbilityPanel
                {
                    Root = go,
                    Icon = icon,
                    NameText = nameText,
                    StatusText = statusText
                });
            }

            _builtForClass = className;
            _builtAbilityCount = _panels.Count;
            return _panels.Count > 0;
        }

        private static void PreloadAbilityIcons(string className)
        {
            _spriteCache.Clear();

            string[] iconNames;
            switch (className)
            {
                case "Assassin":
                    iconNames = new[] { "MarkedByFate", "BladeDance" };
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

        private static T FindChild<T>(GameObject parent, string childName) where T : Component
        {
            var t = parent.transform.Find(childName);
            if (t != null) return t.GetComponent<T>();

            foreach (Transform child in parent.transform)
            {
                if (child.name.IndexOf(childName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return child.GetComponent<T>();
            }
            return null;
        }
    }
}
