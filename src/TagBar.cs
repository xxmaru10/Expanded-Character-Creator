using System;
using System.Collections.Generic;
using SlickUi;
using UnityEngine;
using UnityEngine.UI;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// P10 — an inline tag bar right under the search box: a "+" to create a tag (type a name + OK) and
    /// one clickable chip per tag. Clicking a chip selects it — which BOTH filters the open list to that
    /// tag AND makes new imports get stamped with it (<see cref="TagManager"/>). Clicking the selected
    /// chip again clears it. Rendered above the item grid; follows the search box each frame.
    /// </summary>
    internal class TagBar : MonoBehaviour
    {
        private static TagBar _current;

        private GameObject _btnTemplate;
        private GameObject _inputTemplate;
        private RectTransform _anchorRt;   // the "Só custom" button — we glue to its right side
        private RectTransform _parentRt;
        private RectTransform _rt;

        private readonly List<GameObject> _items = new List<GameObject>();
        private bool _creating;
        private UiInputField _createInput;

        private const float BarH = 30f;
        private const float Gap = 3f;
        private const float BarW = 260f; // fixed width; chips shrink to fit inside it
        private static readonly Color ChipActive = new Color(0.20f, 0.45f, 0.55f, 1f);
        private static readonly Color ChipIdle = new Color(0.16f, 0.17f, 0.20f, 0.98f);
        private static readonly Color PlusColor = new Color(0.18f, 0.38f, 0.22f, 1f);
        private static readonly Color DeleteColor = new Color(0.72f, 0.16f, 0.16f, 0.98f);

        internal static void Ensure(CharacterCreator creator)
        {
            if (_current != null) return;
            if (creator == null) return;

            RectTransform anchor = CustomFilterButton.Rect;
            if (anchor == null || !(anchor.parent is RectTransform parent)) return;

            var go = new GameObject("CustomTagBar", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);

            _current = go.AddComponent<TagBar>();
            _current._rt = go.GetComponent<RectTransform>();
            _current._anchorRt = anchor;
            _current._parentRt = parent;
            _current._btnTemplate = creator.createNew != null ? creator.createNew.gameObject : null;
            _current._inputTemplate = creator.characterName != null ? creator.characterName.gameObject : null;

            _current._rt.anchorMin = _current._rt.anchorMax = new Vector2(0f, 1f);
            _current._rt.pivot = new Vector2(0f, 1f);
            _current._rt.sizeDelta = new Vector2(BarW, BarH);

            go.transform.SetAsLastSibling();
            _current.Follow();
            _current.Rebuild();

            Plugin.Log.LogInfo("Barra de Tags (P10) posicionada ao lado do 'Só custom'.");
        }

        private void LateUpdate() => Follow();

        /// <summary>Glue the bar right of the "Só custom" button, same top edge, regardless of pivot/
        /// layout timing — read the button's world corners and reproject into our shared parent.</summary>
        private void Follow()
        {
            if (_anchorRt == null || _parentRt == null || _rt == null) return;
            var c = new Vector3[4];
            _anchorRt.GetWorldCorners(c); // 0 BL, 1 TL, 2 TR, 3 BR
            Vector3 localTL = _parentRt.InverseTransformPoint(c[1]);
            Vector3 localTR = _parentRt.InverseTransformPoint(c[2]);
            Rect pr = _parentRt.rect;
            _rt.anchoredPosition = new Vector2(localTR.x - pr.xMin, localTL.y - pr.yMax) + new Vector2(10f, 0f);
        }

        private void Clear()
        {
            foreach (var it in _items) if (it != null) Destroy(it);
            _items.Clear();
            _createInput = null;
        }

        private void Rebuild()
        {
            Clear();
            float barW = BarW;
            if (_creating) BuildCreateRow(barW);
            else BuildChips(barW);
        }

        private void BuildChips(float barW)
        {
            Place(MakeButton("+", PlusColor, EnterCreate), 0f, 28f);

            var tags = TagManager.AllTags();
            float x = 32f;
            float avail = Mathf.Max(1f, barW - x);
            int n = Mathf.Max(1, tags.Count);
            float chipW = Mathf.Clamp(avail / n - Gap, 50f, 110f);
            const float delW = 16f, delGap = 2f;
            float labelW = Mathf.Max(24f, chipW - delW - delGap);

            foreach (var t in tags)
            {
                string tag = t;
                var chip = MakeButton(tag, tag == TagManager.SelectedTag ? ChipActive : ChipIdle,
                    () => { TagManager.Toggle(tag); Rebuild(); });
                Place(chip, x, labelW);

                var del = MakeButton("x", DeleteColor, () => { TagManager.DeleteTag(tag); Rebuild(); });
                Place(del, x + labelW + delGap, delW);

                x += chipW + Gap;
                if (x > barW) break; // don't run far past the bar
            }
        }

        private void BuildCreateRow(float barW)
        {
            const float okW = 40f, xW = 28f;
            float inputW = Mathf.Max(60f, barW - okW - xW - 2f * Gap);

            _createInput = PanelUi.Input(_inputTemplate, transform, new Vector2(0f, 0f), new Vector2(inputW, BarH), "", null);
            if (_createInput != null) _items.Add(_createInput.gameObject);

            Place(MakeButton("OK", ChipActive, ConfirmCreate), inputW + Gap, okW);
            Place(MakeButton("X", ChipIdle, () => { _creating = false; Rebuild(); }), inputW + Gap + okW + Gap, xW);
        }

        private void EnterCreate() { _creating = true; Rebuild(); }

        private void ConfirmCreate()
        {
            string typed = _createInput != null && _createInput.input != null ? _createInput.input.text : "";
            TagManager.CreateTag(typed);
            _creating = false;
            Rebuild();
        }

        private UiButton MakeButton(string label, Color color, Action onClick)
        {
            var btn = UiFactory.TextButton(_btnTemplate, transform, label, _ => onClick());
            if (btn == null) return null;
            _items.Add(btn.gameObject);
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = color;
            return btn;
        }

        private void Place(UiButton btn, float x, float w)
        {
            if (btn == null) return;
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(w, BarH);
            rt.anchoredPosition = new Vector2(x, 0f);
        }
    }
}
