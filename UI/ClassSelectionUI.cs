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
        private GameObject[] _requirementSlots;
        private Scrollbar _recipeScrollbar;
        private ScrollRect _listScrollRect;
        private ScrollRectEnsureVisible _ensureVisible;
        private ScrollRect _descriptionScrollRect;
        private Scrollbar _descriptionScrollbar;
        private int _descScrollResetFrames;

        // ── Tab buttons ──
        private GameObject _tabClasses;
        private GameObject _tabSkills;
        private GameObject _tabAbout;
        private int _activeTab; // 0 = Classes, 1 = Skills, 2 = About
        private static readonly string[] TabNames = { "Classes", "Skills", "About" };

        // ── About panel ──
        private GameObject _aboutPanel;

        // ── Skills panel ──
        private GameObject _skillsPanel;
        private TMP_Text _skillsText;

        // ── Class list elements (instantiated from recipe element prefab) ──
        private readonly List<GameObject> _classElements = new List<GameObject>();

        // ── Player preview ──
        private RenderTexture _previewRT;
        private GameObject _previewCamGO;
        private Camera _previewCamera;
        private GameObject _previewClone;
        private GameObject _previewLightGO;
        private Light _previewLight;
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
            if (_previewLight != null)
                _previewLight.enabled = true;

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
            if (_previewLight != null)
                _previewLight.enabled = false;
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
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

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
            if (_previewLightGO != null) Destroy(_previewLightGO);
            if (_previewRT != null) { _previewRT.Release(); Destroy(_previewRT); }
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
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(null);
            }
            if (ZInput.GetButtonDown("JoyLStickUp") || ZInput.GetButtonDown("JoyDPadUp"))
            {
                int prev = (_selectedIndex < 0) ? 0 : Mathf.Max(0, _selectedIndex - 1);
                SelectClass(prev);
                EnsureClassVisible(prev);
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(null);
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

            // LB / RB to switch tabs
            if (ZInput.GetButtonDown("JoyTabLeft"))
                SwitchTab(_activeTab - 1);
            if (ZInput.GetButtonDown("JoyTabRight"))
                SwitchTab(_activeTab + 1);

            // Right stick scrolls the active text panel (description / skills / about)
            ScrollRect activeScroll = null;
            if (_activeTab == 0 && _descriptionScrollRect != null)
                activeScroll = _descriptionScrollRect;
            else if (_activeTab == 1 && _skillsPanel != null)
                activeScroll = _skillsPanel.GetComponentInChildren<ScrollRect>();
            else if (_activeTab == 2 && _aboutPanel != null)
                activeScroll = _aboutPanel.GetComponentInChildren<ScrollRect>();

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

            // Widen panel to fit a third column (preview) equal to description width.
            float previewColumnWidth = descriptionColumnWidth;
            float previewPadding = 24f;
            float contentBaseWidth = origPanelWidth + listWidthIncrease;
            float panelAddedWidth = listWidthIncrease + previewColumnWidth + previewPadding;
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

            // Nudge all requirement slots left to center them above the confirm button
            if (_requirementSlots != null)
            {
                float nudge = -7f;
                foreach (var slot in _requirementSlots)
                {
                    if (slot == null) continue;
                    var rt = slot.GetComponent<RectTransform>();
                    if (rt != null)
                        rt.anchoredPosition += new Vector2(nudge, 0f);
                }
            }


            // Remove UIGroupHandler from clone to avoid input conflicts with original inventory
            foreach (var c in _clonedPanel.GetComponentsInChildren<UIGroupHandler>(true))
                Destroy(c);

            // Hide the gold outline frame and ALL gold decorative lines (including nested ones)
            var selectedFrame = _clonedPanel.transform.Find("selected_frame");
            if (selectedFrame != null)
                selectedFrame.gameObject.SetActive(false);
            foreach (Transform t in _clonedPanel.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("BraidLine"))
                    t.gameObject.SetActive(false);
            }

            // All scrollbars in the clone (recipe list + description) are wanted — no hiding needed.

            // Force the left class-list panel to match the description panel width.
            var listPanelRT = _clonedPanel.transform.Find("RecipeList") as RectTransform;
            var descPanelRT = _clonedPanel.transform.Find("Decription") as RectTransform;
            if (listPanelRT != null && descPanelRT != null)
            {
                float parentWidth = panelRT.sizeDelta.x;
                float descWidth = GetRectWidth(descPanelRT, parentWidth);
                float descLeft = GetRectLeft(descPanelRT, parentWidth);
                float gapToDescription = 4f;

                float targetRight = descLeft - gapToDescription;
                float targetLeft = targetRight - descWidth;
                float minLeft = 6f;
                if (targetLeft < minLeft)
                {
                    targetLeft = minLeft;
                    targetRight = targetLeft + descWidth;
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
                _recipeDescription.fontSize = 20f;
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
                // Keep text inside requirement slots
                bool inSlot = false;
                if (_requirementSlots != null)
                {
                    foreach (var slot in _requirementSlots)
                    {
                        if (slot != null && txt.transform.IsChildOf(slot.transform))
                        { inSlot = true; break; }
                    }
                }
                if (inSlot) continue;
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
                scrollAreaRT.offsetMin = new Vector2(8f, 145f);  // bottom inset: clears requirements + craft button with gap
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
                trackImg.color = new Color(0f, 0f, 0f, 0.3f);

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
                handleImg.color = new Color(0.83f, 0.64f, 0.31f, 0.9f); // gold to match UI

                _descriptionScrollbar = scrollbarGO.AddComponent<Scrollbar>();
                _descriptionScrollbar.handleRect = handleRT;
                _descriptionScrollbar.direction = Scrollbar.Direction.BottomToTop;
                _descriptionScrollbar.targetGraphic = handleImg;

                _descriptionScrollRect.verticalScrollbar = _descriptionScrollbar;
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

            // Push the three content panels down by tabHeight + extra nudge
            float panelNudge = 5f;
            float totalShiftDown = tabHeight + panelNudge;
            if (tabListPanel != null)
                tabListPanel.offsetMax = new Vector2(tabListPanel.offsetMax.x, tabListPanel.offsetMax.y - totalShiftDown);
            if (tabDescPanel != null)
                tabDescPanel.offsetMax = new Vector2(tabDescPanel.offsetMax.x, tabDescPanel.offsetMax.y - totalShiftDown);
            if (tabPreviewPanel != null)
                tabPreviewPanel.offsetMax = new Vector2(tabPreviewPanel.offsetMax.x, tabPreviewPanel.offsetMax.y - totalShiftDown);

            // Create tab buttons above each panel, cloning the craft button's visual style
            if (_craftButton != null && tabListPanel != null && tabDescPanel != null && tabPreviewPanel != null)
            {
                var craftRT = _craftButton.GetComponent<RectTransform>();
                float craftW = craftRT != null ? craftRT.rect.width : 140f;
                float craftH = craftRT != null ? craftRT.rect.height : 30f;
                float tabTopPad = 11f;
                float pw = panelRT.sizeDelta.x;
                float ph = panelRT.sizeDelta.y;

                _tabClasses = CreateTabButton("Classes", 0, tabListPanel, pw, ph, craftW, craftH, tabTopPad);
                _tabSkills  = CreateTabButton("Skills",  1, tabDescPanel, pw, ph, craftW, craftH, tabTopPad);
                _tabAbout   = CreateTabButton("About",   2, tabPreviewPanel, pw, ph, craftW, craftH, tabTopPad);

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
                    _aboutPanel = new GameObject("AboutPanel", typeof(RectTransform), typeof(Image));
                    _aboutPanel.transform.SetParent(_clonedPanel.transform, false);
                    var aboutRT = _aboutPanel.GetComponent<RectTransform>();
                    aboutRT.anchorMin = aboutListPanel.anchorMin;
                    aboutRT.anchorMax = new Vector2(aboutPreviewPanel.anchorMax.x, aboutListPanel.anchorMax.y);
                    aboutRT.pivot = new Vector2(0.5f, 0.5f);
                    aboutRT.offsetMin = new Vector2(leftEdge - aboutRT.anchorMin.x * pw2, aboutListPanel.offsetMin.y);
                    aboutRT.offsetMax = new Vector2(rightEdge - aboutRT.anchorMax.x * pw2, aboutListPanel.offsetMax.y);

                    var aboutImg = _aboutPanel.GetComponent<Image>();
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
                    aboutScrollGO.transform.SetParent(_aboutPanel.transform, false);
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
                    // Copy font from existing UI text so TMP can render
                    if (_recipeDescription != null)
                    {
                        aboutTxt.font = _recipeDescription.font;
                        aboutTxt.fontSharedMaterial = _recipeDescription.fontSharedMaterial;
                    }
                    aboutTxt.fontSize = 18f;
                    aboutTxt.color = Color.white;
                    aboutTxt.textWrappingMode = TextWrappingModes.Normal;
                    aboutTxt.overflowMode = TextOverflowModes.Overflow;
                    aboutTxt.richText = true;
                    aboutTxt.alignment = TextAlignmentOptions.TopLeft;
                    aboutTxt.text = GetAboutText();

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
                    aboutSBGO.transform.SetParent(_aboutPanel.transform, false);
                    var aboutSBRT = aboutSBGO.GetComponent<RectTransform>();
                    aboutSBRT.anchorMin = new Vector2(1f, 0f);
                    aboutSBRT.anchorMax = new Vector2(1f, 1f);
                    aboutSBRT.pivot = new Vector2(1f, 0.5f);
                    aboutSBRT.sizeDelta = new Vector2(aboutSBWidth, 0f);
                    aboutSBRT.offsetMin = new Vector2(-aboutSBWidth, 4f);
                    aboutSBRT.offsetMax = new Vector2(-2f, -4f);

                    var aboutTrack = aboutSBGO.AddComponent<Image>();
                    aboutTrack.color = new Color(0f, 0f, 0f, 0.3f);

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
                    aboutHandleImg.color = new Color(0.83f, 0.64f, 0.31f, 0.9f);

                    var aboutSB = aboutSBGO.AddComponent<Scrollbar>();
                    aboutSB.handleRect = aboutHandleRT;
                    aboutSB.direction = Scrollbar.Direction.BottomToTop;
                    aboutSB.targetGraphic = aboutHandleImg;

                    aboutSR.verticalScrollbar = aboutSB;
                    aboutSR.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

                    // Start hidden — Classes tab is default
                    _aboutPanel.SetActive(false);
                }
            }

            // ══════════════════════════════════════════
            //  Skills panel — spans description + preview columns (class list stays visible)
            // ══════════════════════════════════════════
            {
                var skillsListPanel = _clonedPanel.transform.Find("RecipeList") as RectTransform;
                var skillsPreviewPanel = _clonedPanel.transform.Find("PreviewContainer") as RectTransform;
                var skillsDescPanel = _clonedPanel.transform.Find("Decription") as RectTransform;
                Image skillsDescImg = skillsDescPanel != null ? skillsDescPanel.GetComponent<Image>() : null;

                if (skillsDescPanel != null && skillsPreviewPanel != null)
                {
                    float pw3 = panelRT.sizeDelta.x;
                    float leftEdge = GetRectLeft(skillsDescPanel, pw3);
                    float rightEdge = GetRectRight(skillsPreviewPanel, pw3);

                    _skillsPanel = new GameObject("SkillsPanel", typeof(RectTransform), typeof(Image));
                    _skillsPanel.transform.SetParent(_clonedPanel.transform, false);
                    var skillsRT = _skillsPanel.GetComponent<RectTransform>();
                    skillsRT.anchorMin = skillsDescPanel.anchorMin;
                    skillsRT.anchorMax = new Vector2(skillsPreviewPanel.anchorMax.x, skillsDescPanel.anchorMax.y);
                    skillsRT.pivot = new Vector2(0.5f, 0.5f);
                    skillsRT.offsetMin = new Vector2(leftEdge - skillsRT.anchorMin.x * pw3, skillsDescPanel.offsetMin.y);
                    skillsRT.offsetMax = new Vector2(rightEdge - skillsRT.anchorMax.x * pw3, skillsDescPanel.offsetMax.y);

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
                    if (_recipeDescription != null)
                    {
                        _skillsText.font = _recipeDescription.font;
                        _skillsText.fontSharedMaterial = _recipeDescription.fontSharedMaterial;
                    }
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
                    skillsTrack.color = new Color(0f, 0f, 0f, 0.3f);

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
                    skillsHandleImg.color = new Color(0.83f, 0.64f, 0.31f, 0.9f);

                    var skillsSB = skillsSBGO.AddComponent<Scrollbar>();
                    skillsSB.handleRect = skillsHandleRT;
                    skillsSB.direction = Scrollbar.Direction.BottomToTop;
                    skillsSB.targetGraphic = skillsHandleImg;

                    skillsSR.verticalScrollbar = skillsSB;
                    skillsSR.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

                    _skillsPanel.SetActive(false);
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
            _previewCamGO.AddComponent<PreviewCameraHelper>();

            int charLayer = LayerMask.NameToLayer("character");
            if (charLayer < 0) charLayer = 9;
            int charNetLayer = LayerMask.NameToLayer("character_net");
            int previewMask = (1 << charLayer);
            if (charNetLayer >= 0) previewMask |= (1 << charNetLayer);
            _previewCamera.cullingMask = previewMask;

            Vector3 cloneCenter = PreviewSpawnPos + Vector3.up * 0.85f;
            _previewCamGO.transform.position = cloneCenter + Vector3.forward * 5.0f;
            _previewCamGO.transform.LookAt(cloneCenter);

            // ── Single directional light — clean neutral illumination ──
            _previewLightGO = new GameObject("ClassPreview_Light");
            DontDestroyOnLoad(_previewLightGO);
            _previewLightGO.transform.position = PreviewSpawnPos + new Vector3(0f, 3f, 2f);
            _previewLightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            _previewLight = _previewLightGO.AddComponent<Light>();
            _previewLight.type = LightType.Directional;
            _previewLight.color = new Color(1f, 0.98f, 0.95f);
            _previewLight.intensity = 1.1f;
            _previewLight.cullingMask = previewMask;
            _previewLight.enabled = false;

            // Strip post-processing from preview camera so scene colour grading doesn't desaturate
            foreach (var mb in _previewCamGO.GetComponents<MonoBehaviour>())
            {
                if (mb is PreviewCameraHelper) continue;
                Destroy(mb);
            }

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
                float scrollGap = -1f;
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
                    if (drop == null) continue;

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
                DestroyImmediate(starBox.transform.GetChild(i).gameObject);

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
                        SelectClass(idx);
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
                        selImg.color = new Color(0.6f, 0.6f, 0.6f, 0.4f);
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
            bool showAbout = (_activeTab == 2);

            // Class list is visible on both Classes and Skills tabs
            var listPanel = _clonedPanel.transform.Find("RecipeList");
            var descPanel = _clonedPanel.transform.Find("Decription");
            var previewPanel = _clonedPanel.transform.Find("PreviewContainer");
            if (listPanel != null) listPanel.gameObject.SetActive(showClasses || showSkills);
            if (descPanel != null) descPanel.gameObject.SetActive(showClasses);
            if (previewPanel != null) previewPanel.gameObject.SetActive(showClasses);

            // Toggle the skills panel (spans description + preview columns)
            if (_skillsPanel != null) _skillsPanel.SetActive(showSkills);
            if (showSkills) RefreshSkillsPanel();

            // Toggle the about panel
            if (_aboutPanel != null) _aboutPanel.SetActive(showAbout);
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
            var sb = new System.Text.StringBuilder();

            // Calculate total points needed to unlock everything
            int totalCost = 0;
            if (cls.Abilities != null)
                foreach (var a in cls.Abilities)
                    totalCost += a.PointCost;

            // Header with class name and points display
            sb.AppendLine($"<size=24><color=#D4A24E>{cls.Name} \u2014 Skill Tree</color></size>");
            sb.AppendLine($"<size=18>Available: <color=#8AE58A>0</color>  |  Total Needed: <color=#D4A24E>{totalCost}</color></size>");
            sb.AppendLine();

            if (cls.Abilities == null || cls.Abilities.Count == 0)
            {
                sb.AppendLine("<color=#999999>No abilities defined for this class.</color>");
                _skillsText.text = sb.ToString();
                return;
            }

            string[] tierLabels = { "Passive", "Tier I", "Tier II", "Ultimate" };

            for (int i = 0; i < cls.Abilities.Count; i++)
            {
                var ability = cls.Abilities[i];
                string tierLabel = i < tierLabels.Length ? tierLabels[i] : $"Tier {i}";

                if (ability.IsPassive)
                {
                    // Passive ability — unlocked, green accents
                    sb.AppendLine("<color=#8AE58A>━━━━━━━━━━━━━━━━━━━━━━━━━━━━</color>");
                    sb.AppendLine($"  <size=20><color=#8AE58A>\u2605 {ability.Name}</color></size>  <size=15><color=#8AE58A>({tierLabel} \u2014 Unlocked)</color></size>");
                    sb.AppendLine($"  <size=17>{ability.Description}</size>");

                    // Show related skill bonuses
                    if (cls.SkillBonuses != null && cls.SkillBonuses.Count > 0)
                    {
                        sb.Append("  <size=16><color=#8AE58A>");
                        for (int j = 0; j < cls.SkillBonuses.Count; j++)
                        {
                            if (j > 0) sb.Append("  |  ");
                            string skillName = FormatPascalCase(cls.SkillBonuses[j].SkillType.ToString());
                            sb.Append($"{skillName} +{cls.SkillBonuses[j].BonusLevel:0}");
                        }
                        sb.AppendLine("</color></size>");
                    }
                    sb.AppendLine("<color=#8AE58A>━━━━━━━━━━━━━━━━━━━━━━━━━━━━</color>");
                }
                else
                {
                    // Locked ability — color-coded by tier
                    string borderColor = ability.PointCost >= 50 ? "#8B4513" : "#555555";
                    string nameColor = ability.PointCost >= 50 ? "#D4A24E" : "#999999";
                    string descColor = ability.PointCost >= 50 ? "#888888" : "#777777";

                    sb.AppendLine($"<color={borderColor}>━━━━━━━━━━━━━━━━━━━━━━━━━━━━</color>");
                    sb.AppendLine($"  <size=20><color={nameColor}>\u2726 {ability.Name}</color></size>  <size=15><color=#666666>({tierLabel} \u2014 Locked)</color></size>");
                    sb.AppendLine($"  <size=17><color={descColor}>{ability.Description}</color></size>");
                    sb.AppendLine($"  <size=16><color=#D4A24E>\u25C6 Cost: {ability.PointCost} Skill Points</color>  <color=#666666>(You have: 0)</color></size>");
                    sb.AppendLine($"<color={borderColor}>━━━━━━━━━━━━━━━━━━━━━━━━━━━━</color>");
                }

                // Connector between abilities
                if (i < cls.Abilities.Count - 1)
                {
                    sb.AppendLine("                    <size=18><color=#666666>\u2502</color></size>");
                    sb.AppendLine("                    <size=18><color=#666666>\u25BC</color></size>");
                }
            }

            _skillsText.text = sb.ToString();
            _skillsText.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_skillsText.rectTransform);
        }

        private void RefreshTabHighlights()
        {
            var tabs = new[] { _tabClasses, _tabSkills, _tabAbout };
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] == null) continue;
                var btn = tabs[i].GetComponent<Button>();
                if (btn != null)
                    btn.interactable = (i != _activeTab);
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

            // Hide icon (not used for class descriptions)
            if (_recipeIcon != null)
                _recipeIcon.enabled = false;

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
                    desc += "\n\n<color=#8AE58A>Skill Bonuses:</color>";
                    foreach (var bonus in cls.SkillBonuses)
                    {
                        string skillName = FormatPascalCase(bonus.SkillType.ToString());
                        desc += $"\n  {skillName}  <color=#8AE58A>+{bonus.BonusLevel:0}</color>";
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
            if (!_isVisible) return;
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

        private static string GetAboutText()
        {
            return
                "<size=24><color=#D4A24E>Starting Class Mod</color></size>\n\n" +

                "<size=20><color=#66B3E5>How It Works</color></size>\n" +
                "When you first enter the world, you'll choose a starting class. " +
                "Each class gives you a unique set of starting gear, skill bonuses, " +
                "and a passive ability that shapes your early adventure.\n\n" +

                "<size=20><color=#66B3E5>Starting Gear</color></size>\n" +
                "Your class determines the weapons, armour, and tools you begin with. " +
                "These items are added to your inventory when you confirm your selection. " +
                "Choosing a class that fits your playstyle means you can skip the initial " +
                "resource grind and jump straight into exploring.\n\n" +

                "<size=20><color=#66B3E5>Skill Bonuses</color></size>\n" +
                "Each class starts with <color=#8AE58A>bonus levels</color> in one or more skills. " +
                "For example, an Archer begins with bonus levels in Bows, while a " +
                "Warrior starts with bonus levels in Swords and Blocking. " +
                "These bonuses give you a head start — skills continue to level up " +
                "naturally through use, just like normal.\n\n" +

                "<size=20><color=#66B3E5>Passive Abilities</color></size>\n" +
                "Every class has a <color=#D4A24E>passive ability</color> that is always active. " +
                "These provide unique advantages: the Ranger takes less fall damage, " +
                "the Berserker gains strength at low health, and the Sailor moves " +
                "faster on boats. Passives require no activation and work automatically.\n\n" +

                "<size=20><color=#66B3E5>Locked Abilities</color></size>\n" +
                "Each class also has a <color=#999999>locked ability</color> shown in grey. " +
                "These represent advanced powers that unlock as you master your class skills. " +
                "Keep training your core skills to eventually unlock them.\n\n" +

                "<size=20><color=#66B3E5>Reselecting Your Class</color></size>\n" +
                "If you change your mind, a server admin can grant access to the " +
                "<color=#D4A24E>/reclass</color> command, which reopens the selection screen " +
                "and lets you pick a new class. Your inventory will be cleared " +
                "and replaced with the new class's starting gear.\n\n" +

                "<size=16><color=#999999>Choose wisely, Viking. Odin watches.</color></size>";
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

        private GameObject CreateTabButton(string label, int tabIndex, RectTransform panel, float parentWidth, float parentHeight, float craftBtnWidth, float craftBtnHeight, float topPad)
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

            // Hide ALL children except the one containing the label text.
            // The text may be nested (e.g. inside a container), so check IsChildOf.
            var txt = tabGO.GetComponentInChildren<TMP_Text>(true);
            foreach (Transform child in tabGO.transform)
            {
                // Keep the child if the TMP_Text lives inside it (or IS it)
                if (txt != null && (child.gameObject == txt.gameObject || txt.transform.IsChildOf(child)))
                {
                    child.gameObject.SetActive(true);
                    continue;
                }
                child.gameObject.SetActive(false);
            }
            if (txt != null)
            {
                txt.text = label;
                txt.gameObject.SetActive(true);
            }

            // Position: centered above the panel, same width/height as craft button
            var tabRT = tabGO.GetComponent<RectTransform>();
            float panelLeft = GetRectLeft(panel, parentWidth);
            float panelRight = GetRectRight(panel, parentWidth);
            float panelTop = panel.anchorMax.y * parentHeight + panel.offsetMax.y;

            tabRT.anchorMin = new Vector2(0f, 0f);
            tabRT.anchorMax = new Vector2(0f, 0f);
            tabRT.pivot = new Vector2(0.5f, 0f);
            float cx = (panelLeft + panelRight) / 2f;
            tabRT.sizeDelta = new Vector2(craftBtnWidth, craftBtnHeight);
            tabRT.anchoredPosition = new Vector2(cx, panelTop + topPad);

            return tabGO;
        }

        // ══════════════════════════════════════════
        //  VALHEIM DATA LOOKUPS
        // ══════════════════════════════════════════

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
                if (drop != null && Localization.instance != null)
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

        /// <summary>
        /// Attached to the preview camera to disable scene fog and control ambient light
        /// during rendering, preventing the washed-out look caused by Valheim's environment.
        /// </summary>
        private class PreviewCameraHelper : MonoBehaviour
        {
            private bool _savedFog;
            private float _savedAmbientIntensity;
            private Color _savedAmbientLight;

            void OnPreRender()
            {
                _savedFog = RenderSettings.fog;
                _savedAmbientIntensity = RenderSettings.ambientIntensity;
                _savedAmbientLight = RenderSettings.ambientLight;

                RenderSettings.fog = false;
                RenderSettings.ambientIntensity = 1.0f;
                RenderSettings.ambientLight = new Color(0.75f, 0.75f, 0.78f);
            }

            void OnPostRender()
            {
                RenderSettings.fog = _savedFog;
                RenderSettings.ambientIntensity = _savedAmbientIntensity;
                RenderSettings.ambientLight = _savedAmbientLight;
            }
        }
    }
}
