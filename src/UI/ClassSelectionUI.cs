using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StartingClassMod
{
    /// <summary>
    /// Class selection UI built from scratch at runtime using Valheim's visual assets
    /// (panel sprite, font, recipe element prefab, button style) extracted once on first open.
    /// Three-column layout: class list | description | player preview.
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

        // ── Extracted Valheim assets (cached once at build time) ──
        private Sprite _rootPanelSprite;      // from crafting panel root — brownish Valheim frame
        private Image.Type _rootPanelSpriteType;
        private Color _rootPanelSpriteColor;
        private Sprite _bgSprite; // PanelBackground.png — main panel background
        private GameObject _recipeElementPrefab;
        private GameObject _buttonTemplate;
        private float _scrollSensitivity = 40f;
        private TMP_FontAsset _valheimFont;

        // ── Panel structure ──
        private GameObject _mainPanel;
        private RectTransform _leftColumn;
        private RectTransform _middleColumn;
        private RectTransform _rightColumn;
        // ── UI element references ──
        private TMP_Text _recipeName;
        private TMP_Text _recipeDescription;
        private RectTransform _recipeListRoot;
        private Button _craftButton;
        private TMP_Text _craftButtonLabel;
        private Scrollbar _recipeScrollbar;
        private ScrollRect _listScrollRect;
        private ScrollRect _descriptionScrollRect;
        private int _descScrollResetFrames;

        // ── Tab buttons ──
        private GameObject _tabClasses;
        private GameObject _tabSkills;
        private GameObject _tabArmor;
        private int _activeTab; // 0 = Classes, 1 = Skills, 2 = Armor
        private static readonly string[] TabNames = { "Classes", "Skills", "Armor" };

        // ── Armor tab state ──
        private int _armorSelectedSlot;

        // ── Skills panel ──
        private GameObject _skillsPanel;
        private TMP_Text _skillsText;
        private ScrollRect _skillsScrollRect;
        private Button _unlockButton;
        private TMP_Text _unlockButtonLabel;

        // ── Equip button (Skills tab) ──
        private Button _equipButton;
        private TMP_Text _equipButtonLabel;

        // ── Panel focus for gamepad (Skills tab: 0=left class list, 1=middle skill text) ──
        private int _panelFocus;

        // ── Class list elements (instantiated from recipe element prefab) ──
        private readonly List<GameObject> _classElements = new List<GameObject>();

        // ── Player preview ──
        private RenderTexture _previewRT;
        private GameObject _previewCamGO;
        private Camera _previewCamera;
        private GameObject _previewClone;
        private GameObject _previewLightRig;
        private static readonly Vector3 PreviewSpawnPos = new Vector3(10000f, 5000f, 10000f);

        // ── Ambient override for preview render pass ──
        private Color _savedAmbientColor;
        private float _savedAmbientIntensity;
        private UnityEngine.Rendering.AmbientMode _savedAmbientMode;

        // ── Preview rotation ──
        private float _previewRotation;
        private const float AutoRotateSpeed = 12f;

        // ── Colors ──
        static readonly Color ColOverlay = new Color(0f, 0f, 0f, 0.65f);

        // ── Layout — computed from Valheim's crafting panel at runtime ──
        private const float ColGap = 4f;
        private const float TabTopGap = 6f;
        private const float ExtraMiddleWidth = 80f;
        private const float OuterPad = 6f;

        // Computed at ExtractAssets time
        private float _panelWidth;
        private float _panelHeight;
        private float _leftColWidth;
        private float _midColWidth;
        private float _rightColWidth;
        private float _leftPad;
        private float _bottomPad;
        private float _colTopInset;
        private float _tabBtnHeight;
        private float _craftBtnHeight;

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
            {
                _previewCamera.enabled = true;
                Camera.onPreRender += OnPreRenderPreview;
                Camera.onPostRender += OnPostRenderPreview;
            }

            // If the player already has a class, highlight it in the list
            var localPlayer = Player.m_localPlayer;
            if (localPlayer != null)
            {
                string existingClass = ClassPersistence.GetSelectedClassName(localPlayer);
                if (!string.IsNullOrEmpty(existingClass))
                {
                    for (int i = 0; i < _classes.Count; i++)
                    {
                        if (_classes[i].Name == existingClass) { _selectedIndex = i; break; }
                    }
                }
            }

            PopulateClassList();

            int restoreIndex = (_selectedIndex >= 0 && _selectedIndex < _classes.Count)
                ? _selectedIndex
                : 0;
            SelectClass(restoreIndex);
        }

        public void Close()
        {
            _isVisible = false;
            Camera.onPreRender -= OnPreRenderPreview;
            Camera.onPostRender -= OnPostRenderPreview;
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

            if (!ZInput.IsGamepadActive())
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.visible = false;
            }

            if (_isFromCommand && Input.GetKeyDown(KeyCode.Escape))
                Close();

            if (Input.GetKeyDown(KeyCode.Q))
                SwitchTab(_activeTab - 1);
            if (Input.GetKeyDown(KeyCode.E))
                SwitchTab(_activeTab + 1);

            _previewRotation = (_previewRotation + AutoRotateSpeed * Time.deltaTime) % 360f;
            UpdatePreviewCamera();
            UpdateGamepadInput();
        }

        private void LateUpdate()
        {
            if (!_isVisible) return;
            var hud = Hud.instance;
            if (hud != null && hud.m_crosshair != null)
                hud.m_crosshair.color = Color.clear;

            if (_descScrollResetFrames > 0 && _descriptionScrollRect != null)
            {
                _descriptionScrollRect.verticalNormalizedPosition = 1f;
                _descriptionScrollRect.velocity = Vector2.zero;
                _descScrollResetFrames--;
            }

            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void OnDestroy()
        {
            Camera.onPreRender -= OnPreRenderPreview;
            Camera.onPostRender -= OnPostRenderPreview;
            if (_previewCamera != null) _previewCamera.enabled = false;
            ClearPreviewClone();
            if (_previewCamGO != null) Destroy(_previewCamGO);
            if (_previewRT != null) { _previewRT.Release(); Destroy(_previewRT); }
            if (_buttonTemplate != null) Destroy(_buttonTemplate);
            if (_canvasGO != null) Destroy(_canvasGO);
        }

        // ══════════════════════════════════════════
        //  GAMEPAD / CONTROLLER INPUT
        // ══════════════════════════════════════════

        private void UpdateGamepadInput()
        {
            if (_classes == null || _classes.Count == 0) return;

            if (ZInput.GetButtonDown("JoyTabLeft"))
                SwitchTab(_activeTab - 1);
            if (ZInput.GetButtonDown("JoyTabRight"))
                SwitchTab(_activeTab + 1);

            if (ZInput.GetButtonDown("JoyButtonB"))
            {
                if (_isFromCommand)
                    Close();
            }

            if (_activeTab == 1)
            {
                if (ZInput.GetButtonDown("JoyLStickLeft") || ZInput.GetButtonDown("JoyDPadLeft"))
                {
                    if (_panelFocus > 0)
                    {
                        _panelFocus--;
                        if (EventSystem.current != null)
                            EventSystem.current.SetSelectedGameObject(null);
                    }
                }
                if (ZInput.GetButtonDown("JoyLStickRight") || ZInput.GetButtonDown("JoyDPadRight"))
                {
                    if (_panelFocus < 1)
                    {
                        _panelFocus++;
                        if (EventSystem.current != null)
                            EventSystem.current.SetSelectedGameObject(null);
                    }
                }
            }

            if (ZInput.GetButtonDown("JoyLStickDown") || ZInput.GetButtonDown("JoyDPadDown"))
            {
                if (_activeTab == 1 && _panelFocus == 1)
                {
                    if (_skillsScrollRect != null)
                    {
                        _skillsScrollRect.verticalNormalizedPosition -= 0.1f;
                        _skillsScrollRect.verticalNormalizedPosition = Mathf.Clamp01(_skillsScrollRect.verticalNormalizedPosition);
                    }
                }
                else if (_activeTab == 2)
                {
                    var sets = ArmorUpgradeSystem.GetAllSets();
                    _armorSelectedSlot = Mathf.Min(sets.Length - 1, _armorSelectedSlot + 1);
                    RefreshArmorPanel();
                }
                else
                {
                    int next = (_selectedIndex < 0) ? 0 : Mathf.Min(_classes.Count - 1, _selectedIndex + 1);
                    SelectClass(next);
                    EnsureClassVisible(next);
                }
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(null);
            }
            if (ZInput.GetButtonDown("JoyLStickUp") || ZInput.GetButtonDown("JoyDPadUp"))
            {
                if (_activeTab == 1 && _panelFocus == 1)
                {
                    if (_skillsScrollRect != null)
                    {
                        _skillsScrollRect.verticalNormalizedPosition += 0.1f;
                        _skillsScrollRect.verticalNormalizedPosition = Mathf.Clamp01(_skillsScrollRect.verticalNormalizedPosition);
                    }
                }
                else if (_activeTab == 2)
                {
                    _armorSelectedSlot = Mathf.Max(0, _armorSelectedSlot - 1);
                    RefreshArmorPanel();
                }
                else
                {
                    int prev = (_selectedIndex < 0) ? 0 : Mathf.Max(0, _selectedIndex - 1);
                    SelectClass(prev);
                    EnsureClassVisible(prev);
                }
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(null);
            }

            if (ZInput.GetButtonDown("JoyButtonA"))
            {
                if (_activeTab == 1)
                {
                    if (_panelFocus == 0)
                    {
                        if (_unlockButton != null && _unlockButton.interactable)
                            OnUnlockButtonClicked();
                    }
                    else if (_panelFocus == 1)
                    {
                        if (_equipButton != null && _equipButton.interactable)
                            OnEquipButtonClicked();
                    }
                }
                else if (_activeTab == 2)
                {
                    if (_craftButton != null && _craftButton.interactable)
                        OnUpgradeArmorClicked();
                }
                else
                {
                    if (_selectedIndex >= 0 && _selectedIndex < _classes.Count)
                        ConfirmSelection();
                }
            }

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
            if (_recipeScrollbar == null || _classes.Count <= 1) return;
            float normalized = 1f - ((float)index / (_classes.Count - 1));
            _recipeScrollbar.value = Mathf.Clamp01(normalized);
        }

        // ══════════════════════════════════════════
        //  UI CONSTRUCTION — standalone panel using extracted Valheim assets
        // ══════════════════════════════════════════

        private void BuildUI()
        {
            // ── Extract assets from Valheim's existing UI ──
            if (!ExtractAssets())
            {
                StartingClassPlugin.LogError("Cannot build class selection UI: failed to extract Valheim assets.");
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
            overlay.AddComponent<Image>().color = ColOverlay;

            // ── Main panel ──
            _mainPanel = new GameObject("MainPanel", typeof(RectTransform), typeof(Image));
            _mainPanel.transform.SetParent(_canvasGO.transform, false);
            var mainRT = _mainPanel.GetComponent<RectTransform>();
            mainRT.anchorMin = new Vector2(0.5f, 0.5f);
            mainRT.anchorMax = new Vector2(0.5f, 0.5f);
            mainRT.pivot = new Vector2(0.5f, 0.5f);
            mainRT.sizeDelta = new Vector2(_panelWidth, _panelHeight);
            mainRT.anchoredPosition = Vector2.zero;
            var mainImg = _mainPanel.GetComponent<Image>();
            if (_bgSprite != null)
            {
                mainImg.sprite = _bgSprite;
                mainImg.type = Image.Type.Simple;
                mainImg.preserveAspect = false;
                mainImg.color = Color.white;
            }
            else
            {
                mainImg.sprite = null;
                mainImg.color = new Color(0.18f, 0.14f, 0.09f, 0.92f);
            }

            // ── Compute column X positions from extracted dimensions ──
            float midColX = _leftPad + _leftColWidth + ColGap;
            float rightColX = midColX + _midColWidth + ColGap;

            // ── Three columns ──
            _leftColumn = CreateColumn("LeftColumn", _leftPad, _leftPad + _leftColWidth);
            _middleColumn = CreateColumn("MiddleColumn", midColX, midColX + _midColWidth);
            _rightColumn = CreateColumn("RightColumn", rightColX, rightColX + _rightColWidth);

            // ── Left column: scrollable class list ──
            BuildClassListArea();

            // ── Middle column: class name header + description scroll + craft button ──
            BuildDescriptionColumn();

            // ── Right column: player preview ──
            BuildPreviewColumn();

            // ── Tab buttons ──
            float leftCenter = _leftPad + _leftColWidth / 2f;
            float midCenter = midColX + _midColWidth / 2f;
            float rightCenter = rightColX + _rightColWidth / 2f;
            _tabClasses = CreateTabButton("Classes", 0, leftCenter, _leftColWidth, TabTopGap);
            _tabSkills = CreateTabButton("Skills", 1, midCenter, _midColWidth, TabTopGap);
            _tabArmor = CreateTabButton("Armor", 2, rightCenter, _rightColWidth, TabTopGap);
            _activeTab = 0;
            RefreshTabHighlights();

            // ── Skills panel overlay (matches middle column) ──
            BuildSkillsPanel();

            _canvasGO.SetActive(false);
            _uiBuilt = true;
        }

        /// <summary>
        /// One-time extraction of visual assets AND dimensions from Valheim's existing UI.
        /// After this, we never touch the crafting panel again.
        /// </summary>
        private bool ExtractAssets()
        {
            var invGui = InventoryGui.instance;
            if (invGui == null || invGui.m_crafting == null) return false;

            // ── Visual assets ──

            // Panel background sprite — walk up the hierarchy from m_crafting
            // to find the nearest ancestor Image with a sprite (the actual Valheim panel frame)
            {
                Transform tr = invGui.m_crafting.transform;
                while (tr != null)
                {
                    var img = tr.GetComponent<Image>();
                    if (img != null && img.sprite != null)
                    {
                        _rootPanelSprite = img.sprite;
                        _rootPanelSpriteType = img.type;
                        _rootPanelSpriteColor = img.color;
                        break;
                    }
                    tr = tr.parent;
                }
            }

            var descPanelTr = invGui.m_crafting.transform.Find("Decription");

            // Custom panel background from embedded resource
            var bgTex = TextureLoader.LoadUITexture("PanelBackground");
            if (bgTex != null)
                _bgSprite = Sprite.Create(bgTex, new Rect(0, 0, bgTex.width, bgTex.height), new Vector2(0.5f, 0.5f));

            // Recipe element prefab (for list items)
            _recipeElementPrefab = invGui.m_recipeElementPrefab;
            if (_recipeElementPrefab == null) return false;

            // Button template — read height from ACTIVE button BEFORE cloning
            _craftBtnHeight = 30f;
            if (invGui.m_craftButton != null)
            {
                var origBtnRT = invGui.m_craftButton.GetComponent<RectTransform>();
                if (origBtnRT != null)
                    _craftBtnHeight = Mathf.Max(origBtnRT.rect.height, 30f);

                _buttonTemplate = Instantiate(invGui.m_craftButton.gameObject);
                _buttonTemplate.name = "ButtonTemplate";
                _buttonTemplate.SetActive(false);
                DontDestroyOnLoad(_buttonTemplate);
            }

            // Scroll sensitivity
            if (invGui.m_recipeListRoot != null)
            {
                var sr = invGui.m_recipeListRoot.GetComponentInParent<ScrollRect>();
                if (sr != null) _scrollSensitivity = sr.scrollSensitivity;
            }

            // Font
            _valheimFont = FindValheimFont();

            // ── Dimensions — read from the crafting panel to match Valheim's layout ──

            // Get the crafting panel's actual size by temporarily cloning and centering anchors
            var craftRT = invGui.m_crafting.GetComponent<RectTransform>();
            float origPanelWidth = craftRT.rect.width;
            float origPanelHeight = craftRT.rect.height;

            // Fallback: if rect returns 0 (layout not yet computed), estimate from sizeDelta
            if (origPanelWidth <= 10f)
            {
                float anchorSpanX = craftRT.anchorMax.x - craftRT.anchorMin.x;
                if (anchorSpanX < 0.01f)
                    origPanelWidth = Mathf.Abs(craftRT.sizeDelta.x);
                else
                    origPanelWidth = 567f; // reasonable fallback for Valheim crafting panel
            }
            if (origPanelHeight <= 10f)
            {
                float anchorSpanY = craftRT.anchorMax.y - craftRT.anchorMin.y;
                if (anchorSpanY < 0.01f)
                    origPanelHeight = Mathf.Abs(craftRT.sizeDelta.y);
                else
                    origPanelHeight = 480f;
            }

            // Read description column width
            float descColWidth = 260f;
            if (descPanelTr != null)
            {
                var descRT = descPanelTr as RectTransform;
                if (descRT != null)
                {
                    float w = descRT.rect.width;
                    if (w <= 10f)
                    {
                        float span = descRT.anchorMax.x - descRT.anchorMin.x;
                        w = span * origPanelWidth + descRT.sizeDelta.x;
                    }
                    if (w > 10f) descColWidth = w;
                }
            }

            // Read list column width
            float listColWidth = 200f;
            var listPanelTr = invGui.m_crafting.transform.Find("RecipeList");
            if (listPanelTr != null)
            {
                var listRT = listPanelTr as RectTransform;
                if (listRT != null)
                {
                    float w = listRT.rect.width;
                    if (w <= 10f)
                    {
                        float span = listRT.anchorMax.x - listRT.anchorMin.x;
                        w = span * origPanelWidth + listRT.sizeDelta.x;
                    }
                    if (w > 10f) listColWidth = w;
                }
            }

            // Column widths
            _leftColWidth = descColWidth;   // list widened to match description
            _midColWidth = descColWidth + ExtraMiddleWidth;  // description + extra
            _rightColWidth = descColWidth;   // preview = same as description

            // Compute panel size directly from content + fixed padding
            float totalContentWidth = _leftColWidth + ColGap + _midColWidth + ColGap + _rightColWidth;
            _leftPad = OuterPad;
            _panelWidth = totalContentWidth + OuterPad * 2f;
            _panelHeight = origPanelHeight;

            // Vertical insets
            _bottomPad = OuterPad;

            _tabBtnHeight = Mathf.Max(_craftBtnHeight, 30f);
            _colTopInset = TabTopGap + _tabBtnHeight + 6f;

            StartingClassPlugin.Log($"[ClassSelectionUI] Panel: {_panelWidth:F0}x{_panelHeight:F0}, Cols: {_leftColWidth:F0}/{_midColWidth:F0}/{_rightColWidth:F0}, Pad: {_leftPad:F0}");
            return true;
        }

        /// <summary>Create a column panel inside _mainPanel at the specified X range.</summary>
        private RectTransform CreateColumn(string name, float xLeft, float xRight)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_mainPanel.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(xLeft, _bottomPad);
            rt.offsetMax = new Vector2(xRight, -_colTopInset);
            ApplyPanelStyle(go.GetComponent<Image>());
            return rt;
        }

        /// <summary>Build the scrollable class list inside the left column.</summary>
        private void BuildClassListArea()
        {
            // Scroll viewport
            var scrollGO = new GameObject("ClassListScroll", typeof(RectTransform), typeof(Image), typeof(Mask));
            scrollGO.transform.SetParent(_leftColumn, false);
            var scrollRT = scrollGO.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(2f, 2f);
            scrollRT.offsetMax = new Vector2(-2f, -2f);
            scrollGO.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            scrollGO.GetComponent<Mask>().showMaskGraphic = false;

            // Content root for list items
            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(scrollGO.transform, false);
            _recipeListRoot = contentGO.GetComponent<RectTransform>();
            _recipeListRoot.anchorMin = new Vector2(0f, 1f);
            _recipeListRoot.anchorMax = new Vector2(1f, 1f);
            _recipeListRoot.pivot = new Vector2(0.5f, 1f);
            _recipeListRoot.anchoredPosition = Vector2.zero;
            _recipeListRoot.sizeDelta = Vector2.zero;

            // Hidden scrollbar
            _recipeScrollbar = CreateHiddenScrollbar(_leftColumn);

            // ScrollRect
            _listScrollRect = scrollGO.AddComponent<ScrollRect>();
            _listScrollRect.content = _recipeListRoot;
            _listScrollRect.viewport = scrollRT;
            _listScrollRect.vertical = true;
            _listScrollRect.horizontal = false;
            _listScrollRect.movementType = ScrollRect.MovementType.Clamped;
            _listScrollRect.scrollSensitivity = _scrollSensitivity;
            _listScrollRect.verticalScrollbar = _recipeScrollbar;
            _listScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        /// <summary>Build the description column: name header, scrollable text, action button.</summary>
        private void BuildDescriptionColumn()
        {
            float scrollbarWidth = 10f;
            float btnH = _craftBtnHeight;

            // ── Class name header at top ──
            var nameGO = new GameObject("ClassName", typeof(RectTransform));
            nameGO.transform.SetParent(_middleColumn, false);
            _recipeName = nameGO.AddComponent<TextMeshProUGUI>();
            if (_valheimFont != null) _recipeName.font = _valheimFont;
            _recipeName.fontSize = 24f;
            _recipeName.color = Color.white;
            _recipeName.alignment = TextAlignmentOptions.Center;
            _recipeName.enableAutoSizing = false;
            _recipeName.text = "";

            var nameRT = nameGO.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0f, 1f);
            nameRT.anchorMax = new Vector2(1f, 1f);
            nameRT.pivot = new Vector2(0.5f, 1f);
            nameRT.sizeDelta = new Vector2(-20f, 32f);
            nameRT.anchoredPosition = new Vector2(0f, -4f);

            // ── Action button at bottom ──
            if (_buttonTemplate != null)
            {
                var craftGO = Instantiate(_buttonTemplate, _middleColumn);
                craftGO.name = "CraftButton";
                craftGO.SetActive(true);

                _craftButton = craftGO.GetComponent<Button>();
                if (_craftButton != null)
                {
                    _craftButton.onClick.RemoveAllListeners();
                    _craftButton.onClick.AddListener(ConfirmSelection);
                    _craftButton.interactable = false;
                    _craftButton.navigation = new Navigation { mode = Navigation.Mode.None };
                }

                _craftButtonLabel = craftGO.GetComponentInChildren<TMP_Text>(true);
                if (_craftButtonLabel != null)
                {
                    _craftButtonLabel.gameObject.SetActive(true);
                    _craftButtonLabel.text = "Select a Class";
                }
                StripButtonHints(craftGO, _craftButtonLabel);

                var craftRT = craftGO.GetComponent<RectTransform>();
                craftRT.anchorMin = new Vector2(0f, 0f);
                craftRT.anchorMax = new Vector2(1f, 0f);
                craftRT.pivot = new Vector2(0.5f, 0f);
                craftRT.sizeDelta = new Vector2(-24f, btnH);
                craftRT.anchoredPosition = new Vector2(0f, 8f);
            }

            // ── Scrollable description area (between header and button) ──
            var descScrollGO = new GameObject("DescScrollArea", typeof(RectTransform), typeof(Image), typeof(Mask));
            descScrollGO.transform.SetParent(_middleColumn, false);
            var descScrollRT = descScrollGO.GetComponent<RectTransform>();
            descScrollRT.anchorMin = Vector2.zero;
            descScrollRT.anchorMax = Vector2.one;
            descScrollRT.offsetMin = new Vector2(8f, btnH + 16f);
            descScrollRT.offsetMax = new Vector2(-scrollbarWidth - 4f, -38f);
            descScrollGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.003f);
            descScrollGO.GetComponent<Mask>().showMaskGraphic = false;

            // Description text (IS the scroll content)
            var descTextGO = new GameObject("DescText", typeof(RectTransform));
            descTextGO.transform.SetParent(descScrollGO.transform, false);
            _recipeDescription = descTextGO.AddComponent<TextMeshProUGUI>();
            if (_valheimFont != null) _recipeDescription.font = _valheimFont;
            _recipeDescription.fontSize = 18f;
            _recipeDescription.color = Color.white;
            _recipeDescription.alignment = TextAlignmentOptions.TopLeft;
            _recipeDescription.textWrappingMode = TextWrappingModes.Normal;
            _recipeDescription.overflowMode = TextOverflowModes.Overflow;
            _recipeDescription.richText = true;
            _recipeDescription.enableAutoSizing = false;
            _recipeDescription.text = "";

            var descTextRT = descTextGO.GetComponent<RectTransform>();
            descTextRT.anchorMin = new Vector2(0f, 1f);
            descTextRT.anchorMax = new Vector2(1f, 1f);
            descTextRT.pivot = new Vector2(0.5f, 1f);
            descTextRT.anchoredPosition = Vector2.zero;
            descTextRT.sizeDelta = Vector2.zero;

            descTextGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Scrollbar
            var descScrollbar = CreateHiddenScrollbar(_middleColumn);

            // ScrollRect
            _descriptionScrollRect = descScrollGO.AddComponent<ScrollRect>();
            _descriptionScrollRect.content = descTextRT;
            _descriptionScrollRect.viewport = descScrollRT;
            _descriptionScrollRect.vertical = true;
            _descriptionScrollRect.horizontal = false;
            _descriptionScrollRect.movementType = ScrollRect.MovementType.Clamped;
            _descriptionScrollRect.scrollSensitivity = _scrollSensitivity * 8f;
            _descriptionScrollRect.verticalScrollbar = descScrollbar;
            _descriptionScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        /// <summary>Build the player preview (camera + RawImage) inside the right column.</summary>
        private void BuildPreviewColumn()
        {
            float columnWidth = _rightColWidth;
            float columnHeight = _panelHeight - _colTopInset - _bottomPad;
            int rtScale = 4;
            int rtW = Mathf.Max(64, Mathf.RoundToInt(columnWidth) * rtScale);
            int rtH = Mathf.Max(64, Mathf.RoundToInt(columnHeight) * rtScale);
            _previewRT = new RenderTexture(rtW, rtH, 24, RenderTextureFormat.ARGB32);
            _previewRT.antiAliasing = 4;
            _previewRT.filterMode = FilterMode.Trilinear;

            // Preview Camera
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

            Vector3 cloneCenter = PreviewSpawnPos + Vector3.up * 0.9f;
            _previewCamGO.transform.position = cloneCenter + new Vector3(0f, 0.3f, 5.0f);
            _previewCamGO.transform.LookAt(cloneCenter);

            // RawImage inside the right column
            var rawImgGO = new GameObject("PreviewImage", typeof(RectTransform));
            rawImgGO.transform.SetParent(_rightColumn, false);
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

        /// <summary>Build the skills panel overlay (matches middle column position).</summary>
        private void BuildSkillsPanel()
        {
            _skillsPanel = new GameObject("SkillsPanel", typeof(RectTransform), typeof(Image));
            _skillsPanel.transform.SetParent(_mainPanel.transform, false);
            CopyRectPosition(_middleColumn, _skillsPanel.GetComponent<RectTransform>());
            ApplyPanelStyle(_skillsPanel.GetComponent<Image>());

            float scrollbarWidth = 10f;

            // Scroll area
            var skillsScrollGO = new GameObject("SkillsScrollArea", typeof(RectTransform), typeof(Image), typeof(Mask));
            skillsScrollGO.transform.SetParent(_skillsPanel.transform, false);
            var skillsScrollRT = skillsScrollGO.GetComponent<RectTransform>();
            skillsScrollRT.anchorMin = Vector2.zero;
            skillsScrollRT.anchorMax = Vector2.one;
            skillsScrollRT.offsetMin = new Vector2(12f, 8f);
            skillsScrollRT.offsetMax = new Vector2(-scrollbarWidth - 6f, -8f);
            skillsScrollGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.003f);
            skillsScrollGO.GetComponent<Mask>().showMaskGraphic = false;

            // Skills text content
            var skillsTextGO = new GameObject("SkillsText", typeof(RectTransform));
            skillsTextGO.transform.SetParent(skillsScrollGO.transform, false);
            var skillsTextRT = skillsTextGO.GetComponent<RectTransform>();
            skillsTextRT.anchorMin = new Vector2(0f, 1f);
            skillsTextRT.anchorMax = new Vector2(1f, 1f);
            skillsTextRT.pivot = new Vector2(0.5f, 1f);
            skillsTextRT.anchoredPosition = Vector2.zero;
            skillsTextRT.sizeDelta = Vector2.zero;

            _skillsText = skillsTextGO.AddComponent<TextMeshProUGUI>();
            if (_valheimFont != null) _skillsText.font = _valheimFont;
            _skillsText.fontSize = 18f;
            _skillsText.color = Color.white;
            _skillsText.textWrappingMode = TextWrappingModes.Normal;
            _skillsText.overflowMode = TextOverflowModes.Overflow;
            _skillsText.richText = true;
            _skillsText.alignment = TextAlignmentOptions.TopLeft;

            skillsTextGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var skillsScrollbar = CreateHiddenScrollbar(_skillsPanel.transform);

            _skillsScrollRect = skillsScrollGO.AddComponent<ScrollRect>();
            _skillsScrollRect.content = skillsTextRT;
            _skillsScrollRect.viewport = skillsScrollRT;
            _skillsScrollRect.vertical = true;
            _skillsScrollRect.horizontal = false;
            _skillsScrollRect.movementType = ScrollRect.MovementType.Clamped;
            _skillsScrollRect.scrollSensitivity = _scrollSensitivity * 8f;
            _skillsScrollRect.verticalScrollbar = skillsScrollbar;
            _skillsScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            // Unlock Skill button at bottom
            if (_buttonTemplate != null)
            {
                var unlockGO = Instantiate(_buttonTemplate, _skillsPanel.transform);
                unlockGO.name = "UnlockSkillButton";
                unlockGO.SetActive(true);

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
                unlockRT.anchorMin = new Vector2(0f, 0f);
                unlockRT.anchorMax = new Vector2(1f, 0f);
                unlockRT.pivot = new Vector2(0.5f, 0f);
                unlockRT.sizeDelta = new Vector2(-24f, _craftBtnHeight);
                unlockRT.anchoredPosition = new Vector2(0f, 8f);

                // Raise scroll area bottom to clear the buttons
                skillsScrollRT.offsetMin = new Vector2(12f, (_craftBtnHeight + 12f) * 2f);
            }

            // Equip Skill button above unlock button
            if (_buttonTemplate != null)
            {
                var equipGO = Instantiate(_buttonTemplate, _skillsPanel.transform);
                equipGO.name = "EquipSkillButton";
                equipGO.SetActive(true);

                _equipButton = equipGO.GetComponent<Button>();
                if (_equipButton != null)
                {
                    _equipButton.onClick.RemoveAllListeners();
                    _equipButton.onClick.AddListener(OnEquipButtonClicked);
                    _equipButton.navigation = new Navigation { mode = Navigation.Mode.None };
                    _equipButton.interactable = false;
                }

                _equipButtonLabel = equipGO.GetComponentInChildren<TMP_Text>(true);
                if (_equipButtonLabel != null)
                {
                    _equipButtonLabel.gameObject.SetActive(true);
                    _equipButtonLabel.text = "Equip Skill";
                }
                StripButtonHints(equipGO, _equipButtonLabel);

                var equipRT = equipGO.GetComponent<RectTransform>();
                equipRT.anchorMin = new Vector2(0f, 0f);
                equipRT.anchorMax = new Vector2(1f, 0f);
                equipRT.pivot = new Vector2(0.5f, 0f);
                equipRT.sizeDelta = new Vector2(-24f, _craftBtnHeight);
                equipRT.anchoredPosition = new Vector2(0f, _craftBtnHeight + 16f);
            }

            _skillsPanel.SetActive(false);
        }

        // ══════════════════════════════════════════
        //  UI CONSTRUCTION HELPERS
        // ══════════════════════════════════════════

        /// <summary>Semi-transparent overlay for column panels — lets the main background show through.</summary>
        private void ApplyPanelStyle(Image img)
        {
            if (img == null) return;
            img.sprite = null;
            img.color = new Color(0.22f, 0.10f, 0.04f, 0.65f); // dark brown, slightly transparent
        }

        /// <summary>Apply a subtle style to list entries (class items, power entries, armor sets).</summary>
        private void ApplyEntryStyle(Image img)
        {
            if (img == null) return;
            img.color = new Color(0f, 0f, 0f, 0.25f);
        }

        /// <summary>Copy anchor/offset/pivot from one RectTransform to another.</summary>
        private static void CopyRectPosition(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.offsetMin = source.offsetMin;
            target.offsetMax = source.offsetMax;
        }

        /// <summary>Create a hidden scrollbar (invisible but functional for ScrollRect).</summary>
        private Scrollbar CreateHiddenScrollbar(Transform parent)
        {
            float sbWidth = 10f;
            var sbGO = new GameObject("Scrollbar", typeof(RectTransform));
            sbGO.transform.SetParent(parent, false);
            var sbRT = sbGO.GetComponent<RectTransform>();
            sbRT.anchorMin = new Vector2(1f, 0f);
            sbRT.anchorMax = new Vector2(1f, 1f);
            sbRT.pivot = new Vector2(1f, 0.5f);
            sbRT.sizeDelta = new Vector2(sbWidth, 0f);
            sbRT.offsetMin = new Vector2(-sbWidth, 4f);
            sbRT.offsetMax = new Vector2(-2f, -4f);
            sbGO.AddComponent<Image>().color = Color.clear;

            var slidingGO = new GameObject("Sliding Area", typeof(RectTransform));
            slidingGO.transform.SetParent(sbGO.transform, false);
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
            handleGO.GetComponent<Image>().color = Color.clear;

            var sb = sbGO.AddComponent<Scrollbar>();
            sb.handleRect = handleRT;
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.targetGraphic = handleGO.GetComponent<Image>();
            return sb;
        }

        private GameObject CreateTabButton(string label, int tabIndex, float centerX, float width, float topGap)
        {
            var go = Instantiate(_buttonTemplate, _mainPanel.transform);
            go.name = "Tab_" + label;
            go.SetActive(true);

            var btn = go.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                int idx = tabIndex;
                btn.onClick.AddListener(() => SwitchTab(idx));
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
            }

            var txt = go.GetComponentInChildren<TMP_Text>(true);
            if (txt != null)
            {
                txt.text = label;
                txt.gameObject.SetActive(true);
            }
            StripButtonHints(go, txt);

            var tabRT = go.GetComponent<RectTransform>();
            tabRT.anchorMin = new Vector2(0f, 1f);
            tabRT.anchorMax = new Vector2(0f, 1f);
            tabRT.pivot = new Vector2(0.5f, 1f);
            tabRT.sizeDelta = new Vector2(width, _tabBtnHeight);
            tabRT.anchoredPosition = new Vector2(centerX, -topGap);

            return go;
        }

        // ══════════════════════════════════════════
        //  PLAYER PREVIEW
        // ══════════════════════════════════════════

        private void SetupPreviewClone()
        {
            ClearPreviewClone();

            var player = Player.m_localPlayer;
            if (player == null) return;

            var prefab = ZNetScene.instance?.GetPrefab("Player");
            if (prefab == null) return;

            ZNetView.m_forceDisableInit = true;
            try
            {
                _previewClone = Instantiate(prefab, PreviewSpawnPos, Quaternion.identity);
            }
            finally
            {
                ZNetView.m_forceDisableInit = false;
            }

            var rb = _previewClone.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);

            foreach (var mb in _previewClone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is VisEquipment) continue;
                mb.enabled = false;
            }

            foreach (var anim in _previewClone.GetComponentsInChildren<Animator>())
                anim.updateMode = AnimatorUpdateMode.Normal;

            var clonePlayer = _previewClone.GetComponent<Player>();
            if (clonePlayer != null)
            {
                clonePlayer.enabled = true;
                var tempProfile = new PlayerProfile("_preview_tmp", FileHelpers.FileSource.Local);
                tempProfile.SavePlayerData(player);
                tempProfile.LoadPlayerData(clonePlayer);
                clonePlayer.enabled = false;

                var visEquip = _previewClone.GetComponent<VisEquipment>();
                if (visEquip != null)
                {
                    var updateVisuals = AccessTools.Method(typeof(VisEquipment), "UpdateVisuals");
                    updateVisuals?.Invoke(visEquip, null);
                }
            }

            _previewClone.transform.rotation = Quaternion.identity;

            int charLayer = LayerMask.NameToLayer("character");
            if (charLayer < 0) charLayer = 9;
            foreach (var t in _previewClone.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = charLayer;

            // Isolated light rig — point lights with limited range can't reach the real world
            SetupPreviewLights();
        }

        private void ClearPreviewClone()
        {
            if (_previewLightRig != null)
            {
                Destroy(_previewLightRig);
                _previewLightRig = null;
            }
            if (_previewClone != null)
            {
                Destroy(_previewClone);
                _previewClone = null;
            }
        }

        private void SetupPreviewLights()
        {
            if (_previewLightRig != null) Destroy(_previewLightRig);

            _previewLightRig = new GameObject("PreviewLightRig");
            DontDestroyOnLoad(_previewLightRig);
            _previewLightRig.transform.position = PreviewSpawnPos;

            int charLayer = LayerMask.NameToLayer("character");
            if (charLayer < 0) charLayer = 9;
            int lightMask = (1 << charLayer);
            int charNetLayer = LayerMask.NameToLayer("character_net");
            if (charNetLayer >= 0) lightMask |= (1 << charNetLayer);

            // Key light — warm, front-right, main illumination
            var keyGO = new GameObject("KeyLight");
            keyGO.transform.SetParent(_previewLightRig.transform, false);
            keyGO.transform.localPosition = new Vector3(1.5f, 2.5f, 3.5f);
            var keyLight = keyGO.AddComponent<Light>();
            keyLight.type = LightType.Point;
            keyLight.range = 15f;
            keyLight.intensity = 2.0f;
            keyLight.color = new Color(1f, 0.92f, 0.82f);
            keyLight.cullingMask = lightMask;
            keyLight.shadows = LightShadows.None;

            // Fill light — softer, from the left to reduce harsh shadows
            var fillGO = new GameObject("FillLight");
            fillGO.transform.SetParent(_previewLightRig.transform, false);
            fillGO.transform.localPosition = new Vector3(-2.5f, 1.5f, 3f);
            var fillLight = fillGO.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.range = 15f;
            fillLight.intensity = 1.2f;
            fillLight.color = new Color(0.9f, 0.92f, 1f);
            fillLight.cullingMask = lightMask;
            fillLight.shadows = LightShadows.None;

            // Rim light — behind and above for edge definition / silhouette
            var rimGO = new GameObject("RimLight");
            rimGO.transform.SetParent(_previewLightRig.transform, false);
            rimGO.transform.localPosition = new Vector3(0f, 3f, -2.5f);
            var rimLight = rimGO.AddComponent<Light>();
            rimLight.type = LightType.Point;
            rimLight.range = 15f;
            rimLight.intensity = 1.2f;
            rimLight.color = new Color(0.95f, 0.88f, 0.78f);
            rimLight.cullingMask = lightMask;
            rimLight.shadows = LightShadows.None;

            // Bottom fill — subtle upward light to soften under-chin / armor shadows
            var bottomGO = new GameObject("BottomFill");
            bottomGO.transform.SetParent(_previewLightRig.transform, false);
            bottomGO.transform.localPosition = new Vector3(0f, -0.5f, 3f);
            var bottomLight = bottomGO.AddComponent<Light>();
            bottomLight.type = LightType.Point;
            bottomLight.range = 10f;
            bottomLight.intensity = 0.5f;
            bottomLight.color = new Color(0.85f, 0.82f, 0.78f);
            bottomLight.cullingMask = lightMask;
            bottomLight.shadows = LightShadows.None;
        }

        private void OnPreRenderPreview(Camera cam)
        {
            if (cam != _previewCamera) return;
            // Temporarily override ambient lighting for a well-lit preview
            _savedAmbientColor = RenderSettings.ambientLight;
            _savedAmbientIntensity = RenderSettings.ambientIntensity;
            _savedAmbientMode = RenderSettings.ambientMode;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.4f, 0.35f);
            RenderSettings.ambientIntensity = 1.2f;
        }

        private void OnPostRenderPreview(Camera cam)
        {
            if (cam != _previewCamera) return;
            // Restore original ambient settings immediately
            RenderSettings.ambientMode = _savedAmbientMode;
            RenderSettings.ambientLight = _savedAmbientColor;
            RenderSettings.ambientIntensity = _savedAmbientIntensity;
        }

        private void UpdatePreviewCamera()
        {
            if (_previewCamGO == null) return;
            Vector3 cloneCenter = PreviewSpawnPos + Vector3.up * 0.9f;
            float rad = _previewRotation * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Sin(rad), 0.3f, Mathf.Cos(rad)) * 5.0f;
            _previewCamGO.transform.position = cloneCenter + offset;
            _previewCamGO.transform.LookAt(cloneCenter);
        }

        private void UpdatePreviewEquipment(StartingClass cls)
        {
            if (_previewClone == null) return;
            var visEquip = _previewClone.GetComponent<VisEquipment>();
            if (visEquip == null) return;

            var slotFields = new Dictionary<string, string>
            {
                { "m_rightItem", "" }, { "m_leftItem", "" },
                { "m_chestItem", "" }, { "m_legItem", "" },
                { "m_helmetItem", "" }, { "m_shoulderItem", "" },
                { "m_utilityItem", "" }, { "m_leftBackItem", "" },
                { "m_rightBackItem", "" }
            };

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

            foreach (var kv in slotFields)
                AccessTools.Field(typeof(VisEquipment), kv.Key)?.SetValue(visEquip, kv.Value);
            foreach (var kv in variantFields)
                AccessTools.Field(typeof(VisEquipment), kv.Key)?.SetValue(visEquip, kv.Value);

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

        private void UpdatePreviewArmorSet(ArmorSetDef set)
        {
            if (_previewClone == null) return;
            var visEquip = _previewClone.GetComponent<VisEquipment>();
            if (visEquip == null) return;

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
            if (_recipeListRoot == null || _recipeElementPrefab == null)
                return;

            foreach (var elem in _classElements)
                if (elem != null) Destroy(elem);
            _classElements.Clear();

            var templateRT = _recipeElementPrefab.transform as RectTransform;
            float templateHeight = 32f;
            if (templateRT != null)
                templateHeight = Mathf.Max(24f, Mathf.Max(templateRT.rect.height, templateRT.sizeDelta.y));

            float rowHeight = Mathf.Max(templateHeight * 2f, 48f);
            float gap = 6f;
            float spacing = rowHeight + gap;

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

                var element = Instantiate(_recipeElementPrefab, _recipeListRoot);
                element.SetActive(true);
                element.name = "ClassElement_" + cls.Name;

                var trigger = element.GetComponent<EventTrigger>();
                if (trigger != null) DestroyImmediate(trigger);

                var elemRT = element.transform as RectTransform;
                StripLayoutComponents(element);

                elemRT.anchorMin = new Vector2(0f, 1f);
                elemRT.anchorMax = new Vector2(1f, 1f);
                elemRT.pivot = new Vector2(0.5f, 1f);
                elemRT.anchoredPosition = new Vector2(0f, i * -spacing);
                elemRT.sizeDelta = new Vector2(0f, rowHeight);

                var elemImg = element.GetComponent<Image>();
                if (elemImg != null)
                    ApplyEntryStyle(elemImg);

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

                float iconSize = rowHeight - 20f;
                float iconPadding = 4f;
                float textLeftOffset = iconPadding + iconSize + 6f;

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
                        nameTxt.text = cls.Name;
                        nameTxt.color = Color.white;
                        nameTxt.enableAutoSizing = false;
                        nameTxt.fontSize = 18f;
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

            float contentHeight = (_classes.Count > 0)
                ? ((_classes.Count - 1) * spacing + rowHeight)
                : rowHeight;
            _recipeListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);

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
                var selTr = _classElements[i]?.transform.Find("selected");
                if (selTr != null)
                    selTr.gameObject.SetActive(i == _selectedIndex);
            }
        }

        private void SwitchTab(int newTab)
        {
            int count = TabNames.Length;
            newTab = ((newTab % count) + count) % count;
            if (newTab == _activeTab) return;
            _activeTab = newTab;
            _panelFocus = 0;
            RefreshTabHighlights();
            RefreshTabPanels();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void RefreshTabPanels()
        {
            bool showClasses = (_activeTab == 0);
            bool showSkills = (_activeTab == 1);
            bool showArmor = (_activeTab == 2);

            if (_leftColumn != null) _leftColumn.gameObject.SetActive(showClasses || showSkills || showArmor);
            if (_middleColumn != null) _middleColumn.gameObject.SetActive(showClasses || showArmor);
            if (_rightColumn != null) _rightColumn.gameObject.SetActive(showClasses || showArmor);

            if (_previewCamera != null)
                _previewCamera.enabled = showClasses || showArmor;

            if (_skillsPanel != null) _skillsPanel.SetActive(showSkills);
            if (showSkills) RefreshSkillsPanel();

            if (showArmor)
            {
                RefreshArmorPanel();
            }
            else
            {
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
                _skillsText.text = "<size=22><color=#D4A24E>Select a class to view skills</color></size>";
                return;
            }

            var cls = _classes[_selectedIndex];
            var player = Player.m_localPlayer;
            int currentPoints = player != null ? SkillPointSystem.GetPoints(player) : 0;
            int currentXP = player != null ? SkillPointSystem.GetXP(player) : 0;
            string playerClass = player != null ? ClassPersistence.GetSelectedClassName(player) : null;
            bool isPlayerClass = playerClass == cls.Name;
            var equippedSkills = player != null ? AbilityManager.GetEquippedSkills(player) : new List<int>();
            int equippedCount = equippedSkills.Count;

            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"<align=center><size=26><color=#D4A24E>{cls.Name}</color></size></align>");
            sb.AppendLine($"<align=center><size=16><color=#AAAAAA>Passive Skills</color></size></align>");
            sb.AppendLine();
            if (isPlayerClass)
            {
                sb.AppendLine($"<align=center><size=17>Equipped: <color=#D4A24E>{equippedCount}/2</color>  \u2022  Points: <color=#8AE58A>{currentPoints}</color>  \u2022  XP: <color=#66B3E5>{currentXP}/100</color></size></align>");
            }
            else
            {
                sb.AppendLine($"<align=center><size=17><color=#999999>Select this class to unlock skills</color></size></align>");
            }
            sb.AppendLine();

            if (cls.Abilities == null || cls.Abilities.Count == 0)
            {
                sb.AppendLine("<color=#999999>No skills defined for this class.</color>");
                _skillsText.text = sb.ToString();
                return;
            }

            int nextLockedIndex = -1;
            int nextEquippableIndex = -1;

            for (int i = 0; i < cls.Abilities.Count; i++)
            {
                var ability = cls.Abilities[i];
                int pi = AbilityManager.GetPersistenceIndex(cls, i);
                bool unlocked = isPlayerClass && AbilityManager.IsAbilityUnlocked(player, cls.Name, pi);
                bool equipped = isPlayerClass && AbilityManager.IsSkillEquipped(player, cls.Name, pi);

                if (unlocked)
                {
                    if (equipped)
                    {
                        sb.AppendLine($"<size=22><color=#E5C56A>\u2605 {ability.Name}</color>  <size=14><color=#E5C56A>[EQUIPPED]</color></size></size>");
                        sb.AppendLine($"<size=14><color=#66B3E5>Passive</color> <color=#E5C56A>\u2014 Active</color></size>");
                    }
                    else
                    {
                        sb.AppendLine($"<size=22><color=#8AE58A>\u2713 {ability.Name}</color></size>");
                        sb.AppendLine($"<size=14><color=#66B3E5>Passive</color> <color=#8AE58A>\u2014 Unlocked</color></size>");
                        if (nextEquippableIndex < 0) nextEquippableIndex = i;
                    }
                    sb.AppendLine();
                    sb.AppendLine($"<size=16>{ability.Description}</size>");

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
                }
                else
                {
                    if (nextLockedIndex < 0) nextLockedIndex = i;

                    sb.AppendLine($"<size=22><color=#BBBBBB>\u25C8 {ability.Name}</color></size>");
                    sb.AppendLine($"<size=14><color=#66B3E5>Passive</color> <color=#777777>\u2022 {ability.PointCost} pts</color></size>");
                    sb.AppendLine();
                    sb.AppendLine($"<size=16><color=#888888>{ability.Description}</color></size>");
                    sb.AppendLine();
                    string haveColor = currentPoints >= ability.PointCost ? "#8AE58A" : "#666666";
                    sb.AppendLine($"<size=15><color=#D4A24E>\u25C6 {ability.PointCost} Skill Points</color>  <color={haveColor}>(You have: {currentPoints})</color></size>");
                }

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

            // Unlock button
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

            // Equip button
            if (_equipButton != null)
            {
                if (!isPlayerClass)
                {
                    _equipButton.interactable = false;
                    if (_equipButtonLabel != null)
                        _equipButtonLabel.text = "Not Your Class";
                }
                else if (equippedCount >= 2 && nextEquippableIndex < 0)
                {
                    // All slots full and no unequipped skills — allow unequip
                    _equipButton.interactable = true;
                    if (_equipButtonLabel != null)
                        _equipButtonLabel.text = "Unequip Last";
                }
                else if (nextEquippableIndex >= 0 && equippedCount < 2)
                {
                    _equipButton.interactable = true;
                    if (_equipButtonLabel != null)
                        _equipButtonLabel.text = $"Equip {cls.Abilities[nextEquippableIndex].Name}";
                }
                else
                {
                    _equipButton.interactable = false;
                    if (_equipButtonLabel != null)
                        _equipButtonLabel.text = equippedCount >= 2 ? "Slots Full" : "No Skills to Equip";
                }
            }
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

            for (int i = 0; i < cls.Abilities.Count; i++)
            {
                int pi = AbilityManager.GetPersistenceIndex(cls, i);
                if (AbilityManager.IsAbilityUnlocked(player, cls.Name, pi)) continue;

                var ability = cls.Abilities[i];
                if (AbilityManager.UnlockAbility(player, cls.Name, pi))
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

        private void OnEquipButtonClicked()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _classes.Count) return;
            var cls = _classes[_selectedIndex];
            var player = Player.m_localPlayer;
            if (player == null) return;

            string playerClass = ClassPersistence.GetSelectedClassName(player);
            if (playerClass != cls.Name) return;

            if (cls.Abilities == null) return;

            int equippedCount = AbilityManager.GetEquippedCount(player);

            // If slots full and no unequipped skills available, unequip the last equipped
            if (equippedCount >= 2)
            {
                var equipped = AbilityManager.GetEquippedSkills(player);
                if (equipped.Count > 0)
                {
                    int lastPi = equipped[equipped.Count - 1];
                    AbilityManager.UnequipSkill(player, cls.Name, lastPi);
                    RefreshSkillsPanel();
                }
                return;
            }

            // Equip the first unlocked-but-not-equipped skill
            for (int i = 0; i < cls.Abilities.Count; i++)
            {
                int pi = AbilityManager.GetPersistenceIndex(cls, i);
                if (!AbilityManager.IsAbilityUnlocked(player, cls.Name, pi)) continue;
                if (AbilityManager.IsSkillEquipped(player, cls.Name, pi)) continue;

                if (AbilityManager.EquipSkill(player, cls.Name, pi))
                {
                    player.Message(MessageHud.MessageType.Center, $"Equipped: {cls.Abilities[i].Name}!");
                    RefreshSkillsPanel();
                }
                return;
            }
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
                btn.transition = Selectable.Transition.None;

                var img = tabs[i].GetComponent<Image>();
                if (img != null)
                    img.color = isActive
                        ? new Color(0.83f, 0.64f, 0.31f, 1f)
                        : new Color(0.45f, 0.45f, 0.45f, 1f);
            }
        }

        private void ClearDetail()
        {
            if (_recipeName != null) _recipeName.text = "";
            if (_recipeDescription != null) _recipeDescription.text = "";
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

            if (_recipeName != null)
            {
                _recipeName.text = cls.Name;
            }

            if (_recipeDescription != null)
            {
                string desc = cls.Description;

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
                _recipeDescription.ForceMeshUpdate();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_recipeDescription.rectTransform);
            }

            Canvas.ForceUpdateCanvases();
            _descScrollResetFrames = 3;

            if (_craftButton != null)
            {
                var p = Player.m_localPlayer;
                bool hasClass = p != null && ClassPersistence.HasSelectedClass(p);
                _craftButton.interactable = !hasClass;
                if (_craftButtonLabel != null)
                    _craftButtonLabel.text = hasClass ? "Class Already Selected" : $"Begin as {cls.Name}";
            }
        }

        private void ConfirmSelection()
        {
            if (!_isVisible) return;

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

            // Block re-selection once a class is already chosen — use /ClassReset to change
            if (ClassPersistence.HasSelectedClass(player))
            {
                StartingClassPlugin.LogWarning("Class already selected. Use /ClassReset to reset.");
                return;
            }

            ClassApplicator.ApplyClass(player, _classes[_selectedIndex], _isFromCommand);
            Close();
        }

        // ══════════════════════════════════════════
        //  ARMOR TAB
        // ══════════════════════════════════════════

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

            if (_recipeListRoot != null && _classes.Count > 0)
            {
                var templateRT = _recipeElementPrefab != null ? _recipeElementPrefab.transform as RectTransform : null;
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

        private void EnsureArmorListElements(int needed)
        {
            if (_classElements.Count >= needed) return;
            if (_recipeListRoot == null || _recipeElementPrefab == null) return;

            var templateRT = _recipeElementPrefab.transform as RectTransform;
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

                var element = Instantiate(_recipeElementPrefab, _recipeListRoot);
                element.SetActive(false);
                element.name = "ArmorSetElement_" + i;

                var trigger = element.GetComponent<EventTrigger>();
                if (trigger != null) DestroyImmediate(trigger);

                var elemRT = element.transform as RectTransform;
                StripLayoutComponents(element);

                elemRT.anchorMin = new Vector2(0f, 1f);
                elemRT.anchorMax = new Vector2(1f, 1f);
                elemRT.pivot = new Vector2(0.5f, 1f);
                elemRT.anchoredPosition = new Vector2(0f, i * -spacing);
                elemRT.sizeDelta = new Vector2(0f, rowHeight);

                var elemImg = element.GetComponent<Image>();
                if (elemImg != null)
                    ApplyEntryStyle(elemImg);

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

                float iconSize = rowHeight - 20f;
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
                        nameTxt.fontSize = 18f;
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

            float contentHeight = needed > 0 ? ((needed - 1) * spacing + rowHeight) : rowHeight;
            _recipeListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        }

        private void RefreshArmorPanel()
        {
            var player = Player.m_localPlayer;
            var allSets = ArmorUpgradeSystem.GetAllSets();
            int points = player != null ? SkillPointSystem.GetPoints(player) : 0;

            if (_armorSelectedSlot < 0) _armorSelectedSlot = 0;
            if (_armorSelectedSlot >= allSets.Length) _armorSelectedSlot = allSets.Length - 1;

            EnsureArmorListElements(allSets.Length);

            for (int i = 0; i < _classElements.Count; i++)
            {
                if (i < allSets.Length)
                {
                    _classElements[i].SetActive(true);
                    var set = allSets[i];
                    bool equipped = player != null && ArmorUpgradeSystem.IsSetEquipped(player, set);
                    int level = player != null ? ArmorUpgradeSystem.GetSetLevel(player, set) : 0;

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

                    var iconTr = _classElements[i].transform.Find("icon");
                    if (iconTr != null)
                    {
                        var iconImg = iconTr.GetComponent<Image>();
                        if (iconImg != null)
                        {
                            var setIcon = ArmorUpgradeSystem.GetSetIcon(set);
                            if (setIcon != null)
                            {
                                iconImg.sprite = setIcon;
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

                    var selTr = _classElements[i].transform.Find("selected");
                    if (selTr != null)
                        selTr.gameObject.SetActive(i == _armorSelectedSlot);
                }
                else
                {
                    _classElements[i].SetActive(false);
                }
            }

            var selSet = (_armorSelectedSlot >= 0 && _armorSelectedSlot < allSets.Length)
                ? allSets[_armorSelectedSlot] : null;
            bool setEquipped = selSet != null && player != null && ArmorUpgradeSystem.IsSetEquipped(player, selSet);
            int currentLevel = selSet != null && player != null ? ArmorUpgradeSystem.GetSetLevel(player, selSet) : 0;

            UpdatePreviewArmorSet(selSet);

            if (_recipeName != null)
            {
                _recipeName.text = selSet != null ? selSet.DisplayName : "Armor";
            }

            if (_recipeDescription != null)
            {
                var sb = new System.Text.StringBuilder();

                if (selSet != null)
                {
                    int pieceCount = selSet.Pieces.Length;
                    int remainingCost = (ArmorUpgradeSystem.MaxLevel - currentLevel) * ArmorUpgradeSystem.UpgradeCost;

                    sb.AppendLine($"<align=center><size=16><color=#AAAAAA>Set Enhancement \u2022 {pieceCount} Pieces</color></size></align>");
                    sb.AppendLine();
                    if (setEquipped)
                        sb.AppendLine($"<align=center><size=17>Available: <color=#8AE58A>{points}</color>  \u2022  Remaining: <color=#D4A24E>{remainingCost}</color></size></align>");
                    else
                        sb.AppendLine($"<align=center><size=17>Available: <color=#8AE58A>{points}</color>  \u2022  <color=#FF6666>Not Equipped</color></size></align>");
                    sb.AppendLine();

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
                            sb.AppendLine($"<size=22><color={accentColor}>\u2605 {tierName}</color></size>");
                            sb.AppendLine($"<size=14><color=#D4A24E>Enhancement</color> <color={accentColor}>\u2014 Unlocked</color></size>");
                            sb.AppendLine();
                            sb.AppendLine($"<size=16>{tierDesc}</size>");
                            sb.AppendLine();
                            sb.AppendLine($"<size=15><color=#8AE58A>+{perLevel:F0} per piece ({pieceCount} pcs = +{tierTotal:F0} Armor)</color></size>");
                        }
                        else
                        {
                            string nameColor = isMax ? "#D4A24E" : "#BBBBBB";
                            string descColor = isMax ? "#999999" : "#888888";
                            string iconChar = isMax ? "\u25C6" : "\u25C8";

                            sb.AppendLine($"<size=22><color={nameColor}>{iconChar} {tierName}</color></size>");
                            sb.AppendLine($"<size=14><color=#D4A24E>Enhancement</color> <color=#777777>\u2022 Tier {lvl} \u2022 {ArmorUpgradeSystem.UpgradeCost} pts</color></size>");
                            sb.AppendLine();
                            sb.AppendLine($"<size=16><color={descColor}>{tierDesc}</color></size>");
                            sb.AppendLine();
                            sb.AppendLine($"<size=15><color={descColor}>+{perLevel:F0} per piece ({pieceCount} pcs = +{tierTotal:F0} Armor)</color></size>");
                            sb.AppendLine();
                            string haveColor = points >= ArmorUpgradeSystem.UpgradeCost ? "#8AE58A" : "#666666";
                            sb.AppendLine($"<size=15><color=#D4A24E>\u25C6 {ArmorUpgradeSystem.UpgradeCost} Skill Points</color>  <color={haveColor}>(You have: {points})</color></size>");
                        }

                        if (lvl < ArmorUpgradeSystem.MaxLevel)
                        {
                            string arrowColor = lvl < currentLevel ? "#E5C56A" : "#555555";
                            sb.AppendLine($"<align=center><size=20><color={arrowColor}>\u2502</color></size></align>");
                            sb.AppendLine($"<align=center><size=20><color={arrowColor}>\u25BC</color></size></align>");
                        }
                    }

                    if (currentLevel > 0)
                    {
                        sb.AppendLine($"<align=center><size=17><color=#8AE58A>Total: +{totalPerPiece:F0} per piece \u2022 +{totalAllPieces:F0} Armor across set</color></size></align>");
                    }
                }

                _recipeDescription.text = sb.ToString();
                _recipeDescription.ForceMeshUpdate();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_recipeDescription.rectTransform);
            }

            if (_craftButton != null)
            {
                bool selMaxed = currentLevel >= ArmorUpgradeSystem.MaxLevel;
                bool canAfford = points >= ArmorUpgradeSystem.UpgradeCost;

                _craftButton.interactable = setEquipped && !selMaxed && canAfford;
                if (_craftButtonLabel != null)
                {
                    if (!setEquipped)
                        _craftButtonLabel.text = "Not Equipped";
                    else if (selMaxed)
                        _craftButtonLabel.text = "Max Level";
                    else if (!canAfford)
                        _craftButtonLabel.text = $"Need {ArmorUpgradeSystem.UpgradeCost} pts";
                    else
                        _craftButtonLabel.text = $"Enhance Set ({ArmorUpgradeSystem.UpgradeCost} pts)";
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
        //  HELPERS
        // ══════════════════════════════════════════

        private static void StripButtonHints(GameObject buttonGO, TMP_Text labelText)
        {
            for (int i = buttonGO.transform.childCount - 1; i >= 0; i--)
            {
                var child = buttonGO.transform.GetChild(i);
                if (labelText != null &&
                    (child.gameObject == labelText.gameObject || labelText.transform.IsChildOf(child)))
                    continue;
                DestroyImmediate(child.gameObject);
            }
        }

        private static void StripLayoutComponents(GameObject go)
        {
            foreach (var c in go.GetComponents<LayoutGroup>())
                DestroyImmediate(c);
            foreach (var c in go.GetComponents<ContentSizeFitter>())
                DestroyImmediate(c);
            foreach (var c in go.GetComponents<LayoutElement>())
                DestroyImmediate(c);
        }

        /// <summary>Find the Valheim TMP font from existing UI or loaded assets.</summary>
        private TMP_FontAsset FindValheimFont()
        {
            if (_valheimFont != null) return _valheimFont;
            var invGui = InventoryGui.instance;
            if (invGui != null)
            {
                if (invGui.m_recipeName != null && invGui.m_recipeName.font != null)
                    return invGui.m_recipeName.font;
                if (invGui.m_recipeDecription != null && invGui.m_recipeDecription.font != null)
                    return invGui.m_recipeDecription.font;
            }
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (f.name.Contains("Valheim") || f.name.Contains("Averia"))
                    return f;
            return null;
        }

        // ══════════════════════════════════════════
        //  DATA LOOKUPS
        // ══════════════════════════════════════════

        private static Sprite GetClassIcon(StartingClass cls)
        {
            if (!string.IsNullOrEmpty(cls.IconPrefab))
            {
                var icon = GetItemIcon(cls.IconPrefab);
                if (icon != null) return icon;
            }

            if (cls.PreviewEquipment != null && cls.PreviewEquipment.Count > 0)
            {
                var icon = GetItemIcon(cls.PreviewEquipment[0]);
                if (icon != null) return icon;
            }

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
