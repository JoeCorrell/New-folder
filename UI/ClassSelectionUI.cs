using System.Collections.Generic;
using HarmonyLib;
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
        private ScrollRect _descriptionScrollRect;
        private Scrollbar _descriptionScrollbar;

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

            SetupPreviewClone();
            _previewRotation = 0f;
            if (_previewCamera != null)
                _previewCamera.enabled = true;
            if (_previewLight != null)
                _previewLight.enabled = true;

            PopulateClassList();
            ClearDetail();

        }

        public void Close()
        {
            _isVisible = false;
            _selectedIndex = -1;
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

            // Slow automatic camera rotation.
            _previewRotation += AutoRotateSpeed * Time.deltaTime;

            // Update preview camera position
            UpdatePreviewCamera();

            // Gamepad input
            UpdateGamepadInput();
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
            panelRT.sizeDelta = new Vector2(origPanelWidth + panelAddedWidth, panelRT.sizeDelta.y);
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

            _recipeListSpace = invGui.m_recipeListSpace;

            // Find ScrollRect and ScrollRectEnsureVisible in the clone (for scrollbar + gamepad centering)
            if (_recipeListRoot != null)
            {
                _listScrollRect = _recipeListRoot.GetComponentInParent<ScrollRect>();
                _ensureVisible = _recipeListRoot.GetComponentInParent<ScrollRectEnsureVisible>();
            }

            // Find and configure description panel scroll area/scrollbar.
            if (invGui.m_recipeDecription != null)
            {
                var origDescScrollRect = invGui.m_recipeDecription.GetComponentInParent<ScrollRect>();
                if (origDescScrollRect != null)
                {
                    _descriptionScrollRect = FindCloned<ScrollRect>(origRoot, origDescScrollRect.transform);
                    if (origDescScrollRect.verticalScrollbar != null)
                        _descriptionScrollbar = FindCloned<Scrollbar>(origRoot, origDescScrollRect.verticalScrollbar.transform);
                    if (_descriptionScrollRect != null && _descriptionScrollbar == null)
                        _descriptionScrollbar = _descriptionScrollRect.verticalScrollbar;

                    if (_descriptionScrollRect != null)
                    {
                        _descriptionScrollRect.vertical = true;
                        _descriptionScrollRect.horizontal = false;
                        _descriptionScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
                    }

                    if (_descriptionScrollbar != null)
                    {
                        _descriptionScrollbar.gameObject.SetActive(true);
                        if (_descriptionScrollRect != null)
                            _descriptionScrollRect.verticalScrollbar = _descriptionScrollbar;
                    }
                }
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
                _recipeListRoot.anchoredPosition = new Vector2(_recipeListRoot.anchoredPosition.x, 0f);
            }

            // ══════════════════════════════════════════
            //  Repurpose cloned elements
            // ══════════════════════════════════════════

            // Title → "Choose Your Starting Class"
            if (_titleText != null)
                _titleText.text = "Choose Your Starting Class";

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

            // ── Player preview (camera view in the right column) ──
            CreatePreviewPanel(invGui, previewColumnWidth, contentBaseWidth);

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
            int rtW = Mathf.Max(64, Mathf.RoundToInt(columnWidth));
            int rtH = Mathf.Max(64, Mathf.RoundToInt(panelHeight));
            _previewRT = new RenderTexture(rtW, rtH, 24, RenderTextureFormat.ARGB32);

            // ── Preview Camera ──
            _previewCamGO = new GameObject("ClassPreview_Camera");
            DontDestroyOnLoad(_previewCamGO);
            _previewCamera = _previewCamGO.AddComponent<Camera>();
            _previewCamera.targetTexture = _previewRT;
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _previewCamera.fieldOfView = 35f;
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

            // ── Single directional light — clean neutral illumination like FejdStartup ──
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
                float scrollGap = 4f;
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
            foreach (var field in AccessTools.GetDeclaredFields(typeof(VisEquipment)))
            {
                if (field.FieldType == typeof(int) && field.Name.StartsWith("m_current") && field.Name.Contains("Hash"))
                    field.SetValue(visEquip, -1);
            }

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

            float rowHeight = Mathf.Max(templateHeight * 1.25f, templateHeight + 10f);
            float gap = 6f;
            float spacing = rowHeight + gap;

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
                elemRT.anchorMin = new Vector2(0f, 1f);
                elemRT.anchorMax = new Vector2(1f, 1f);
                elemRT.pivot = new Vector2(0.5f, 1f);
                elemRT.sizeDelta = new Vector2(elemRT.sizeDelta.x, rowHeight);
                elemRT.offsetMin = new Vector2(2f, elemRT.offsetMin.y);
                elemRT.offsetMax = new Vector2(-2f, elemRT.offsetMax.y);
                elemRT.anchoredPosition = new Vector2(0f, i * -spacing);

                // Style background to match the dark description panel
                var elemImg = element.GetComponent<Image>();
                if (elemImg != null && descImg != null)
                {
                    elemImg.sprite = descImg.sprite;
                    elemImg.type = descImg.type;
                    elemImg.color = descImg.color;
                }

                // Wire up button click
                var btn = element.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => SelectClass(idx));
                }

                // Class icon — use the first PreviewEquipment item's icon
                var iconTr = element.transform.Find("icon");
                if (iconTr != null)
                {
                    var iconImg = iconTr.GetComponent<Image>();
                    Sprite classIcon = null;
                    if (cls.PreviewEquipment != null && cls.PreviewEquipment.Count > 0)
                        classIcon = GetItemIcon(cls.PreviewEquipment[0]);
                    if (classIcon == null && cls.Items.Count > 0)
                        classIcon = GetItemIcon(cls.Items[0].PrefabName);

                    if (iconImg != null && classIcon != null)
                    {
                        iconImg.sprite = classIcon;
                        iconImg.color = Color.white;
                        iconTr.gameObject.SetActive(true);
                    }
                    else
                    {
                        iconTr.gameObject.SetActive(false);
                    }
                }

                // Set class name
                var nameTr = element.transform.Find("name");
                if (nameTr != null)
                {
                    var nameTxt = nameTr.GetComponent<TMP_Text>();
                    if (nameTxt != null)
                    {
                        nameTxt.text = cls.Name;
                        nameTxt.color = Color.white;
                        nameTxt.enableAutoSizing = false;
                        nameTxt.fontSize = Mathf.Max(nameTxt.fontSize, 24f);
                    }
                }

                // Hide unused children
                var durTr = element.transform.Find("Durability");
                if (durTr != null) durTr.gameObject.SetActive(false);
                var qualTr = element.transform.Find("QualityLevel");
                if (qualTr != null) qualTr.gameObject.SetActive(false);
                var selTr = element.transform.Find("selected");
                if (selTr != null) selTr.gameObject.SetActive(false);

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

        private void ClearDetail()
        {
            if (_recipeIcon != null) _recipeIcon.enabled = false;
            if (_recipeName != null) _recipeName.enabled = false;
            if (_recipeDescription != null) _recipeDescription.enabled = false;
            if (_descriptionScrollbar != null) _descriptionScrollbar.value = 1f;
            if (_descriptionScrollRect != null) _descriptionScrollRect.verticalNormalizedPosition = 1f;

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
                    desc += "\n\n<color=#66B3E5>Skill Bonuses:</color>";
                    foreach (var bonus in cls.SkillBonuses)
                    {
                        string skillName = FormatPascalCase(bonus.SkillType.ToString());
                        desc += $"\n  {skillName}  <color=#66B3E5>+{bonus.BonusLevel:0}</color>";
                    }
                }
                _recipeDescription.text = desc;
                _recipeDescription.enabled = true;
                _recipeDescription.ForceMeshUpdate();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_recipeDescription.rectTransform);
            }

            if (_descriptionScrollbar != null) _descriptionScrollbar.value = 1f;
            if (_descriptionScrollRect != null) _descriptionScrollRect.verticalNormalizedPosition = 1f;

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
    }
}
