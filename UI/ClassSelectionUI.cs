using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StartingClassMod
{
    /// <summary>
    /// Class selection UI built by cloning Valheim's crafting panel at runtime.
    /// The left side lists available classes (like recipes), the right side shows
    /// class details (like item info), and the requirement slots show starting items.
    /// Full controller/gamepad support matching Valheim's input scheme.
    /// </summary>
    public class ClassSelectionUI : MonoBehaviour
    {
        // ── State ──
        private bool _isVisible;
        private bool _isFromCommand;
        private int _selectedIndex = -1;
        private List<StartingClass> _classes;

        // ── UI root ──
        private GameObject _canvasGO;
        private bool _uiBuilt;

        // ── Cloned crafting panel references ──
        private GameObject _clonedPanel;
        private TMP_Text _titleText;
        private Image _recipeIcon;
        private TMP_Text _recipeName;
        private TMP_Text _recipeDescription;
        private RectTransform _recipeListRoot;
        private float _recipeListSpace;
        private Button _craftButton;
        private TMP_Text _craftButtonLabel;
        private GameObject[] _requirementSlots;
        private Scrollbar _recipeScrollbar;
        private ScrollRect _listScrollRect;
        private ScrollRectEnsureVisible _ensureVisible;

        // ── Class list elements (instantiated from recipe element prefab) ──
        private readonly List<GameObject> _classElements = new List<GameObject>();

        // ── Close button (added manually for command-based opens) ──
        private GameObject _closeButton;

        // ── Colors ──
        static readonly Color ColOverlay = new Color(0f, 0f, 0f, 0.65f);

        // ══════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════

        public bool IsVisible => _isVisible;

        public void Open(bool isFromCommand)
        {
            _isFromCommand = isFromCommand;
            _classes = ClassDefinitions.GetAll();
            _selectedIndex = -1;

            if (!_uiBuilt) BuildUI();
            if (!_uiBuilt) return;

            _canvasGO.SetActive(true);
            _isVisible = true;

            PopulateClassList();
            ClearDetail();

            if (_closeButton != null)
                _closeButton.SetActive(isFromCommand);
        }

        public void Close()
        {
            _isVisible = false;
            _selectedIndex = -1;
            if (_canvasGO != null)
                _canvasGO.SetActive(false);
        }

        // ══════════════════════════════════════════
        //  MONO CALLBACKS
        // ══════════════════════════════════════════

        private void Update()
        {
            if (!_isVisible) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Keyboard close
            if (_isFromCommand && Input.GetKeyDown(KeyCode.Escape))
                Close();

            // Gamepad input
            UpdateGamepadInput();
        }

        private void OnDestroy()
        {
            if (_canvasGO != null) Destroy(_canvasGO);
        }

        // ══════════════════════════════════════════
        //  GAMEPAD / CONTROLLER INPUT
        // ══════════════════════════════════════════

        private void UpdateGamepadInput()
        {
            if (_classes == null || _classes.Count == 0) return;

            // Navigate class list with D-pad / left stick (matches UpdateRecipeGamepadInput)
            if (ZInput.GetButtonDown("JoyLStickDown") || ZInput.GetButtonDown("JoyDPadDown"))
            {
                int next = (_selectedIndex < 0) ? 0 : Mathf.Min(_classes.Count - 1, _selectedIndex + 1);
                SelectClass(next);
                EnsureClassVisible(next);
            }
            if (ZInput.GetButtonDown("JoyLStickUp") || ZInput.GetButtonDown("JoyDPadUp"))
            {
                int prev = (_selectedIndex < 0) ? 0 : Mathf.Max(0, _selectedIndex - 1);
                SelectClass(prev);
                EnsureClassVisible(prev);
            }

            // Confirm selection with A button
            if (ZInput.GetButtonDown("JoyButtonA"))
            {
                if (_selectedIndex >= 0 && _selectedIndex < _classes.Count)
                    ConfirmSelection();
            }

            // Close with B button (command-based or escape equivalent)
            if (ZInput.GetButtonDown("JoyButtonB"))
            {
                if (_isFromCommand)
                    Close();
            }
        }

        private void EnsureClassVisible(int index)
        {
            if (index < 0 || index >= _classElements.Count) return;

            // Use Valheim's ScrollRectEnsureVisible if available (same as CenterOnItem in SetRecipe)
            if (_ensureVisible != null)
            {
                var elemRT = _classElements[index].transform as RectTransform;
                if (elemRT != null)
                    _ensureVisible.CenterOnItem(elemRT);
                return;
            }

            // Fallback: manually adjust scrollbar position
            if (_recipeScrollbar == null || _classes.Count <= 1) return;
            float normalized = 1f - ((float)index / (_classes.Count - 1));
            _recipeScrollbar.value = Mathf.Clamp01(normalized);
        }

        // ══════════════════════════════════════════
        //  UI CONSTRUCTION — clone the crafting panel
        // ══════════════════════════════════════════

        private void BuildUI()
        {
            var invGui = InventoryGui.instance;
            if (invGui == null || invGui.m_crafting == null)
            {
                StartingClassPlugin.LogError("Cannot build class selection UI: InventoryGui not available.");
                return;
            }

            // ── Canvas ──
            _canvasGO = new GameObject("StartingClass_Canvas");
            _canvasGO.transform.SetParent(transform);

            var canvas = _canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = _canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasGO.AddComponent<GraphicRaycaster>();

            // ── Full-screen click-blocker ──
            var overlay = new GameObject("Overlay", typeof(RectTransform));
            overlay.transform.SetParent(_canvasGO.transform, false);
            var overlayRT = overlay.GetComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = ColOverlay;

            // ══════════════════════════════════════════
            //  CLONE the entire crafting panel
            // ══════════════════════════════════════════
            _clonedPanel = Instantiate(invGui.m_crafting.gameObject, _canvasGO.transform);
            _clonedPanel.name = "ClassSelection_CraftingClone";
            _clonedPanel.SetActive(true);

            // Center on screen
            var panelRT = _clonedPanel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.anchoredPosition = Vector2.zero;


            // ══════════════════════════════════════════
            //  Find cloned element references via path matching
            // ══════════════════════════════════════════
            var origRoot = invGui.m_crafting;

            _titleText         = FindCloned<TMP_Text>(origRoot, invGui.m_craftingStationName?.transform);
            _recipeName        = FindCloned<TMP_Text>(origRoot, invGui.m_recipeName?.transform);
            _recipeDescription = FindCloned<TMP_Text>(origRoot, invGui.m_recipeDecription?.transform);
            _recipeIcon        = FindCloned<Image>(origRoot, invGui.m_recipeIcon?.transform);
            _recipeListRoot    = FindCloned<RectTransform>(origRoot, invGui.m_recipeListRoot);
            _craftButton       = FindCloned<Button>(origRoot, invGui.m_craftButton?.transform);
            _recipeScrollbar   = FindCloned<Scrollbar>(origRoot, invGui.m_recipeListScroll?.transform);

            _recipeListSpace = invGui.m_recipeListSpace;

            // Find ScrollRect and ScrollRectEnsureVisible in the clone (for scrollbar + gamepad centering)
            if (_recipeListRoot != null)
            {
                _listScrollRect = _recipeListRoot.GetComponentInParent<ScrollRect>();
                _ensureVisible = _recipeListRoot.GetComponentInParent<ScrollRectEnsureVisible>();
            }

            // Find requirement slots in the clone
            if (invGui.m_recipeRequirementList != null)
            {
                _requirementSlots = new GameObject[invGui.m_recipeRequirementList.Length];
                for (int i = 0; i < invGui.m_recipeRequirementList.Length; i++)
                {
                    if (invGui.m_recipeRequirementList[i] != null)
                        _requirementSlots[i] = FindClonedGO(origRoot, invGui.m_recipeRequirementList[i].transform);
                }
            }

            // ══════════════════════════════════════════
            //  Hide crafting-specific elements we don't need
            // ══════════════════════════════════════════
            HideInClone(origRoot, invGui.m_tabCraft?.transform);
            HideInClone(origRoot, invGui.m_tabUpgrade?.transform);
            HideInClone(origRoot, invGui.m_repairButton?.transform);
            HideInClone(origRoot, invGui.m_repairPanel);
            HideInClone(origRoot, invGui.m_repairButtonGlow?.transform);
            HideInClone(origRoot, invGui.m_repairPanelSelection);
            HideInClone(origRoot, invGui.m_craftProgressPanel);
            HideInClone(origRoot, invGui.m_craftCancelButton?.transform);
            HideInClone(origRoot, invGui.m_variantButton?.transform);
            HideInClone(origRoot, invGui.m_itemCraftType?.transform);
            HideInClone(origRoot, invGui.m_craftingStationIcon?.transform);
            HideInClone(origRoot, invGui.m_craftingStationLevelRoot);

            // Hide quality panel (arrows, level selector)
            HideInClone(origRoot, invGui.m_qualityPanel);
            HideInClone(origRoot, invGui.m_qualityLevelDown?.transform);
            HideInClone(origRoot, invGui.m_qualityLevelUp?.transform);
            HideInClone(origRoot, invGui.m_qualityLevel?.transform);

            // Hide upgrade item section
            HideInClone(origRoot, invGui.m_upgradeItemIcon?.transform);
            HideInClone(origRoot, invGui.m_upgradeItemName?.transform);
            HideInClone(origRoot, invGui.m_upgradeItemDurability?.transform);
            HideInClone(origRoot, invGui.m_upgradeItemQuality?.transform);
            HideInClone(origRoot, invGui.m_upgradeItemQualityArrow?.transform);
            HideInClone(origRoot, invGui.m_upgradeItemNextQuality?.transform);
            HideInClone(origRoot, invGui.m_upgradeItemIndex?.transform);

            // ══════════════════════════════════════════
            //  Repurpose star/quality box as 5th requirement slot
            // ══════════════════════════════════════════
            RepurposeStarBox(origRoot, invGui);

            // Remove UIGroupHandler from clone to avoid input conflicts with original inventory
            foreach (var c in _clonedPanel.GetComponentsInChildren<UIGroupHandler>(true))
                Destroy(c);

            // ══════════════════════════════════════════
            //  Ensure recipe list root anchors to top for proper alignment
            // ══════════════════════════════════════════
            if (_recipeListRoot != null)
            {
                _recipeListRoot.pivot = new Vector2(_recipeListRoot.pivot.x, 1f);
                _recipeListRoot.anchorMin = new Vector2(_recipeListRoot.anchorMin.x, 1f);
                _recipeListRoot.anchorMax = new Vector2(_recipeListRoot.anchorMax.x, 1f);
                _recipeListRoot.anchoredPosition = new Vector2(_recipeListRoot.anchoredPosition.x, 0f);
            }

            // ══════════════════════════════════════════
            //  Repurpose cloned elements
            // ══════════════════════════════════════════

            // Title → "Choose Your Starting Class"
            if (_titleText != null)
                _titleText.text = "Choose Your Starting Class";

            // Craft button → confirm class selection
            if (_craftButton != null)
            {
                _craftButton.onClick.RemoveAllListeners();
                _craftButton.onClick.AddListener(ConfirmSelection);
                _craftButton.interactable = false;
                _craftButtonLabel = _craftButton.GetComponentInChildren<TMP_Text>();
                if (_craftButtonLabel != null)
                    _craftButtonLabel.text = "Select a Class";
            }

            // Clear any existing recipe elements from the clone
            if (_recipeListRoot != null)
            {
                for (int i = _recipeListRoot.childCount - 1; i >= 0; i--)
                    Destroy(_recipeListRoot.GetChild(i).gameObject);
            }

            // Reset detail section
            ClearDetail();

            // ── Close button (top-right corner, command-only) ──
            CreateCloseButton();

            _canvasGO.SetActive(false);
            _uiBuilt = true;
            StartingClassPlugin.Log("Class selection UI built (cloned from crafting panel).");
        }

        private void CreateCloseButton()
        {
            var closeGO = new GameObject("CloseButton", typeof(RectTransform));
            closeGO.transform.SetParent(_clonedPanel.transform, false);

            var closeRT = closeGO.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(1, 1);
            closeRT.anchorMax = new Vector2(1, 1);
            closeRT.pivot = new Vector2(1, 1);
            closeRT.sizeDelta = new Vector2(30, 30);
            closeRT.anchoredPosition = new Vector2(-5, -5);

            var closeBG = closeGO.AddComponent<Image>();
            closeBG.color = new Color(0.38f, 0.11f, 0.11f, 0.9f);

            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeBG;
            closeBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            closeBtn.onClick.AddListener(Close);

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(closeGO.transform, false);
            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            var labelTxt = labelGO.AddComponent<TextMeshProUGUI>();
            if (_recipeName != null)
                labelTxt.font = _recipeName.font;
            labelTxt.text = "X";
            labelTxt.fontSize = 18;
            labelTxt.color = Color.white;
            labelTxt.alignment = TextAlignmentOptions.Center;
            labelTxt.raycastTarget = false;

            _closeButton = closeGO;
            closeGO.SetActive(false);
        }

        // ══════════════════════════════════════════
        //  REPURPOSE STAR BOX AS 5TH REQUIREMENT SLOT
        // ══════════════════════════════════════════

        private void RepurposeStarBox(Transform origRoot, InventoryGui invGui)
        {
            if (invGui.m_minStationLevelIcon == null || _requirementSlots == null || _requirementSlots.Length == 0)
                return;

            // The star icon sits inside a "box" container — find it in the clone
            var clonedStarIcon = FindClonedGO(origRoot, invGui.m_minStationLevelIcon.transform);
            if (clonedStarIcon == null) return;

            // The box is the parent of the star icon
            var starBox = clonedStarIcon.transform.parent?.gameObject;
            if (starBox == null || starBox == _clonedPanel) return;

            // Clear the star box's children (star icon, level text, etc.)
            for (int i = starBox.transform.childCount - 1; i >= 0; i--)
                Destroy(starBox.transform.GetChild(i).gameObject);

            // Clone the internal structure from an existing requirement slot into the star box
            var templateSlot = _requirementSlots[0];
            if (templateSlot == null) return;

            foreach (Transform child in templateSlot.transform)
            {
                var clonedChild = Instantiate(child.gameObject, starBox.transform);
                clonedChild.name = child.name;
            }

            // Copy UITooltip if the template has one
            var srcTooltip = templateSlot.GetComponent<UITooltip>();
            if (srcTooltip != null && starBox.GetComponent<UITooltip>() == null)
                starBox.AddComponent<UITooltip>();

            // Match the star box's size to the requirement slots
            var templateRT = templateSlot.GetComponent<RectTransform>();
            var starBoxRT = starBox.GetComponent<RectTransform>();
            starBoxRT.sizeDelta = templateRT.sizeDelta;

            // Close the gap: position the star box right next to the first requirement slot
            // Calculate slot spacing from the existing requirement slots
            float slotSpacing = 0f;
            if (_requirementSlots.Length > 1 && _requirementSlots[1] != null)
            {
                var slot0RT = _requirementSlots[0].GetComponent<RectTransform>();
                var slot1RT = _requirementSlots[1].GetComponent<RectTransform>();
                slotSpacing = slot1RT.anchoredPosition.x - slot0RT.anchoredPosition.x;
            }
            else
            {
                slotSpacing = templateRT.sizeDelta.x + 4f;
            }

            // Place star box one spacing step to the left of slot 0
            starBoxRT.anchoredPosition = new Vector2(
                templateRT.anchoredPosition.x - slotSpacing,
                templateRT.anchoredPosition.y
            );

            // Insert at beginning of requirement slots array
            var newSlots = new GameObject[_requirementSlots.Length + 1];
            newSlots[0] = starBox;
            System.Array.Copy(_requirementSlots, 0, newSlots, 1, _requirementSlots.Length);
            _requirementSlots = newSlots;

            // Hide all children initially
            HideRequirement(starBox.transform);

            StartingClassPlugin.Log("Repurposed star box as 5th requirement slot.");
        }

        // ══════════════════════════════════════════
        //  CLASS LIST POPULATION
        // ══════════════════════════════════════════

        private void PopulateClassList()
        {
            var invGui = InventoryGui.instance;
            if (_recipeListRoot == null || invGui == null || invGui.m_recipeElementPrefab == null)
                return;

            // Clear previous elements
            foreach (var elem in _classElements)
                if (elem != null) Destroy(elem);
            _classElements.Clear();

            for (int i = 0; i < _classes.Count; i++)
            {
                int idx = i;
                var cls = _classes[i];

                // Instantiate from original recipe element prefab into our cloned list root
                var element = Instantiate(invGui.m_recipeElementPrefab, _recipeListRoot);
                element.SetActive(true);
                element.name = "ClassElement_" + cls.Name;

                // Position exactly like Valheim does in AddRecipeToList
                var elemRT = element.transform as RectTransform;
                elemRT.anchoredPosition = new Vector2(0f, i * -_recipeListSpace);

                // Hide class icon (not needed in class list)
                var iconTr = element.transform.Find("icon");
                if (iconTr != null)
                    iconTr.gameObject.SetActive(false);

                // Set class name (like recipe name)
                var nameTr = element.transform.Find("name");
                if (nameTr != null)
                {
                    var nameTxt = nameTr.GetComponent<TMP_Text>();
                    if (nameTxt != null)
                    {
                        nameTxt.text = cls.Name;
                        nameTxt.color = Color.white;
                    }
                }

                // Hide durability bar (not relevant for classes)
                var durTr = element.transform.Find("Durability");
                if (durTr != null) durTr.gameObject.SetActive(false);

                // Hide quality level (not relevant for classes)
                var qualTr = element.transform.Find("QualityLevel");
                if (qualTr != null) qualTr.gameObject.SetActive(false);

                // Set selected highlight to inactive
                var selTr = element.transform.Find("selected");
                if (selTr != null) selTr.gameObject.SetActive(false);

                // Wire up button click
                var btn = element.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => SelectClass(idx));
                }

                _classElements.Add(element);
            }

            // Set content height for scrolling (same as Valheim: max of base size or item count * spacing)
            float contentHeight = _classes.Count * _recipeListSpace;
            _recipeListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);

            // Reset scroll to top
            if (_recipeScrollbar != null)
                _recipeScrollbar.value = 1f;
            if (_listScrollRect != null)
                _listScrollRect.verticalNormalizedPosition = 1f;
        }

        // ══════════════════════════════════════════
        //  SELECTION LOGIC
        // ══════════════════════════════════════════

        private void SelectClass(int index)
        {
            if (index < 0 || index >= _classes.Count) return;
            _selectedIndex = index;
            RefreshHighlights();
            RefreshDetail();
        }

        private void RefreshHighlights()
        {
            // Toggle "selected" child exactly like Valheim's SetRecipe
            for (int i = 0; i < _classElements.Count; i++)
            {
                var selTr = _classElements[i]?.transform.Find("selected");
                if (selTr != null)
                    selTr.gameObject.SetActive(i == _selectedIndex);
            }
        }

        private void ClearDetail()
        {
            if (_recipeIcon != null) _recipeIcon.enabled = false;
            if (_recipeName != null) _recipeName.enabled = false;
            if (_recipeDescription != null) _recipeDescription.enabled = false;

            if (_craftButton != null)
            {
                _craftButton.interactable = false;
                if (_craftButtonLabel != null)
                    _craftButtonLabel.text = "Select a Class";
            }

            HideAllRequirements();
        }

        private void RefreshDetail()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _classes.Count)
            {
                ClearDetail();
                return;
            }

            var cls = _classes[_selectedIndex];

            // Class icon → recipe icon
            if (_recipeIcon != null)
            {
                Sprite icon = GetItemIcon(cls.IconItemName);
                if (icon != null)
                {
                    _recipeIcon.sprite = icon;
                    _recipeIcon.enabled = true;
                }
                else
                {
                    _recipeIcon.enabled = false;
                }
            }

            // Class name → recipe name
            if (_recipeName != null)
            {
                _recipeName.text = cls.Name;
                _recipeName.enabled = true;
            }

            // Class description + skill bonuses → recipe description
            if (_recipeDescription != null)
            {
                string desc = cls.Description;
                if (cls.SkillBonuses.Count > 0)
                {
                    desc += "\n\n<color=#66B3E5>Skill Bonuses:</color>";
                    foreach (var bonus in cls.SkillBonuses)
                    {
                        string skillName = FormatPascalCase(bonus.SkillType.ToString());
                        desc += $"\n  {skillName}  <color=#66B3E5>+{bonus.BonusLevel:0}</color>";
                    }
                }
                _recipeDescription.text = desc;
                _recipeDescription.enabled = true;
            }

            // Confirm button → "Begin as X"
            if (_craftButton != null)
            {
                _craftButton.interactable = true;
                if (_craftButtonLabel != null)
                    _craftButtonLabel.text = $"Begin as {cls.Name}";
            }

            // Requirement slots → starting items
            SetupStartingItems(cls);
        }

        private void SetupStartingItems(StartingClass cls)
        {
            if (_requirementSlots == null) return;

            int slotIndex = 0;
            for (int i = 0; i < cls.Items.Count && slotIndex < _requirementSlots.Length; i++, slotIndex++)
            {
                var slot = _requirementSlots[slotIndex];
                if (slot == null) continue;

                var item = cls.Items[i];
                var root = slot.transform;

                // res_icon
                var iconTr = root.Find("res_icon");
                if (iconTr != null)
                {
                    var img = iconTr.GetComponent<Image>();
                    if (img != null)
                    {
                        Sprite icon = GetItemIcon(item.PrefabName);
                        if (icon != null)
                        {
                            img.sprite = icon;
                            img.color = Color.white;
                        }
                        img.gameObject.SetActive(icon != null);
                    }
                }

                // res_name
                var nameTr = root.Find("res_name");
                if (nameTr != null)
                {
                    var txt = nameTr.GetComponent<TMP_Text>();
                    if (txt != null)
                    {
                        txt.text = GetLocalizedName(item.PrefabName);
                        txt.color = Color.white;
                        txt.gameObject.SetActive(true);
                    }
                }

                // res_amount
                var amtTr = root.Find("res_amount");
                if (amtTr != null)
                {
                    var txt = amtTr.GetComponent<TMP_Text>();
                    if (txt != null)
                    {
                        txt.text = item.Quantity.ToString();
                        txt.color = Color.white;
                        txt.gameObject.SetActive(true);
                    }
                }

                // UITooltip
                var tooltip = root.GetComponent<UITooltip>();
                if (tooltip != null)
                    tooltip.m_text = GetLocalizedName(item.PrefabName);
            }

            // Hide remaining unused slots
            for (int i = slotIndex; i < _requirementSlots.Length; i++)
            {
                if (_requirementSlots[i] != null)
                    HideRequirement(_requirementSlots[i].transform);
            }
        }

        private void HideRequirement(Transform root)
        {
            var iconTr = root.Find("res_icon");
            if (iconTr != null) iconTr.gameObject.SetActive(false);
            var nameTr = root.Find("res_name");
            if (nameTr != null) nameTr.gameObject.SetActive(false);
            var amtTr = root.Find("res_amount");
            if (amtTr != null) amtTr.gameObject.SetActive(false);
            var tooltip = root.GetComponent<UITooltip>();
            if (tooltip != null) tooltip.m_text = "";
        }

        private void HideAllRequirements()
        {
            if (_requirementSlots == null) return;
            foreach (var slot in _requirementSlots)
                if (slot != null) HideRequirement(slot.transform);
        }

        private void ConfirmSelection()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _classes.Count) return;

            var player = Player.m_localPlayer;
            if (player == null)
            {
                StartingClassPlugin.LogError("Cannot apply class: no local player.");
                Close();
                return;
            }

            ClassApplicator.ApplyClass(player, _classes[_selectedIndex], _isFromCommand);
            Close();
        }

        // ══════════════════════════════════════════
        //  CLONE HELPERS
        // ══════════════════════════════════════════

        /// <summary>
        /// Computes the relative transform path from originalRoot to originalChild,
        /// then finds the same path in the cloned panel and returns the component.
        /// </summary>
        private T FindCloned<T>(Transform originalRoot, Transform originalChild) where T : Component
        {
            if (originalChild == null) return null;
            string path = GetRelativePath(originalRoot, originalChild);
            if (path == null) return null;
            if (path.Length == 0) return _clonedPanel.GetComponent<T>();
            var found = _clonedPanel.transform.Find(path);
            if (found == null)
            {
                StartingClassPlugin.LogWarning($"Cloned element not found at path: '{path}'");
                return null;
            }
            return found.GetComponent<T>();
        }

        private GameObject FindClonedGO(Transform originalRoot, Transform originalChild)
        {
            if (originalChild == null) return null;
            string path = GetRelativePath(originalRoot, originalChild);
            if (path == null) return null;
            if (path.Length == 0) return _clonedPanel;
            var found = _clonedPanel.transform.Find(path);
            if (found == null)
            {
                StartingClassPlugin.LogWarning($"Cloned GO not found at path: '{path}'");
                return null;
            }
            return found.gameObject;
        }

        private void HideInClone(Transform originalRoot, Transform originalChild)
        {
            if (originalChild == null) return;
            var go = FindClonedGO(originalRoot, originalChild);
            if (go != null) go.SetActive(false);
        }

        private static string GetRelativePath(Transform root, Transform child)
        {
            var parts = new List<string>();
            var current = child;
            while (current != null && current != root)
            {
                parts.Insert(0, current.name);
                current = current.parent;
            }
            if (current != root) return null; // child is not a descendant of root
            return string.Join("/", parts);
        }

        // ══════════════════════════════════════════
        //  VALHEIM DATA LOOKUPS
        // ══════════════════════════════════════════

        private static Sprite GetItemIcon(string prefabName)
        {
            var prefab = ZNetScene.instance?.GetPrefab(prefabName);
            if (prefab == null) return null;
            var drop = prefab.GetComponent<ItemDrop>();
            if (drop == null) return null;
            return drop.m_itemData.GetIcon();
        }

        private static string GetLocalizedName(string prefabName)
        {
            var prefab = ZNetScene.instance?.GetPrefab(prefabName);
            if (prefab != null)
            {
                var drop = prefab.GetComponent<ItemDrop>();
                if (drop != null)
                {
                    string loc = Localization.instance.Localize(drop.m_itemData.m_shared.m_name);
                    if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("["))
                        return loc;
                }
            }
            return FormatPascalCase(prefabName);
        }

        private static string FormatPascalCase(string input)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in input)
            {
                if (char.IsUpper(c) && sb.Length > 0) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
