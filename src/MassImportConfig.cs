using System;
using UnityEngine;
using SlickUi;
using TMPro;

namespace CustomPartsMod
{
    /// <summary>
    /// P1 — the "conventions" panel shown BEFORE a folder is picked for mass import. The user fixes
    /// gender (Feminine/Masculine/Both), paint channel, uniform scale (×), per-axis scale, rotation
    /// and position once; every .obj in the chosen folder inherits these values. Texture is the only
    /// thing not set here (it is paired automatically per model). No live preview: there is no part
    /// yet — the settings are captured and handed to <see cref="MassImportFlow"/> on confirm.
    /// </summary>
    internal class MassImportConfig : MonoBehaviour
    {
        private static MassImportConfig _current;
        private static Vector2 _lastPanelPos = new Vector2(0f, -60f);

        private RectTransform _panelRt;
        private Action<MassImportSettings> _onConfirm;

        private UiInputField _scaleField;
        private UiInputField _sxField, _syField, _szField;
        private UiInputField _rxField, _ryField, _rzField;
        private UiInputField _xField, _yField, _zField;
        private UiButton _genderBtn, _channelBtn;
        private UiButton _footBtn;

        private string _gender = "";
        private string _channel = ChannelMap.Primary;
        private bool _footApplies;  // feet/shoe category: show the left/right choice
        private bool _footLeft;     // chosen side (true = left / legLowerL)

        // Kept in sync with ScaleSession's cycle lists (the two panels offer the same choices).
        private static readonly string[] GenderTags = { "", "Feminine", "Masculine" };
        private static readonly string[] GenderLabels = { "Gênero: Ambos", "Gênero: Feminino", "Gênero: Masculino" };

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

        /// <summary>Opens the panel seeded from the category's saved default (P6) + auto channel, so the
        /// user starts from a sensible baseline and only tweaks what they want.</summary>
        internal static void Open(GameObject buttonTemplate, GameObject inputTemplate, Transform canvas,
            string[] category, Action<MassImportSettings> onConfirm)
        {
            Close();
            if (canvas == null || buttonTemplate == null || inputTemplate == null)
            {
                Plugin.Log.LogWarning("Painel de import em massa nao pode abrir (canvas/template ausente).");
                return;
            }

            var panelGo = new GameObject("MassImportConfigPanel", typeof(RectTransform));
            _current = panelGo.AddComponent<MassImportConfig>();
            _current.BuildUi(panelGo, buttonTemplate, inputTemplate, canvas, category, onConfirm);
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
            Transform canvas, string[] category, Action<MassImportSettings> onConfirm)
        {
            _onConfirm = onConfirm;

            // Seed from the category default (multiplier/axis/rotation/offset) and the auto channel.
            float startMult = 1f;
            Vector3 startAxis = Vector3.one, startEuler = Vector3.zero, startOffset = Vector3.zero;
            if (ScaleStore.TryGetCategoryDefault(category, out float m, out Vector3 a, out Vector3 e, out Vector3 o))
            {
                startMult = m; startAxis = a; startEuler = e; startOffset = o;
            }
            _channel = ChannelMap.ForCategory(category);

            // Feet/shoe: the whole folder goes to one side; seed the toggle from the last-used side.
            _footApplies = FootSide.AppliesTo(category);
            _footLeft = _footApplies && ScaleStore.GetLastFootSideLeft();

            float height = _footApplies ? 392f : 348f; // extra row for the left/right toggle
            _panelRt = PanelUi.BuildShell(panelGo, canvas, buttonTemplate,
                new Vector2(480f, height), _lastPanelPos, "Importar pasta — convenções");

            // Uniform scale as a MULTIPLIER over each model's normalized size (1 = tamanho padrão),
            // so differently-sized OBJs still land consistently.
            PanelUi.SmallLabel(panelGo.transform, "Escala ×", new Vector2(12f, -52f), new Vector2(70f, 28f));
            _scaleField = PanelUi.Input(inputTemplate, panelGo.transform, new Vector2(86f, -52f), new Vector2(150f, 30f), PanelUi.Fmt(startMult), null);

            PanelUi.SmallLabel(panelGo.transform, "Esc XYZ", new Vector2(12f, -90f), new Vector2(70f, 28f));
            _sxField = PanelUi.Input(inputTemplate, panelGo.transform, new Vector2(86f, -90f), new Vector2(120f, 30f), PanelUi.Fmt(startAxis.x), null);
            _syField = PanelUi.Input(inputTemplate, panelGo.transform, new Vector2(210f, -90f), new Vector2(120f, 30f), PanelUi.Fmt(startAxis.y), null);
            _szField = PanelUi.Input(inputTemplate, panelGo.transform, new Vector2(334f, -90f), new Vector2(120f, 30f), PanelUi.Fmt(startAxis.z), null);

            PanelUi.SmallLabel(panelGo.transform, "Rotação", new Vector2(12f, -128f), new Vector2(70f, 28f));
            _rxField = PanelUi.Input(inputTemplate, panelGo.transform, new Vector2(86f, -128f), new Vector2(120f, 30f), PanelUi.Fmt(startEuler.x), null);
            _ryField = PanelUi.Input(inputTemplate, panelGo.transform, new Vector2(210f, -128f), new Vector2(120f, 30f), PanelUi.Fmt(startEuler.y), null);
            _rzField = PanelUi.Input(inputTemplate, panelGo.transform, new Vector2(334f, -128f), new Vector2(120f, 30f), PanelUi.Fmt(startEuler.z), null);

            PanelUi.SmallLabel(panelGo.transform, "Posição", new Vector2(12f, -166f), new Vector2(70f, 28f));
            _xField = PanelUi.Input(inputTemplate, panelGo.transform, new Vector2(86f, -166f), new Vector2(120f, 30f), PanelUi.Fmt(startOffset.x), null);
            _yField = PanelUi.Input(inputTemplate, panelGo.transform, new Vector2(210f, -166f), new Vector2(120f, 30f), PanelUi.Fmt(startOffset.y), null);
            _zField = PanelUi.Input(inputTemplate, panelGo.transform, new Vector2(334f, -166f), new Vector2(120f, 30f), PanelUi.Fmt(startOffset.z), null);

            // Paint channel (pele vs roupas etc.) applied to every imported part.
            _channelBtn = PanelUi.Button(buttonTemplate, panelGo.transform, ChannelLabelFor(_channel), new Vector2(12f, -208f), new Vector2(456f, 34f), CycleChannel);

            // Gender filter: Ambos / Feminino / Masculino — tags every imported part accordingly (P3).
            _genderBtn = PanelUi.Button(buttonTemplate, panelGo.transform, GenderLabelFor(_gender), new Vector2(12f, -246f), new Vector2(456f, 34f), CycleGender);

            // Feet/shoe only: which foot the whole folder attaches to (legLowerL / legLowerR).
            float y = -292f;
            if (_footApplies)
            {
                _footBtn = PanelUi.Button(buttonTemplate, panelGo.transform, FootSideLabel(_footLeft), new Vector2(12f, y), new Vector2(456f, 34f), CycleFootSide);
                y -= 44f;
            }

            PanelUi.Button(buttonTemplate, panelGo.transform, "Escolher pasta e importar", new Vector2(12f, y), new Vector2(300f, 40f), Confirm);
            PanelUi.Button(buttonTemplate, panelGo.transform, "Cancelar", new Vector2(322f, y), new Vector2(146f, 40f), () => Close());
        }

        private void CycleFootSide()
        {
            _footLeft = !_footLeft;
            PanelUi.SetButtonLabel(_footBtn, FootSideLabel(_footLeft));
        }

        private static string FootSideLabel(bool left) => left ? "Lado: Esquerda ◀" : "Lado: Direita ▶";

        private void CycleGender()
        {
            int i = Array.IndexOf(GenderTags, _gender);
            if (i < 0) i = 0;
            _gender = GenderTags[(i + 1) % GenderTags.Length];
            PanelUi.SetButtonLabel(_genderBtn, GenderLabelFor(_gender));
        }

        private void CycleChannel()
        {
            int i = Array.IndexOf(Channels, _channel);
            if (i < 0) i = 0;
            _channel = Channels[(i + 1) % Channels.Length];
            PanelUi.SetButtonLabel(_channelBtn, ChannelLabelFor(_channel));
        }

        private void Confirm()
        {
            var settings = new MassImportSettings
            {
                ScaleMultiplier = ParseOr(_scaleField, 1f, positive: true),
                ScaleAxis = new Vector3(ParseOr(_sxField, 1f, positive: true), ParseOr(_syField, 1f, positive: true), ParseOr(_szField, 1f, positive: true)),
                Euler = new Vector3(ParseOr(_rxField, 0f), ParseOr(_ryField, 0f), ParseOr(_rzField, 0f)),
                Offset = new Vector3(ParseOr(_xField, 0f), ParseOr(_yField, 0f), ParseOr(_zField, 0f)),
                Gender = _gender,
                Channel = _channel,
                FootSideLeft = _footLeft,
            };

            var cb = _onConfirm;
            Close();
            cb?.Invoke(settings);
        }

        /// <summary>Parse a field; on blank/invalid use fallback. When <paramref name="positive"/>,
        /// guard against 0/negative (would collapse/mirror the mesh) and fall back instead.</summary>
        private static float ParseOr(UiInputField f, float fallback, bool positive = false)
        {
            if (f == null || f.input == null) return fallback;
            if (!PanelUi.TryParse(f.input.text, out float v)) return fallback;
            if (positive && v <= 1e-4f) return fallback;
            return v;
        }

        private static string GenderLabelFor(string tag)
        {
            int i = Array.IndexOf(GenderTags, tag);
            return GenderLabels[i < 0 ? 0 : i];
        }

        private static string ChannelLabelFor(string channel)
        {
            int i = Array.IndexOf(Channels, channel);
            return "Paleta de cor: " + ChannelLabels[i < 0 ? 0 : i];
        }

        private void Update()
        {
            if (_panelRt != null) _lastPanelPos = _panelRt.anchoredPosition; // remember drag position
            if (Input.GetKeyDown(KeyCode.Escape) && !AnyFieldFocused()) Close();
        }

        private bool AnyFieldFocused()
        {
            return IsFocused(_scaleField)
                || IsFocused(_sxField) || IsFocused(_syField) || IsFocused(_szField)
                || IsFocused(_rxField) || IsFocused(_ryField) || IsFocused(_rzField)
                || IsFocused(_xField) || IsFocused(_yField) || IsFocused(_zField);
        }

        private static bool IsFocused(UiInputField f) => f != null && f.input != null && f.input.isFocused;
    }
}
