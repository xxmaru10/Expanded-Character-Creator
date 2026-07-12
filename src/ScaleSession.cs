using System;
using System.Globalization;
using SimpleFileBrowser;
using SlickUi;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityUtils;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// Floating panel shown right after importing a part (or reopened via the edit button, P6):
    /// previews the mesh in place and lets the user type uniform scale, per-axis scale (P4),
    /// rotation (P5), X/Y/Z offset and gender (P3) — all applied live. Texture variants (P13) live in
    /// the top-center VariantBar shown on selection. "Confirmar" persists the model's placement
    /// (<see cref="ScaleStore"/>) and either recalibrates the global scale factor or — when the
    /// "Salvar como padrão desta categoria" checkbox is ticked — pins this tab's category override.
    /// "Cancelar" reverts to the opening values without persisting. One active at a time.
    /// </summary>
    internal class ScaleSession : MonoBehaviour
    {
        private static ScaleSession _current;

        internal static bool IsModeling => _current != null && _current._modeling;

        // Remember where the user dragged the panel so it reopens in the same spot this session.
        private static Vector2 _lastPanelPos = new Vector2(0f, -60f);

        private RectTransform _panelRt;

        private CustomBodyPartAttachment _attachment;
        private string _storeKey;
        private string _displayName;
        private FolderImportContext _folderCtx; // non-null: mass-import preview (values apply to the folder)

        private UiInputField _scaleField;
        private UiInputField _sxField, _syField, _szField; // per-axis scale multiplier (P4)
        private UiInputField _xField, _yField, _zField;    // offset
        private UiInputField _rxField, _ryField, _rzField; // rotation (P5)
        private UiInputField _stepField;                   // position nudge step (fine control)
        private UiInputField _rotStepField;                // rotation nudge step in degrees
        private UiInputField _tagField;                    // P10 — edit this part's tag/theme
        private UiButton _genderBtn;                       // gender cycle (P3)
        private UiButton _channelBtn;                      // paint channel cycle (P2)
        private UiButton _encaixeBtn;                      // attach-mode cycle (P14)
        private string _texturePath;
        private string _gender = "";                       // "", "Feminine", "Masculine"
        private string _channel = ChannelMap.Primary;      // paint channel id (P2)
        private int _additiveMode;                         // P14: 0=auto, 1=accessory(additive), 2=replace
        private string _tag = "";                          // P10 — this part's tag/theme

        // Values the panel opened with — "Cancelar" reverts the live instance to these (persists nothing).
        private float _startScale;
        private Vector3 _startOffset, _startEuler, _startScaleAxis;

        // Checkbox: when ON, "Confirmar" pins this placement as the category default (overriding the
        // global factor for this tab); when OFF, "Confirmar" (re)calibrates the global factor instead.
        private bool _saveCategoryDefault;
        private UiButton _catDefaultBtn;
        private bool _tPoseActive;
        private UiButton _tPoseBtn;

        // Tab system and collapse state
        private int _activeTab = 0;
        private bool _collapsed = false;
        private GameObject _tabsRowGo;
        private GameObject _tab0Go; // Mov/Esc
        private GameObject _tab1Go; // Rotação
        private GameObject _tab2Go; // Opções/3D
        private GameObject _confirmRowGo;
        private TMP_Text _collapseBtnText;
        private readonly UiButton[] _tabButtons = new UiButton[3];

        private static readonly string[] EncaixeLabels =
            { "Encaixe: Auto", "Encaixe: Acessório (por cima)", "Encaixe: Substitui o slot" };

        // P11 — drag-to-model mode: grab the part in the preview and drag to move/scale/rotate it.
        // Toggle with the "Ativar modo modelagem" checkbox; keys 2/3/4 pick the axis of action.
        private GameObject _buttonTemplate;                // remembered clone source for the drag blocker
        private UiButton _modelBtn;                         // the "checkbox" toggle
        private readonly UiButton[] _modeChips = new UiButton[3]; // 2=posição, 3=grossura, 4=rotação
        private bool _modeling;
        private int _mode;                                 // 0=position, 1=thickness(scale), 2=rotation
        private bool _dragging;
        private Vector3 _lastMouse;
        private Vector3 _dragStartMouse;
        private int _dragLockedAxis = -1;                  // -1=unlocked, 0=horizontal (sides), 1=vertical (up/down)
        private Camera _previewCam;                        // creatorCam.cam
        private RectTransform _previewRect;                // creatorCam.inputPanel
        private Camera _uiCam;                             // canvas render camera (null for overlay)

        private static readonly Color ModelOn = new Color(0.16f, 0.38f, 0.22f, 1f);
        private static readonly Color ModelOff = new Color(0.16f, 0.17f, 0.20f, 0.98f);
        private static readonly Color ChipActive = new Color(0.20f, 0.55f, 0.28f, 1f);
        private static readonly Color ChipIdle = new Color(0.14f, 0.15f, 0.18f, 0.98f);
        private static readonly string[] ModeLabels = { "2  Posição", "3  Grossura", "4  Rotação" };

        private static readonly string[] GenderTags = { "", "Feminine", "Masculine" };
        private static readonly string[] GenderLabels = { "Gênero: Ambos", "Gênero: Feminino", "Gênero: Masculino" };

        // Paint channels the user can pick per part (P2). Order = cycle order; labels are friendly names.
        private static readonly string[] Channels =
        {
            ChannelMap.Skin, ChannelMap.Hair, ChannelMap.Eyes,
            ChannelMap.Primary, ChannelMap.Secondary,
            ChannelMap.LeatherA, ChannelMap.LeatherB,
            ChannelMap.MetalA, ChannelMap.MetalB, ChannelMap.MetalDark,
            ChannelMap.Emission,
        };
        private static readonly string[] ChannelLabels =
        {
            "Pele", "Cabelo", "Olhos",
            "Primário (torso)", "Secundário (pernas)",
            "Couro A (botas)", "Couro B (joelho/ombro)",
            "Metal A (braço)", "Metal B (mãos)", "Metal escuro (capacete)",
            "Brilho (acessório)",
        };

        internal static void Open(GameObject buttonTemplate, GameObject inputTemplate, Transform canvas,
            CustomBodyPartAttachment attachment, string storeKey, string displayName,
            float startScale, Vector3 startOffset, Vector3 startEuler, Vector3 startScaleAxis, string genderTag,
            FolderImportContext folderCtx = null)
        {
            Close();
            if (attachment == null || canvas == null || buttonTemplate == null)
            {
                Plugin.Log.LogWarning("Painel de escala nao pode abrir (canvas/template ausente).");
                return;
            }

            var panelGo = new GameObject("CustomPartScalePanel", typeof(RectTransform));
            panelGo.transform.SetParent(canvas, worldPositionStays: false);

            _current = panelGo.AddComponent<ScaleSession>();
            _current._folderCtx = folderCtx;
            _current.BuildUi(panelGo, buttonTemplate, inputTemplate, attachment, storeKey, displayName,
                startScale, startOffset, startEuler, startScaleAxis, genderTag);
        }

        internal static void Close()
        {
            if (_current != null)
            {
                if (_current._tPoseActive)
                {
                    _current.ApplyTPose(false);
                }
                UnityEngine.Object.Destroy(_current.gameObject);
                _current = null;
            }
        }

        private void BuildUi(GameObject panelGo, GameObject buttonTemplate, GameObject inputTemplate,
            CustomBodyPartAttachment attachment, string storeKey, string displayName,
            float startScale, Vector3 startOffset, Vector3 startEuler, Vector3 startScaleAxis, string genderTag)
        {
            _attachment = attachment;
            _storeKey = storeKey;
            _displayName = string.IsNullOrEmpty(displayName) ? "Parte" : displayName;
            _texturePath = attachment.Part != null ? attachment.Part.TexturePath : null;
            _gender = NormalizeGender(genderTag);
            _channel = attachment.Part != null && !string.IsNullOrEmpty(attachment.Part.ChannelId)
                ? attachment.Part.ChannelId : ChannelMap.Primary;
            _additiveMode = attachment.Part != null ? attachment.Part.AdditiveOverride : 0;
            _tag = attachment.Part != null && attachment.Part.Tag != null ? attachment.Part.Tag : "";

            // Remember the opening placement so "Cancelar" can restore it.
            _startScale = startScale;
            _startOffset = startOffset;
            _startEuler = startEuler;
            _startScaleAxis = startScaleAxis;

            var rt = panelGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            // Folder-preview mode adds one extra full-width button row at the bottom.
            rt.sizeDelta = new Vector2(480f, _folderCtx != null ? 614f : 566f);
            rt.anchoredPosition = _lastPanelPos; // reopen where the user last dragged it
            _panelRt = rt;
            _buttonTemplate = buttonTemplate;

            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.10f, 0.95f);

            // Whole panel is draggable (empty areas / header / labels move it; fields keep their own drag).
            var drag = panelGo.AddComponent<DragHandle>();
            drag.target = rt;

            // Absorb SlickUi's custom pointer system: it routes drags to the frontmost UiButton under
            // the cursor, ignoring plain Images. Without a UiButton on the panel, pressing an empty area
            // falls through to the preview panel behind → rotates the camera. A transparent full-panel
            // UiButton makes US the frontmost hit, so dragging the panel no longer spins the character.
            AddDragSurface(buttonTemplate, panelGo.transform);

            // A title bar at the top for a clear "grab here to move" affordance.
            var header = new GameObject("Header", typeof(RectTransform), typeof(Image));
            header.transform.SetParent(panelGo.transform, worldPositionStays: false);
            var hrt = header.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.sizeDelta = new Vector2(0f, 40f);
            hrt.anchoredPosition = Vector2.zero;
            header.GetComponent<Image>().color = new Color(0.16f, 0.17f, 0.26f, 1f);

            // Collapse/Expand button in the header
            var colBtn = MakeButton(buttonTemplate, header.transform, "▲", new Vector2(8f, -5f), new Vector2(30f, 30f), ToggleCollapse);
            if (colBtn != null) _collapseBtnText = colBtn.GetComponentInChildren<TMP_Text>(true);

            // Title label in the header
            var title = UiFactory.Label(header.transform, _displayName, fill: false);
            PlaceTopLeft(title.rectTransform, new Vector2(46f, -8f), new Vector2(250f, 22f));
            title.enableAutoSizing = false;
            title.fontSize = 14f;
            title.alignment = TextAlignmentOptions.Left;
            title.color = Color.white;

            // Drag hint in the header
            var hint = UiFactory.Label(header.transform, "arraste para mover", fill: false);
            PlaceTopLeft(hint.rectTransform, new Vector2(300f, -8f), new Vector2(170f, 22f));
            hint.enableAutoSizing = false;
            hint.fontSize = 11f;
            hint.alignment = TextAlignmentOptions.Right;
            hint.color = new Color(0.75f, 0.78f, 0.9f, 1f);

            // Tab row container
            _tabsRowGo = new GameObject("TabsRow", typeof(RectTransform));
            _tabsRowGo.transform.SetParent(panelGo.transform, worldPositionStays: false);
            var trt = _tabsRowGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(0f, 36f);
            trt.anchoredPosition = new Vector2(0f, -40f);

            // Create 3 tab buttons
            _tabButtons[0] = MakeButton(buttonTemplate, _tabsRowGo.transform, "Mov/Esc", new Vector2(12f, -3f), new Vector2(148f, 30f), () => SetTab(0));
            _tabButtons[1] = MakeButton(buttonTemplate, _tabsRowGo.transform, "Rotação", new Vector2(166f, -3f), new Vector2(148f, 30f), () => SetTab(1));
            _tabButtons[2] = MakeButton(buttonTemplate, _tabsRowGo.transform, "Opções/3D", new Vector2(320f, -3f), new Vector2(148f, 30f), () => SetTab(2));

            // Create 3 tab containers
            _tab0Go = new GameObject("Tab0_MovEsc", typeof(RectTransform));
            _tab0Go.transform.SetParent(panelGo.transform, worldPositionStays: false);
            var rt0 = _tab0Go.GetComponent<RectTransform>();
            rt0.anchorMin = new Vector2(0f, 1f); rt0.anchorMax = new Vector2(1f, 1f); rt0.pivot = new Vector2(0.5f, 1f);
            rt0.anchoredPosition = new Vector2(0f, -76f); rt0.sizeDelta = new Vector2(0f, 220f);

            _tab1Go = new GameObject("Tab1_Rot", typeof(RectTransform));
            _tab1Go.transform.SetParent(panelGo.transform, worldPositionStays: false);
            var rt1 = _tab1Go.GetComponent<RectTransform>();
            rt1.anchorMin = new Vector2(0f, 1f); rt1.anchorMax = new Vector2(1f, 1f); rt1.pivot = new Vector2(0.5f, 1f);
            rt1.anchoredPosition = new Vector2(0f, -76f); rt1.sizeDelta = new Vector2(0f, 128f);

            _tab2Go = new GameObject("Tab2_Opcoes", typeof(RectTransform));
            _tab2Go.transform.SetParent(panelGo.transform, worldPositionStays: false);
            var rt2 = _tab2Go.GetComponent<RectTransform>();
            rt2.anchorMin = new Vector2(0f, 1f); rt2.anchorMax = new Vector2(1f, 1f); rt2.pivot = new Vector2(0.5f, 1f);
            rt2.anchoredPosition = new Vector2(0f, -76f); rt2.sizeDelta = new Vector2(0f, 300f);

            // ==================== TAB 0: MOVE & SCALE ====================
            // Uniform scale row.
            SmallLabel(_tab0Go.transform, "Escala", new Vector2(12f, -12f), new Vector2(70f, 28f));
            _scaleField = MakeInput(inputTemplate, _tab0Go.transform, new Vector2(86f, -12f), new Vector2(150f, 30f),
                Fmt(startScale), s => { if (TryParse(s, out var v)) ApplyScale(v); RefreshFields(); });
            MakeButton(buttonTemplate, _tab0Go.transform, "–", new Vector2(240f, -12f), new Vector2(44f, 30f), () => NudgeScale(1f / 1.25f));
            MakeButton(buttonTemplate, _tab0Go.transform, "+", new Vector2(288f, -12f), new Vector2(44f, 30f), () => NudgeScale(1.25f));

            // Per-axis scale row:
            SmallLabel(_tab0Go.transform, "Esc XYZ", new Vector2(12f, -50f), new Vector2(70f, 28f));
            _sxField = MakeInput(inputTemplate, _tab0Go.transform, new Vector2(86f, -50f), new Vector2(120f, 30f), Fmt(startScaleAxis.x), _ => ApplyScaleAxisFromFields());
            _syField = MakeInput(inputTemplate, _tab0Go.transform, new Vector2(210f, -50f), new Vector2(120f, 30f), Fmt(startScaleAxis.y), _ => ApplyScaleAxisFromFields());
            _szField = MakeInput(inputTemplate, _tab0Go.transform, new Vector2(334f, -50f), new Vector2(120f, 30f), Fmt(startScaleAxis.z), _ => ApplyScaleAxisFromFields());

            // Mirror/Invert Row
            SmallLabel(_tab0Go.transform, "Espelhar", new Vector2(12f, -88f), new Vector2(70f, 28f));
            MakeButton(buttonTemplate, _tab0Go.transform, "Inverter X", new Vector2(86f, -88f), new Vector2(120f, 30f), () => InvertScaleAxis(0));
            MakeButton(buttonTemplate, _tab0Go.transform, "Inverter Y", new Vector2(210f, -88f), new Vector2(120f, 30f), () => InvertScaleAxis(1));
            MakeButton(buttonTemplate, _tab0Go.transform, "Inverter Z", new Vector2(334f, -88f), new Vector2(120f, 30f), () => InvertScaleAxis(2));

            // Position row.
            SmallLabel(_tab0Go.transform, "Posição", new Vector2(12f, -126f), new Vector2(70f, 28f));
            _xField = MakeInput(inputTemplate, _tab0Go.transform, new Vector2(86f, -126f), new Vector2(120f, 30f), Fmt(startOffset.x), _ => ApplyOffsetFromFields());
            _yField = MakeInput(inputTemplate, _tab0Go.transform, new Vector2(210f, -126f), new Vector2(120f, 30f), Fmt(startOffset.y), _ => ApplyOffsetFromFields());
            _zField = MakeInput(inputTemplate, _tab0Go.transform, new Vector2(334f, -126f), new Vector2(120f, 30f), Fmt(startOffset.z), _ => ApplyOffsetFromFields());

            // Fine position nudge row
            SmallLabel(_tab0Go.transform, "Passo", new Vector2(12f, -164f), new Vector2(44f, 28f));
            _stepField = MakeInput(inputTemplate, _tab0Go.transform, new Vector2(58f, -164f), new Vector2(48f, 30f), "0.05", null);
            MakeButton(buttonTemplate, _tab0Go.transform, "X–", new Vector2(112f, -164f), new Vector2(42f, 30f), () => NudgePos(0, -1f));
            MakeButton(buttonTemplate, _tab0Go.transform, "X+", new Vector2(156f, -164f), new Vector2(42f, 30f), () => NudgePos(0, +1f));
            MakeButton(buttonTemplate, _tab0Go.transform, "Y–", new Vector2(206f, -164f), new Vector2(42f, 30f), () => NudgePos(1, -1f));
            MakeButton(buttonTemplate, _tab0Go.transform, "Y+", new Vector2(250f, -164f), new Vector2(42f, 30f), () => NudgePos(1, +1f));
            MakeButton(buttonTemplate, _tab0Go.transform, "Z–", new Vector2(300f, -164f), new Vector2(42f, 30f), () => NudgePos(2, -1f));
            MakeButton(buttonTemplate, _tab0Go.transform, "Z+", new Vector2(344f, -164f), new Vector2(42f, 30f), () => NudgePos(2, +1f));

            // ==================== TAB 1: ROTATION ====================
            // Rotation row
            SmallLabel(_tab1Go.transform, "Rotação", new Vector2(12f, -12f), new Vector2(70f, 28f));
            _rxField = MakeInput(inputTemplate, _tab1Go.transform, new Vector2(86f, -12f), new Vector2(120f, 30f), Fmt(startEuler.x), _ => ApplyEulerFromFields());
            _ryField = MakeInput(inputTemplate, _tab1Go.transform, new Vector2(210f, -12f), new Vector2(120f, 30f), Fmt(startEuler.y), _ => ApplyEulerFromFields());
            _rzField = MakeInput(inputTemplate, _tab1Go.transform, new Vector2(334f, -12f), new Vector2(120f, 30f), Fmt(startEuler.z), _ => ApplyEulerFromFields());

            // Rotation nudge row
            SmallLabel(_tab1Go.transform, "Passo°", new Vector2(12f, -50f), new Vector2(48f, 28f));
            _rotStepField = MakeInput(inputTemplate, _tab1Go.transform, new Vector2(58f, -50f), new Vector2(48f, 30f), "15", null);
            MakeButton(buttonTemplate, _tab1Go.transform, "RX–", new Vector2(112f, -50f), new Vector2(42f, 30f), () => NudgeRot(0, -1f));
            MakeButton(buttonTemplate, _tab1Go.transform, "RX+", new Vector2(156f, -50f), new Vector2(42f, 30f), () => NudgeRot(0, +1f));
            MakeButton(buttonTemplate, _tab1Go.transform, "RY–", new Vector2(206f, -50f), new Vector2(42f, 30f), () => NudgeRot(1, -1f));
            MakeButton(buttonTemplate, _tab1Go.transform, "RY+", new Vector2(250f, -50f), new Vector2(42f, 30f), () => NudgeRot(1, +1f));
            MakeButton(buttonTemplate, _tab1Go.transform, "RZ–", new Vector2(300f, -50f), new Vector2(42f, 30f), () => NudgeRot(2, -1f));
            MakeButton(buttonTemplate, _tab1Go.transform, "RZ+", new Vector2(344f, -50f), new Vector2(42f, 30f), () => NudgeRot(2, +1f));

            // Presets
            SmallLabel(_tab1Go.transform, "Preset", new Vector2(12f, -88f), new Vector2(48f, 28f));
            MakeButton(buttonTemplate, _tab1Go.transform, "X=0",   new Vector2( 62f, -88f), new Vector2(46f, 28f), () => SetRotAxis(0, 0f));
            MakeButton(buttonTemplate, _tab1Go.transform, "X=90",  new Vector2(112f, -88f), new Vector2(46f, 28f), () => SetRotAxis(0, 90f));
            MakeButton(buttonTemplate, _tab1Go.transform, "X=180", new Vector2(162f, -88f), new Vector2(50f, 28f), () => SetRotAxis(0, 180f));
            MakeButton(buttonTemplate, _tab1Go.transform, "Y=0",   new Vector2(218f, -88f), new Vector2(46f, 28f), () => SetRotAxis(1, 0f));
            MakeButton(buttonTemplate, _tab1Go.transform, "Y=90",  new Vector2(268f, -88f), new Vector2(46f, 28f), () => SetRotAxis(1, 90f));
            MakeButton(buttonTemplate, _tab1Go.transform, "Y=180", new Vector2(318f, -88f), new Vector2(50f, 28f), () => SetRotAxis(1, 180f));
            MakeButton(buttonTemplate, _tab1Go.transform, "Zerar", new Vector2(374f, -88f), new Vector2(86f, 28f), () => { SetRotAxis(0, 0f); SetRotAxis(1, 0f); SetRotAxis(2, 0f); });

            // ==================== TAB 2: OPTIONS & 3D ====================
            _channelBtn = MakeButton(buttonTemplate, _tab2Go.transform, ChannelLabelFor(_channel), new Vector2(12f, -12f), new Vector2(456f, 34f), CycleChannel);
            _genderBtn = MakeButton(buttonTemplate, _tab2Go.transform, GenderLabelFor(_gender), new Vector2(12f, -50f), new Vector2(456f, 34f), CycleGender);
            _encaixeBtn = MakeButton(buttonTemplate, _tab2Go.transform, EncaixeLabelFor(_additiveMode), new Vector2(12f, -90f), new Vector2(456f, 34f), CycleEncaixe);

            // Modeling
            _modelBtn = MakeButton(buttonTemplate, _tab2Go.transform, "", new Vector2(12f, -130f), new Vector2(456f, 30f), ToggleModeling);
            _modeChips[0] = MakeButton(buttonTemplate, _tab2Go.transform, ModeLabels[0], new Vector2(12f, -166f), new Vector2(145f, 30f), () => SetMode(0));
            _modeChips[1] = MakeButton(buttonTemplate, _tab2Go.transform, ModeLabels[1], new Vector2(167f, -166f), new Vector2(145f, 30f), () => SetMode(1));
            _modeChips[2] = MakeButton(buttonTemplate, _tab2Go.transform, ModeLabels[2], new Vector2(322f, -166f), new Vector2(145f, 30f), () => SetMode(2));
            RefreshModelingUi();

            // Default & TPose
            _catDefaultBtn = MakeButton(buttonTemplate, _tab2Go.transform, "", new Vector2(12f, -206f), new Vector2(220f, 30f), ToggleCategoryDefault);
            RefreshCategoryDefaultUi();
            _tPoseBtn = MakeButton(buttonTemplate, _tab2Go.transform, "", new Vector2(242f, -206f), new Vector2(220f, 30f), ToggleTPose);
            RefreshTPoseUi();

            // Tag (P10) — change this model's tag/theme from the edit panel. Type a name and "Aplicar",
            // or "Sem tag" to clear it. Persists immediately (survives reload) even without Confirmar.
            SmallLabel(_tab2Go.transform, "Tag", new Vector2(12f, -246f), new Vector2(44f, 28f));
            _tagField = MakeInput(inputTemplate, _tab2Go.transform, new Vector2(58f, -246f), new Vector2(190f, 30f), _tag, null);
            MakeButton(buttonTemplate, _tab2Go.transform, "Aplicar", new Vector2(252f, -246f), new Vector2(104f, 30f), ApplyTag);
            MakeButton(buttonTemplate, _tab2Go.transform, "Sem tag", new Vector2(360f, -246f), new Vector2(108f, 30f), ClearTag);

            // ==================== CONFIRM / CANCEL ROW ====================
            _confirmRowGo = new GameObject("ConfirmRow", typeof(RectTransform));
            _confirmRowGo.transform.SetParent(panelGo.transform, worldPositionStays: false);
            var crt = _confirmRowGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(0.5f, 1f);
            crt.sizeDelta = new Vector2(0f, _folderCtx != null ? 100f : 56f);

            var confirmBtn = MakeButton(buttonTemplate, _confirmRowGo.transform, _folderCtx != null ? "Confirmar (só esta)" : "Confirmar",
                new Vector2(12f, -10f), new Vector2(220f, 40f), Confirm);
            var cancelBtn = MakeButton(buttonTemplate, _confirmRowGo.transform, "Cancelar", new Vector2(242f, -10f), new Vector2(220f, 40f), Cancel);

            if (_folderCtx != null)
            {
                int total = Mathf.Max(1, _folderCtx.TotalCount);
                var folderBtn = MakeButton(buttonTemplate, _confirmRowGo.transform,
                    Loc.T("Aplicar a toda a pasta") + " (" + total + ")",
                    new Vector2(12f, -54f), new Vector2(456f, 40f), ApplyToFolder);
            }

            ApplyScale(startScale);
            _attachment.SetUserScaleAxis(startScaleAxis);
            _attachment.SetUserOffset(startOffset);
            _attachment.SetUserEuler(startEuler);

            // Select first tab by default & layout
            SetTab(0);
            RefreshLabel();
        }

        // ---- actions ----

        private void ApplyScale(float scale)
        {
            if (_attachment != null) _attachment.SetUserScale(scale);
            RefreshLabel();
        }

        private void NudgeScale(float factor)
        {
            if (_attachment == null) return;
            ApplyScale(_attachment.UserScale * factor);
            RefreshFields();
        }

        private void ApplyScaleAxisFromFields()
        {
            if (_attachment == null) return;
            Vector3 cur = _attachment.UserScaleAxis;
            float x = TryParse(_sxField?.input.text, out var vx) ? vx : cur.x;
            float y = TryParse(_syField?.input.text, out var vy) ? vy : cur.y;
            float z = TryParse(_szField?.input.text, out var vz) ? vz : cur.z;
            _attachment.SetUserScaleAxis(new Vector3(x, y, z));
            RefreshLabel();
        }

        private void InvertScaleAxis(int axis)
        {
            if (_attachment == null) return;
            Vector3 s = _attachment.UserScaleAxis;
            if (axis == 0) s.x = -s.x;
            else if (axis == 1) s.y = -s.y;
            else if (axis == 2) s.z = -s.z;
            _attachment.SetUserScaleAxis(s);
            RefreshScaleAxisFields();
            RefreshLabel();
        }

        private void RefreshScaleAxisFields()
        {
            if (_attachment == null) return;
            Vector3 s = _attachment.UserScaleAxis;
            _sxField?.SetValueWithoutNotify(Fmt(s.x));
            _syField?.SetValueWithoutNotify(Fmt(s.y));
            _szField?.SetValueWithoutNotify(Fmt(s.z));
        }

        private void ApplyEulerFromFields()
        {
            if (_attachment == null) return;
            Vector3 cur = _attachment.UserEuler;
            float x = TryParse(_rxField?.input.text, out var vx) ? vx : cur.x;
            float y = TryParse(_ryField?.input.text, out var vy) ? vy : cur.y;
            float z = TryParse(_rzField?.input.text, out var vz) ? vz : cur.z;
            _attachment.SetUserEuler(new Vector3(x, y, z));
            RefreshLabel();
        }

        private void ApplyOffsetFromFields()
        {
            if (_attachment == null) return;
            Vector3 cur = _attachment.UserOffset;
            float x = TryParse(_xField?.input.text, out var vx) ? vx : cur.x;
            float y = TryParse(_yField?.input.text, out var vy) ? vy : cur.y;
            float z = TryParse(_zField?.input.text, out var vz) ? vz : cur.z;
            _attachment.SetUserOffset(new Vector3(x, y, z));
            RefreshLabel();
        }

        /// <summary>Nudge one position axis by ±the "Passo" step (fine, adjustable control).</summary>
        private void NudgePos(int axis, float dir)
        {
            if (_attachment == null) return;
            float step = PosStep();
            Vector3 o = _attachment.UserOffset;
            if (axis == 0) o.x += dir * step;
            else if (axis == 1) o.y += dir * step;
            else o.z += dir * step;
            _attachment.SetUserOffset(o);
            RefreshOffsetFields();
            RefreshLabel();
        }

        private float PosStep()
        {
            if (TryParse(_stepField?.input.text, out var v) && v > 1e-5f) return v;
            return 0.05f;
        }

        private void NudgeRot(int axis, float dir)
        {
            if (_attachment == null) return;
            float step = RotStep();
            Vector3 e = _attachment.UserEuler;
            if (axis == 0) e.x += dir * step;
            else if (axis == 1) e.y += dir * step;
            else e.z += dir * step;
            _attachment.SetUserEuler(e);
            RefreshRotationFields();
            RefreshLabel();
        }

        private float RotStep()
        {
            if (TryParse(_rotStepField?.input.text, out var v) && v > 1e-3f) return v;
            return 15f;
        }

        private void SetRotAxis(int axis, float degrees)
        {
            if (_attachment == null) return;
            Vector3 e = _attachment.UserEuler;
            if (axis == 0) e.x = degrees;
            else if (axis == 1) e.y = degrees;
            else e.z = degrees;
            _attachment.SetUserEuler(e);
            RefreshRotationFields();
            RefreshLabel();
        }

        private void RefreshOffsetFields()
        {
            if (_attachment == null) return;
            Vector3 o = _attachment.UserOffset;
            _xField?.SetValueWithoutNotify(Fmt(o.x));
            _yField?.SetValueWithoutNotify(Fmt(o.y));
            _zField?.SetValueWithoutNotify(Fmt(o.z));
        }

        private void CycleGender()
        {
            int i = Array.IndexOf(GenderTags, _gender);
            if (i < 0) i = 0;
            _gender = GenderTags[(i + 1) % GenderTags.Length];

            // Apply live: update the catalog tag and re-filter the tab so it shows/hides accordingly.
            if (_attachment != null && _attachment.Part != null)
                CustomPartCatalog.SetGender(_attachment.Part.PartId, _gender);
            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator != null && creator.itemTabsLoader != null) creator.itemTabsLoader.Refresh();

            SetGenderButtonLabel();
        }

        private void CycleChannel()
        {
            int i = Array.IndexOf(Channels, _channel);
            if (i < 0) i = 0;
            _channel = Channels[(i + 1) % Channels.Length];

            // Live: this is the channel the SetColor broadcast will match against (Patch_Paint).
            // Untextured parts respond to every channel through the shared material regardless; for
            // textured parts this decides which colour picker tints them from now on.
            if (_attachment != null && _attachment.Part != null)
                _attachment.Part.ChannelId = _channel;

            SetChannelButtonLabel();
        }

        /// <summary>P14 — cycle attach mode: Auto → Acessório (additive) → Substitui. Re-attaches the
        /// part live so the base is kept (accessory) or the slot cleared (replace).</summary>
        private void CycleEncaixe()
        {
            _additiveMode = (_additiveMode + 1) % 3;
            ApplyAdditiveMode(_additiveMode);
            SetEncaixeButtonLabel();
        }

        private void ApplyAdditiveMode(int mode)
        {
            if (_attachment == null || _attachment.Part == null) return;
            var part = _attachment.Part;
            part.AdditiveOverride = mode;

            bool wanted = AccessoryMap.ResolveAdditive(part.CategoryPath, mode);
            if (wanted == part.Additive) return; // nothing structural changes
            part.Additive = wanted;

            // Rebuild the attachment so Build re-runs with the new flag: additive keeps the base part
            // (skips RemoveAllChildren); replace clears its slot. Re-link the panel to the new instance.
            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator == null || creator.dummy == null) return;
            string id = part.PartId;
            if (creator.dummy.Contains(id)) creator.SpawnAlongside(id); // remove current attachment
            creator.SpawnAlongside(id);                                 // re-add -> fresh Build
            if (creator.dummy.attachedItems.TryGetValue(id, out var att) && att is CustomBodyPartAttachment c)
                _attachment = c;
        }

        private void SetEncaixeButtonLabel()
        {
            if (_encaixeBtn == null) return;
            var txt = _encaixeBtn.GetComponentInChildren<TMP_Text>(true);
            if (txt != null) txt.text = Loc.T(EncaixeLabelFor(_additiveMode));
        }

        private static string EncaixeLabelFor(int mode)
            => EncaixeLabels[mode < 0 || mode >= EncaixeLabels.Length ? 0 : mode];

        /// <summary>P10 — set this part's tag from the typed field. Updates the live part, persists it to
        /// scales.json immediately (so it survives reload even without Confirmar), registers the tag so
        /// its chip appears in the tag bar, and refreshes the tab so the tag filter re-evaluates.</summary>
        private void ApplyTag()
        {
            if (_attachment == null || _attachment.Part == null) return;
            string t = _tagField != null && _tagField.input != null ? (_tagField.input.text ?? "").Trim() : "";
            SetTag(t);
            if (!string.IsNullOrEmpty(t)) TagManager.NoteTag(t);
            Compat.ShowSuccess(string.IsNullOrEmpty(t)
                ? Loc.T("Tag removida.")
                : Loc.T("Tag aplicada:") + " " + t);
        }

        /// <summary>P10 — clear this part's tag.</summary>
        private void ClearTag()
        {
            if (_attachment == null || _attachment.Part == null) return;
            SetTag("");
            _tagField?.SetValueWithoutNotify("");
            Compat.ShowSuccess(Loc.T("Tag removida."));
        }

        private void SetTag(string t)
        {
            _tag = t ?? "";
            _attachment.Part.Tag = _tag;
            if (!string.IsNullOrEmpty(_attachment.Part.SourceKey))
                ScaleStore.TryUpdateTag(_attachment.Part.SourceKey, _tag);

            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator != null && creator.itemTabsLoader != null) creator.itemTabsLoader.Refresh();
        }

        /// <summary>Writes this part's exact placement (per-model record) and photographs it for the tab
        /// icon. Shared by "Confirmar" and the folder-apply path (the previewed piece is committed the
        /// same way a single import would be).</summary>
        private void PersistPerModel()
        {
            if (_attachment == null) return;
            var p = _attachment.Part;

            // Per-model: exact absolute scale/axis/rotation/offset/gender/texture + enough to
            // REBUILD it next session (model path, category, slot). Reload reproduces it exactly.
            ScaleStore.Set(_storeKey, new PartTransform
            {
                scale = _attachment.UserScale,
                scaleAxis = _attachment.UserScaleAxis,
                offset = _attachment.UserOffset,
                euler = _attachment.UserEuler,
                gender = _gender,
                channel = _channel,
                texturePath = _texturePath,
                textureVariants = p != null ? p.TextureVariants.ToArray() : null,
                activeVariant = p != null ? p.ActiveVariant : 0,
                modelPath = p != null ? p.ModelPath : null,
                category = p != null ? p.CategoryPath : null,
                slot = p != null ? p.Slot.ToString() : null,
                additive = _additiveMode, // P14 attach-mode override
                tag = p != null ? p.Tag : null, // P10 user tag
                link = p != null ? p.LinkGroupId : null,
            });

            // P7 — photograph just this part (framed on it) and use it as the tab-button icon, then
            // repaint the button so the picture shows immediately.
            Thumbnailer.Capture(_attachment);
            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator != null && creator.itemTabsLoader != null) creator.itemTabsLoader.Refresh();
        }

        /// <summary>Mass-import preview: commit the previewed piece, then import every remaining .obj in
        /// the folder using the values the user just dialed in here (scale multiplier, per-axis, rotation,
        /// position, gender, channel). No per-part editing needed afterwards.</summary>
        private void ApplyToFolder()
        {
            if (_attachment == null || _folderCtx == null) { Close(); return; }

            var p = _attachment.Part;
            PersistPerModel(); // the previewed piece is committed like a normal single import

            // Store scale as a MULTIPLIER over each mesh's normalized base so it carries to the other
            // (possibly differently-sized) models in the folder and still lands visible.
            float baseN = p != null ? p.NormalizedScale : 0f;
            float mult = baseN > 1e-6f ? _attachment.UserScale / baseN : 1f;
            var settings = new MassImportSettings
            {
                ScaleMultiplier = mult,
                ScaleAxis = _attachment.UserScaleAxis,
                Euler = _attachment.UserEuler,
                Offset = _attachment.UserOffset,
                Gender = _gender,
                Channel = _channel,
                SideLeft = _folderCtx.SideLeft,
            };

            var ctx = _folderCtx;
            Close();
            MassImportFlow.ImportRemaining(ctx, settings);
        }

        private void Confirm()
        {
            if (_attachment != null)
            {
                var p = _attachment.Part;

                PersistPerModel();

                // Scale is stored as a MULTIPLIER over this mesh's normalized base, so it carries to the
                // next (possibly differently-scaled) model correctly and always lands visible. Where it
                // goes depends on the checkbox: ticked pins the CATEGORY default (overrides the global
                // for this tab); left unticked (re)calibrates the GLOBAL factor for every future import.
                float baseN = p != null ? p.NormalizedScale : 0f;
                float mult = baseN > 1e-6f ? _attachment.UserScale / baseN : 1f;
                if (_saveCategoryDefault && p != null)
                {
                    ScaleStore.SetCategoryDefault(p.CategoryPath, p.Slot, _gender, mult,
                        _attachment.UserScaleAxis, _attachment.UserEuler, _attachment.UserOffset);
                    Compat.ShowSuccess(Loc.T("Padrão desta categoria salvo — novos modelos deste tipo virão assim.") + " (" + _displayName + ")");
                }
                else
                {
                    ScaleStore.SetGlobalMult(mult, _attachment.UserOffset);
                    Compat.ShowSuccess(Loc.T("Escala confirmada e definida como padrão global.") + " (" + _displayName + ")");
                }

                // Apply changes live to any other characters currently on the map wearing this exact same part.
                // We update both: (a) our live CustomBodyPartAttachment objects via SetUser*, and
                // (b) the underlying CustomPart metadata (already done in SetUser*), so any future
                // AddPart rebuild uses the new values.
                if (p != null)
                {
                    var allParts = FindObjectsOfType<CustomBodyPartAttachment>();
                    int updated = 0;
                    foreach (var part in allParts)
                    {
                        if (part != _attachment && part.Part != null && part.Part.SourceKey == p.SourceKey)
                        {
                            part.SetUserScale(_attachment.UserScale);
                            part.SetUserScaleAxis(_attachment.UserScaleAxis);
                            part.SetUserEuler(_attachment.UserEuler);
                            part.SetUserOffset(_attachment.UserOffset);
                            updated++;
                        }
                    }
                    Plugin.Log.LogInfo($"[confirm] Updated {updated} other instance(s) of '{p.SourceKey}' on the map (found {allParts.Length} total attachments).");
                }
            }
            Close();
        }

        /// <summary>Revert the live instance to the values the panel opened with and close — nothing is
        /// persisted (the part stays imported; use the trash button to remove it).</summary>
        private void Cancel()
        {
            if (_attachment != null)
            {
                _attachment.SetUserScale(_startScale);
                _attachment.SetUserScaleAxis(_startScaleAxis);
                _attachment.SetUserOffset(_startOffset);
                _attachment.SetUserEuler(_startEuler);
            }
            Close();
        }

        private void ToggleCategoryDefault()
        {
            _saveCategoryDefault = !_saveCategoryDefault;
            RefreshCategoryDefaultUi();
        }

        private void RefreshCategoryDefaultUi()
        {
            if (_catDefaultBtn == null) return;
            var t = _catDefaultBtn.GetComponentInChildren<TMP_Text>(true);
            if (t != null)
                t.text = Loc.T(_saveCategoryDefault
                    ? "[X] Salvar como padrão desta categoria"
                    : "[ ] Salvar como padrão desta categoria");
            var img = _catDefaultBtn.GetComponent<Image>();
            if (img != null) img.color = _saveCategoryDefault ? ModelOn : ModelOff;
        }

        private void ToggleTPose()
        {
            _tPoseActive = !_tPoseActive;
            ApplyTPose(_tPoseActive);
            RefreshTPoseUi();
        }

        private void ApplyTPose(bool active)
        {
            var character = _attachment != null ? _attachment.GetComponentInParent<PickupableCharacter>() : null;
            if (character == null) return;
            var animator = character.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.enabled = !active;
                if (active)
                {
                    animator.Rebind();
                }
            }
        }

        private void RefreshTPoseUi()
        {
            if (_tPoseBtn == null) return;
            var t = _tPoseBtn.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.text = Loc.T(_tPoseActive ? "[X] Pose T ativa" : "[ ] Forçar Pose T");
            var img = _tPoseBtn.GetComponent<Image>();
            if (img != null) img.color = _tPoseActive ? ModelOn : ModelOff;
        }

        private void ToggleCollapse()
        {
            _collapsed = !_collapsed;
            RefreshPanelLayout();
        }

        private void SetTab(int tabIndex)
        {
            _activeTab = tabIndex;
            for (int i = 0; i < 3; i++)
            {
                if (_tabButtons[i] != null)
                {
                    var img = _tabButtons[i].GetComponent<Image>();
                    if (img != null) img.color = (i == _activeTab) ? ChipActive : ChipIdle;
                }
            }
            RefreshPanelLayout();
        }

        private void RefreshPanelLayout()
        {
            if (_panelRt == null) return;

            float contentHeight = 220f;
            if (_collapsed)
            {
                contentHeight = 0f;
            }
            else
            {
                if (_activeTab == 0) contentHeight = 206f; // Tab 0 has inputs up to -164 (164 + 30 + 12 = 206)
                else if (_activeTab == 1) contentHeight = 128f; // Tab 1 has inputs up to -88 (88 + 28 + 12 = 128)
                else contentHeight = 288f; // Tab 2 has inputs up to -246 (246 + 30 + 12 = 288)
            }

            float totalHeight = 40f; // Header
            if (!_collapsed)
            {
                totalHeight += 36f; // Tabs row
                totalHeight += contentHeight; // Content
                totalHeight += 12f; // spacing
                totalHeight += 56f; // Confirm/Cancel row (height 40 + 16 spacing)
                if (_folderCtx != null)
                {
                    totalHeight += 44f; // Apply to folder row
                }
            }

            _panelRt.sizeDelta = new Vector2(480f, totalHeight);

            // Hide/show tab row and containers
            if (_tabsRowGo != null) _tabsRowGo.SetActive(!_collapsed);
            if (_tab0Go != null) _tab0Go.SetActive(!_collapsed && _activeTab == 0);
            if (_tab1Go != null) _tab1Go.SetActive(!_collapsed && _activeTab == 1);
            if (_tab2Go != null) _tab2Go.SetActive(!_collapsed && _activeTab == 2);
            if (_confirmRowGo != null) _confirmRowGo.SetActive(!_collapsed);

            if (!_collapsed)
            {
                // Position confirm row container at the bottom
                var crt = _confirmRowGo.GetComponent<RectTransform>();
                if (crt != null)
                {
                    crt.sizeDelta = new Vector2(0f, _folderCtx != null ? 100f : 56f);
                    crt.anchoredPosition = new Vector2(0f, -(40f + 36f + contentHeight + 12f));
                }
            }

            if (_collapseBtnText != null)
            {
                _collapseBtnText.text = _collapsed ? "▼" : "▲";
            }
        }

        // ---- P11 modeling mode (drag the part in the preview) ----

        private void ToggleModeling()
        {
            if (!_modeling)
            {
                if (!ResolvePreview()) { Compat.ShowError("Não achei o preview do personagem para modelar."); return; }
                _modeling = true;
            }
            else
            {
                _modeling = false;
                _dragging = false;
            }
            RefreshModelingUi();
        }

        private void SetMode(int m)
        {
            _mode = Mathf.Clamp(m, 0, 2);
            RefreshModelingUi();
        }

        private void RefreshModelingUi()
        {
            if (_modelBtn != null)
            {
                var t = _modelBtn.GetComponentInChildren<TMP_Text>(true);
                if (t != null) t.text = Loc.T(_modeling ? "[X] Modo modelagem ATIVO — arraste a peça" : "[ ] Ativar modo modelagem");
                var img = _modelBtn.GetComponent<Image>();
                if (img != null) img.color = _modeling ? ModelOn : ModelOff;
            }
            for (int i = 0; i < _modeChips.Length; i++)
            {
                var img = _modeChips[i] != null ? _modeChips[i].GetComponent<Image>() : null;
                if (img != null) img.color = (_modeling && i == _mode) ? ChipActive : ChipIdle;
            }
        }

        /// <summary>Grab the preview camera + its UI panel so mouse drags can be projected into the world.</summary>
        private bool ResolvePreview()
        {
            var creator = UniqueMono<CharacterCreator>.instance;
            var camc = creator != null && creator.creatorCam != null
                ? creator.creatorCam : UniqueMono<RpgEngine.CharacterCreatorCamera>.instance;
            if (camc == null) return false;
            _previewCam = camc.cam;
            _previewRect = camc.inputPanel != null ? camc.inputPanel.transform as RectTransform : null;
            if (_previewCam == null || _previewRect == null) return false;
            var canvas = _previewRect.GetComponentInParent<Canvas>();
            _uiCam = (canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.rootCanvas.worldCamera : null;
            return true;
        }

        private void HandleModelingDrag()
        {
            if (_attachment == null || _previewCam == null || _previewRect == null) return;
            Vector3 mouse = Input.mousePosition;

            if (Input.GetMouseButtonDown(0))
            {
                // Start only when pressing on the preview and NOT on the panel controls.
                if (OverRect(_previewRect, mouse) && !OverRect(_panelRt, mouse))
                {
                    _dragging = true;
                    _lastMouse = mouse;
                    _dragStartMouse = mouse;
                    _dragLockedAxis = -1;
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _dragging = false;
                _dragLockedAxis = -1;
            }

            if (!_dragging || !Input.GetMouseButton(0)) return;

            // Lock axis based on dominant direction if not locked yet
            if (_dragLockedAxis == -1)
            {
                Vector2 totalDelta = (Vector2)mouse - (Vector2)_dragStartMouse;
                if (totalDelta.magnitude > 5f) // 5 pixels threshold
                {
                    _dragLockedAxis = Mathf.Abs(totalDelta.x) > Mathf.Abs(totalDelta.y) ? 0 : 1;
                }
            }

            Vector2 d = (Vector2)mouse - (Vector2)_lastMouse;
            if (d.sqrMagnitude < 1e-4f) return;

            switch (_mode)
            {
                case 0: DragMove(_lastMouse, mouse); break;
                case 1: DragScale(d.x, d.y); break;
                case 2: DragRotate(d.x, d.y); break;
            }
            _lastMouse = mouse;
        }

        private void DragMove(Vector3 fromMouse, Vector3 toMouse)
        {
            Vector3 center = PartCenter();
            var plane = new Plane(_previewCam.transform.forward, center);
            if (!TryPlanePoint(plane, fromMouse, out var pf)) return;
            if (!TryPlanePoint(plane, toMouse, out var pt)) return;
            _attachment.AddWorldOffset(pt - pf);
            RefreshOffsetFields();
            RefreshLabel();
        }

        private void DragScale(float dxPixels, float dyPixels)
        {
            if (_attachment == null || _previewCam == null) return;
            if (_dragLockedAxis == -1) return; // wait until drag direction is locked

            float dx = _dragLockedAxis == 0 ? dxPixels : 0f;
            float dy = _dragLockedAxis == 1 ? dyPixels : 0f;

            Transform t = _attachment.transform;
            // Project camera axes into the part's local space
            Vector3 localCamRight = t.InverseTransformDirection(_previewCam.transform.right);
            Vector3 localCamUp = t.InverseTransformDirection(_previewCam.transform.up);

            // Get absolute alignment weights for each local axis
            Vector3 absRight = new Vector3(Mathf.Abs(localCamRight.x), Mathf.Abs(localCamRight.y), Mathf.Abs(localCamRight.z));
            Vector3 absUp = new Vector3(Mathf.Abs(localCamUp.x), Mathf.Abs(localCamUp.y), Mathf.Abs(localCamUp.z));

            // Normalize weights so the maximum weight is 1.0f
            float maxR = Mathf.Max(absRight.x, Mathf.Max(absRight.y, absRight.z));
            if (maxR > 0f) absRight /= maxR;
            float maxU = Mathf.Max(absUp.x, Mathf.Max(absUp.y, absUp.z));
            if (maxU > 0f) absUp /= maxU;

            // Calculate scale multipliers
            Vector3 currentAxis = _attachment.UserScaleAxis;
            Vector3 nextAxis = currentAxis;

            if (_dragLockedAxis == 0)
            {
                // Apply horizontal drag (dx) to local axes aligned with screen-horizontal (e.g. width/girth)
                nextAxis.x *= Mathf.Exp(dx * 0.004f * absRight.x);
                nextAxis.y *= Mathf.Exp(dx * 0.004f * absRight.y);
                nextAxis.z *= Mathf.Exp(dx * 0.004f * absRight.z);
            }
            else if (_dragLockedAxis == 1)
            {
                // Apply vertical drag (dy) to local axes aligned with screen-vertical (e.g. height/length)
                nextAxis.x *= Mathf.Exp(dy * 0.004f * absUp.x);
                nextAxis.y *= Mathf.Exp(dy * 0.004f * absUp.y);
                nextAxis.z *= Mathf.Exp(dy * 0.004f * absUp.z);
            }

            _attachment.SetUserScaleAxis(nextAxis);
            RefreshScaleAxisFields();
            RefreshLabel();
        }

        private void DragRotate(float dxPixels, float dyPixels)
        {
            if (_attachment == null || _previewCam == null) return;

            // Get current local rotation from UserEuler
            Quaternion currentLocalRot = Quaternion.Euler(_attachment.UserEuler);
            // Convert to world rotation using parent's rotation
            Transform parent = _attachment.transform.parent;
            Quaternion currentWorldRot = parent != null ? parent.rotation * currentLocalRot : currentLocalRot;

            // Apply camera-relative delta rotation in world space
            Vector3 camUp = _previewCam.transform.up;
            Vector3 camRight = _previewCam.transform.right;
            Quaternion deltaRot = Quaternion.AngleAxis(-dyPixels * 0.4f, camRight) * Quaternion.AngleAxis(dxPixels * 0.4f, camUp);
            Quaternion newWorldRot = deltaRot * currentWorldRot;

            // Convert back to local rotation
            Quaternion newLocalRot = parent != null ? Quaternion.Inverse(parent.rotation) * newWorldRot : newWorldRot;

            // Update UserEuler
            _attachment.SetUserEuler(newLocalRot.eulerAngles);
            RefreshRotationFields();
            RefreshLabel();
        }

        private Vector3 PartCenter()
        {
            var r = _attachment.GetComponent<Renderer>();
            return r != null ? r.bounds.center : _attachment.transform.position;
        }

        private bool TryPlanePoint(Plane plane, Vector3 mouse, out Vector3 world)
        {
            world = Vector3.zero;
            if (!ToViewport(mouse, out var vp)) return false;
            Ray ray = _previewCam.ViewportPointToRay(vp);
            if (!plane.Raycast(ray, out float enter)) return false;
            world = ray.GetPoint(enter);
            return true;
        }

        private bool ToViewport(Vector3 mouse, out Vector2 vp)
        {
            vp = Vector2.zero;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_previewRect, mouse, _uiCam, out var local)) return false;
            Rect r = _previewRect.rect;
            if (r.width <= 0f || r.height <= 0f) return false;
            vp = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
            return true;
        }

        private bool OverRect(RectTransform rt, Vector3 screen)
            => rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screen, _uiCam);

        private void RefreshRotationFields()
        {
            if (_attachment == null) return;
            Vector3 e = _attachment.UserEuler;
            _rxField?.SetValueWithoutNotify(Fmt(e.x));
            _ryField?.SetValueWithoutNotify(Fmt(e.y));
            _rzField?.SetValueWithoutNotify(Fmt(e.z));
        }



        // Texture variants (P13) live in the top-center VariantBar (shown when the part is selected),
        // so the panel no longer hosts them. Confirm still persists part.TextureVariants below.

        private void RefreshLabel()
        {
        }

        private void RefreshFields()
        {
            if (_attachment == null) return;
            _scaleField?.SetValueWithoutNotify(Fmt(_attachment.UserScale));
        }

        private void SetGenderButtonLabel()
        {
            if (_genderBtn == null) return;
            var txt = _genderBtn.GetComponentInChildren<TMP_Text>(true);
            if (txt != null) txt.text = Loc.T(GenderLabelFor(_gender));
        }

        private static string GenderLabelFor(string tag)
        {
            int i = Array.IndexOf(GenderTags, NormalizeGender(tag));
            return GenderLabels[i < 0 ? 0 : i];
        }

        private void SetChannelButtonLabel()
        {
            if (_channelBtn == null) return;
            var txt = _channelBtn.GetComponentInChildren<TMP_Text>(true);
            if (txt != null) txt.text = Loc.T(ChannelLabelFor(_channel));
        }

        private static string ChannelLabelFor(string channel)
        {
            int i = Array.IndexOf(Channels, channel);
            return "Paleta de cor: " + ChannelLabels[i < 0 ? 0 : i];
        }

        private static string NormalizeGender(string tag)
        {
            if (string.Equals(tag, "Feminine", StringComparison.OrdinalIgnoreCase)) return "Feminine";
            if (string.Equals(tag, "Masculine", StringComparison.OrdinalIgnoreCase)) return "Masculine";
            return "";
        }

        private void Update()
        {
            if (_attachment == null) { Close(); return; }
            if (_panelRt != null) _lastPanelPos = _panelRt.anchoredPosition; // remember drag position
            if (AnyFieldFocused()) return; // don't hijack keys while typing

            // P11 — modeling mode: keys 2/3/4 pick move/thickness/rotation; drag the part in the preview.
            if (_modeling)
            {
                if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SetMode(0);
                else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SetMode(1);
                else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) SetMode(2);
                HandleModelingDrag();
            }

            // Enter only commits the value being typed (handled by the field's onEndEdit) — it does
            // NOT confirm. Confirming is only via the buttons. Esc just closes the panel.
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Plus))
                NudgeScale(1.1f);
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                NudgeScale(1f / 1.1f);
            if (Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        private bool AnyFieldFocused()
        {
            return IsFocused(_scaleField)
                || IsFocused(_sxField) || IsFocused(_syField) || IsFocused(_szField)
                || IsFocused(_xField) || IsFocused(_yField) || IsFocused(_zField)
                || IsFocused(_rxField) || IsFocused(_ryField) || IsFocused(_rzField)
                || IsFocused(_stepField) || IsFocused(_rotStepField) || IsFocused(_tagField);
        }

        private static bool IsFocused(UiInputField f) => f != null && f.input != null && f.input.isFocused;

        // ---- builders ----

        /// <summary>
        /// A transparent, full-panel <see cref="UiButton"/> behind all controls. It exists only to be
        /// the frontmost UiButton for SlickUi's pointer system so pressing the panel doesn't fall
        /// through to the preview (which would rotate the camera). It has no click/drag behaviour;
        /// moving the panel is done by the root <see cref="DragHandle"/> (events bubble up to it).
        /// </summary>
        private void AddDragSurface(GameObject buttonTemplate, Transform panel)
        {
            if (buttonTemplate == null) return;

            var surfGo = UnityEngine.Object.Instantiate(buttonTemplate, panel);
            surfGo.name = "DragSurface";

            var surfBtn = surfGo.GetComponent<UiButton>();
            if (surfBtn == null) { UnityEngine.Object.Destroy(surfGo); return; }

            try
            {
                surfBtn.onLeftMouseClick.RemoveAllListeners();
                surfBtn.whileMouseDrag.RemoveAllListeners();
                surfBtn.whileLeftMouseHeld.RemoveAllListeners();
            }
            catch { }

            var srt = surfGo.GetComponent<RectTransform>();
            if (srt != null)
            {
                srt.anchorMin = Vector2.zero;
                srt.anchorMax = Vector2.one;
                srt.offsetMin = Vector2.zero;
                srt.offsetMax = Vector2.zero;
            }

            // Invisible but still raycastable; hide the cloned template's icon/label graphics.
            var simg = surfGo.GetComponent<Image>();
            if (simg != null) { simg.sprite = null; simg.color = new Color(0f, 0f, 0f, 0f); simg.raycastTarget = true; }
            foreach (var img in surfGo.GetComponentsInChildren<Image>(true))
                if (img.gameObject != surfGo) img.enabled = false;
            foreach (var raw in surfGo.GetComponentsInChildren<RawImage>(true)) raw.enabled = false;
            foreach (var t in surfGo.GetComponentsInChildren<TMP_Text>(true)) t.enabled = false;

            surfGo.transform.SetAsFirstSibling(); // behind all controls
        }

        private UiButton MakeButton(GameObject template, Transform parent, string label, Vector2 pos, Vector2 size, Action onClick)
        {
            UiButton btn = UiFactory.TextButton(template, parent, label, _ => onClick());
            if (btn == null) return null;
            var rt = btn.GetComponent<RectTransform>();
            if (rt != null) PlaceTopLeft(rt, pos, size);
            return btn;
        }

        private UiInputField MakeInput(GameObject template, Transform parent, Vector2 pos, Vector2 size,
            string initial, UnityAction<string> onEndEdit)
        {
            if (template == null) return null;
            var cloneGo = UnityEngine.Object.Instantiate(template, parent);
            cloneGo.name = "Input";

            var field = cloneGo.GetComponent<UiInputField>();
            if (field == null || field.input == null)
            {
                UnityEngine.Object.Destroy(cloneGo);
                return null;
            }

            var rt = cloneGo.GetComponent<RectTransform>();
            if (rt != null) PlaceTopLeft(rt, pos, size);

            // Neutralize any inherited (persistent or runtime) handlers, then wire ours.
            Neutralize(field.input.onEndEdit);
            Neutralize(field.input.onValueChanged);
            field.SetValueWithoutNotify(initial);
            if (onEndEdit != null) field.onEndEdit.AddListener(onEndEdit);

            return field;
        }

        private static void Neutralize(UnityEventBase ev)
        {
            try
            {
                for (int i = 0; i < ev.GetPersistentEventCount(); i++)
                    ev.SetPersistentListenerState(i, UnityEventCallState.Off);
            }
            catch { }
        }

        private static void Neutralize(UnityEvent<string> ev)
        {
            Neutralize((UnityEventBase)ev);
            ev.RemoveAllListeners();
        }

        private TMP_Text SmallLabel(Transform parent, string text, Vector2 pos, Vector2 size)
        {
            var t = UiFactory.Label(parent, text, fill: false);
            PlaceTopLeft(t.rectTransform, pos, size);
            t.enableAutoSizing = false;
            t.fontSize = 15f;
            t.alignment = TextAlignmentOptions.Left;
            return t;
        }

        private static void PlaceTopLeft(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        // ---- parsing ----

        private static bool TryParse(string s, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim().Replace(',', '.');
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string Fmt(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private void OnDestroy()
        {
            VariantBar.Hide();
        }
    }
}
