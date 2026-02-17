using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StartingClassMod
{
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

        // ── Dynamic UI references ──
        private GameObject _detailSection;
        private GameObject _placeholder;
        private GameObject _closeButton;
        private TextMeshProUGUI _classNameLabel;
        private TextMeshProUGUI _classDescLabel;
        private TextMeshProUGUI _confirmLabel;
        private Transform _equipmentList;
        private Transform _skillsList;
        private readonly List<Image> _classBtnBGs = new List<Image>();

        // ── Cached Valheim visuals ──
        private TMP_FontAsset _font;
        private Sprite _panelSprite;
        private Color _panelColor = Color.white;
        private Sprite _buttonSprite;
        private ColorBlock _buttonCB;
        private bool _hasButtonSprite;
        private Sprite _slotSprite;
        private Color _slotColor = Color.white;
        private bool _visualsCached;

        // ── Fallback palette ──
        static readonly Color FallPanelBG    = new Color(0.051f, 0.051f, 0.078f, 0.96f);
        static readonly Color FallBorder     = new Color(0.851f, 0.722f, 0.361f, 0.55f);
        static readonly Color FallBtnNormal  = new Color(0.098f, 0.098f, 0.133f);
        static readonly Color FallBtnSelect  = new Color(0.220f, 0.192f, 0.118f, 0.92f);
        static readonly Color FallConfirm    = new Color(0.133f, 0.329f, 0.133f);
        static readonly Color FallClose      = new Color(0.38f, 0.11f, 0.11f);
        static readonly Color FallIconBG     = new Color(0.10f, 0.10f, 0.14f, 0.8f);

        // ── Always-used colors ──
        static readonly Color ColGold        = new Color(0.851f, 0.722f, 0.361f);
        static readonly Color ColText        = new Color(0.78f, 0.78f, 0.78f);
        static readonly Color ColTextBright  = new Color(0.95f, 0.93f, 0.88f);
        static readonly Color ColSkill       = new Color(0.400f, 0.702f, 0.898f);
        static readonly Color ColSeparator   = new Color(0.851f, 0.722f, 0.361f, 0.30f);
        static readonly Color ColOverlay     = new Color(0f, 0f, 0f, 0.6f);

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

            _canvasGO.SetActive(true);
            _isVisible = true;

            RefreshDetail();
            RefreshButtonHighlights();

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

            if (_isFromCommand && Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        private void OnDestroy()
        {
            if (_canvasGO != null) Destroy(_canvasGO);
        }

        // ══════════════════════════════════════════
        //  CACHE VALHEIM VISUALS
        // ══════════════════════════════════════════

        private void CacheVisuals()
        {
            if (_visualsCached) return;

            // ── Font ──
            foreach (var t in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
            {
                if (t.font != null) { _font = t.font; break; }
            }
            if (_font == null) _font = TMP_Settings.defaultFontAsset;

            // ── Panel + button sprites from InventoryGui ──
            var invGui = InventoryGui.instance;
            if (invGui != null)
            {
                // Panel background: find the largest sliced Image child
                Image bestPanel = null;
                float bestArea = 0;
                foreach (var img in invGui.GetComponentsInChildren<Image>(true))
                {
                    if (img.sprite == null || img.sprite.name == "UISprite" || img.sprite.name == "Background")
                        continue;
                    if (img.type != Image.Type.Sliced && img.type != Image.Type.Tiled)
                        continue;
                    float area = img.rectTransform.rect.width * img.rectTransform.rect.height;
                    if (area > bestArea)
                    {
                        bestArea = area;
                        bestPanel = img;
                    }
                }
                if (bestPanel != null)
                {
                    _panelSprite = bestPanel.sprite;
                    _panelColor = bestPanel.color;
                    StartingClassPlugin.Log($"Cached panel sprite: '{_panelSprite.name}'");
                }

                // Button sprites: grab from first button with a real sprite
                foreach (var btn in invGui.GetComponentsInChildren<Button>(true))
                {
                    var img = btn.targetGraphic as Image;
                    if (img == null || img.sprite == null) continue;
                    if (img.sprite.name == "UISprite" || img.sprite.name == "Background") continue;

                    _buttonSprite = img.sprite;
                    _buttonCB = btn.colors;
                    _hasButtonSprite = true;
                    StartingClassPlugin.Log($"Cached button sprite: '{_buttonSprite.name}'");
                    break;
                }
            }

            // ── Item slot sprite ──
            // Look for a square sprite used for inventory slots
            if (invGui != null)
            {
                foreach (var img in invGui.GetComponentsInChildren<Image>(true))
                {
                    if (img.sprite == null) continue;
                    var r = img.sprite.rect;
                    // Inventory slots are typically square-ish, small sprites
                    if (r.width > 30 && r.width < 120 && Mathf.Abs(r.width - r.height) < 10
                        && img.sprite.name != "UISprite" && img.sprite.name != "Background"
                        && img.sprite != _panelSprite && img.sprite != _buttonSprite)
                    {
                        _slotSprite = img.sprite;
                        _slotColor = img.color;
                        StartingClassPlugin.Log($"Cached slot sprite: '{_slotSprite.name}'");
                        break;
                    }
                }
            }

            // Only mark cached once we've had a chance to grab sprites
            // If InventoryGui wasn't ready, we'll retry on next BuildUI call
            _visualsCached = invGui != null;
        }

        // ══════════════════════════════════════════
        //  UI CONSTRUCTION
        // ══════════════════════════════════════════

        private void BuildUI()
        {
            CacheVisuals();

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
            var overlay = MakeRect("Overlay", _canvasGO.transform);
            Stretch(overlay);
            AddImage(overlay, ColOverlay);

            // ── Panel frame ──
            var frame = MakeRect("Frame", _canvasGO.transform);
            AnchorCenter(frame, 720, 640);

            if (_panelSprite != null)
            {
                // Use the real Valheim panel sprite with its original color
                var frameImg = AddImage(frame, _panelColor);
                frameImg.sprite = _panelSprite;
                frameImg.type = Image.Type.Sliced;
            }
            else
            {
                // Fallback: gold border + dark inner
                AddImage(frame, FallBorder);
                var inner = MakeRect("Inner", frame);
                Stretch(inner, 2);
                AddImage(inner, FallPanelBG);
            }

            // ── Content column ──
            var contentParent = _panelSprite != null ? frame : frame.GetChild(0);
            var content = MakeRect("Content", contentParent);
            Stretch(content, 20);
            var vLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 6;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;

            // ── Title ──
            MakeText(content, "Title", "Choose Your Starting Class",
                     28, ColGold, TextAlignmentOptions.Center, FontStyles.Bold, 42);

            MakeSeparator(content);

            // ── Class button row ──
            var btnRow = MakeRect("BtnRow", content);
            SetLayout(btnRow.gameObject, prefH: 54);
            var hLayout = btnRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 8;
            hLayout.childForceExpandWidth = true;
            hLayout.childForceExpandHeight = true;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;

            _classBtnBGs.Clear();
            for (int i = 0; i < _classes.Count; i++)
            {
                int idx = i;
                MakeClassButton(btnRow, _classes[i], () => SelectClass(idx));
            }

            MakeSeparator(content);

            // ── Placeholder ──
            var ph = MakeText(content, "Placeholder",
                              "Select a class above to view details",
                              17, ColText, TextAlignmentOptions.Center, FontStyles.Italic, -1);
            _placeholder = ph.gameObject;
            SetLayout(_placeholder, flexH: 1);

            // ── Detail section ──
            _detailSection = MakeRect("Detail", content).gameObject;
            SetLayout(_detailSection, flexH: 1);
            var dLayout = _detailSection.AddComponent<VerticalLayoutGroup>();
            dLayout.spacing = 3;
            dLayout.childForceExpandWidth = true;
            dLayout.childForceExpandHeight = false;
            dLayout.childControlWidth = true;
            dLayout.childControlHeight = true;
            _detailSection.SetActive(false);

            _classNameLabel = MakeText(_detailSection.transform, "ClassName", "",
                                       22, ColGold, TextAlignmentOptions.Left, FontStyles.Bold, 30);

            _classDescLabel = MakeText(_detailSection.transform, "ClassDesc", "",
                                       14, ColText, TextAlignmentOptions.TopLeft, FontStyles.Normal, 40);
            _classDescLabel.textWrappingMode = TextWrappingModes.Normal;

            SetLayout(MakeRect("Sp1", _detailSection.transform).gameObject, prefH: 2);

            MakeText(_detailSection.transform, "EqHeader", "Starting Equipment",
                     16, ColGold, TextAlignmentOptions.Left, FontStyles.Bold, 24);

            var eqList = MakeRect("EqList", _detailSection.transform);
            var eqVL = eqList.gameObject.AddComponent<VerticalLayoutGroup>();
            eqVL.spacing = 2;
            eqVL.childForceExpandWidth = true;
            eqVL.childForceExpandHeight = false;
            eqVL.childControlWidth = true;
            eqVL.childControlHeight = true;
            eqVL.padding = new RectOffset(6, 0, 0, 0);
            _equipmentList = eqList;

            SetLayout(MakeRect("Sp2", _detailSection.transform).gameObject, prefH: 4);

            MakeText(_detailSection.transform, "SkHeader", "Skill Bonuses",
                     16, ColGold, TextAlignmentOptions.Left, FontStyles.Bold, 24);

            var skList = MakeRect("SkList", _detailSection.transform);
            var skVL = skList.gameObject.AddComponent<VerticalLayoutGroup>();
            skVL.spacing = 2;
            skVL.childForceExpandWidth = true;
            skVL.childForceExpandHeight = false;
            skVL.childControlWidth = true;
            skVL.childControlHeight = true;
            skVL.padding = new RectOffset(6, 0, 0, 0);
            _skillsList = skList;

            SetLayout(MakeRect("SpFlex", _detailSection.transform).gameObject, flexH: 1);

            MakeConfirmButton(_detailSection.transform);

            // ── Close button (command only) ──
            _closeButton = MakeStyledButton(content, "CloseBtn", "Close", FallClose, 36,
                                            () => Close()).gameObject;
            _closeButton.SetActive(false);

            _canvasGO.SetActive(false);
            _uiBuilt = true;
        }

        // ══════════════════════════════════════════
        //  SELECTION LOGIC
        // ══════════════════════════════════════════

        private void SelectClass(int index)
        {
            if (index < 0 || index >= _classes.Count) return;
            _selectedIndex = index;
            RefreshDetail();
            RefreshButtonHighlights();
        }

        private void RefreshButtonHighlights()
        {
            for (int i = 0; i < _classBtnBGs.Count; i++)
            {
                bool sel = i == _selectedIndex;
                if (_hasButtonSprite)
                {
                    // Tint the Valheim button sprite for selected state
                    _classBtnBGs[i].color = sel
                        ? new Color(0.9f, 0.8f, 0.5f)
                        : Color.white;
                }
                else
                {
                    _classBtnBGs[i].color = sel ? FallBtnSelect : FallBtnNormal;
                }
            }
        }

        private void RefreshDetail()
        {
            bool hasSel = _selectedIndex >= 0 && _selectedIndex < _classes.Count;
            _placeholder.SetActive(!hasSel);
            _detailSection.SetActive(hasSel);
            if (!hasSel) return;

            var cls = _classes[_selectedIndex];
            _classNameLabel.text = cls.Name;
            _classDescLabel.text = cls.Description.Replace("\n", " ");
            _confirmLabel.text = $"Begin as {cls.Name}";

            ClearChildren(_equipmentList);
            foreach (var item in cls.Items)
                MakeItemRow(_equipmentList, item);

            ClearChildren(_skillsList);
            foreach (var skill in cls.SkillBonuses)
                MakeSkillRow(_skillsList, skill);
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
        //  ELEMENT BUILDERS
        // ══════════════════════════════════════════

        private void MakeClassButton(Transform parent, StartingClass cls, System.Action onClick)
        {
            var rt = MakeRect("Btn_" + cls.Name, parent);
            Image bg;

            if (_hasButtonSprite)
            {
                bg = AddImage(rt, Color.white);
                bg.sprite = _buttonSprite;
                bg.type = Image.Type.Sliced;
            }
            else
            {
                bg = AddImage(rt, FallBtnNormal);
            }
            _classBtnBGs.Add(bg);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };

            if (_hasButtonSprite)
            {
                btn.colors = _buttonCB;
            }
            else
            {
                var cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1.6f, 1.5f, 1.3f);
                cb.pressedColor = new Color(1.3f, 1.2f, 1.1f);
                cb.fadeDuration = 0.08f;
                btn.colors = cb;
            }
            btn.onClick.AddListener(() => onClick());

            // Button content: icon + label
            var row = MakeRect("BtnContent", rt);
            Stretch(row);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 6;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.padding = new RectOffset(8, 8, 4, 4);

            Sprite icon = GetItemIcon(cls.IconItemName);
            if (icon != null)
            {
                var iconGO = MakeRect("Icon", row);
                var iconImg = AddImage(iconGO, Color.white);
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                SetLayout(iconGO.gameObject, prefW: 32, prefH: 32);
            }

            var label = MakeText(row, "Label", cls.Name,
                                 16, ColTextBright, TextAlignmentOptions.Center, FontStyles.Bold, -1);
            label.raycastTarget = false;
            SetLayout(label.gameObject, flexW: 1);
        }

        private void MakeConfirmButton(Transform parent)
        {
            var rt = MakeStyledButton(parent, "ConfirmBtn", "Begin as ...", FallConfirm, 48,
                                      () => ConfirmSelection());
            _confirmLabel = rt.GetComponentInChildren<TextMeshProUGUI>();
        }

        private RectTransform MakeStyledButton(Transform parent, string name, string label,
                                               Color fallbackColor, float height, System.Action onClick)
        {
            var rt = MakeRect(name, parent);
            SetLayout(rt.gameObject, prefH: height);

            Image bg;
            if (_hasButtonSprite)
            {
                bg = AddImage(rt, Color.white);
                bg.sprite = _buttonSprite;
                bg.type = Image.Type.Sliced;
            }
            else
            {
                bg = AddImage(rt, fallbackColor);
            }

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };

            if (_hasButtonSprite)
            {
                btn.colors = _buttonCB;
            }
            else
            {
                var cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1.35f, 1.35f, 1.35f);
                cb.pressedColor = new Color(0.85f, 0.85f, 0.85f);
                cb.fadeDuration = 0.08f;
                btn.colors = cb;
            }
            btn.onClick.AddListener(() => onClick());

            var txt = MakeText(rt, "Label", label, height > 40 ? 19 : 15,
                               ColGold, TextAlignmentOptions.Center, FontStyles.Bold, -1);
            Stretch(txt.rectTransform);
            txt.raycastTarget = false;

            return rt;
        }

        private void MakeItemRow(Transform parent, StartingItem item)
        {
            var row = MakeRect("Item_" + item.PrefabName, parent);
            SetLayout(row.gameObject, prefH: 36);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 8;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childAlignment = TextAnchor.MiddleLeft;

            // Icon slot (uses inventory slot sprite if available)
            var iconSlot = MakeRect("IconSlot", row);
            SetLayout(iconSlot.gameObject, prefW: 34, prefH: 34);

            if (_slotSprite != null)
            {
                var slotImg = AddImage(iconSlot, _slotColor);
                slotImg.sprite = _slotSprite;
                slotImg.type = Image.Type.Sliced;
                slotImg.raycastTarget = false;
            }
            else
            {
                var slotImg = AddImage(iconSlot, FallIconBG);
                slotImg.raycastTarget = false;
            }

            Sprite icon = GetItemIcon(item.PrefabName);
            if (icon != null)
            {
                var iconRT = MakeRect("Icon", iconSlot);
                Stretch(iconRT, 2);
                var img = AddImage(iconRT, Color.white);
                img.sprite = icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }

            // Item name + quantity
            string displayName = GetLocalizedName(item.PrefabName);
            string qty = item.Quantity > 1 ? $"  <color=#B0B0B0>x{item.Quantity}</color>" : "";
            var txt = MakeText(row, "Name", $"{displayName}{qty}",
                               14, ColTextBright, TextAlignmentOptions.Left, FontStyles.Normal, -1);
            txt.richText = true;
            txt.raycastTarget = false;
            SetLayout(txt.gameObject, flexW: 1);
        }

        private void MakeSkillRow(Transform parent, SkillBonus skill)
        {
            var row = MakeRect("Skill_" + skill.SkillType, parent);
            SetLayout(row.gameObject, prefH: 26);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 6;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childAlignment = TextAnchor.MiddleLeft;

            string skillName = FormatPascalCase(skill.SkillType.ToString());
            MakeText(row, "Name", $"\u2022  {skillName}", 14, ColText,
                     TextAlignmentOptions.Left, FontStyles.Normal, -1).raycastTarget = false;
            MakeText(row, "Bonus", $"+{skill.BonusLevel:0}", 14, ColSkill,
                     TextAlignmentOptions.Left, FontStyles.Bold, -1).raycastTarget = false;
        }

        private void MakeSeparator(Transform parent)
        {
            var sep = MakeRect("Sep", parent);
            SetLayout(sep.gameObject, prefH: 1);
            var img = AddImage(sep, ColSeparator);
            img.raycastTarget = false;
        }

        // ══════════════════════════════════════════
        //  LOW-LEVEL HELPERS
        // ══════════════════════════════════════════

        private static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rt, int inset = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        private static void Stretch(GameObject go, int inset = 0)
        {
            Stretch(go.GetComponent<RectTransform>(), inset);
        }

        private static void AnchorCenter(RectTransform rt, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static Image AddImage(RectTransform rt, Color color)
        {
            return AddImage(rt.gameObject, color);
        }

        private static Image AddImage(GameObject go, Color color)
        {
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            return img;
        }

        private TextMeshProUGUI MakeText(Transform parent, string name, string text,
                                          float size, Color color, TextAlignmentOptions align,
                                          FontStyles style, float prefHeight)
        {
            var rt = MakeRect(name, parent);
            if (prefHeight > 0)
                SetLayout(rt.gameObject, prefH: prefHeight);

            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = _font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        private static void SetLayout(GameObject go,
                                       float prefH = -1, float minH = -1, float flexH = -1,
                                       float prefW = -1, float minW = -1, float flexW = -1)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (prefH >= 0) le.preferredHeight = prefH;
            if (minH >= 0) le.minHeight = minH;
            if (flexH >= 0) le.flexibleHeight = flexH;
            if (prefW >= 0) le.preferredWidth = prefW;
            if (minW >= 0) le.minWidth = minW;
            if (flexW >= 0) le.flexibleWidth = flexW;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
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
