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

        // ── Class list elements (instantiated from recipe element prefab) ──
        private readonly List<GameObject> _classElements = new List<GameObject>();

        // ── Player preview ──
        private RenderTexture _previewRT;
        private GameObject _previewCamGO;
        private Camera _previewCamera;
        private GameObject _previewClone;
        private Light _previewLight;
        private GameObject _previewPanel;
        private static readonly Vector3 PreviewSpawnPos = new Vector3(10000f, 5000f, 10000f);

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
            if (_previewRT != null) { _previewRT.Release(); Destroy(_previewRT); }
            if (_previewPanel != null) Destroy(_previewPanel);
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

            // Compute the list column width from the ScrollRect's anchors and the panel's sizeDelta.
            // rect.width is unreliable before a layout pass, but sizeDelta and anchors are immediate.
            float origPanelWidth = panelRT.sizeDelta.x;
            float listColumnWidth = 200f; // fallback
            if (invGui.m_recipeListRoot != null)
            {
                var scrollRect = invGui.m_recipeListRoot.GetComponentInParent<ScrollRect>();
                if (scrollRect != null)
                {
                    var scrollRT = scrollRect.transform as RectTransform;
                    // width = anchorSpan * parentWidth + sizeDelta.x
                    float anchorSpan = scrollRT.anchorMax.x - scrollRT.anchorMin.x;
                    listColumnWidth = anchorSpan * origPanelWidth + scrollRT.sizeDelta.x;
                    if (listColumnWidth <= 10f) listColumnWidth = 200f; // sanity check
                }
            }

            // Widen panel to the right by the list column width (left edge stays in place)
            float extraWidth = listColumnWidth;
            panelRT.sizeDelta = new Vector2(origPanelWidth + extraWidth, panelRT.sizeDelta.y);
            panelRT.anchoredPosition = new Vector2(extraWidth / 2f, 0f);

            // Children pinned to the right edge (anchorMin.x ~= 1 && anchorMax.x ~= 1)
            // ride the right edge when the panel widens — shift them back left.
            // Full-stretch backgrounds (0→1) are left alone so they cover the whole panel.
            foreach (RectTransform child in _clonedPanel.transform)
            {
                if (child.anchorMin.x >= 0.99f && child.anchorMax.x >= 0.99f)
                    child.anchoredPosition += new Vector2(-extraWidth, 0f);
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

            // Hide the gold outline frame
            var selectedFrame = _clonedPanel.transform.Find("selected_frame");
            if (selectedFrame != null)
                selectedFrame.gameObject.SetActive(false);

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

            _canvasGO.SetActive(false);
            _uiBuilt = true;
        }

        // ══════════════════════════════════════════
        //  PLAYER PREVIEW (separate panel to the right)
        // ══════════════════════════════════════════

        private void CreatePreviewPanel(InventoryGui invGui)
        {
            // ── RenderTexture ──
            _previewRT = new RenderTexture(256, 512, 24, RenderTextureFormat.ARGB32);

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
            _previewCamera.cullingMask = 1 << charLayer;

            Vector3 cloneCenter = PreviewSpawnPos + Vector3.up * 0.9f;
            _previewCamGO.transform.position = cloneCenter + Vector3.forward * 4.5f;
            _previewCamGO.transform.LookAt(cloneCenter);

            // ── Dedicated light ──
            var lightGO = new GameObject("ClassPreview_Light");
            lightGO.transform.SetParent(_previewCamGO.transform, false);
            lightGO.transform.localPosition = new Vector3(0.5f, 1f, 0f);
            lightGO.transform.localRotation = Quaternion.Euler(30f, -15f, 0f);
            _previewLight = lightGO.AddComponent<Light>();
            _previewLight.type = LightType.Directional;
            _previewLight.color = new Color(1f, 0.95f, 0.85f);
            _previewLight.intensity = 1.2f;
            _previewLight.cullingMask = 1 << charLayer;

            // ── Separate preview panel, positioned to the right of the class selection panel ──
            var panelRT = _clonedPanel.GetComponent<RectTransform>();
            float panelWidth = panelRT.sizeDelta.x;
            float panelHeight = panelRT.sizeDelta.y;
            float gap = 10f;
            float previewWidth = panelHeight * 0.4f; // portrait aspect

            // Find the actual background image (may be on the root or a child)
            Image sourceImg = null;
            var rootImg = _clonedPanel.GetComponent<Image>();
            if (rootImg != null && rootImg.sprite != null)
            {
                sourceImg = rootImg;
            }
            else
            {
                // Search direct children for the largest Image with a sprite
                foreach (Transform child in _clonedPanel.transform)
                {
                    var img = child.GetComponent<Image>();
                    if (img != null && img.sprite != null && img.gameObject.activeSelf)
                    {
                        if (sourceImg == null || img.rectTransform.rect.width * img.rectTransform.rect.height >
                            sourceImg.rectTransform.rect.width * sourceImg.rectTransform.rect.height)
                            sourceImg = img;
                    }
                }
            }

            _previewPanel = new GameObject("ClassPreview_Panel", typeof(RectTransform), typeof(Image));
            _previewPanel.transform.SetParent(_canvasGO.transform, false);

            var prevPanelRT = _previewPanel.GetComponent<RectTransform>();
            prevPanelRT.anchorMin = new Vector2(0.5f, 0.5f);
            prevPanelRT.anchorMax = new Vector2(0.5f, 0.5f);
            prevPanelRT.pivot = new Vector2(0.5f, 0.5f);
            prevPanelRT.sizeDelta = new Vector2(previewWidth, panelHeight);
            // Place to the right of the class panel, vertically centered
            float previewX = panelWidth / 2f + gap + previewWidth / 2f;
            prevPanelRT.anchoredPosition = new Vector2(previewX, 0f);

            var prevPanelImg = _previewPanel.GetComponent<Image>();
            if (sourceImg != null)
            {
                prevPanelImg.sprite = sourceImg.sprite;
                prevPanelImg.type = sourceImg.type;
                prevPanelImg.material = sourceImg.material;
                prevPanelImg.color = sourceImg.color;
                prevPanelImg.pixelsPerUnitMultiplier = sourceImg.pixelsPerUnitMultiplier;
            }
            else
            {
                prevPanelImg.color = new Color(0f, 0f, 0f, 0.8f);
            }

            // ── RawImage inside the preview panel ──
            var rawImgGO = new GameObject("PreviewImage", typeof(RectTransform));
            rawImgGO.transform.SetParent(_previewPanel.transform, false);
            var rawRT = rawImgGO.GetComponent<RectTransform>();
            rawRT.anchorMin = Vector2.zero;
            rawRT.anchorMax = Vector2.one;
            rawRT.offsetMin = new Vector2(10f, 10f);
            rawRT.offsetMax = new Vector2(-10f, -10f);

            var rawImg = rawImgGO.AddComponent<RawImage>();
            rawImg.texture = _previewRT;
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
            _previewClone = Instantiate(prefab, PreviewSpawnPos, Quaternion.identity);
            ZNetView.m_forceDisableInit = false;

            // Remove physics so the clone doesn't fall
            var rb = _previewClone.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);

            // Set animators to normal update mode (same as FejdStartup)
            foreach (var anim in _previewClone.GetComponentsInChildren<Animator>())
                anim.updateMode = AnimatorUpdateMode.Normal;

            // Copy current player's appearance into the clone
            var clonePlayer = _previewClone.GetComponent<Player>();
            if (clonePlayer != null)
            {
                // Use a temporary profile to transfer appearance data
                var tempProfile = new PlayerProfile("_preview", FileHelpers.FileSource.Local);
                tempProfile.SavePlayerData(player);
                tempProfile.LoadPlayerData(clonePlayer);

                // Force VisEquipment to create hair/beard/equipment meshes.
                // CustomUpdate() won't run because m_nview.IsValid() is false on the clone,
                // so we call UpdateVisuals() directly via reflection.
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
            // Camera position is fixed (set once in CreatePreviewPanel), no per-frame update needed
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

            float gap = 4f;
            float spacing = _recipeListSpace + gap;

            for (int i = 0; i < _classes.Count; i++)
            {
                int idx = i;
                var cls = _classes[i];

                // Use the recipe element prefab (correct sizing for the list)
                var element = Instantiate(invGui.m_recipeElementPrefab, _recipeListRoot);
                element.SetActive(true);
                element.name = "ClassElement_" + cls.Name;

                // Position with gap between each entry
                var elemRT = element.transform as RectTransform;
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

                // Hide icon (not needed)
                var iconTr = element.transform.Find("icon");
                if (iconTr != null)
                    iconTr.gameObject.SetActive(false);

                // Set class name
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
            float contentHeight = _classes.Count * spacing;
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


