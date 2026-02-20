using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private Button _craftButton;
        private TMP_Text _craftButtonLabel;
        private Scrollbar _recipeScrollbar;
        private ScrollRect _listScrollRect;
        private ScrollRectEnsureVisible _ensureVisible;
        private ScrollRect _descriptionScrollRect;
        private int _descScrollResetFrames;

        // ── Tab buttons ──
        private GameObject _tabClasses;
        private GameObject _tabSkills;
        private GameObject _tabArmor;
        private int _activeTab; // 0 = Classes, 1 = Skills, 2 = Armor
        private static readonly string[] TabNames = { "Classes", "Skills", "Armor" };

        // ── Armor panel ──
        private GameObject _armorPanel;
        private TMP_Text _armorText;
        private ScrollRect _armorScrollRect;
        private int _armorSelectedSlot; // index into ArmorUpgradeSystem.GetAllSets()

        // ── Skills panel ──
        private GameObject _skillsPanel;
        private TMP_Text _skillsText;
        private ScrollRect _skillsScrollRect;
        private Button _unlockButton;
        private TMP_Text _unlockButtonLabel;

        // ── Active Power panel (right side of Skills tab) ──
        private GameObject _activePowerPanel;
        private RectTransform _powerListRoot;
        private ScrollRect _powerScrollRect;
        private Button _selectPowerButton;
        private TMP_Text _selectPowerLabel;
        private int _selectedPowerIndex = -1;
        private readonly List<GameObject> _powerEntries = new List<GameObject>();
        private readonly List<string> _powerIds = new List<string>();

        // ── Panel focus for gamepad (Skills tab: 0=left class list, 1=middle skill text, 2=right powers) ──
        private int _panelFocus; // 0 = left, 1 = middle, 2 = right

        // ── Class list elements (instantiated from recipe element prefab) ──
        private readonly List<GameObject> _classElements = new List<GameObject>();

        // ── Player preview ──
        private RenderTexture _previewRT;
        private GameObject _previewCamGO;
        private Camera _previewCamera;
        private GameObject _previewClone;
        private static readonly Vector3 PreviewSpawnPos = new Vector3(10000f, 5000f, 10000f);

        // ── Preview rotation ──
        private float _previewRotation;
        private const float AutoRotateSpeed = 12f; // degrees per second

        // ── Colors ──
        static readonly Color ColOverlay = new Color(0f, 0f, 0f, 0.65f);

        // ── Cached reflection data for VisEquipment hash resets ──
        private static System.Reflection.FieldInfo[] _visEquipHashFields;

        // ══════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════

        public bool IsVisible => _isVisible;

        public void Open(bool isFromCommand)
        {
            _isFromCommand = isFromCommand;
            _classes = ClassDefinitions.GetAll();

            if (!_uiBuilt) BuildUI();
            if (!_uiBuilt) return;

            _canvasGO.SetActive(true);
            _isVisible = true;

            SetupPreviewClone();
            _previewRotation = 0f;
            if (_previewCamera != null)
                _previewCamera.enabled = true;

            PopulateClassList();

            // Restore previous selection, or default to the first class
            int restoreIndex = (_selectedIndex >= 0 && _selectedIndex < _classes.Count)
                ? _selectedIndex
                : 0;
            SelectClass(restoreIndex);

        }

        public void Close()
        {
            _isVisible = false;
            if (_previewCamera != null)
                _previewCamera.enabled = false;
            ClearPreviewClone();
            if (_canvasGO != null)
                _canvasGO.SetActive(false);
        }

        // ══════════════════════════════════════════
        //  MONO CALLBACKS
        // ══════════════════════════════════════════

        private void Update()
        {
            if (!_isVisible) return;

            // Only force cursor visible when using mouse/keyboard — hide on gamepad
            if (!ZInput.IsGamepadActive())
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.visible = false;
            }

            // Keyboard close
            if (_isFromCommand && Input.GetKeyDown(KeyCode.Escape))
                Close();

            // Keyboard tab switching (Q / E)
            if (Input.GetKeyDown(KeyCode.Q))
                SwitchTab(_activeTab - 1);
            if (Input.GetKeyDown(KeyCode.E))
                SwitchTab(_activeTab + 1);

            // Slow automatic camera rotation.
            _previewRotation = (_previewRotation + AutoRotateSpeed * Time.deltaTime) % 360f;

            // Update preview camera position
            UpdatePreviewCamera();

            // Gamepad input
            UpdateGamepadInput();
        }

        private void LateUpdate()
        {
            if (!_isVisible) return;
            var hud = Hud.instance;
            if (hud != null && hud.m_crosshair != null)
                hud.m_crosshair.color = Color.clear;

            // Deferred scroll-to-top: apply for several frames so ScrollRect's internal
            // LateUpdate doesn't override our position before layout has settled.
            if (_descScrollResetFrames > 0 && _descriptionScrollRect != null)
            {
                _descriptionScrollRect.verticalNormalizedPosition = 1f;
                _descriptionScrollRect.velocity = Vector2.zero;
                _descScrollResetFrames--;
            }

            // Force-clear EventSystem selection every frame to prevent stale
            // button highlights. Unity's Button re-selects after onClick handlers,
            // so a single SetSelectedGameObject(null) in the handler isn't enough.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void OnDestroy()
        {
            if (_previewCamera != null) _previewCamera.enabled = false;
            ClearPreviewClone();
            if (_previewCamGO != null) Destroy(_previewCamGO);
            if (_previewRT != null) { _previewRT.Release(); Destroy(_previewRT); }
            if (_canvasGO != null) Destroy(_canvasGO);
        }

        // ══════════════════════════════════════════
        //  GAMEPAD / CONTROLLER INPUT
        // ══════════════════════════════════════════

        private void UpdateGamepadInput()
        {
            if (_classes == null || _classes.Count == 0) return;

            // ── LB / RB — switch tabs ──
            if (ZInput.GetButtonDown("JoyTabLeft"))
                SwitchTab(_activeTab - 1);
            if (ZInput.GetButtonDown("JoyTabRight"))
                SwitchTab(_activeTab + 1);

            // ── B button — close menu ──
            if (ZInput.GetButtonDown("JoyButtonB"))
            {
                if (_isFromCommand)
                    Close();
            }

            // ── Skills tab: left stick LEFT/RIGHT cycles panel focus (0=class list, 1=skills text, 2=powers) ──
            if (_activeTab == 1)
            {
                if (ZInput.GetButtonDown("JoyLStickLeft") || ZInput.GetButtonDown("JoyDPadLeft"))
                {
                    if (_panelFocus > 0)
                    {
                        _panelFocus--;
                        if (_panelFocus == 2 && _selectedPowerIndex < 0 && _powerEntries.Count > 0)
                        {
                            _selectedPowerIndex = 0;
                            UpdatePowerSelection();
                        }
                        if (EventSystem.current != null)
                            EventSystem.current.SetSelectedGameObject(null);

                    }
                }
                if (ZInput.GetButtonDown("JoyLStickRight") || ZInput.GetButtonDown("JoyDPadRight"))
                {
                    int maxFocus = (_powerEntries.Count > 0) ? 2 : 1;
                    if (_panelFocus < maxFocus)
                    {
                        _panelFocus++;
                        if (_panelFocus == 2 && _selectedPowerIndex < 0 && _powerEntries.Count > 0)
                        {
                            _selectedPowerIndex = 0;
                            UpdatePowerSelection();
                        }
                        if (EventSystem.current != null)
                            EventSystem.current.SetSelectedGameObject(null);

                    }
                }
            }

            // ── Left stick / D-pad UP/DOWN — navigate based on panel focus ──
            if (ZInput.GetButtonDown("JoyLStickDown") || ZInput.GetButtonDown("JoyDPadDown"))
            {
                if (_activeTab == 1 && _panelFocus == 2)
                {
                    // Right panel: navigate power entries
                    if (_powerEntries.Count > 0)
                    {
                        _selectedPowerIndex = Mathf.Min(_powerEntries.Count - 1, _selectedPowerIndex + 1);
                        UpdatePowerSelection();
                    }
                }
                else if (_activeTab == 1 && _panelFocus == 1)
                {
                    // Middle panel: scroll skills text down
                    if (_skillsScrollRect != null)
                    {
                        _skillsScrollRect.verticalNormalizedPosition -= 0.1f;
                        _skillsScrollRect.verticalNormalizedPosition = Mathf.Clamp01(_skillsScrollRect.verticalNormalizedPosition);
                    }
                }
                else if (_activeTab == 2)
                {
                    // Armor tab: navigate armor sets
                    var sets = ArmorUpgradeSystem.GetAllSets();
                    _armorSelectedSlot = Mathf.Min(sets.Length - 1, _armorSelectedSlot + 1);
                    RefreshArmorPanel();
                }
                else
                {
                    // Left panel: navigate class list
                    int next = (_selectedIndex < 0) ? 0 : Mathf.Min(_classes.Count - 1, _selectedIndex + 1);
                    SelectClass(next);
                    EnsureClassVisible(next);
                }
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(null);
            }
            if (ZInput.GetButtonDown("JoyLStickUp") || ZInput.GetButtonDown("JoyDPadUp"))
            {
                if (_activeTab == 1 && _panelFocus == 2)
                {
                    // Right panel: navigate power entries
                    if (_powerEntries.Count > 0)
                    {
                        _selectedPowerIndex = Mathf.Max(0, _selectedPowerIndex - 1);
                        UpdatePowerSelection();
                    }
                }
                else if (_activeTab == 1 && _panelFocus == 1)
                {
                    // Middle panel: scroll skills text up
                    if (_skillsScrollRect != null)
                    {
                        _skillsScrollRect.verticalNormalizedPosition += 0.1f;
                        _skillsScrollRect.verticalNormalizedPosition = Mathf.Clamp01(_skillsScrollRect.verticalNormalizedPosition);
                    }
                }
                else if (_activeTab == 2)
                {
                    // Armor tab: navigate armor slots
                    _armorSelectedSlot = Mathf.Max(0, _armorSelectedSlot - 1);
                    RefreshArmorPanel();
                }
                else
                {
                    // Left panel: navigate class list
                    int prev = (_selectedIndex < 0) ? 0 : Mathf.Max(0, _selectedIndex - 1);
                    SelectClass(prev);
                    EnsureClassVisible(prev);
                }
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(null);
            }

            // ── A button — context-dependent per tab and panel focus ──
            if (ZInput.GetButtonDown("JoyButtonA"))
            {
                if (_activeTab == 1)
                {
                    if (_panelFocus == 2)
                    {
                        // Right panel: select the highlighted power
                        if (_selectPowerButton != null && _selectPowerButton.interactable)
                            OnSelectPowerClicked();
                    }
                    else if (_panelFocus == 0)
                    {
                        // Left panel: unlock ability
                        if (_unlockButton != null && _unlockButton.interactable)
                            OnUnlockButtonClicked();
                    }
                    // Middle panel (focus 1): A does nothing (just scrollable text)
                }
                else if (_activeTab == 2)
                {
                    // Armor tab: upgrade selected armor slot
                    if (_craftButton != null && _craftButton.interactable)
                        OnUpgradeArmorClicked();
                }
                else
                {
                    // Classes tab: confirm class selection
                    if (_selectedIndex >= 0 && _selectedIndex < _classes.Count)
                        ConfirmSelection();
                }
            }

            // ── Right stick — scroll the middle panel text (always available as secondary scroll) ──
            ScrollRect activeScroll = null;
            if (_activeTab == 0 && _descriptionScrollRect != null)
                activeScroll = _descriptionScrollRect;
            else if (_activeTab == 1 && _skillsScrollRect != null)
                activeScroll = _skillsScrollRect;
            else if (_activeTab == 2 && _descriptionScrollRect != null)
                activeScroll = _descriptionScrollRect;

            if (activeScroll != null)
            {
                float scrollSpeed = 2f;
                if (ZInput.GetButton("JoyRStickDown"))
                {
                    activeScroll.verticalNormalizedPosition -= scrollSpeed * Time.deltaTime;
                    activeScroll.verticalNormalizedPosition = Mathf.Clamp01(activeScroll.verticalNormalizedPosition);
                }
                if (ZInput.GetButton("JoyRStickUp"))
                {
                    activeScroll.verticalNormalizedPosition += scrollSpeed * Time.deltaTime;
                    activeScroll.verticalNormalizedPosition = Mathf.Clamp01(activeScroll.verticalNormalizedPosition);
                }
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
            //  CLONE the crafting panel (unchanged dimensions)
            // ══════════════════════════════════════════
            _clonedPanel = Instantiate(invGui.m_crafting.gameObject, _canvasGO.transform);
            _clonedPanel.name = "ClassSelection_CraftingClone";
            _clonedPanel.SetActive(true);

            var panelRT = _clonedPanel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);

            // Derive source column widths from the cloned panel before any resizing.
            float origPanelWidth = panelRT.sizeDelta.x;
            var clonedListPanel = _clonedPanel.transform.Find("RecipeList") as RectTransform;
            var clonedDescPanel = _clonedPanel.transform.Find("Decription") as RectTransform;

            float descriptionColumnWidth = 260f; // fallback
            if (clonedDescPanel != null)
            {
                descriptionColumnWidth = GetRectWidth(clonedDescPanel, origPanelWidth);
                if (descriptionColumnWidth <= 10f) descriptionColumnWidth = 260f;
            }

            float listColumnWidth = 200f; // fallback
            if (clonedListPanel != null)
            {
                listColumnWidth = GetRectWidth(clonedListPanel, origPanelWidth);
                if (listColumnWidth <= 10f) listColumnWidth = 200f;
            }
            else if (invGui.m_recipeListRoot != null)
            {
                var scrollRect = invGui.m_recipeListRoot.GetComponentInParent<ScrollRect>();
                if (scrollRect != null)
                {
                    var scrollRT = scrollRect.transform as RectTransform;
                    float anchorSpan = scrollRT.anchorMax.x - scrollRT.anchorMin.x;
                    listColumnWidth = anchorSpan * origPanelWidth + scrollRT.sizeDelta.x;
                    if (listColumnWidth <= 10f) listColumnWidth = 200f;
                }
            }

            // Match class-list panel width to the description panel width.
            float listWidthIncrease = 0f;
            if (clonedListPanel != null && descriptionColumnWidth > listColumnWidth + 1f)
            {
                float originalListRight = GetRectRight(clonedListPanel, origPanelWidth);
                listWidthIncrease = descriptionColumnWidth - listColumnWidth;
                SetRectWidthKeepingLeft(clonedListPanel, origPanelWidth, descriptionColumnWidth);

                // Shift right-side content so the widened list panel does not overlap it.
                foreach (RectTransform child in _clonedPanel.transform)
                {
                    if (child == clonedListPanel) continue;
                    bool fullStretch = child.anchorMin.x <= 0.01f && child.anchorMax.x >= 0.99f;
                    if (fullStretch) continue;

                    float childLeft = GetRectLeft(child, origPanelWidth);
                    if (childLeft >= originalListRight - 2f)
                        ShiftRectX(child, listWidthIncrease);
                }
            }

            // Widen panel to fit a third column (preview) equal to description width,
            // plus extra width assigned to the middle (description) column.
            float extraWidth = 80f; // extra pixels for the middle panel
            float previewColumnWidth = descriptionColumnWidth;
            float previewPadding = 24f;
            float contentBaseWidth = origPanelWidth + listWidthIncrease;
            float panelAddedWidth = listWidthIncrease + previewColumnWidth + previewPadding + extraWidth;
            float tabHeight = 2f;
            panelRT.sizeDelta = new Vector2(origPanelWidth + panelAddedWidth, panelRT.sizeDelta.y + tabHeight);
            panelRT.anchoredPosition = Vector2.zero;

            // Fix children that ride or stretch to the right edge when the panel widens:
            // 1. Right-PINNED (anchorMin.x ~= 1 && anchorMax.x ~= 1): shift left by panelAddedWidth
            // 2. Right-STRETCHED (anchorMax.x ~= 1 but anchorMin.x is partway): clamp right edge
            // Full-stretch backgrounds (anchorMin.x ~= 0 → anchorMax.x ~= 1) stay as-is.
            foreach (RectTransform child in _clonedPanel.transform)
            {
                bool pinnedRight = child.anchorMin.x >= 0.99f && child.anchorMax.x >= 0.99f;
                bool stretchedRight = child.anchorMax.x >= 0.99f && child.anchorMin.x >= 0.01f && child.anchorMin.x < 0.99f;

                if (pinnedRight)
                {
                    child.anchoredPosition += new Vector2(-panelAddedWidth, 0f);
                }
                else if (stretchedRight)
                {
                    // Clamp the right edge so it doesn't stretch into the preview column
                    child.offsetMax += new Vector2(-panelAddedWidth, 0f);
                }
            }

            // Give the extra width to the Description (middle) panel so it's bigger
            if (clonedDescPanel != null)
            {
                clonedDescPanel.offsetMax += new Vector2(extraWidth, 0f);
                // Also shift the preview column right to make room
                contentBaseWidth += extraWidth;
            }

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

            // Find ScrollRect and ScrollRectEnsureVisible in the clone (for scrollbar + gamepad centering)
            if (_recipeListRoot != null)
            {
                _listScrollRect = _recipeListRoot.GetComponentInParent<ScrollRect>();
                _ensureVisible = _recipeListRoot.GetComponentInParent<ScrollRectEnsureVisible>();
            }

            // Description scroll area is built from scratch later (crafting UI has none).

            // Hide requirement slots — starting gear is shown in the description text instead
            if (invGui.m_recipeRequirementList != null)
            {
                for (int i = 0; i < invGui.m_recipeRequirementList.Length; i++)
                {
                    if (invGui.m_recipeRequirementList[i] != null)
                    {
                        var slotGO = FindClonedGO(origRoot, invGui.m_recipeRequirementList[i].transform);
                        if (slotGO != null) slotGO.SetActive(false);
                    }
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

            // Hide the star/quality box (no longer used)
            HideInClone(origRoot, invGui.m_minStationLevelIcon?.transform?.parent);

            // Remove UIGroupHandler from clone to avoid input conflicts with original inventory
            foreach (var c in _clonedPanel.GetComponentsInChildren<UIGroupHandler>(true))
                Destroy(c);

            // Hide the gold outline frame and ALL gold decorative lines/accents
            var selectedFrame = _clonedPanel.transform.Find("selected_frame");
            if (selectedFrame != null)
                selectedFrame.gameObject.SetActive(false);
            foreach (Transform t in _clonedPanel.GetComponentsInChildren<Transform>(true))
            {
                if (t == _clonedPanel.transform) continue;
                string n = t.name;
                // Hide any named decorative elements
                if (n.Contains("BraidLine") || n.Contains("Border") || n.Contains("border")
                    || n.Contains("Line") || n.Contains("Bkg2") || n.Contains("Outline"))
                {
                    t.gameObject.SetActive(false);
                    continue;
                }
                // Hide any very thin Image children (accent lines) — height or width < 4px
                var rt = t as RectTransform;
                var img = t.GetComponent<Image>();
                if (rt != null && img != null && t.childCount == 0)
                {
                    float h = Mathf.Abs(rt.sizeDelta.y);
                    float w = Mathf.Abs(rt.sizeDelta.x);
                    if ((h > 0f && h < 4f) || (w > 0f && w < 4f))
                        t.gameObject.SetActive(false);
                }
            }

            // Hide the cloned recipe list scrollbar visually (keep it functional for ScrollRect)
            if (_recipeScrollbar != null)
            {
                var sbImages = _recipeScrollbar.GetComponentsInChildren<Image>(true);
                foreach (var img in sbImages) img.color = new Color(0f, 0f, 0f, 0f);
            }

            // Force the left class-list panel to match the original (pre-extra) description width.
            var listPanelRT = _clonedPanel.transform.Find("RecipeList") as RectTransform;
            var descPanelRT = _clonedPanel.transform.Find("Decription") as RectTransform;
            if (listPanelRT != null && descPanelRT != null)
            {
                float parentWidth = panelRT.sizeDelta.x;
                float descLeft = GetRectLeft(descPanelRT, parentWidth);
                float gapToDescription = 4f;

                // Use the original column width, not the widened description
                float listWidth = descriptionColumnWidth;
                float targetRight = descLeft - gapToDescription;
                float targetLeft = targetRight - listWidth;
                float minLeft = 6f;
                if (targetLeft < minLeft)
                {
                    targetLeft = minLeft;
                    targetRight = targetLeft + listWidth;
                }

                SetRectHorizontalEdges(listPanelRT, parentWidth, targetLeft, targetRight);

                // Match the list panel's vertical extent to the description panel
                listPanelRT.anchorMin = new Vector2(listPanelRT.anchorMin.x, descPanelRT.anchorMin.y);
                listPanelRT.anchorMax = new Vector2(listPanelRT.anchorMax.x, descPanelRT.anchorMax.y);
                listPanelRT.offsetMin = new Vector2(listPanelRT.offsetMin.x, descPanelRT.offsetMin.y);
                listPanelRT.offsetMax = new Vector2(listPanelRT.offsetMax.x, descPanelRT.offsetMax.y);
            }

            // ══════════════════════════════════════════
            //  Ensure recipe list root anchors to top for proper alignment
            // ══════════════════════════════════════════
            if (_recipeListRoot != null)
            {
                float rightInset = (_recipeScrollbar != null) ? 22f : 2f;
                _recipeListRoot.pivot = new Vector2(0.5f, 1f);
                _recipeListRoot.anchorMin = new Vector2(0f, 1f);
                _recipeListRoot.anchorMax = new Vector2(1f, 1f);
                _recipeListRoot.offsetMin = new Vector2(2f, _recipeListRoot.offsetMin.y);
                _recipeListRoot.offsetMax = new Vector2(-rightInset, _recipeListRoot.offsetMax.y);
                _recipeListRoot.anchoredPosition = new Vector2(0f, 0f);
            }

            // Ensure every RectTransform between RecipeList and _recipeListRoot stretches to fill.
            // The ScrollRect's own GO and any intermediate containers may have fixed widths
            // from the original (narrower) clone that don't update when RecipeList is widened.
            if (_listScrollRect != null)
            {
                var listPanel = _clonedPanel.transform.Find("RecipeList");

                // Force the ScrollRect's own RectTransform to fill RecipeList
                var scrollRT = _listScrollRect.transform as RectTransform;
                if (scrollRT != null && scrollRT != listPanel)
                {
                    scrollRT.anchorMin = Vector2.zero;
                    scrollRT.anchorMax = Vector2.one;
                    scrollRT.offsetMin = Vector2.zero;
                    scrollRT.offsetMax = Vector2.zero;

                    // Also stretch any intermediate parents between ScrollRect and RecipeList
                    var parent = scrollRT.parent as RectTransform;
                    while (parent != null && parent != listPanel)
                    {
                        parent.anchorMin = Vector2.zero;
                        parent.anchorMax = Vector2.one;
                        parent.offsetMin = Vector2.zero;
                        parent.offsetMax = Vector2.zero;
                        parent = parent.parent as RectTransform;
                    }
                }

                var viewport = _listScrollRect.viewport;
                if (viewport != null)
                {
                    viewport.anchorMin = new Vector2(0f, 0f);
                    viewport.anchorMax = new Vector2(1f, 1f);
                    viewport.offsetMin = new Vector2(2f, 2f);
                    viewport.offsetMax = new Vector2(-2f, -2f);
                }

                if (_listScrollRect.content != null)
                {
                    var contentRT = _listScrollRect.content;
                    contentRT.pivot = new Vector2(0.5f, 1f);
                    contentRT.anchorMin = new Vector2(0f, 1f);
                    contentRT.anchorMax = new Vector2(1f, 1f);
                    contentRT.offsetMin = new Vector2(2f, contentRT.offsetMin.y);
                    contentRT.offsetMax = new Vector2(-2f, contentRT.offsetMax.y);
                    contentRT.anchoredPosition = new Vector2(0f, 0f);
                }
            }

            // ══════════════════════════════════════════
            //  Repurpose cloned elements
            // ══════════════════════════════════════════

            // Hide the title — tab buttons serve as headers now
            if (_titleText != null)
                _titleText.gameObject.SetActive(false);

            // Stretch the class name label across the full description width so centering works
            // (originally it's offset to make room for the recipe icon, which we hide)
            if (_recipeName != null)
            {
                var nameRT = _recipeName.GetComponent<RectTransform>();
                if (nameRT != null)
                {
                    nameRT.anchorMin = new Vector2(0f, nameRT.anchorMin.y);
                    nameRT.anchorMax = new Vector2(1f, nameRT.anchorMax.y);
                    nameRT.offsetMin = new Vector2(10f, nameRT.offsetMin.y);
                    nameRT.offsetMax = new Vector2(-10f, nameRT.offsetMax.y);
                }
                _recipeName.alignment = TextAlignmentOptions.Center;
            }

            // Make class description text larger
            if (_recipeDescription != null)
            {
                _recipeDescription.enableAutoSizing = false;
                _recipeDescription.fontSize = 18f;
            }

            // Craft button → confirm class selection
            if (_craftButton != null)
            {
                _craftButton.onClick.RemoveAllListeners();
                _craftButton.onClick.AddListener(ConfirmSelection);
                _craftButton.interactable = false;
                _craftButtonLabel = _craftButton.GetComponentInChildren<TMP_Text>(true);
                if (_craftButtonLabel != null)
                {
                    _craftButtonLabel.gameObject.SetActive(true);
                    _craftButtonLabel.text = "Select a Class";
                }

                // Remove button hint children (key_bkg images, gamepad prompts, etc.)
                StripButtonHints(_craftButton.gameObject, _craftButtonLabel);
            }

            // Clear any existing recipe elements from the clone
            if (_recipeListRoot != null)
            {
                for (int i = _recipeListRoot.childCount - 1; i >= 0; i--)
                    Destroy(_recipeListRoot.GetChild(i).gameObject);
            }

            // Hide orphaned text elements from the entire cloned panel (e.g. stuck armor names)
            foreach (var txt in _clonedPanel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (txt == _recipeName) continue;
                if (txt == _recipeDescription) continue;
                if (txt == _craftButtonLabel) continue;
                if (txt == _titleText) continue;
                // Keep text inside the craft button
                if (_craftButton != null && txt.transform.IsChildOf(_craftButton.transform)) continue;
                // Keep text inside the recipe list (class element names)
                if (_recipeListRoot != null && txt.transform.IsChildOf(_recipeListRoot)) continue;
                txt.gameObject.SetActive(false);
            }

            // Reset detail section
            ClearDetail();

            // ══════════════════════════════════════════
            //  Build scrollable description area (crafting UI has none by default)
            //  Parent directly to the Decription panel with anchor-based layout.
            // ══════════════════════════════════════════
            var descScrollPanel = _clonedPanel.transform.Find("Decription") as RectTransform;
            if (_recipeDescription != null && descScrollPanel != null)
            {
                var descTextRT = _recipeDescription.rectTransform;
                float scrollbarWidth = 10f;

                // Strip any layout components on the Decription panel that could override positioning
                StripLayoutComponents(descScrollPanel.gameObject);

                // Scroll area — fills Decription panel between the class name and requirements
                var scrollGO = new GameObject("DescScrollArea", typeof(RectTransform), typeof(Image), typeof(Mask));
                scrollGO.transform.SetParent(descScrollPanel, false);
                var scrollAreaRT = scrollGO.GetComponent<RectTransform>();
                scrollAreaRT.anchorMin = new Vector2(0f, 0f);
                scrollAreaRT.anchorMax = new Vector2(1f, 1f);
                scrollAreaRT.offsetMin = new Vector2(8f, 85f);  // bottom inset: clears craft button with margin
                scrollAreaRT.offsetMax = new Vector2(-scrollbarWidth - 4f, -38f); // top inset: clears class name header
                scrollAreaRT.pivot = new Vector2(0.5f, 0.5f);
                scrollGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.003f);
                scrollGO.GetComponent<Mask>().showMaskGraphic = false;

                // Reparent text into scroll area — text IS the scroll content
                descTextRT.SetParent(scrollGO.transform, false);
                descTextRT.anchorMin = new Vector2(0f, 1f);
                descTextRT.anchorMax = new Vector2(1f, 1f);
                descTextRT.pivot = new Vector2(0.5f, 1f);
                descTextRT.anchoredPosition = Vector2.zero;
                descTextRT.sizeDelta = new Vector2(0f, 0f);
                _recipeDescription.textWrappingMode = TextWrappingModes.Normal;
                _recipeDescription.overflowMode = TextOverflowModes.Overflow;

                // ContentSizeFitter drives the text height to its preferred size
                var descCSF = _recipeDescription.gameObject.AddComponent<ContentSizeFitter>();
                descCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // ScrollRect — the scroll area itself is the viewport (has Mask)
                _descriptionScrollRect = scrollGO.AddComponent<ScrollRect>();
                _descriptionScrollRect.content = descTextRT;
                _descriptionScrollRect.viewport = scrollAreaRT;
                _descriptionScrollRect.vertical = true;
                _descriptionScrollRect.horizontal = false;
                _descriptionScrollRect.movementType = ScrollRect.MovementType.Clamped;
                _descriptionScrollRect.scrollSensitivity = _listScrollRect != null ? _listScrollRect.scrollSensitivity : 40f;

                // Scrollbar — sibling of scroll area, on the right edge of Decription panel
                var scrollbarGO = new GameObject("DescScrollbar", typeof(RectTransform));
                scrollbarGO.transform.SetParent(descScrollPanel, false);
                var scrollbarRT = scrollbarGO.GetComponent<RectTransform>();
                scrollbarRT.anchorMin = new Vector2(1f, 0f);
                scrollbarRT.anchorMax = new Vector2(1f, 1f);
                scrollbarRT.pivot = new Vector2(1f, 0.5f);
                scrollbarRT.sizeDelta = new Vector2(scrollbarWidth, 0f);
                scrollbarRT.offsetMin = new Vector2(-scrollbarWidth, 4f);  // extend to bottom of panel
                scrollbarRT.offsetMax = new Vector2(-2f, -4f);             // extend to top of panel

                var trackImg = scrollbarGO.AddComponent<Image>();
                trackImg.color = new Color(0f, 0f, 0f, 0f);

                var slidingGO = new GameObject("Sliding Area", typeof(RectTransform));
                slidingGO.transform.SetParent(scrollbarGO.transform, false);
                var slidingRT = slidingGO.GetComponent<RectTransform>();
                slidingRT.anchorMin = Vector2.zero;
                slidingRT.anchorMax = Vector2.one;
                slidingRT.offsetMin = Vector2.zero;
                slidingRT.offsetMax = Vector2.zero;

                var handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
                handleGO.transform.SetParent(slidingGO.transform, false);
                var handleRT = handleGO.GetComponent<RectTransform>();
                handleRT.anchorMin = Vector2.zero;
                handleRT.anchorMax = Vector2.one;
                handleRT.offsetMin = Vector2.zero;
                handleRT.offsetMax = Vector2.zero;
                var handleImg = handleGO.GetComponent<Image>();
                handleImg.color = new Color(0f, 0f, 0f, 0f); // hidden but functional

                var descScrollbar = scrollbarGO.AddComponent<Scrollbar>();
                descScrollbar.handleRect = handleRT;
                descScrollbar.direction = Scrollbar.Direction.BottomToTop;
                descScrollbar.targetGraphic = handleImg;

                _descriptionScrollRect.verticalScrollbar = descScrollbar;
                _descriptionScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }

            // ── Player preview (camera view in the right column) ──
            CreatePreviewPanel(invGui, previewColumnWidth, contentBaseWidth);

            // ══════════════════════════════════════════
            //  Shift content panels down and add tab buttons in the freed space
            // ══════════════════════════════════════════
            var tabListPanel = _clonedPanel.transform.Find("RecipeList") as RectTransform;
            var tabDescPanel = _clonedPanel.transform.Find("Decription") as RectTransform;
            var tabPreviewPanel = _clonedPanel.transform.Find("PreviewContainer") as RectTransform;

            // Shift panels up so they fill the UI background fully.
            // Top edge extends to match the background, bottom lifts up.
            float liftUp = 15f;
            foreach (var p in new[] { tabListPanel, tabDescPanel, tabPreviewPanel })
            {
                if (p == null) continue;
                p.offsetMin = new Vector2(p.offsetMin.x, p.offsetMin.y + liftUp - 4f);
                p.offsetMax = new Vector2(p.offsetMax.x, p.offsetMax.y + liftUp);
            }

            // Create tab buttons above each panel, cloning the craft button's visual style
            if (_craftButton != null && tabListPanel != null && tabDescPanel != null && tabPreviewPanel != null)
            {
                var craftRT = _craftButton.GetComponent<RectTransform>();
                float craftW = craftRT != null ? craftRT.rect.width : 140f;
                float craftH = craftRT != null ? craftRT.rect.height : 30f;
                float tabTopPad = 6f;
                float pw = panelRT.sizeDelta.x;
                float ph = panelRT.sizeDelta.y;

                // Compute a uniform Y for all tabs using the highest panel top edge
                float listTop = tabListPanel.anchorMax.y * ph + tabListPanel.offsetMax.y;
                float descTop = tabDescPanel.anchorMax.y * ph + tabDescPanel.offsetMax.y;
                float prevTop = tabPreviewPanel.anchorMax.y * ph + tabPreviewPanel.offsetMax.y;
                float uniformTabY = Mathf.Max(listTop, Mathf.Max(descTop, prevTop)) + tabTopPad;

                _tabClasses = CreateTabButton("Classes", 0, tabListPanel, pw, craftW, craftH, uniformTabY);
                _tabSkills  = CreateTabButton("Skills",  1, tabDescPanel, pw, craftW, craftH, uniformTabY);
                _tabArmor   = CreateTabButton("Armor",   2, tabPreviewPanel, pw, craftW, craftH, uniformTabY);

                _activeTab = 0;
                RefreshTabHighlights();
            }

            // ── Center all three columns within the panel ──
            var previewContainerRT = _clonedPanel.transform.Find("PreviewContainer") as RectTransform;
            var finalListPanel = _clonedPanel.transform.Find("RecipeList") as RectTransform;
            if (previewContainerRT != null && finalListPanel != null)
            {
                float pw = panelRT.sizeDelta.x;
                float leftContent = GetRectLeft(finalListPanel, pw);
                float rightContent = GetRectRight(previewContainerRT, pw);
                float totalMargin = pw - (rightContent - leftContent);
                float targetMargin = totalMargin / 2f;
                float shiftX = targetMargin - leftContent;

                if (Mathf.Abs(shiftX) > 1f)
                {
                    foreach (RectTransform child in _clonedPanel.transform)
                    {
                        bool fullStretch = child.anchorMin.x <= 0.01f && child.anchorMax.x >= 0.99f;
                        if (!fullStretch)
                            ShiftRectX(child, shiftX);
                    }
                }
            }

            // ══════════════════════════════════════════
            //  About panel — full-width overlay spanning all three columns
            // ══════════════════════════════════════════
            {
                var aboutListPanel = _clonedPanel.transform.Find("RecipeList") as RectTransform;
                var aboutPreviewPanel = _clonedPanel.transform.Find("PreviewContainer") as RectTransform;
                var aboutDescPanel = _clonedPanel.transform.Find("Decription");
                Image aboutDescImg = aboutDescPanel != null ? aboutDescPanel.GetComponent<Image>() : null;

                if (aboutListPanel != null && aboutPreviewPanel != null)
                {
                    float pw2 = panelRT.sizeDelta.x;
                    float leftEdge = GetRectLeft(aboutListPanel, pw2);
                    float rightEdge = GetRectRight(aboutPreviewPanel, pw2);

                    // Panel background
                    _armorPanel = new GameObject("AboutPanel", typeof(RectTransform), typeof(Image));
                    _armorPanel.transform.SetParent(_clonedPanel.transform, false);
                    var aboutRT = _armorPanel.GetComponent<RectTransform>();
                    aboutRT.anchorMin = aboutListPanel.anchorMin;
                    aboutRT.anchorMax = new Vector2(aboutPreviewPanel.anchorMax.x, aboutListPanel.anchorMax.y);
                    aboutRT.pivot = new Vector2(0.5f, 0.5f);
                    aboutRT.offsetMin = new Vector2(leftEdge - aboutRT.anchorMin.x * pw2, aboutListPanel.offsetMin.y);
                    aboutRT.offsetMax = new Vector2(rightEdge - aboutRT.anchorMax.x * pw2, aboutListPanel.offsetMax.y);

                    var aboutImg = _armorPanel.GetComponent<Image>();
                    if (aboutDescImg != null && aboutDescImg.sprite != null)
                    {
                        aboutImg.sprite = aboutDescImg.sprite;
                        aboutImg.type = aboutDescImg.type;
                        aboutImg.color = aboutDescImg.color;
                    }
                    else
                    {
                        aboutImg.color = new Color(0f, 0f, 0f, 0.45f);
                    }

                    // Scroll area inside the About panel
                    float aboutSBWidth = 10f;
                    var aboutScrollGO = new GameObject("AboutScrollArea", typeof(RectTransform), typeof(Image), typeof(Mask));
                    aboutScrollGO.transform.SetParent(_armorPanel.transform, false);
                    var aboutScrollRT = aboutScrollGO.GetComponent<RectTransform>();
                    aboutScrollRT.anchorMin = Vector2.zero;
                    aboutScrollRT.anchorMax = Vector2.one;
                    aboutScrollRT.offsetMin = new Vector2(12f, 8f);
                    aboutScrollRT.offsetMax = new Vector2(-aboutSBWidth - 6f, -8f);
                    aboutScrollGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.003f);
                    aboutScrollGO.GetComponent<Mask>().showMaskGraphic = false;

                    // Text content
                    var aboutTextGO = new GameObject("AboutText", typeof(RectTransform));
                    aboutTextGO.transform.SetParent(aboutScrollGO.transform, false);
                    var aboutTextRT = aboutTextGO.GetComponent<RectTransform>();
                    aboutTextRT.anchorMin = new Vector2(0f, 1f);
                    aboutTextRT.anchorMax = new Vector2(1f, 1f);
                    aboutTextRT.pivot = new Vector2(0.5f, 1f);
                    aboutTextRT.anchoredPosition = Vector2.zero;
                    aboutTextRT.sizeDelta = Vector2.zero;

                    var aboutTxt = aboutTextGO.AddComponent<TextMeshProUGUI>();
                    var aboutFont = FindValheimFont();
                    if (aboutFont != null)
                        aboutTxt.font = aboutFont;
                    aboutTxt.fontSize = 18f;
                    aboutTxt.color = Color.white;
                    aboutTxt.textWrappingMode = TextWrappingModes.Normal;
                    aboutTxt.overflowMode = TextOverflowModes.Overflow;
                    aboutTxt.richText = true;
                    aboutTxt.alignment = TextAlignmentOptions.TopLeft;
                    aboutTxt.text = "";
                    _armorText = aboutTxt;

                    var aboutCSF = aboutTextGO.AddComponent<ContentSizeFitter>();
                    aboutCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                    // ScrollRect
                    var aboutSR = aboutScrollGO.AddComponent<ScrollRect>();
                    aboutSR.content = aboutTextRT;
                    aboutSR.viewport = aboutScrollRT;
                    aboutSR.vertical = true;
                    aboutSR.horizontal = false;
                    aboutSR.movementType = ScrollRect.MovementType.Clamped;
                    aboutSR.scrollSensitivity = _listScrollRect != null ? _listScrollRect.scrollSensitivity : 40f;

                    // Scrollbar
                    var aboutSBGO = new GameObject("AboutScrollbar", typeof(RectTransform));
                    aboutSBGO.transform.SetParent(_armorPanel.transform, false);
                    var aboutSBRT = aboutSBGO.GetComponent<RectTransform>();
                    aboutSBRT.anchorMin = new Vector2(1f, 0f);
                    aboutSBRT.anchorMax = new Vector2(1f, 1f);
                    aboutSBRT.pivot = new Vector2(1f, 0.5f);
                    aboutSBRT.sizeDelta = new Vector2(aboutSBWidth, 0f);
                    aboutSBRT.offsetMin = new Vector2(-aboutSBWidth, 4f);
                    aboutSBRT.offsetMax = new Vector2(-2f, -4f);

                    var aboutTrack = aboutSBGO.AddComponent<Image>();
                    aboutTrack.color = new Color(0f, 0f, 0f, 0f);

                    var aboutSliding = new GameObject("Sliding Area", typeof(RectTransform));
                    aboutSliding.transform.SetParent(aboutSBGO.transform, false);
                    var aboutSlidingRT = aboutSliding.GetComponent<RectTransform>();
                    aboutSlidingRT.anchorMin = Vector2.zero;
                    aboutSlidingRT.anchorMax = Vector2.one;
                    aboutSlidingRT.offsetMin = Vector2.zero;
                    aboutSlidingRT.offsetMax = Vector2.zero;

                    var aboutHandle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
                    aboutHandle.transform.SetParent(aboutSliding.transform, false);
                    var aboutHandleRT = aboutHandle.GetComponent<RectTransform>();
                    aboutHandleRT.anchorMin = Vector2.zero;
                    aboutHandleRT.anchorMax = Vector2.one;
                    aboutHandleRT.offsetMin = Vector2.zero;
                    aboutHandleRT.offsetMax = Vector2.zero;
                    var aboutHandleImg = aboutHandle.GetComponent<Image>();
                    aboutHandleImg.color = new Color(0f, 0f, 0f, 0f);

                    var aboutSB = aboutSBGO.AddComponent<Scrollbar>();
                    aboutSB.handleRect = aboutHandleRT;
                    aboutSB.direction = Scrollbar.Direction.BottomToTop;
                    aboutSB.targetGraphic = aboutHandleImg;

                    aboutSR.verticalScrollbar = aboutSB;
                    aboutSR.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

                    _armorScrollRect = aboutSR;

                    // Start hidden — Classes tab is default
                    _armorPanel.SetActive(false);
                }
            }

            // ══════════════════════════════════════════
            //  Skills panel — occupies the Description column only (skill tree text + unlock button)
            // ══════════════════════════════════════════
            {
                var skillsDescPanel = _clonedPanel.transform.Find("Decription") as RectTransform;
                Image skillsDescImg = skillsDescPanel != null ? skillsDescPanel.GetComponent<Image>() : null;

                if (skillsDescPanel != null)
                {
                    float pw3 = panelRT.sizeDelta.x;

                    _skillsPanel = new GameObject("SkillsPanel", typeof(RectTransform), typeof(Image));
                    _skillsPanel.transform.SetParent(_clonedPanel.transform, false);
                    var skillsRT = _skillsPanel.GetComponent<RectTransform>();
                    // Match Description panel position exactly
                    skillsRT.anchorMin = skillsDescPanel.anchorMin;
                    skillsRT.anchorMax = skillsDescPanel.anchorMax;
                    skillsRT.pivot = skillsDescPanel.pivot;
                    skillsRT.offsetMin = skillsDescPanel.offsetMin;
                    skillsRT.offsetMax = skillsDescPanel.offsetMax;

                    var skillsBg = _skillsPanel.GetComponent<Image>();
                    if (skillsDescImg != null && skillsDescImg.sprite != null)
                    {
                        skillsBg.sprite = skillsDescImg.sprite;
                        skillsBg.type = skillsDescImg.type;
                        skillsBg.color = skillsDescImg.color;
                    }
                    else
                    {
                        skillsBg.color = new Color(0f, 0f, 0f, 0.45f);
                    }

                    // Scroll area
                    float skillsSBWidth = 10f;
                    var skillsScrollGO = new GameObject("SkillsScrollArea", typeof(RectTransform), typeof(Image), typeof(Mask));
                    skillsScrollGO.transform.SetParent(_skillsPanel.transform, false);
                    var skillsScrollRT = skillsScrollGO.GetComponent<RectTransform>();
                    skillsScrollRT.anchorMin = Vector2.zero;
                    skillsScrollRT.anchorMax = Vector2.one;
                    skillsScrollRT.offsetMin = new Vector2(12f, 8f);
                    skillsScrollRT.offsetMax = new Vector2(-skillsSBWidth - 6f, -8f);
                    skillsScrollGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.003f);
                    skillsScrollGO.GetComponent<Mask>().showMaskGraphic = false;

                    // Text content
                    var skillsTextGO = new GameObject("SkillsText", typeof(RectTransform));
                    skillsTextGO.transform.SetParent(skillsScrollGO.transform, false);
                    var skillsTextRT = skillsTextGO.GetComponent<RectTransform>();
                    skillsTextRT.anchorMin = new Vector2(0f, 1f);
                    skillsTextRT.anchorMax = new Vector2(1f, 1f);
                    skillsTextRT.pivot = new Vector2(0.5f, 1f);
                    skillsTextRT.anchoredPosition = Vector2.zero;
                    skillsTextRT.sizeDelta = Vector2.zero;

                    _skillsText = skillsTextGO.AddComponent<TextMeshProUGUI>();
                    var skillsFont = FindValheimFont();
                    if (skillsFont != null)
                        _skillsText.font = skillsFont;
                    _skillsText.fontSize = 18f;
                    _skillsText.color = Color.white;
                    _skillsText.textWrappingMode = TextWrappingModes.Normal;
                    _skillsText.overflowMode = TextOverflowModes.Overflow;
                    _skillsText.richText = true;
                    _skillsText.alignment = TextAlignmentOptions.TopLeft;
                    _skillsText.text = "";

                    var skillsCSF = skillsTextGO.AddComponent<ContentSizeFitter>();
                    skillsCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                    // ScrollRect
                    var skillsSR = skillsScrollGO.AddComponent<ScrollRect>();
                    skillsSR.content = skillsTextRT;
                    skillsSR.viewport = skillsScrollRT;
                    skillsSR.vertical = true;
                    skillsSR.horizontal = false;
                    skillsSR.movementType = ScrollRect.MovementType.Clamped;
                    skillsSR.scrollSensitivity = _listScrollRect != null ? _listScrollRect.scrollSensitivity : 40f;

                    // Scrollbar
                    var skillsSBGO = new GameObject("SkillsScrollbar", typeof(RectTransform));
                    skillsSBGO.transform.SetParent(_skillsPanel.transform, false);
                    var skillsSBRT = skillsSBGO.GetComponent<RectTransform>();
                    skillsSBRT.anchorMin = new Vector2(1f, 0f);
                    skillsSBRT.anchorMax = new Vector2(1f, 1f);
                    skillsSBRT.pivot = new Vector2(1f, 0.5f);
                    skillsSBRT.sizeDelta = new Vector2(skillsSBWidth, 0f);
                    skillsSBRT.offsetMin = new Vector2(-skillsSBWidth, 4f);
                    skillsSBRT.offsetMax = new Vector2(-2f, -4f);

                    var skillsTrack = skillsSBGO.AddComponent<Image>();
                    skillsTrack.color = new Color(0f, 0f, 0f, 0f);

                    var skillsSliding = new GameObject("Sliding Area", typeof(RectTransform));
                    skillsSliding.transform.SetParent(skillsSBGO.transform, false);
                    var skillsSlidingRT = skillsSliding.GetComponent<RectTransform>();
                    skillsSlidingRT.anchorMin = Vector2.zero;
                    skillsSlidingRT.anchorMax = Vector2.one;
                    skillsSlidingRT.offsetMin = Vector2.zero;
                    skillsSlidingRT.offsetMax = Vector2.zero;

                    var skillsHandle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
                    skillsHandle.transform.SetParent(skillsSliding.transform, false);
                    var skillsHandleRT = skillsHandle.GetComponent<RectTransform>();
                    skillsHandleRT.anchorMin = Vector2.zero;
                    skillsHandleRT.anchorMax = Vector2.one;
                    skillsHandleRT.offsetMin = Vector2.zero;
                    skillsHandleRT.offsetMax = Vector2.zero;
                    var skillsHandleImg = skillsHandle.GetComponent<Image>();
                    skillsHandleImg.color = new Color(0f, 0f, 0f, 0f);

                    var skillsSB = skillsSBGO.AddComponent<Scrollbar>();
                    skillsSB.handleRect = skillsHandleRT;
                    skillsSB.direction = Scrollbar.Direction.BottomToTop;
                    skillsSB.targetGraphic = skillsHandleImg;

                    skillsSR.verticalScrollbar = skillsSB;
                    skillsSR.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

                    _skillsScrollRect = skillsSR;

                    // ── Unlock Skill button at the bottom of the skills panel ──
                    if (_craftButton != null)
                    {
                        var unlockGO = Instantiate(_craftButton.gameObject, _skillsPanel.transform);
                        unlockGO.name = "UnlockSkillButton";

                        _unlockButton = unlockGO.GetComponent<Button>();
                        if (_unlockButton != null)
                        {
                            _unlockButton.onClick.RemoveAllListeners();
                            _unlockButton.onClick.AddListener(OnUnlockButtonClicked);
                            _unlockButton.navigation = new Navigation { mode = Navigation.Mode.None };
                            _unlockButton.interactable = false;
                        }

                        _unlockButtonLabel = unlockGO.GetComponentInChildren<TMP_Text>(true);
                        if (_unlockButtonLabel != null)
                        {
                            _unlockButtonLabel.gameObject.SetActive(true);
                            _unlockButtonLabel.text = "Unlock Skill";
                        }
                        StripButtonHints(unlockGO, _unlockButtonLabel);

                        var unlockRT = unlockGO.GetComponent<RectTransform>();
                        var craftBtnRT = _craftButton.GetComponent<RectTransform>();
                        float btnH = craftBtnRT != null ? craftBtnRT.rect.height : 30f;
                        unlockRT.anchorMin = new Vector2(0f, 0f);
                        unlockRT.anchorMax = new Vector2(1f, 0f);
                        unlockRT.pivot = new Vector2(0.5f, 0f);
                        unlockRT.sizeDelta = new Vector2(-24f, btnH);
                        unlockRT.anchoredPosition = new Vector2(0f, 8f);

                        skillsScrollRT.offsetMin = new Vector2(12f, btnH + 16f);
                    }

                    _skillsPanel.SetActive(false);
                }
            }

            // ══════════════════════════════════════════
            //  Abilities panel — occupies the Preview column on the Skills tab
            //  Built identically to the class list (RecipeList clone structure)
            // ══════════════════════════════════════════
            {
                var abilitiesPreviewPanel = _clonedPanel.transform.Find("PreviewContainer") as RectTransform;
                var abilitiesDescPanel = _clonedPanel.transform.Find("Decription") as RectTransform;
                Image abilitiesDescImg = abilitiesDescPanel != null ? abilitiesDescPanel.GetComponent<Image>() : null;

                if (abilitiesPreviewPanel != null)
                {
                    _activePowerPanel = new GameObject("AbilitiesPanel", typeof(RectTransform), typeof(Image));
                    _activePowerPanel.transform.SetParent(_clonedPanel.transform, false);
                    var apRT = _activePowerPanel.GetComponent<RectTransform>();
                    // Match Preview column position exactly
                    apRT.anchorMin = abilitiesPreviewPanel.anchorMin;
                    apRT.anchorMax = abilitiesPreviewPanel.anchorMax;
                    apRT.pivot = abilitiesPreviewPanel.pivot;
                    apRT.offsetMin = abilitiesPreviewPanel.offsetMin;
                    apRT.offsetMax = abilitiesPreviewPanel.offsetMax;

                    var apBg = _activePowerPanel.GetComponent<Image>();
                    if (abilitiesDescImg != null && abilitiesDescImg.sprite != null)
                    {
                        apBg.sprite = abilitiesDescImg.sprite;
                        apBg.type = abilitiesDescImg.type;
                        apBg.color = abilitiesDescImg.color;
                    }
                    else
                    {
                        apBg.color = new Color(0f, 0f, 0f, 0.45f);
                    }

                    // ── Scrollable list area (identical to RecipeList internal structure) ──
                    float apSBWidth = 10f;

                    var apScrollGO = new GameObject("AbilitiesScrollArea", typeof(RectTransform), typeof(Image), typeof(Mask));
                    apScrollGO.transform.SetParent(_activePowerPanel.transform, false);
                    var apScrollRT = apScrollGO.GetComponent<RectTransform>();
                    apScrollRT.anchorMin = Vector2.zero;
                    apScrollRT.anchorMax = Vector2.one;
                    apScrollRT.offsetMin = new Vector2(2f, 0f);
                    apScrollRT.offsetMax = new Vector2(-apSBWidth - 2f, 0f);
                    apScrollGO.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);
                    apScrollGO.GetComponent<Mask>().showMaskGraphic = false;

                    var apContentGO = new GameObject("Content", typeof(RectTransform));
                    apContentGO.transform.SetParent(apScrollGO.transform, false);
                    _powerListRoot = apContentGO.GetComponent<RectTransform>();
                    _powerListRoot.anchorMin = new Vector2(0f, 1f);
                    _powerListRoot.anchorMax = new Vector2(1f, 1f);
                    _powerListRoot.pivot = new Vector2(0.5f, 1f);
                    _powerListRoot.anchoredPosition = Vector2.zero;
                    _powerListRoot.sizeDelta = Vector2.zero;

                    // ScrollRect
                    var apSR = apScrollGO.AddComponent<ScrollRect>();
                    apSR.content = _powerListRoot;
                    apSR.viewport = apScrollRT;
                    apSR.vertical = true;
                    apSR.horizontal = false;
                    apSR.movementType = ScrollRect.MovementType.Clamped;
                    apSR.scrollSensitivity = _listScrollRect != null ? _listScrollRect.scrollSensitivity : 40f;
                    _powerScrollRect = apSR;

                    // Scrollbar (identical to class list)
                    var apSBGO = new GameObject("AbilitiesScrollbar", typeof(RectTransform));
                    apSBGO.transform.SetParent(_activePowerPanel.transform, false);
                    var apSBRT = apSBGO.GetComponent<RectTransform>();
                    apSBRT.anchorMin = new Vector2(1f, 0f);
                    apSBRT.anchorMax = new Vector2(1f, 1f);
                    apSBRT.pivot = new Vector2(1f, 0.5f);
                    apSBRT.sizeDelta = new Vector2(apSBWidth, 0f);
                    apSBRT.offsetMin = new Vector2(-apSBWidth, 4f);
                    apSBRT.offsetMax = new Vector2(-2f, -4f);

                    var apTrack = apSBGO.AddComponent<Image>();
                    apTrack.color = new Color(0f, 0f, 0f, 0f);

                    var apSliding = new GameObject("Sliding Area", typeof(RectTransform));
                    apSliding.transform.SetParent(apSBGO.transform, false);
                    var apSlidingRT = apSliding.GetComponent<RectTransform>();
                    apSlidingRT.anchorMin = Vector2.zero;
                    apSlidingRT.anchorMax = Vector2.one;
                    apSlidingRT.offsetMin = Vector2.zero;
                    apSlidingRT.offsetMax = Vector2.zero;

                    var apHandle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
                    apHandle.transform.SetParent(apSliding.transform, false);
                    var apHandleRT = apHandle.GetComponent<RectTransform>();
                    apHandleRT.anchorMin = Vector2.zero;
                    apHandleRT.anchorMax = Vector2.one;
                    apHandleRT.offsetMin = Vector2.zero;
                    apHandleRT.offsetMax = Vector2.zero;
                    var apHandleImg = apHandle.GetComponent<Image>();
                    apHandleImg.color = new Color(0f, 0f, 0f, 0f);

                    var apSB = apSBGO.AddComponent<Scrollbar>();
                    apSB.handleRect = apHandleRT;
                    apSB.direction = Scrollbar.Direction.BottomToTop;
                    apSB.targetGraphic = apHandleImg;

                    apSR.verticalScrollbar = apSB;
                    apSR.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

                    // ── Select Power button at the bottom ──
                    if (_craftButton != null)
                    {
                        var selectGO = Instantiate(_craftButton.gameObject, _activePowerPanel.transform);
                        selectGO.name = "SelectPowerButton";
                        selectGO.SetActive(true);

                        var selectBtnRT = selectGO.GetComponent<RectTransform>();
                        var craftBtnRT2 = _craftButton.GetComponent<RectTransform>();
                        float btnH2 = craftBtnRT2 != null ? craftBtnRT2.rect.height : 30f;
                        selectBtnRT.anchorMin = new Vector2(0f, 0f);
                        selectBtnRT.anchorMax = new Vector2(1f, 0f);
                        selectBtnRT.pivot = new Vector2(0.5f, 0f);
                        selectBtnRT.sizeDelta = new Vector2(-24f, btnH2);
                        selectBtnRT.anchoredPosition = new Vector2(0f, 8f);

                        _selectPowerButton = selectGO.GetComponent<Button>();
                        if (_selectPowerButton != null)
                        {
                            _selectPowerButton.onClick.RemoveAllListeners();
                            _selectPowerButton.onClick.AddListener(OnSelectPowerClicked);
                            _selectPowerButton.navigation = new Navigation { mode = Navigation.Mode.None };
                        }

                        _selectPowerLabel = selectGO.GetComponentInChildren<TMP_Text>();
                        if (_selectPowerLabel != null)
                            _selectPowerLabel.text = "Select Power";
                        StripButtonHints(selectGO, _selectPowerLabel);

                        // Raise scroll area bottom to clear the button
                        apScrollRT.offsetMin = new Vector2(2f, btnH2 + 16f);
                    }

                    _activePowerPanel.SetActive(false);
                }
            }

            _canvasGO.SetActive(false);
            _uiBuilt = true;
        }

        // ══════════════════════════════════════════
        //  PLAYER PREVIEW (camera in the right column of the widened panel)
        // ══════════════════════════════════════════

        private void CreatePreviewPanel(InventoryGui invGui, float columnWidth, float contentBaseWidth)
        {
            // Match the RT aspect ratio to the actual display area to avoid squishing.
            // Column is columnWidth wide, panel height from sizeDelta.
            var panelRT = _clonedPanel.GetComponent<RectTransform>();
            float panelHeight = panelRT.sizeDelta.y;
            int rtScale = 2; // supersampling for sharper preview
            int rtW = Mathf.Max(64, Mathf.RoundToInt(columnWidth) * rtScale);
            int rtH = Mathf.Max(64, Mathf.RoundToInt(panelHeight) * rtScale);
            _previewRT = new RenderTexture(rtW, rtH, 24, RenderTextureFormat.ARGB32);
            _previewRT.antiAliasing = 4;

            // ── Preview Camera ──
            _previewCamGO = new GameObject("ClassPreview_Camera");
            DontDestroyOnLoad(_previewCamGO);
            _previewCamera = _previewCamGO.AddComponent<Camera>();
            _previewCamera.targetTexture = _previewRT;
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _previewCamera.fieldOfView = 30f;
            _previewCamera.nearClipPlane = 0.1f;
            _previewCamera.farClipPlane = 10f;
            _previewCamera.depth = -2;
            _previewCamera.enabled = false;

            int charLayer = LayerMask.NameToLayer("character");
            if (charLayer < 0) charLayer = 9;
            int charNetLayer = LayerMask.NameToLayer("character_net");
            int previewMask = (1 << charLayer);
            if (charNetLayer >= 0) previewMask |= (1 << charNetLayer);
            _previewCamera.cullingMask = previewMask;

            Vector3 cloneCenter = PreviewSpawnPos + Vector3.up * 0.85f;
            _previewCamGO.transform.position = cloneCenter + Vector3.forward * 5.0f;
            _previewCamGO.transform.LookAt(cloneCenter);

            // ── Background panel using Decription panel's style ──
            var descPanel = _clonedPanel.transform.Find("Decription");
            Image descImg = descPanel != null ? descPanel.GetComponent<Image>() : null;

            // Match the RecipeList's vertical position for consistency
            var origRecipeList = _clonedPanel.transform.Find("RecipeList") as RectTransform;

            var containerGO = new GameObject("PreviewContainer", typeof(RectTransform), typeof(Image));
            containerGO.transform.SetParent(_clonedPanel.transform, false);
            var containerRT = containerGO.GetComponent<RectTransform>();

            if (origRecipeList != null)
            {
                // Place in the right column: same vertical position as RecipeList,
                // but horizontally aligned to fill the extra column width.
                float totalWidth = panelRT.sizeDelta.x;  // 770
                float margin = origRecipeList.offsetMin.x; // left margin from RecipeList

                containerRT.anchorMin = origRecipeList.anchorMin;
                containerRT.anchorMax = origRecipeList.anchorMax;
                containerRT.pivot = origRecipeList.pivot;

                // Keep a small gap from the description panel and match width to class-list column.
                float scrollGap = 1f;
                float leftEdge = contentBaseWidth - margin + scrollGap;
                float rightEdge = leftEdge + columnWidth;
                float maxRightEdge = totalWidth - margin - 3f;
                if (rightEdge > maxRightEdge)
                    rightEdge = maxRightEdge;
                containerRT.offsetMin = new Vector2(leftEdge, origRecipeList.offsetMin.y);
                containerRT.offsetMax = new Vector2(rightEdge, origRecipeList.offsetMax.y);
            }
            else
            {
                containerRT.anchorMin = new Vector2(1f, 0f);
                containerRT.anchorMax = new Vector2(1f, 1f);
                containerRT.pivot = new Vector2(1f, 0.5f);
                containerRT.sizeDelta = new Vector2(columnWidth - 20f, 0f);
                containerRT.anchoredPosition = new Vector2(-10f, 0f);
                containerRT.offsetMin = new Vector2(containerRT.offsetMin.x, 100f);
                containerRT.offsetMax = new Vector2(containerRT.offsetMax.x, -30f);
            }

            var containerImg = containerGO.GetComponent<Image>();
            if (descImg != null && descImg.sprite != null)
            {
                containerImg.sprite = descImg.sprite;
                containerImg.type = descImg.type;
                containerImg.color = descImg.color;
            }
            else
            {
                containerImg.color = new Color(0f, 0f, 0f, 0.45f);
            }

            // ── RawImage inside the container ──
            var rawImgGO = new GameObject("PreviewImage", typeof(RectTransform));
            rawImgGO.transform.SetParent(containerGO.transform, false);
            var rawRT = rawImgGO.GetComponent<RectTransform>();
            rawRT.anchorMin = Vector2.zero;
            rawRT.anchorMax = Vector2.one;
            rawRT.offsetMin = Vector2.zero;
            rawRT.offsetMax = Vector2.zero;

            var rawImg = rawImgGO.AddComponent<RawImage>();
            rawImg.texture = _previewRT;
            rawImg.color = Color.white;
            rawImg.raycastTarget = false;

        }

        /// <summary>
        /// Creates a clone of the player model for preview, identical to FejdStartup.SetupCharacterPreview.
        /// The clone is placed far away so only the preview camera sees it.
        /// </summary>
        private void SetupPreviewClone()
        {
            ClearPreviewClone();

            var player = Player.m_localPlayer;
            if (player == null) return;

            // Get the player prefab
            var prefab = ZNetScene.instance?.GetPrefab("Player");
            if (prefab == null) return;

            // Instantiate without network initialization (same as FejdStartup)
            ZNetView.m_forceDisableInit = true;
            try
            {
                _previewClone = Instantiate(prefab, PreviewSpawnPos, Quaternion.identity);
            }
            finally
            {
                ZNetView.m_forceDisableInit = false;
            }

            // Remove physics so the clone doesn't fall
            var rb = _previewClone.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);

            // Disable game-logic MonoBehaviours on the clone to prevent NullRef spam
            // (the clone has no ZDO, so Player/Humanoid/Character Update loops would fail)
            foreach (var mb in _previewClone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is VisEquipment) continue;    // keep for equipment rendering
                mb.enabled = false;
            }

            // Set animators to normal update mode (same as FejdStartup)
            foreach (var anim in _previewClone.GetComponentsInChildren<Animator>())
                anim.updateMode = AnimatorUpdateMode.Normal;

            // Copy current player's appearance into the clone
            var clonePlayer = _previewClone.GetComponent<Player>();
            if (clonePlayer != null)
            {
                // Temporarily enable Player to load appearance data, then disable again
                clonePlayer.enabled = true;
                var tempProfile = new PlayerProfile("_preview_tmp", FileHelpers.FileSource.Local);
                tempProfile.SavePlayerData(player);
                tempProfile.LoadPlayerData(clonePlayer);
                clonePlayer.enabled = false;

                // Force VisEquipment to create hair/beard/equipment meshes
                var visEquip = _previewClone.GetComponent<VisEquipment>();
                if (visEquip != null)
                {
                    var updateVisuals = AccessTools.Method(typeof(VisEquipment), "UpdateVisuals");
                    updateVisuals?.Invoke(visEquip, null);
                }
            }

            // Face the clone toward the preview camera (camera is at +Z, so clone faces +Z = 0° rotation)
            _previewClone.transform.rotation = Quaternion.identity;

            // Force all renderers onto the character layer so only they appear in the preview
            int charLayer = LayerMask.NameToLayer("character");
            if (charLayer < 0) charLayer = 9;
            foreach (var t in _previewClone.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = charLayer;
        }

        private void ClearPreviewClone()
        {
            if (_previewClone != null)
            {
                Destroy(_previewClone);
                _previewClone = null;
            }
        }

        private void UpdatePreviewCamera()
        {
            if (_previewCamGO == null) return;
            Vector3 cloneCenter = PreviewSpawnPos + Vector3.up * 0.85f;
            float rad = _previewRotation * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * 5.0f;
            _previewCamGO.transform.position = cloneCenter + offset;
            _previewCamGO.transform.LookAt(cloneCenter);
        }

        /// <summary>
        /// Updates the preview clone's visual equipment to match the selected class.
        /// Uses reflection to set equipment fields and force hash resets, bypassing ZDO dependency.
        /// Re-applies character layer to newly attached equipment models.
        /// </summary>
        private void UpdatePreviewEquipment(StartingClass cls)
        {
            if (_previewClone == null) return;
            var visEquip = _previewClone.GetComponent<VisEquipment>();
            if (visEquip == null) return;

            // Map of VisEquipment field name → prefab name to set
            var slotFields = new Dictionary<string, string>
            {
                { "m_rightItem", "" }, { "m_leftItem", "" },
                { "m_chestItem", "" }, { "m_legItem", "" },
                { "m_helmetItem", "" }, { "m_shoulderItem", "" },
                { "m_utilityItem", "" }, { "m_leftBackItem", "" },
                { "m_rightBackItem", "" }
            };

            // Also track variant fields
            var variantFields = new Dictionary<string, int>
            {
                { "m_leftItemVariant", 0 }, { "m_shoulderItemVariant", 0 },
                { "m_leftBackItemVariant", 0 }
            };

            if (cls.PreviewEquipment != null)
            {
                foreach (var prefabName in cls.PreviewEquipment)
                {
                    var prefab = ZNetScene.instance?.GetPrefab(prefabName);
                    if (prefab == null) continue;
                    var drop = prefab.GetComponent<ItemDrop>();
                    if (drop == null || drop.m_itemData == null) continue;

                    switch (drop.m_itemData.m_shared.m_itemType)
                    {
                        case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                        case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                        case ItemDrop.ItemData.ItemType.Tool:
                            slotFields["m_rightItem"] = prefabName;
                            break;
                        case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                        case ItemDrop.ItemData.ItemType.Shield:
                        case ItemDrop.ItemData.ItemType.Torch:
                            slotFields["m_leftItem"] = prefabName;
                            break;
                        case ItemDrop.ItemData.ItemType.Bow:
                            slotFields["m_leftBackItem"] = prefabName;
                            break;
                        case ItemDrop.ItemData.ItemType.Chest:
                            slotFields["m_chestItem"] = prefabName;
                            break;
                        case ItemDrop.ItemData.ItemType.Legs:
                            slotFields["m_legItem"] = prefabName;
                            break;
                        case ItemDrop.ItemData.ItemType.Helmet:
                            slotFields["m_helmetItem"] = prefabName;
                            break;
                        case ItemDrop.ItemData.ItemType.Shoulder:
                            slotFields["m_shoulderItem"] = prefabName;
                            break;
                        case ItemDrop.ItemData.ItemType.Utility:
                            slotFields["m_utilityItem"] = prefabName;
                            break;
                    }
                }
            }

            // Directly set equipment name fields via reflection (bypasses ZDO)
            foreach (var kv in slotFields)
                AccessTools.Field(typeof(VisEquipment), kv.Key)?.SetValue(visEquip, kv.Value);
            foreach (var kv in variantFields)
                AccessTools.Field(typeof(VisEquipment), kv.Key)?.SetValue(visEquip, kv.Value);

            // Reset all tracking hash fields to force UpdateVisuals to re-apply everything
            if (_visEquipHashFields == null)
            {
                var allFields = AccessTools.GetDeclaredFields(typeof(VisEquipment));
                var hashList = new List<System.Reflection.FieldInfo>();
                foreach (var f in allFields)
                {
                    if (f.FieldType == typeof(int) && f.Name.StartsWith("m_current") && f.Name.Contains("Hash"))
                        hashList.Add(f);
                }
                _visEquipHashFields = hashList.ToArray();
            }
            foreach (var field in _visEquipHashFields)
                field.SetValue(visEquip, -1);

            AccessTools.Method(typeof(VisEquipment), "UpdateVisuals")?.Invoke(visEquip, null);

            // Equipment models are spawned by UpdateVisuals — force them onto character layer
            int charLayer = LayerMask.NameToLayer("character");
            if (charLayer < 0) charLayer = 9;
            foreach (var t in _previewClone.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = charLayer;
        }

        private void UpdatePreviewArmorSet(ArmorSetDef set)
        {
            if (_previewClone == null) return;
            var visEquip = _previewClone.GetComponent<VisEquipment>();
            if (visEquip == null) return;

            // Clear all slots
            var slotFields = new Dictionary<string, string>
            {
                { "m_rightItem", "" }, { "m_leftItem", "" },
                { "m_chestItem", "" }, { "m_legItem", "" },
                { "m_helmetItem", "" }, { "m_shoulderItem", "" },
                { "m_utilityItem", "" }, { "m_leftBackItem", "" },
                { "m_rightBackItem", "" }
            };

            if (set != null)
            {
                foreach (var prefabName in set.Pieces)
                {
                    var prefab = ZNetScene.instance?.GetPrefab(prefabName);
                    if (prefab == null) continue;
                    var drop = prefab.GetComponent<ItemDrop>();
                    if (drop == null || drop.m_itemData == null) continue;

                    switch (drop.m_itemData.m_shared.m_itemType)
                    {
                        case ItemDrop.ItemData.ItemType.Chest:
                            slotFields["m_chestItem"] = prefabName;
                            break;
                        case ItemDrop.ItemData.ItemType.Legs:
                            slotFields["m_legItem"] = prefabName;
                            break;
                        case ItemDrop.ItemData.ItemType.Helmet:
                            slotFields["m_helmetItem"] = prefabName;
                            break;
                        case ItemDrop.ItemData.ItemType.Shoulder:
                            slotFields["m_shoulderItem"] = prefabName;
                            break;
                    }
                }
            }

            foreach (var kv in slotFields)
                AccessTools.Field(typeof(VisEquipment), kv.Key)?.SetValue(visEquip, kv.Value);

            // Reset hash fields to force visual update
            if (_visEquipHashFields == null)
            {
                var allFields = AccessTools.GetDeclaredFields(typeof(VisEquipment));
                var hashList = new List<System.Reflection.FieldInfo>();
                foreach (var f in allFields)
                {
                    if (f.FieldType == typeof(int) && f.Name.StartsWith("m_current") && f.Name.Contains("Hash"))
                        hashList.Add(f);
                }
                _visEquipHashFields = hashList.ToArray();
            }
            foreach (var field in _visEquipHashFields)
                field.SetValue(visEquip, -1);

            AccessTools.Method(typeof(VisEquipment), "UpdateVisuals")?.Invoke(visEquip, null);

            int charLayer = LayerMask.NameToLayer("character");
            if (charLayer < 0) charLayer = 9;
            foreach (var t in _previewClone.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = charLayer;
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


            // Get the description panel's dark background style
            var descPanel = _clonedPanel.transform.Find("Decription");
            Image descImg = descPanel != null ? descPanel.GetComponent<Image>() : null;

            var templateRT = invGui.m_recipeElementPrefab.transform as RectTransform;
            float templateHeight = 32f;
            if (templateRT != null)
                templateHeight = Mathf.Max(24f, Mathf.Max(templateRT.rect.height, templateRT.sizeDelta.y));

            float rowHeight = Mathf.Max(templateHeight * 2f, 48f);
            float gap = 6f;
            float spacing = rowHeight + gap;

            // Strip ALL layout components from _recipeListRoot and its parents up to RecipeList.
            // Must use DestroyImmediate — Destroy() is deferred and layout components would
            // still override our manual sizing during the current frame's layout pass.
            StripLayoutComponents(_recipeListRoot.gameObject);
            if (_listScrollRect != null)
            {
                StripLayoutComponents(_listScrollRect.gameObject);
                if (_listScrollRect.viewport != null)
                    StripLayoutComponents(_listScrollRect.viewport.gameObject);
            }

            for (int i = 0; i < _classes.Count; i++)
            {
                int idx = i;
                var cls = _classes[i];

                // Use the recipe element prefab (correct sizing for the list)
                var element = Instantiate(invGui.m_recipeElementPrefab, _recipeListRoot);
                element.SetActive(true);
                element.name = "ClassElement_" + cls.Name;

                // Bigger, wider class-list entry buttons.
                var elemRT = element.transform as RectTransform;
                StripLayoutComponents(element);

                // Set anchors and pivot first, then position, then size last
                // sizeDelta.x = 0 with anchors 0-1 means full parent width
                elemRT.anchorMin = new Vector2(0f, 1f);
                elemRT.anchorMax = new Vector2(1f, 1f);
                elemRT.pivot = new Vector2(0.5f, 1f);
                elemRT.anchoredPosition = new Vector2(0f, i * -spacing);
                elemRT.sizeDelta = new Vector2(0f, rowHeight);

                // Style background to match the dark description panel
                var elemImg = element.GetComponent<Image>();
                if (elemImg != null && descImg != null)
                {
                    elemImg.sprite = descImg.sprite;
                    elemImg.type = descImg.type;
                    elemImg.color = descImg.color;
                }

                // Wire up button click and disable auto-navigation to prevent stale highlights
                var btn = element.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        if (_activeTab == 2)
                        {
                            // Armor tab: select armor slot
                            _armorSelectedSlot = idx;
                            RefreshArmorPanel();
                        }
                        else
                        {
                            SelectClass(idx);
                        }
                        if (EventSystem.current != null)
                            EventSystem.current.SetSelectedGameObject(null);
                    });
                    btn.navigation = new Navigation { mode = Navigation.Mode.None };
                }

                // Class icon — pinned to the left of the row
                float iconSize = rowHeight - 8f;
                float iconPadding = 4f;
                float textLeftOffset = iconPadding + iconSize + 6f; // space after icon

                var iconTr = element.transform.Find("icon");
                if (iconTr != null)
                {
                    var iconImg = iconTr.GetComponent<Image>();
                    Sprite classIcon = GetClassIcon(cls);

                    if (iconImg != null && classIcon != null)
                    {
                        iconImg.sprite = classIcon;
                        iconImg.color = Color.white;
                        iconImg.preserveAspect = true;
                        iconTr.gameObject.SetActive(true);

                        var iconRT = iconTr as RectTransform;
                        if (iconRT != null)
                        {
                            // Pin icon to left-center of the row
                            iconRT.anchorMin = new Vector2(0f, 0.5f);
                            iconRT.anchorMax = new Vector2(0f, 0.5f);
                            iconRT.pivot = new Vector2(0f, 0.5f);
                            iconRT.sizeDelta = new Vector2(iconSize, iconSize);
                            iconRT.anchoredPosition = new Vector2(iconPadding, 0f);
                        }
                    }
                    else
                    {
                        iconTr.gameObject.SetActive(false);
                    }
                }

                // Class name — positioned to the right of the icon
                var nameTr = element.transform.Find("name");
                if (nameTr != null)
                {
                    var nameRT = nameTr as RectTransform;
                    if (nameRT != null)
                    {
                        // Stretch from after the icon to the right edge
                        nameRT.anchorMin = new Vector2(0f, 0f);
                        nameRT.anchorMax = new Vector2(1f, 1f);
                        nameRT.pivot = new Vector2(0.5f, 0.5f);
                        nameRT.offsetMin = new Vector2(textLeftOffset, 0f);
                        nameRT.offsetMax = new Vector2(-4f, 0f);
                    }
                    var nameTxt = nameTr.GetComponent<TMP_Text>();
                    if (nameTxt != null)
                    {
                        nameTxt.text = cls.Name;
                        nameTxt.color = Color.white;
                        nameTxt.enableAutoSizing = false;
                        nameTxt.fontSize = Mathf.Max(nameTxt.fontSize, 24f);
                        nameTxt.alignment = TextAlignmentOptions.MidlineLeft;
                    }
                }

                // Hide unused children
                var durTr = element.transform.Find("Durability");
                if (durTr != null) durTr.gameObject.SetActive(false);
                var qualTr = element.transform.Find("QualityLevel");
                if (qualTr != null) qualTr.gameObject.SetActive(false);
                var selTr = element.transform.Find("selected");
                if (selTr != null)
                {
                    selTr.gameObject.SetActive(false);
                    var selImg = selTr.GetComponent<Image>();
                    if (selImg != null)
                        selImg.color = new Color(0.83f, 0.64f, 0.31f, 0.5f);
                }

                _classElements.Add(element);
            }

            // Set content height for scrolling
            float contentHeight = (_classes.Count > 0)
                ? ((_classes.Count - 1) * spacing + rowHeight)
                : rowHeight;
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
            UpdatePreviewEquipment(_classes[index]);
            if (_activeTab == 1) RefreshSkillsPanel();
        }

        private void RefreshHighlights()
        {
            for (int i = 0; i < _classElements.Count; i++)
            {
                // Toggle "selected" child highlight
                var selTr = _classElements[i]?.transform.Find("selected");
                if (selTr != null)
                    selTr.gameObject.SetActive(i == _selectedIndex);
            }
        }

        private void SwitchTab(int newTab)
        {
            // Wrap around: 0 → 1 → 2 → 0
            int count = TabNames.Length;
            newTab = ((newTab % count) + count) % count;
            if (newTab == _activeTab) return;
            _activeTab = newTab;
            _panelFocus = 0; // Reset to left panel when switching tabs
            RefreshTabHighlights();
            RefreshTabPanels();
            // Clear EventSystem selection to prevent stale button highlights
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void RefreshTabPanels()
        {
            bool showClasses = (_activeTab == 0);
            bool showSkills = (_activeTab == 1);
            bool showArmor = (_activeTab == 2);

            // Class list panel visible on Classes, Skills, and Armor tabs
            var listPanel = _clonedPanel.transform.Find("RecipeList");
            var descPanel = _clonedPanel.transform.Find("Decription");
            var previewPanel = _clonedPanel.transform.Find("PreviewContainer");
            if (listPanel != null) listPanel.gameObject.SetActive(showClasses || showSkills || showArmor);
            if (descPanel != null) descPanel.gameObject.SetActive(showClasses || showArmor);
            if (previewPanel != null) previewPanel.gameObject.SetActive(showClasses || showArmor);

            // Preview camera on Classes and Armor tabs
            if (_previewCamera != null)
                _previewCamera.enabled = showClasses || showArmor;

            // Toggle the skills panel (Description column) and abilities panel (Preview column)
            if (_skillsPanel != null) _skillsPanel.SetActive(showSkills);
            if (_activePowerPanel != null) _activePowerPanel.SetActive(showSkills);
            if (showSkills) RefreshSkillsPanel();

            // The old armor overlay panel (full-width) is no longer used on Armor tab
            if (_armorPanel != null) _armorPanel.SetActive(false);

            // Armor tab reuses the class list + description + preview panels
            if (showArmor)
            {
                RefreshArmorPanel();
            }
            else
            {
                // Restore class list entries after leaving Armor tab
                RestoreClassListEntries();
                if (_selectedIndex >= 0 && _selectedIndex < _classes.Count)
                    RefreshDetail();
            }
        }

        private void RefreshSkillsPanel()
        {
            if (_skillsText == null) return;
            if (_selectedIndex < 0 || _selectedIndex >= _classes.Count)
            {
                _skillsText.text = "<size=22><color=#D4A24E>Select a class to view abilities</color></size>";
                return;
            }

            var cls = _classes[_selectedIndex];
            var player = Player.m_localPlayer;
            int currentPoints = player != null ? SkillPointSystem.GetPoints(player) : 0;
            string playerClass = player != null ? ClassPersistence.GetSelectedClassName(player) : null;
            bool isPlayerClass = playerClass == cls.Name;

            var sb = new System.Text.StringBuilder();

            // Calculate remaining cost (only locked abilities)
            int remainingCost = 0;
            if (cls.Abilities != null)
                for (int ai = 0; ai < cls.Abilities.Count; ai++)
                    if (!cls.Abilities[ai].IsPassive && isPlayerClass && !AbilityManager.IsAbilityUnlocked(player, cls.Name, ai))
                        remainingCost += cls.Abilities[ai].PointCost;

            // Header
            sb.AppendLine($"<align=center><size=26><color=#D4A24E>{cls.Name}</color></size></align>");
            sb.AppendLine($"<align=center><size=16><color=#AAAAAA>Skill Tree</color></size></align>");
            sb.AppendLine();
            if (isPlayerClass)
                sb.AppendLine($"<align=center><size=17>Available: <color=#8AE58A>{currentPoints}</color>  \u2022  Remaining: <color=#D4A24E>{remainingCost}</color></size></align>");
            else
                sb.AppendLine($"<align=center><size=17><color=#999999>Select this class to unlock abilities</color></size></align>");
            sb.AppendLine();

            if (cls.Abilities == null || cls.Abilities.Count == 0)
            {
                sb.AppendLine("<color=#999999>No abilities defined for this class.</color>");
                _skillsText.text = sb.ToString();
                return;
            }

            string[] tierLabels = { "Passive", "Tier I", "Tier II", "Tier III", "Tier IV", "Ultimate" };

            // Track next locked ability index for the unlock button
            int nextLockedIndex = -1;

            for (int i = 0; i < cls.Abilities.Count; i++)
            {
                var ability = cls.Abilities[i];
                string tierLabel = i < tierLabels.Length ? tierLabels[i] : $"Tier {i}";
                bool unlocked = isPlayerClass && AbilityManager.IsAbilityUnlocked(player, cls.Name, i);

                // Determine type tag and colors based on passive vs ability
                string typeTag = ability.IsPassive ? "Passive" : "Ability";
                // Passives: blue/teal, Abilities: orange/gold
                string typeColor = ability.IsPassive ? "#66B3E5" : "#D4A24E";

                if (unlocked)
                {
                    // ── Unlocked ──
                    string accentColor = ability.IsPassive ? "#8AE58A" : "#E5C56A";
                    sb.AppendLine($"<align=center><color={accentColor}>━━━━━━━━━━━━━━━━━━━━━━</color></align>");
                    sb.AppendLine($"<size=22><color={accentColor}>\u2605 {ability.Name}</color></size>");
                    sb.AppendLine($"<size=14><color={typeColor}>{typeTag}</color> <color={accentColor}>\u2014 Unlocked</color></size>");
                    sb.AppendLine();
                    sb.AppendLine($"<size=16>{ability.Description}</size>");

                    // Skill bonuses only on the first passive (index 0)
                    if (i == 0 && cls.SkillBonuses != null && cls.SkillBonuses.Count > 0)
                    {
                        sb.AppendLine();
                        sb.Append("<size=15><color=#8AE58A>");
                        for (int j = 0; j < cls.SkillBonuses.Count; j++)
                        {
                            if (j > 0) sb.Append("  \u2022  ");
                            string skillName = FormatPascalCase(cls.SkillBonuses[j].SkillType.ToString());
                            sb.Append($"{skillName} +{cls.SkillBonuses[j].BonusLevel:0}");
                        }
                        sb.AppendLine("</color></size>");
                    }
                    sb.AppendLine($"<align=center><color={accentColor}>━━━━━━━━━━━━━━━━━━━━━━</color></align>");
                }
                else
                {
                    // ── Locked ──
                    if (nextLockedIndex < 0) nextLockedIndex = i;

                    bool isUltimate = i == cls.Abilities.Count - 1 && !ability.IsPassive;
                    string borderColor = isUltimate ? "#8B4513" : "#555555";
                    string nameColor = isUltimate ? "#D4A24E" : "#BBBBBB";
                    string descColor = isUltimate ? "#999999" : "#888888";
                    string icon = isUltimate ? "\u25C6" : "\u25C8";

                    sb.AppendLine($"<align=center><color={borderColor}>━━━━━━━━━━━━━━━━━━━━━━</color></align>");
                    sb.AppendLine($"<size=22><color={nameColor}>{icon} {ability.Name}</color></size>");
                    sb.AppendLine($"<size=14><color={typeColor}>{typeTag}</color> <color=#777777>\u2022 {tierLabel} \u2022 {ability.PointCost} pts</color></size>");
                    sb.AppendLine();
                    sb.AppendLine($"<size=16><color={descColor}>{ability.Description}</color></size>");
                    sb.AppendLine();
                    string haveColor = currentPoints >= ability.PointCost ? "#8AE58A" : "#666666";
                    sb.AppendLine($"<size=15><color=#D4A24E>\u25C6 {ability.PointCost} Skill Points</color>  <color={haveColor}>(You have: {currentPoints})</color></size>");
                    sb.AppendLine($"<align=center><color={borderColor}>━━━━━━━━━━━━━━━━━━━━━━</color></align>");
                }

                // Connector arrow between abilities
                if (i < cls.Abilities.Count - 1)
                {
                    string arrowColor = unlocked ? "#8AE58A" : "#666666";
                    sb.AppendLine($"<align=center><size=20><color={arrowColor}>\u2502</color></size></align>");
                    sb.AppendLine($"<align=center><size=20><color={arrowColor}>\u25BC</color></size></align>");
                }
            }

            _skillsText.text = sb.ToString();
            _skillsText.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_skillsText.rectTransform);

            // Update unlock button
            if (_unlockButton != null)
            {
                if (!isPlayerClass)
                {
                    _unlockButton.interactable = false;
                    if (_unlockButtonLabel != null)
                        _unlockButtonLabel.text = "Not Your Class";
                }
                else if (nextLockedIndex >= 0)
                {
                    var nextAbility = cls.Abilities[nextLockedIndex];
                    bool canAfford = currentPoints >= nextAbility.PointCost;
                    _unlockButton.interactable = canAfford;
                    if (_unlockButtonLabel != null)
                        _unlockButtonLabel.text = $"Unlock {nextAbility.Name} ({nextAbility.PointCost} pts)";
                }
                else
                {
                    _unlockButton.interactable = false;
                    if (_unlockButtonLabel != null)
                        _unlockButtonLabel.text = "All Skills Unlocked";
                }
            }

            // Also refresh the active power panel
            RefreshActivePowerPanel();
        }

        private void OnUnlockButtonClicked()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _classes.Count) return;
            var cls = _classes[_selectedIndex];
            var player = Player.m_localPlayer;
            if (player == null) return;

            string playerClass = ClassPersistence.GetSelectedClassName(player);
            if (playerClass != cls.Name) return;

            if (cls.Abilities == null) return;

            // Find the next locked ability (sequential unlock order)
            for (int i = 0; i < cls.Abilities.Count; i++)
            {
                if (AbilityManager.IsAbilityUnlocked(player, cls.Name, i)) continue;

                // This is the next ability to unlock
                var ability = cls.Abilities[i];
                if (AbilityManager.UnlockAbility(player, cls.Name, i, ability.PointCost))
                {
                    player.m_skillLevelupEffects.Create(player.GetHeadPoint(), player.transform.rotation, player.transform);
                    player.Message(MessageHud.MessageType.Center, $"Unlocked: {ability.Name}!");
                    RefreshSkillsPanel();
                }
                else
                {
                    player.Message(MessageHud.MessageType.Center, "Not enough Skill Points!");
                }
                return;
            }
        }

        private void RefreshActivePowerPanel()
        {
            if (_powerListRoot == null) return;

            var player = Player.m_localPlayer;
            if (player == null) return;

            var invGui = InventoryGui.instance;
            if (invGui == null || invGui.m_recipeElementPrefab == null) return;

            string playerClass = ClassPersistence.GetSelectedClassName(player);
            string activePower = ActivePowerManager.GetActivePower(player);

            // Clear old entries
            foreach (var entry in _powerEntries)
            {
                if (entry != null) Destroy(entry);
            }
            _powerEntries.Clear();
            _powerIds.Clear();

            // Use identical row sizing as class list
            var templateRT = invGui.m_recipeElementPrefab.transform as RectTransform;
            float templateHeight = 32f;
            if (templateRT != null)
                templateHeight = Mathf.Max(24f, Mathf.Max(templateRT.rect.height, templateRT.sizeDelta.y));
            float rowHeight = Mathf.Max(templateHeight * 2f, 48f);
            float gap = 6f;
            float spacing = rowHeight + gap;

            // Find description panel image for background style (same as class list)
            var descPanel = _clonedPanel?.transform.Find("Decription");
            Image descImg = descPanel != null ? descPanel.GetComponent<Image>() : null;

            // Build entries
            int entryIndex = 0;

            // Forsaken Power entry
            {
                string forsakenName = "Forsaken Power";
                StatusEffect gpSE = null;
                player.GetGuardianPowerHUD(out gpSE, out _);
                if (gpSE != null)
                    forsakenName = Localization.instance.Localize(gpSE.m_name);

                Sprite icon = (gpSE != null) ? gpSE.m_icon : null;
                bool isCurrent = activePower == ActivePowerManager.Forsaken;
                var entry = CreatePowerEntryFromPrefab(invGui, "forsaken", forsakenName, icon, true, isCurrent, entryIndex, spacing, rowHeight, descImg);
                _powerEntries.Add(entry);
                _powerIds.Add("forsaken");
                entryIndex++;
            }

            // Class ability entries
            if (!string.IsNullOrEmpty(playerClass))
            {
                StartingClass playerCls = null;
                foreach (var c in _classes)
                {
                    if (c.Name == playerClass) { playerCls = c; break; }
                }

                if (playerCls != null && playerCls.Abilities != null)
                {
                    for (int i = 0; i < playerCls.Abilities.Count; i++)
                    {
                        var ability = playerCls.Abilities[i];
                        if (ability.IsPassive) continue;

                        string powerId = GetPowerIdForAbility(playerCls.Name, i);
                        if (powerId == null) continue;

                        bool unlocked = AbilityManager.IsAbilityUnlocked(player, playerCls.Name, i);
                        bool isCurrent = unlocked && activePower == powerId;
                        var entry = CreatePowerEntryFromPrefab(invGui, powerId, ability.Name, null, unlocked, isCurrent, entryIndex, spacing, rowHeight, descImg);
                        _powerEntries.Add(entry);
                        _powerIds.Add(powerId);
                        entryIndex++;
                    }
                }
            }

            // Set content height
            float contentH = entryIndex > 0 ? ((entryIndex - 1) * spacing + rowHeight) : rowHeight;
            _powerListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentH);

            // Auto-select the currently active power
            int activeIdx = _powerIds.IndexOf(activePower);
            _selectedPowerIndex = activeIdx >= 0 ? activeIdx : 0;
            UpdatePowerSelection();
        }

        private GameObject CreatePowerEntryFromPrefab(InventoryGui invGui, string powerId, string displayName,
            Sprite icon, bool unlocked, bool isCurrent, int index, float spacing, float rowHeight, Image descImg)
        {
            var element = Instantiate(invGui.m_recipeElementPrefab, _powerListRoot);
            element.SetActive(true);
            element.name = "PowerEntry_" + powerId;

            var elemRT = element.transform as RectTransform;
            StripLayoutComponents(element);

            elemRT.anchorMin = new Vector2(0f, 1f);
            elemRT.anchorMax = new Vector2(1f, 1f);
            elemRT.pivot = new Vector2(0.5f, 1f);
            elemRT.anchoredPosition = new Vector2(0f, index * -spacing);
            elemRT.sizeDelta = new Vector2(0f, rowHeight);

            // Style background to match class list
            var elemImg = element.GetComponent<Image>();
            if (elemImg != null && descImg != null)
            {
                elemImg.sprite = descImg.sprite;
                elemImg.type = descImg.type;
                elemImg.color = descImg.color;
            }

            // Button — click to highlight (select), not directly activate
            var btn = element.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                int capturedIdx = index;
                btn.onClick.AddListener(() =>
                {
                    _selectedPowerIndex = capturedIdx;
                    UpdatePowerSelection();
                    if (EventSystem.current != null)
                        EventSystem.current.SetSelectedGameObject(null);
                });
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
                btn.interactable = unlocked;
            }

            // Icon
            float iconSize = rowHeight - 8f;
            float iconPadding = 4f;
            float textLeftOffset = iconPadding + iconSize + 6f;

            var iconTr = element.transform.Find("icon");
            if (iconTr != null)
            {
                var iconImg = iconTr.GetComponent<Image>();
                if (iconImg != null && icon != null)
                {
                    iconImg.sprite = icon;
                    iconImg.color = Color.white;
                    iconImg.preserveAspect = true;
                    iconTr.gameObject.SetActive(true);

                    var iconRT = iconTr as RectTransform;
                    if (iconRT != null)
                    {
                        iconRT.anchorMin = new Vector2(0f, 0.5f);
                        iconRT.anchorMax = new Vector2(0f, 0.5f);
                        iconRT.pivot = new Vector2(0f, 0.5f);
                        iconRT.sizeDelta = new Vector2(iconSize, iconSize);
                        iconRT.anchoredPosition = new Vector2(iconPadding, 0f);
                    }
                }
                else
                {
                    iconTr.gameObject.SetActive(false);
                    textLeftOffset = 8f;
                }
            }

            // Name text
            var nameTr = element.transform.Find("name");
            if (nameTr != null)
            {
                var nameRT = nameTr as RectTransform;
                if (nameRT != null)
                {
                    nameRT.anchorMin = new Vector2(0f, 0f);
                    nameRT.anchorMax = new Vector2(1f, 1f);
                    nameRT.pivot = new Vector2(0.5f, 0.5f);
                    nameRT.offsetMin = new Vector2(textLeftOffset, 0f);
                    nameRT.offsetMax = new Vector2(-4f, 0f);
                }
                var nameTxt = nameTr.GetComponent<TMP_Text>();
                if (nameTxt != null)
                {
                    if (!unlocked)
                    {
                        nameTxt.text = $"<color=#666666>{displayName} (Locked)</color>";
                    }
                    else
                    {
                        nameTxt.text = displayName;
                    }
                    nameTxt.color = Color.white;
                    nameTxt.enableAutoSizing = false;
                    nameTxt.fontSize = Mathf.Max(nameTxt.fontSize, 24f);
                    nameTxt.alignment = TextAlignmentOptions.MidlineLeft;
                }
            }

            // Hide unused children
            var durTr = element.transform.Find("Durability");
            if (durTr != null) durTr.gameObject.SetActive(false);
            var qualTr = element.transform.Find("QualityLevel");
            if (qualTr != null) qualTr.gameObject.SetActive(false);
            var selTr = element.transform.Find("selected");
            if (selTr != null)
            {
                selTr.gameObject.SetActive(isCurrent);
                var selImg = selTr.GetComponent<Image>();
                if (selImg != null)
                    selImg.color = new Color(0.83f, 0.64f, 0.31f, 0.5f);
            }

            return element;
        }

        private void UpdatePowerSelection()
        {
            // Update visual selection highlight on power entries
            for (int i = 0; i < _powerEntries.Count; i++)
            {
                var selTr = _powerEntries[i]?.transform.Find("selected");
                if (selTr != null)
                    selTr.gameObject.SetActive(i == _selectedPowerIndex);
            }

            // Update select button
            if (_selectPowerButton != null)
            {
                bool valid = _selectedPowerIndex >= 0 && _selectedPowerIndex < _powerIds.Count;
                _selectPowerButton.interactable = valid;

                if (_selectPowerLabel != null && valid)
                {
                    var player = Player.m_localPlayer;
                    string currentActive = (player != null) ? ActivePowerManager.GetActivePower(player) : "";
                    string selectedId = _powerIds[_selectedPowerIndex];

                    if (selectedId == currentActive)
                        _selectPowerLabel.text = "Selected";
                    else
                        _selectPowerLabel.text = "Select Power";
                }
            }
        }

        private void OnSelectPowerClicked()
        {
            if (_selectedPowerIndex < 0 || _selectedPowerIndex >= _powerIds.Count) return;

            var player = Player.m_localPlayer;
            if (player == null) return;

            string powerId = _powerIds[_selectedPowerIndex];
            ActivePowerManager.SetActivePower(player, powerId);
            RefreshActivePowerPanel();
        }

        private static string GetPowerIdForAbility(string className, int abilityIndex)
        {
            if (className == "Assassin")
            {
                if (abilityIndex == 1) return "MarkedByFate";
                if (abilityIndex == 5) return "BladeDance";
            }
            return null;
        }

        private string GetSelectedClassName()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _classes.Count) return null;
            return _classes[_selectedIndex].Name;
        }

        private StartingClass GetSelectedClass()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _classes.Count) return null;
            return _classes[_selectedIndex];
        }

        private void RefreshTabHighlights()
        {
            var tabs = new[] { _tabClasses, _tabSkills, _tabArmor };
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] == null) continue;
                var btn = tabs[i].GetComponent<Button>();
                if (btn == null) continue;

                bool isActive = (i == _activeTab);
                btn.interactable = true;

                // Disconnect the Button from the Image so it cannot override our color.
                // Unity's Selectable applies tint to targetGraphic on state changes,
                // which fights with our manual Image.color assignment.
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = null;

                var img = tabs[i].GetComponent<Image>();
                if (img != null)
                    img.color = isActive
                        ? new Color(0.83f, 0.64f, 0.31f, 1f)    // bright gold (active tab)
                        : new Color(0.4f, 0.35f, 0.25f, 0.8f);  // dimmed (inactive tab)
            }
        }

        private void ClearDetail()
        {
            if (_recipeIcon != null) _recipeIcon.enabled = false;
            if (_recipeName != null) _recipeName.enabled = false;
            if (_recipeDescription != null) _recipeDescription.enabled = false;
            _descScrollResetFrames = 3;

            if (_craftButton != null)
            {
                _craftButton.interactable = false;
                if (_craftButtonLabel != null)
                    _craftButtonLabel.text = "Select a Class";
            }
        }

        private void RefreshDetail()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _classes.Count)
            {
                ClearDetail();
                return;
            }

            var cls = _classes[_selectedIndex];

            // Hide icon (not used for class descriptions)
            if (_recipeIcon != null)
                _recipeIcon.enabled = false;

            // Class name → recipe name
            if (_recipeName != null)
            {
                _recipeName.text = cls.Name;
                _recipeName.enabled = true;
            }

            // Class description + starting gear + skill bonuses → recipe description
            if (_recipeDescription != null)
            {
                string desc = cls.Description;

                // Starting Gear section
                if (cls.Items != null && cls.Items.Count > 0)
                {
                    desc += "\n\n<color=#D4A24E>Starting Gear:</color>";
                    foreach (var item in cls.Items)
                    {
                        string itemName = GetLocalizedName(item.PrefabName);
                        if (item.Quantity > 1)
                            desc += $"\n<color=#CCCCCC>\u2022 {itemName} x{item.Quantity}</color>";
                        else
                            desc += $"\n<color=#CCCCCC>\u2022 {itemName}</color>";
                    }
                }

                // Skill Bonuses section
                if (cls.SkillBonuses != null && cls.SkillBonuses.Count > 0)
                {
                    desc += "\n\n<color=#8AE58A>Skill Bonuses:</color>";
                    foreach (var bonus in cls.SkillBonuses)
                    {
                        string skillName = FormatPascalCase(bonus.SkillType.ToString());
                        desc += $"\n<color=#8AE58A>\u2022 {skillName} +{bonus.BonusLevel:0}</color>";
                    }
                }

                _recipeDescription.text = desc;
                _recipeDescription.enabled = true;
                _recipeDescription.ForceMeshUpdate();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_recipeDescription.rectTransform);
            }

            // Force layout recalculation so ContentSizeFitter updates scroll content height
            Canvas.ForceUpdateCanvases();
            // Defer scroll-to-top to LateUpdate so ScrollRect has fully settled
            _descScrollResetFrames = 3;

            // Confirm button → "Begin as X"
            if (_craftButton != null)
            {
                _craftButton.interactable = true;
                if (_craftButtonLabel != null)
                    _craftButtonLabel.text = $"Begin as {cls.Name}";
            }
        }

        private void ConfirmSelection()
        {
            if (!_isVisible) return;

            // Armor tab: route to armor upgrade instead
            if (_activeTab == 2)
            {
                OnUpgradeArmorClicked();
                return;
            }

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

        /// <summary>Find the Valheim TMP font from existing UI or loaded assets.</summary>
        private TMP_FontAsset FindValheimFont()
        {
            // Try our cloned description text first
            if (_recipeDescription != null && _recipeDescription.font != null)
                return _recipeDescription.font;
            // Try other cloned TMP_Text references
            if (_recipeName != null && _recipeName.font != null)
                return _recipeName.font;
            if (_titleText != null && _titleText.font != null)
                return _titleText.font;
            // Fallback: search all loaded TMP fonts for the Valheim font
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (f.name.Contains("Valheim") || f.name.Contains("Averia"))
                    return f;
            return null;
        }

        // ══════════════════════════════════════════
        //  ARMOR TAB
        // ══════════════════════════════════════════

        /// <summary>
        /// Restore list entries to class data after leaving the Armor tab.
        /// </summary>
        private void RestoreClassListEntries()
        {
            if (_classes == null) return;
            for (int i = 0; i < _classElements.Count; i++)
            {
                if (i < _classes.Count)
                {
                    _classElements[i].SetActive(true);
                    var cls = _classes[i];

                    var nameTr = _classElements[i].transform.Find("name");
                    var nameTxt = nameTr != null ? nameTr.GetComponent<TMP_Text>() : null;
                    if (nameTxt != null)
                    {
                        nameTxt.text = cls.Name;
                        nameTxt.color = Color.white;
                    }

                    var iconTr = _classElements[i].transform.Find("icon");
                    if (iconTr != null)
                    {
                        var iconImg = iconTr.GetComponent<Image>();
                        Sprite classIcon = GetClassIcon(cls);
                        if (iconImg != null && classIcon != null)
                        {
                            iconImg.sprite = classIcon;
                            iconImg.color = Color.white;
                            iconImg.preserveAspect = true;
                            iconTr.gameObject.SetActive(true);
                        }
                        else
                        {
                            iconTr.gameObject.SetActive(false);
                        }
                    }
                }
                else
                {
                    _classElements[i].SetActive(false);
                }
            }

            // Restore content height for class count
            if (_recipeListRoot != null && _classes.Count > 0)
            {
                var templateRT = InventoryGui.instance?.m_recipeElementPrefab?.transform as RectTransform;
                float templateHeight = 32f;
                if (templateRT != null)
                    templateHeight = Mathf.Max(24f, Mathf.Max(templateRT.rect.height, templateRT.sizeDelta.y));
                float rowHeight = Mathf.Max(templateHeight * 2f, 48f);
                float spacing = rowHeight + 6f;
                float contentHeight = (_classes.Count - 1) * spacing + rowHeight;
                _recipeListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
            }

            RefreshHighlights();
        }

        /// <summary>
        /// Create additional list elements if we have more armor sets than class entries.
        /// </summary>
        private void EnsureArmorListElements(int needed)
        {
            if (_classElements.Count >= needed) return;

            var invGui = InventoryGui.instance;
            if (_recipeListRoot == null || invGui == null || invGui.m_recipeElementPrefab == null) return;

            var descPanel = _clonedPanel.transform.Find("Decription");
            Image descImg = descPanel != null ? descPanel.GetComponent<Image>() : null;

            var templateRT = invGui.m_recipeElementPrefab.transform as RectTransform;
            float templateHeight = 32f;
            if (templateRT != null)
                templateHeight = Mathf.Max(24f, Mathf.Max(templateRT.rect.height, templateRT.sizeDelta.y));

            float rowHeight = Mathf.Max(templateHeight * 2f, 48f);
            float gap = 6f;
            float spacing = rowHeight + gap;

            while (_classElements.Count < needed)
            {
                int i = _classElements.Count;
                int idx = i;

                var element = Instantiate(invGui.m_recipeElementPrefab, _recipeListRoot);
                element.SetActive(false);
                element.name = "ArmorSetElement_" + i;

                var elemRT = element.transform as RectTransform;
                StripLayoutComponents(element);

                elemRT.anchorMin = new Vector2(0f, 1f);
                elemRT.anchorMax = new Vector2(1f, 1f);
                elemRT.pivot = new Vector2(0.5f, 1f);
                elemRT.anchoredPosition = new Vector2(0f, i * -spacing);
                elemRT.sizeDelta = new Vector2(0f, rowHeight);

                var elemImg = element.GetComponent<Image>();
                if (elemImg != null && descImg != null)
                {
                    elemImg.sprite = descImg.sprite;
                    elemImg.type = descImg.type;
                    elemImg.color = descImg.color;
                }

                var btn = element.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        if (_activeTab == 2)
                        {
                            _armorSelectedSlot = idx;
                            RefreshArmorPanel();
                        }
                        else
                        {
                            SelectClass(idx);
                        }
                        if (EventSystem.current != null)
                            EventSystem.current.SetSelectedGameObject(null);
                    });
                    btn.navigation = new Navigation { mode = Navigation.Mode.None };
                }

                // Icon layout
                float iconSize = rowHeight - 8f;
                float iconPadding = 4f;
                float textLeftOffset = iconPadding + iconSize + 6f;

                var iconTr = element.transform.Find("icon");
                if (iconTr != null)
                {
                    var iconRT = iconTr as RectTransform;
                    if (iconRT != null)
                    {
                        iconRT.anchorMin = new Vector2(0f, 0.5f);
                        iconRT.anchorMax = new Vector2(0f, 0.5f);
                        iconRT.pivot = new Vector2(0f, 0.5f);
                        iconRT.sizeDelta = new Vector2(iconSize, iconSize);
                        iconRT.anchoredPosition = new Vector2(iconPadding, 0f);
                    }
                    iconTr.gameObject.SetActive(false);
                }

                var nameTr = element.transform.Find("name");
                if (nameTr != null)
                {
                    var nameRT = nameTr as RectTransform;
                    if (nameRT != null)
                    {
                        nameRT.anchorMin = new Vector2(0f, 0f);
                        nameRT.anchorMax = new Vector2(1f, 1f);
                        nameRT.pivot = new Vector2(0.5f, 0.5f);
                        nameRT.offsetMin = new Vector2(textLeftOffset, 0f);
                        nameRT.offsetMax = new Vector2(-4f, 0f);
                    }
                    var nameTxt = nameTr.GetComponent<TMP_Text>();
                    if (nameTxt != null)
                    {
                        nameTxt.enableAutoSizing = false;
                        nameTxt.fontSize = Mathf.Max(nameTxt.fontSize, 24f);
                        nameTxt.alignment = TextAlignmentOptions.MidlineLeft;
                    }
                }

                var durTr = element.transform.Find("Durability");
                if (durTr != null) durTr.gameObject.SetActive(false);
                var qualTr = element.transform.Find("QualityLevel");
                if (qualTr != null) qualTr.gameObject.SetActive(false);
                var selTr = element.transform.Find("selected");
                if (selTr != null)
                {
                    selTr.gameObject.SetActive(false);
                    var selImg = selTr.GetComponent<Image>();
                    if (selImg != null)
                        selImg.color = new Color(0.83f, 0.64f, 0.31f, 0.5f);
                }

                _classElements.Add(element);
            }

            // Update content height for scrolling
            float contentHeight = needed > 0 ? ((needed - 1) * spacing + rowHeight) : rowHeight;
            _recipeListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        }

        private void RefreshArmorPanel()
        {
            var player = Player.m_localPlayer;
            var allSets = ArmorUpgradeSystem.GetAllSets();
            int points = player != null ? SkillPointSystem.GetPoints(player) : 0;

            // Clamp selection
            if (_armorSelectedSlot < 0) _armorSelectedSlot = 0;
            if (_armorSelectedSlot >= allSets.Length) _armorSelectedSlot = allSets.Length - 1;

            // Ensure enough list elements exist for all armor sets
            EnsureArmorListElements(allSets.Length);

            // ── Left panel: show all armor sets ──
            for (int i = 0; i < _classElements.Count; i++)
            {
                if (i < allSets.Length)
                {
                    _classElements[i].SetActive(true);
                    var set = allSets[i];
                    bool equipped = player != null && ArmorUpgradeSystem.IsSetEquipped(player, set);
                    int level = player != null ? ArmorUpgradeSystem.GetSetLevel(player, set) : 0;

                    // Update name text
                    var nameTr = _classElements[i].transform.Find("name");
                    var nameTxt = nameTr != null ? nameTr.GetComponent<TMP_Text>() : null;
                    if (nameTxt != null)
                    {
                        string levelTag = level > 0
                            ? $"  <size=16><color=#D4A24E>+{level}</color></size>"
                            : "";
                        nameTxt.text = $"{set.DisplayName}{levelTag}";
                        nameTxt.color = equipped ? Color.white : new Color(0.4f, 0.4f, 0.4f);
                    }

                    // Update icon from ObjectDB prefab
                    var iconTr = _classElements[i].transform.Find("icon");
                    if (iconTr != null)
                    {
                        var iconImg = iconTr.GetComponent<Image>();
                        if (iconImg != null)
                        {
                            var icon = ArmorUpgradeSystem.GetSetIcon(set);
                            if (icon != null)
                            {
                                iconImg.sprite = icon;
                                iconImg.color = equipped ? Color.white : new Color(0.3f, 0.3f, 0.3f);
                                iconImg.preserveAspect = true;
                                iconTr.gameObject.SetActive(true);
                            }
                            else
                            {
                                iconTr.gameObject.SetActive(false);
                            }
                        }
                    }

                    // Highlight selected set
                    var selTr = _classElements[i].transform.Find("selected");
                    if (selTr != null)
                        selTr.gameObject.SetActive(i == _armorSelectedSlot);
                }
                else
                {
                    _classElements[i].SetActive(false);
                }
            }

            // ── Description panel: upgrade tree for the SELECTED set ──
            if (_recipeIcon != null)
                _recipeIcon.enabled = false;

            var selSet = (_armorSelectedSlot >= 0 && _armorSelectedSlot < allSets.Length)
                ? allSets[_armorSelectedSlot] : null;
            bool setEquipped = selSet != null && player != null && ArmorUpgradeSystem.IsSetEquipped(player, selSet);
            int currentLevel = selSet != null && player != null ? ArmorUpgradeSystem.GetSetLevel(player, selSet) : 0;

            // Update preview model to show the selected armor set
            UpdatePreviewArmorSet(selSet);

            if (_recipeName != null)
            {
                _recipeName.text = selSet != null ? selSet.DisplayName : "Armor";
                _recipeName.enabled = true;
            }

            if (_recipeDescription != null)
            {
                var sb = new System.Text.StringBuilder();

                if (selSet != null)
                {
                    int pieceCount = selSet.Pieces.Length;
                    int remainingCost = (ArmorUpgradeSystem.MaxLevel - currentLevel) * ArmorUpgradeSystem.CostPerLevel;

                    // Header (name already shown in _recipeName above)
                    sb.AppendLine($"<align=center><size=16><color=#AAAAAA>Set Enhancement \u2022 {pieceCount} Pieces</color></size></align>");
                    sb.AppendLine();
                    if (setEquipped)
                        sb.AppendLine($"<align=center><size=17>Available: <color=#8AE58A>{points}</color>  \u2022  Remaining: <color=#D4A24E>{remainingCost}</color></size></align>");
                    else
                        sb.AppendLine($"<align=center><size=17>Available: <color=#8AE58A>{points}</color>  \u2022  <color=#FF6666>Not Equipped</color></size></align>");
                    sb.AppendLine();

                    // Named upgrade tiers
                    string[] tierNames = { "Reinforced", "Hardened", "Fortified", "Tempered", "Masterwork" };
                    string[] tierDescs = {
                        "Basic reinforcement strengthens the armor's structure.",
                        "The armor is hardened through skilled craftsmanship.",
                        "Fortified plating provides superior damage resistance.",
                        "Tempering the material pushes it beyond normal limits.",
                        "A masterwork of protection \u2014 the pinnacle of enhancement."
                    };

                    float perLevel = ArmorUpgradeSystem.BonusPerLevel;
                    float totalPerPiece = currentLevel * perLevel;
                    float totalAllPieces = totalPerPiece * pieceCount;

                    for (int lvl = 1; lvl <= ArmorUpgradeSystem.MaxLevel; lvl++)
                    {
                        string tierName = lvl <= tierNames.Length ? tierNames[lvl - 1] : $"Tier {lvl}";
                        string tierDesc = lvl <= tierDescs.Length ? tierDescs[lvl - 1] : "";
                        bool isUnlocked = lvl <= currentLevel;
                        bool isMax = (lvl == ArmorUpgradeSystem.MaxLevel);
                        float tierTotal = perLevel * pieceCount;

                        if (isUnlocked)
                        {
                            string accentColor = "#E5C56A";
                            sb.AppendLine($"<align=center><color={accentColor}>━━━━━━━━━━━━━━━━━━━━━━</color></align>");
                            sb.AppendLine($"<size=22><color={accentColor}>\u2605 {tierName}</color></size>");
                            sb.AppendLine($"<size=14><color=#D4A24E>Enhancement</color> <color={accentColor}>\u2014 Unlocked</color></size>");
                            sb.AppendLine();
                            sb.AppendLine($"<size=16>{tierDesc}</size>");
                            sb.AppendLine();
                            sb.AppendLine($"<size=15><color=#8AE58A>+{perLevel:F0} per piece ({pieceCount} pcs = +{tierTotal:F0} Armor)</color></size>");
                            sb.AppendLine($"<align=center><color={accentColor}>━━━━━━━━━━━━━━━━━━━━━━</color></align>");
                        }
                        else
                        {
                            string borderColor = isMax ? "#8B4513" : "#555555";
                            string nameColor = isMax ? "#D4A24E" : "#BBBBBB";
                            string descColor = isMax ? "#999999" : "#888888";
                            string icon = isMax ? "\u25C6" : "\u25C8";

                            sb.AppendLine($"<align=center><color={borderColor}>━━━━━━━━━━━━━━━━━━━━━━</color></align>");
                            sb.AppendLine($"<size=22><color={nameColor}>{icon} {tierName}</color></size>");
                            sb.AppendLine($"<size=14><color=#D4A24E>Enhancement</color> <color=#777777>\u2022 Tier {lvl} \u2022 {ArmorUpgradeSystem.CostPerLevel} pts</color></size>");
                            sb.AppendLine();
                            sb.AppendLine($"<size=16><color={descColor}>{tierDesc}</color></size>");
                            sb.AppendLine();
                            sb.AppendLine($"<size=15><color={descColor}>+{perLevel:F0} per piece ({pieceCount} pcs = +{tierTotal:F0} Armor)</color></size>");
                            sb.AppendLine();
                            string haveColor = points >= ArmorUpgradeSystem.CostPerLevel ? "#8AE58A" : "#666666";
                            sb.AppendLine($"<size=15><color=#D4A24E>\u25C6 {ArmorUpgradeSystem.CostPerLevel} Skill Points</color>  <color={haveColor}>(You have: {points})</color></size>");
                            sb.AppendLine($"<align=center><color={borderColor}>━━━━━━━━━━━━━━━━━━━━━━</color></align>");
                        }

                        if (lvl < ArmorUpgradeSystem.MaxLevel)
                        {
                            string arrowColor = lvl < currentLevel ? "#E5C56A" : "#555555";
                            sb.AppendLine($"<align=center><size=20><color={arrowColor}>\u2502</color></size></align>");
                            sb.AppendLine($"<align=center><size=20><color={arrowColor}>\u25BC</color></size></align>");
                        }
                    }

                    // Total bonus summary (after the last tier's bottom divider)
                    if (currentLevel > 0)
                    {
                        sb.AppendLine($"<align=center><size=17><color=#8AE58A>Total: +{totalPerPiece:F0} per piece \u2022 +{totalAllPieces:F0} Armor across set</color></size></align>");
                    }
                }

                _recipeDescription.text = sb.ToString();
                _recipeDescription.enabled = true;
                _recipeDescription.ForceMeshUpdate();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_recipeDescription.rectTransform);
            }

            // Craft button
            if (_craftButton != null)
            {
                bool selMaxed = currentLevel >= ArmorUpgradeSystem.MaxLevel;
                bool canAfford = points >= ArmorUpgradeSystem.CostPerLevel;

                _craftButton.interactable = setEquipped && !selMaxed && canAfford;
                if (_craftButtonLabel != null)
                {
                    if (!setEquipped)
                        _craftButtonLabel.text = "Not Equipped";
                    else if (selMaxed)
                        _craftButtonLabel.text = "Max Level";
                    else if (!canAfford)
                        _craftButtonLabel.text = $"Need {ArmorUpgradeSystem.CostPerLevel} pts";
                    else
                        _craftButtonLabel.text = $"Enhance Set ({ArmorUpgradeSystem.CostPerLevel} pts)";
                }
            }
        }

        private void OnUpgradeArmorClicked()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            var allSets = ArmorUpgradeSystem.GetAllSets();
            if (_armorSelectedSlot < 0 || _armorSelectedSlot >= allSets.Length) return;

            var set = allSets[_armorSelectedSlot];
            if (ArmorUpgradeSystem.TryUpgradeSet(player, set))
            {
                player.m_skillLevelupEffects.Create(player.GetHeadPoint(), player.transform.rotation, player.transform);
                RefreshArmorPanel();
            }
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

        private GameObject CreateTabButton(string label, int tabIndex, RectTransform panel, float parentWidth, float craftBtnWidth, float craftBtnHeight, float tabY)
        {
            var tabGO = Instantiate(_craftButton.gameObject, _clonedPanel.transform);
            tabGO.name = "Tab_" + label;

            // Wire up click to switch tabs, disable auto-navigation
            var btn = tabGO.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                int idx = tabIndex;
                btn.onClick.AddListener(() => SwitchTab(idx));
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
            }

            // Set the label text and strip button hints
            var txt = tabGO.GetComponentInChildren<TMP_Text>(true);
            if (txt != null)
            {
                txt.text = label;
                txt.gameObject.SetActive(true);
            }
            StripButtonHints(tabGO, txt);

            // Position: spans the full width of its panel, uniform Y for all tabs
            var tabRT = tabGO.GetComponent<RectTransform>();
            float panelLeft = GetRectLeft(panel, parentWidth);
            float panelRight = GetRectRight(panel, parentWidth);
            float panelW = panelRight - panelLeft;

            tabRT.anchorMin = new Vector2(0f, 0f);
            tabRT.anchorMax = new Vector2(0f, 0f);
            tabRT.pivot = new Vector2(0.5f, 0f);
            float cx = (panelLeft + panelRight) / 2f;
            tabRT.sizeDelta = new Vector2(panelW, craftBtnHeight);
            tabRT.anchoredPosition = new Vector2(cx, tabY);

            return tabGO;
        }

        // ══════════════════════════════════════════
        //  VALHEIM DATA LOOKUPS
        // ══════════════════════════════════════════

        /// <summary>
        /// Removes all button hint/prompt children from a cloned Valheim button.
        /// Keeps only the branch containing the label text; destroys everything else
        /// (key_bkg images, gamepad icons, extra decorations).
        /// </summary>
        private static void StripButtonHints(GameObject buttonGO, TMP_Text labelText)
        {
            for (int i = buttonGO.transform.childCount - 1; i >= 0; i--)
            {
                var child = buttonGO.transform.GetChild(i);
                // Keep the child if it IS the text or CONTAINS the text
                if (labelText != null &&
                    (child.gameObject == labelText.gameObject || labelText.transform.IsChildOf(child)))
                    continue;
                // Destroy everything else (hint images, key backgrounds, etc.)
                DestroyImmediate(child.gameObject);
            }
        }

        /// <summary>
        /// Immediately destroys all layout-constraining components on a GameObject.
        /// Uses DestroyImmediate because Destroy() is deferred and layout components
        /// would still override manual sizing during the current frame.
        /// </summary>
        private static void StripLayoutComponents(GameObject go)
        {
            foreach (var c in go.GetComponents<LayoutGroup>())
                DestroyImmediate(c);
            foreach (var c in go.GetComponents<ContentSizeFitter>())
                DestroyImmediate(c);
            foreach (var c in go.GetComponents<LayoutElement>())
                DestroyImmediate(c);
        }

        private static float GetRectLeft(RectTransform rt, float parentWidth)
        {
            return rt.anchorMin.x * parentWidth + rt.offsetMin.x;
        }

        private static float GetRectRight(RectTransform rt, float parentWidth)
        {
            return rt.anchorMax.x * parentWidth + rt.offsetMax.x;
        }

        private static float GetRectWidth(RectTransform rt, float parentWidth)
        {
            return GetRectRight(rt, parentWidth) - GetRectLeft(rt, parentWidth);
        }

        private static void SetRectWidthKeepingLeft(RectTransform rt, float parentWidth, float targetWidth)
        {
            float left = GetRectLeft(rt, parentWidth);
            float right = left + targetWidth;
            rt.offsetMax = new Vector2(right - rt.anchorMax.x * parentWidth, rt.offsetMax.y);
        }

        private static void ShiftRectX(RectTransform rt, float deltaX)
        {
            rt.offsetMin = new Vector2(rt.offsetMin.x + deltaX, rt.offsetMin.y);
            rt.offsetMax = new Vector2(rt.offsetMax.x + deltaX, rt.offsetMax.y);
        }

        private static void SetRectHorizontalEdges(RectTransform rt, float parentWidth, float left, float right)
        {
            rt.offsetMin = new Vector2(left - rt.anchorMin.x * parentWidth, rt.offsetMin.y);
            rt.offsetMax = new Vector2(right - rt.anchorMax.x * parentWidth, rt.offsetMax.y);
        }

        /// <summary>
        /// Gets the icon for a class: IconPrefab first, then preview equipment, then first item.
        /// </summary>
        private static Sprite GetClassIcon(StartingClass cls)
        {
            // 1. IconPrefab from class definition
            if (!string.IsNullOrEmpty(cls.IconPrefab))
            {
                var icon = GetItemIcon(cls.IconPrefab);
                if (icon != null) return icon;
            }

            // 2. First preview equipment item
            if (cls.PreviewEquipment != null && cls.PreviewEquipment.Count > 0)
            {
                var icon = GetItemIcon(cls.PreviewEquipment[0]);
                if (icon != null) return icon;
            }

            // 3. First starting item
            if (cls.Items.Count > 0)
                return GetItemIcon(cls.Items[0].PrefabName);

            return null;
        }

        private static Sprite GetItemIcon(string prefabName)
        {
            var prefab = ZNetScene.instance?.GetPrefab(prefabName);
            if (prefab == null) return null;
            var drop = prefab.GetComponent<ItemDrop>();
            if (drop == null || drop.m_itemData == null) return null;
            return drop.m_itemData.GetIcon();
        }

        private static string GetLocalizedName(string prefabName)
        {
            var prefab = ZNetScene.instance?.GetPrefab(prefabName);
            if (prefab != null)
            {
                var drop = prefab.GetComponent<ItemDrop>();
                if (drop != null && drop.m_itemData != null && Localization.instance != null)
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
