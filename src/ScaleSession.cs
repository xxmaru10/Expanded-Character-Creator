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
    /// the top-center VariantBar shown on selection. "Salvar padrão"
    /// persists it as the model's default (<see cref="ScaleStore"/>); "Só desta vez" applies it to
    /// the current instance only, without persisting. One active at a time.
    /// </summary>
    internal class ScaleSession : MonoBehaviour
    {
        private static ScaleSession _current;

        // Remember where the user dragged the panel so it reopens in the same spot this session.
        private static Vector2 _lastPanelPos = new Vector2(0f, -60f);

        private RectTransform _panelRt;

        private CustomBodyPartAttachment _attachment;
        private string _storeKey;
        private string _displayName;

        private TMP_Text _label;
        private UiInputField _scaleField;
        private UiInputField _sxField, _syField, _szField; // per-axis scale multiplier (P4)
        private UiInputField _xField, _yField, _zField;    // offset
        private UiInputField _rxField, _ryField, _rzField; // rotation (P5)
        private UiInputField _stepField;                   // position nudge step (fine control)
        private UiButton _genderBtn;                       // gender cycle (P3)
        private UiButton _channelBtn;                      // paint channel cycle (P2)
        private UiButton _encaixeBtn;                      // attach-mode cycle (P14)
        private string _texturePath;
        private string _gender = "";                       // "", "Feminine", "Masculine"
        private string _channel = ChannelMap.Primary;      // paint channel id (P2)
        private int _additiveMode;                         // P14: 0=auto, 1=accessory(additive), 2=replace

        private static readonly string[] EncaixeLabels =
            { "Encaixe: Auto", "Encaixe: Acessório (por cima)", "Encaixe: Substitui o slot" };

        // P11 — drag-to-model mode: grab the part in the preview and drag to move/scale/rotate it.
        // Toggle with the "Ativar modo modelagem" checkbox; keys 2/3/4 pick the axis of action.
        private GameObject _buttonTemplate;                // remembered clone source for the drag blocker
        private UiButton _modelBtn;                         // the "checkbox" toggle
        private readonly UiButton[] _modeChips = new UiButton[3]; // 2=posição, 3=grossura, 4=rotação
        private bool _modeling;
        private int _mode;                                 // 0=position, 1=thickness(scale), 2=rotation
        private GameObject _blocker;                       // absorbs SlickUi presses so the camera won't spin
        private bool _dragging;
        private Vector3 _lastMouse;
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
            float startScale, Vector3 startOffset, Vector3 startEuler, Vector3 startScaleAxis, string genderTag)
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
            _current.BuildUi(panelGo, buttonTemplate, inputTemplate, attachment, storeKey, displayName,
                startScale, startOffset, startEuler, startScaleAxis, genderTag);
        }

        internal static void Close()
        {
            if (_current != null)
            {
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

            var rt = panelGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(480f, 492f);
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

            var hint = UiFactory.Label(panelGo.transform, "arraste para mover", fill: false);
            PlaceTopLeft(hint.rectTransform, new Vector2(300f, -8f), new Vector2(170f, 22f));
            hint.enableAutoSizing = false;
            hint.fontSize = 12f;
            hint.alignment = TextAlignmentOptions.Right;
            hint.color = new Color(0.75f, 0.78f, 0.9f, 1f);

            _label = UiFactory.Label(panelGo.transform, "", fill: false);
            PlaceTopLeft(_label.rectTransform, new Vector2(10f, -6f), new Vector2(280f, 40f));
            _label.enableAutoSizing = false;
            _label.fontSize = 15f;
            _label.alignment = TextAlignmentOptions.TopLeft;

            // Uniform scale row.
            SmallLabel(panelGo.transform, "Escala", new Vector2(12f, -52f), new Vector2(70f, 28f));
            _scaleField = MakeInput(inputTemplate, panelGo.transform, new Vector2(86f, -52f), new Vector2(150f, 30f),
                Fmt(startScale), s => { if (TryParse(s, out var v)) ApplyScale(v); RefreshFields(); });
            MakeButton(buttonTemplate, panelGo.transform, "–", new Vector2(240f, -52f), new Vector2(44f, 30f), () => NudgeScale(1f / 1.25f));
            MakeButton(buttonTemplate, panelGo.transform, "+", new Vector2(288f, -52f), new Vector2(44f, 30f), () => NudgeScale(1.25f));

            // Per-axis scale row (P4): multiplier on top of the uniform scale, so 1/1/1 = no stretch.
            SmallLabel(panelGo.transform, "Esc XYZ", new Vector2(12f, -90f), new Vector2(70f, 28f));
            _sxField = MakeInput(inputTemplate, panelGo.transform, new Vector2(86f, -90f), new Vector2(120f, 30f), Fmt(startScaleAxis.x), _ => ApplyScaleAxisFromFields());
            _syField = MakeInput(inputTemplate, panelGo.transform, new Vector2(210f, -90f), new Vector2(120f, 30f), Fmt(startScaleAxis.y), _ => ApplyScaleAxisFromFields());
            _szField = MakeInput(inputTemplate, panelGo.transform, new Vector2(334f, -90f), new Vector2(120f, 30f), Fmt(startScaleAxis.z), _ => ApplyScaleAxisFromFields());

            // Rotation row (P5): degrees around X/Y/Z, relative to the bone.
            SmallLabel(panelGo.transform, "Rotação", new Vector2(12f, -128f), new Vector2(70f, 28f));
            _rxField = MakeInput(inputTemplate, panelGo.transform, new Vector2(86f, -128f), new Vector2(120f, 30f), Fmt(startEuler.x), _ => ApplyEulerFromFields());
            _ryField = MakeInput(inputTemplate, panelGo.transform, new Vector2(210f, -128f), new Vector2(120f, 30f), Fmt(startEuler.y), _ => ApplyEulerFromFields());
            _rzField = MakeInput(inputTemplate, panelGo.transform, new Vector2(334f, -128f), new Vector2(120f, 30f), Fmt(startEuler.z), _ => ApplyEulerFromFields());

            // Position row.
            SmallLabel(panelGo.transform, "Posição", new Vector2(12f, -166f), new Vector2(70f, 28f));
            _xField = MakeInput(inputTemplate, panelGo.transform, new Vector2(86f, -166f), new Vector2(120f, 30f), Fmt(startOffset.x), _ => ApplyOffsetFromFields());
            _yField = MakeInput(inputTemplate, panelGo.transform, new Vector2(210f, -166f), new Vector2(120f, 30f), Fmt(startOffset.y), _ => ApplyOffsetFromFields());
            _zField = MakeInput(inputTemplate, panelGo.transform, new Vector2(334f, -166f), new Vector2(120f, 30f), Fmt(startOffset.z), _ => ApplyOffsetFromFields());

            // Fine position nudge row: – / + per axis, stepping by an ADJUSTABLE "Passo" (default 0.05),
            // so the user isn't forced to type tiny 0.0x values by hand.
            SmallLabel(panelGo.transform, "Passo", new Vector2(12f, -204f), new Vector2(44f, 28f));
            _stepField = MakeInput(inputTemplate, panelGo.transform, new Vector2(58f, -204f), new Vector2(48f, 30f), "0.05", null);
            MakeButton(buttonTemplate, panelGo.transform, "X–", new Vector2(112f, -204f), new Vector2(42f, 30f), () => NudgePos(0, -1f));
            MakeButton(buttonTemplate, panelGo.transform, "X+", new Vector2(156f, -204f), new Vector2(42f, 30f), () => NudgePos(0, +1f));
            MakeButton(buttonTemplate, panelGo.transform, "Y–", new Vector2(206f, -204f), new Vector2(42f, 30f), () => NudgePos(1, -1f));
            MakeButton(buttonTemplate, panelGo.transform, "Y+", new Vector2(250f, -204f), new Vector2(42f, 30f), () => NudgePos(1, +1f));
            MakeButton(buttonTemplate, panelGo.transform, "Z–", new Vector2(300f, -204f), new Vector2(42f, 30f), () => NudgePos(2, -1f));
            MakeButton(buttonTemplate, panelGo.transform, "Z+", new Vector2(344f, -204f), new Vector2(42f, 30f), () => NudgePos(2, +1f));

            // Paint channel (P2): which colour picker paints this part. Auto-set by category on
            // import; this row lets the user correct it (e.g. force skin instead of clothing) and it
            // persists per model. One wide button cycling through the friendly channel names.
            _channelBtn = MakeButton(buttonTemplate, panelGo.transform, ChannelLabelFor(_channel), new Vector2(12f, -246f), new Vector2(456f, 34f), CycleChannel);

            // Gender (P3). (Texture variants moved to the top-center VariantBar, shown on selection.)
            _genderBtn = MakeButton(buttonTemplate, panelGo.transform, GenderLabelFor(_gender), new Vector2(12f, -284f), new Vector2(456f, 34f), CycleGender);

            // Attach mode (P14): accessory (adds on top, Sims-style) vs replace-the-slot vs auto-by-category.
            _encaixeBtn = MakeButton(buttonTemplate, panelGo.transform, EncaixeLabelFor(_additiveMode), new Vector2(12f, -324f), new Vector2(456f, 34f), CycleEncaixe);

            // P11 — modeling mode: a "checkbox" toggle + a row of 3 mode chips (2 move / 3 grossura /
            // 4 rotação) that highlight the active one. Drag the part in the preview to manipulate it.
            _modelBtn = MakeButton(buttonTemplate, panelGo.transform, "", new Vector2(12f, -364f), new Vector2(456f, 30f), ToggleModeling);
            _modeChips[0] = MakeButton(buttonTemplate, panelGo.transform, ModeLabels[0], new Vector2(12f, -400f), new Vector2(145f, 30f), () => SetMode(0));
            _modeChips[1] = MakeButton(buttonTemplate, panelGo.transform, ModeLabels[1], new Vector2(167f, -400f), new Vector2(145f, 30f), () => SetMode(1));
            _modeChips[2] = MakeButton(buttonTemplate, panelGo.transform, ModeLabels[2], new Vector2(322f, -400f), new Vector2(145f, 30f), () => SetMode(2));
            RefreshModelingUi();

            // Save-as-default (persist) vs apply-once (temporary) — P6.
            MakeButton(buttonTemplate, panelGo.transform, "Salvar padrão", new Vector2(12f, -440f), new Vector2(220f, 40f), Confirm);
            MakeButton(buttonTemplate, panelGo.transform, "Só desta vez", new Vector2(242f, -440f), new Vector2(220f, 40f), ApplyOnce);

            ApplyScale(startScale);
            _attachment.SetUserScaleAxis(startScaleAxis);
            _attachment.SetUserOffset(startOffset);
            _attachment.SetUserEuler(startEuler);
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

        private void Confirm()
        {
            if (_attachment != null)
            {
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
                });

                // Scale as a MULTIPLIER over this mesh's normalized base, so it carries to the next
                // (possibly differently-scaled) model correctly.
                float baseN = p != null ? p.NormalizedScale : 0f;
                float mult = baseN > 1e-6f ? _attachment.UserScale / baseN : 1f;
                ScaleStore.SetLast(mult, _attachment.UserOffset);

                // Category default (per tab): every NEW model imported into this same tab (e.g. all heads)
                // will start from these placement values.
                if (p != null)
                    ScaleStore.SetCategoryDefault(p.CategoryPath, mult, _attachment.UserScaleAxis, _attachment.UserEuler, _attachment.UserOffset);

                Compat.ShowSuccess(Loc.T("Padrão salvo — novos modelos desta categoria virão assim.") + " (" + _displayName + ")");

                // P7 — snapshot just this part (framed on it) and use it as the tab-button icon.
                Thumbnailer.Capture(_attachment);
            }
            Close();
        }

        /// <summary>P6 "só desta vez": keep the live-applied values on this instance but do NOT
        /// persist — the next re-import of the same file reverts to the saved default.</summary>
        private void ApplyOnce()
        {
            Compat.ShowSuccess(Loc.T("Aplicado só nesta sessão:") + " " + _displayName);
            Close();
        }

        // ---- P11 modeling mode (drag the part in the preview) ----

        private void ToggleModeling()
        {
            if (!_modeling)
            {
                if (!ResolvePreview()) { Compat.ShowError("Não achei o preview do personagem para modelar."); return; }
                _modeling = true;
                CreateBlocker();
            }
            else
            {
                _modeling = false;
                _dragging = false;
                DestroyBlocker();
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

        /// <summary>Transparent UiButton over the preview: it becomes the frontmost UiButton so SlickUi
        /// routes presses to it (doing nothing) instead of the preview panel behind (which rotates the
        /// camera). Placed as the FIRST child so it sits over the render texture but under the arrows.</summary>
        private void CreateBlocker()
        {
            if (_blocker != null || _buttonTemplate == null || _previewRect == null) return;
            var go = UnityEngine.Object.Instantiate(_buttonTemplate, _previewRect);
            go.name = "ModelingDragBlocker";

            var btn = go.GetComponent<UiButton>();
            if (btn != null)
                try { btn.onLeftMouseClick.RemoveAllListeners(); btn.whileMouseDrag.RemoveAllListeners(); btn.whileLeftMouseHeld.RemoveAllListeners(); } catch { }

            var brt = go.GetComponent<RectTransform>();
            if (brt != null) { brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero; }

            var img = go.GetComponent<Image>();
            if (img != null) { img.sprite = null; img.color = new Color(0f, 0f, 0f, 0f); img.raycastTarget = true; }
            foreach (var i in go.GetComponentsInChildren<Image>(true)) if (i.gameObject != go) i.enabled = false;
            foreach (var raw in go.GetComponentsInChildren<RawImage>(true)) raw.enabled = false;
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) t.enabled = false;

            go.transform.SetAsFirstSibling();
            _blocker = go;
        }

        private void DestroyBlocker()
        {
            if (_blocker != null) { UnityEngine.Object.Destroy(_blocker); _blocker = null; }
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
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _dragging = false;
            }

            if (!_dragging || !Input.GetMouseButton(0)) return;
            Vector2 d = (Vector2)mouse - (Vector2)_lastMouse;
            if (d.sqrMagnitude < 1e-4f) return;

            switch (_mode)
            {
                case 0: DragMove(_lastMouse, mouse); break;
                case 1: DragScale(d.y); break;
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

        private void DragScale(float dyPixels)
        {
            ApplyScale(_attachment.UserScale * Mathf.Exp(dyPixels * 0.004f)); // drag up = thicker
            RefreshFields();
        }

        private void DragRotate(float dxPixels, float dyPixels)
        {
            Vector3 e = _attachment.UserEuler;
            e.y += dxPixels * 0.4f;
            e.x += -dyPixels * 0.4f;
            _attachment.SetUserEuler(e);
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

        private void OnDestroy() => DestroyBlocker();

        // Texture variants (P13) live in the top-center VariantBar (shown when the part is selected),
        // so the panel no longer hosts them. Confirm still persists part.TextureVariants below.

        private void RefreshLabel()
        {
            if (_label == null || _attachment == null) return;
            Vector3 o = _attachment.UserOffset;
            Vector3 r = _attachment.UserEuler;
            _label.text =
                $"{_displayName}   —   escala {_attachment.UserScale:0.###}\n" +
                $"pos ({o.x:0.##}, {o.y:0.##}, {o.z:0.##})   rot ({r.x:0.#}, {r.y:0.#}, {r.z:0.#})";
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
                || IsFocused(_stepField);
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
    }
}
