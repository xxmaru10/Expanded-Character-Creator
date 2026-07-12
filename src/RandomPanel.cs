using System.Collections.Generic;
using SlickUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CustomPartsMod
{
    /// <summary>
    /// P15 — the "Aleatório" panel: one lock row per category that has custom parts (click to lock/
    /// unlock), a "Aleatorizar" button that re-rolls every unlocked category (custom parts only), and a
    /// close button. Rebuilt each time it opens so newly imported categories show up. Lock state is
    /// held in <see cref="Randomizer"/> (session only).
    /// </summary>
    internal class RandomPanel : MonoBehaviour
    {
        private static RandomPanel _current;
        private static Vector2 _lastPanelPos = new Vector2(0f, -60f);

        private RectTransform _panelRt;
        private GameObject _buttonTemplate;
        private readonly Dictionary<string, UiButton> _lockButtons = new Dictionary<string, UiButton>();
        private readonly Dictionary<string, string> _labels = new Dictionary<string, string>();

        private static readonly Color LockedColor = new Color(0.45f, 0.28f, 0.16f, 1f);
        private static readonly Color UnlockedColor = new Color(0.14f, 0.15f, 0.18f, 0.98f);

        internal static void Open(GameObject buttonTemplate, Transform canvas)
        {
            Close();
            if (canvas == null || buttonTemplate == null)
            {
                Plugin.Log.LogWarning("Painel aleatório não pode abrir (canvas/template ausente).");
                return;
            }
            var panelGo = new GameObject("RandomPanel", typeof(RectTransform));
            _current = panelGo.AddComponent<RandomPanel>();
            _current.BuildUi(panelGo, buttonTemplate, canvas);
        }

        internal static void Close()
        {
            if (_current != null) { UnityEngine.Object.Destroy(_current.gameObject); _current = null; }
        }

        private void BuildUi(GameObject panelGo, GameObject buttonTemplate, Transform canvas)
        {
            _buttonTemplate = buttonTemplate;
            var groups = Randomizer.Groups();

            const float top = 48f, rowH = 34f, pad = 12f, scopeH = 24f;
            float rowsH = Mathf.Max(1, groups.Count) * rowH;
            float height = top + scopeH + rowsH + 8f + 44f + 44f; // scope + rows + gap + Aleatorizar + Fechar

            _panelRt = PanelUi.BuildShell(panelGo, canvas, buttonTemplate,
                new Vector2(360f, height), _lastPanelPos, "Aleatório (só peças custom)");

            // Scope line: makes it obvious the roll is limited to the selected tag (if any).
            string scope = TagManager.FilterActive
                ? "Sorteando na tag: " + TagManager.SelectedTag
                : "Sorteando: todas as tags";
            PanelUi.SmallLabel(panelGo.transform, scope, new Vector2(pad, -top), new Vector2(336f, 20f));

            if (groups.Count == 0)
            {
                PanelUi.SmallLabel(panelGo.transform, "Importe peças custom primeiro.",
                    new Vector2(pad, -top - scopeH), new Vector2(336f, 28f));
            }

            float y = -top - scopeH;
            foreach (var g in groups)
            {
                string key = g.Key;
                _labels[key] = g.Label;
                var btn = PanelUi.Button(buttonTemplate, panelGo.transform, LockLabel(key),
                    new Vector2(pad, y), new Vector2(336f, rowH - 4f), () => ToggleLock(key));
                if (btn != null)
                {
                    _lockButtons[key] = btn;
                    Recolor(key);
                }
                y -= rowH;
            }

            float rollY = y - 8f;
            PanelUi.Button(buttonTemplate, panelGo.transform, "Aleatorizar",
                new Vector2(pad, rollY), new Vector2(336f, 40f), OnRandomize);
            PanelUi.Button(buttonTemplate, panelGo.transform, "Fechar",
                new Vector2(pad, rollY - 44f), new Vector2(336f, 40f), Close);
        }

        private void OnRandomize() => Randomizer.Randomize();

        private void ToggleLock(string key)
        {
            Randomizer.ToggleLock(key);
            Recolor(key);
        }

        private void Recolor(string key)
        {
            if (!_lockButtons.TryGetValue(key, out var btn) || btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = Randomizer.IsLocked(key) ? LockedColor : UnlockedColor;
            var txt = btn.GetComponentInChildren<TMP_Text>(true);
            if (txt != null) txt.text = LockLabel(key);
        }

        private string LockLabel(string key)
        {
            _labels.TryGetValue(key, out var label);
            string mark = Loc.T(Randomizer.IsLocked(key) ? "[X] Travado" : "[ ] Travar");
            return mark + "  " + (label ?? key);
        }

        private void Update()
        {
            if (_panelRt != null) _lastPanelPos = _panelRt.anchoredPosition;
        }
    }
}
