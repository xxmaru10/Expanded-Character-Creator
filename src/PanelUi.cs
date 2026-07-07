using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using SlickUi;

namespace CustomPartsMod
{
    /// <summary>
    /// Stateless builders for a floating config panel (background + header + drag + input/button/label
    /// rows), factored out of the ScaleSession pattern so new panels (e.g. the mass-import config)
    /// share the same look and the same SlickUi pointer-absorption trick. ScaleSession keeps its own
    /// battle-tested copies; this is the shared base for panels added afterwards.
    /// </summary>
    internal static class PanelUi
    {
        private static readonly Color PanelBg = new Color(0.08f, 0.08f, 0.10f, 0.95f);
        private static readonly Color HeaderBg = new Color(0.16f, 0.17f, 0.26f, 1f);

        /// <summary>
        /// Creates the panel shell parented under <paramref name="canvas"/>: sized/anchored top-center,
        /// with a solid background, a title header, a whole-panel <see cref="DragHandle"/>, and the
        /// transparent full-panel UiButton that stops clicks from falling through to the preview camera.
        /// </summary>
        internal static RectTransform BuildShell(GameObject panelGo, Transform canvas, GameObject buttonTemplate,
            Vector2 size, Vector2 pos, string title)
        {
            panelGo.transform.SetParent(canvas, worldPositionStays: false);

            var rt = panelGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            var bg = panelGo.AddComponent<Image>();
            bg.color = PanelBg;

            var drag = panelGo.AddComponent<DragHandle>();
            drag.target = rt;

            AddDragSurface(buttonTemplate, panelGo.transform);

            var header = new GameObject("Header", typeof(RectTransform), typeof(Image));
            header.transform.SetParent(panelGo.transform, worldPositionStays: false);
            var hrt = header.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.sizeDelta = new Vector2(0f, 40f);
            hrt.anchoredPosition = Vector2.zero;
            header.GetComponent<Image>().color = HeaderBg;

            var titleLabel = SmallLabel(panelGo.transform, title, new Vector2(12f, -8f), new Vector2(360f, 26f));
            titleLabel.fontSize = 16f;

            return rt;
        }

        /// <summary>
        /// A transparent, full-panel <see cref="UiButton"/> behind all controls: it exists only to be
        /// the frontmost UiButton for SlickUi's pointer system so pressing the panel doesn't fall
        /// through to the preview (which would rotate the camera). No click/drag behaviour of its own.
        /// </summary>
        internal static void AddDragSurface(GameObject buttonTemplate, Transform panel)
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

            var simg = surfGo.GetComponent<Image>();
            if (simg != null) { simg.sprite = null; simg.color = new Color(0f, 0f, 0f, 0f); simg.raycastTarget = true; }
            foreach (var img in surfGo.GetComponentsInChildren<Image>(true))
                if (img.gameObject != surfGo) img.enabled = false;
            foreach (var raw in surfGo.GetComponentsInChildren<RawImage>(true)) raw.enabled = false;
            foreach (var t in surfGo.GetComponentsInChildren<TMP_Text>(true)) t.enabled = false;

            surfGo.transform.SetAsFirstSibling();
        }

        internal static UiButton Button(GameObject template, Transform parent, string label, Vector2 pos, Vector2 size, Action onClick)
        {
            UiButton btn = UiFactory.TextButton(template, parent, label, _ => onClick());
            if (btn == null) return null;
            var rt = btn.GetComponent<RectTransform>();
            if (rt != null) PlaceTopLeft(rt, pos, size);
            return btn;
        }

        internal static void SetButtonLabel(UiButton btn, string label)
        {
            if (btn == null) return;
            var txt = btn.GetComponentInChildren<TMP_Text>(true);
            if (txt != null) txt.text = Loc.T(label);
        }

        internal static UiInputField Input(GameObject template, Transform parent, Vector2 pos, Vector2 size,
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

            // Neutralize inherited (persistent or runtime) handlers, then wire ours.
            Neutralize(field.input.onEndEdit);
            Neutralize(field.input.onValueChanged);
            field.SetValueWithoutNotify(initial);
            if (onEndEdit != null) field.onEndEdit.AddListener(onEndEdit);

            return field;
        }

        internal static TMP_Text SmallLabel(Transform parent, string text, Vector2 pos, Vector2 size)
        {
            var t = UiFactory.Label(parent, text, fill: false);
            PlaceTopLeft(t.rectTransform, pos, size);
            t.enableAutoSizing = false;
            t.fontSize = 15f;
            t.alignment = TextAlignmentOptions.Left;
            return t;
        }

        internal static void PlaceTopLeft(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        internal static void Neutralize(UnityEventBase ev)
        {
            try
            {
                for (int i = 0; i < ev.GetPersistentEventCount(); i++)
                    ev.SetPersistentListenerState(i, UnityEventCallState.Off);
            }
            catch { }
        }

        internal static void Neutralize(UnityEvent<string> ev)
        {
            Neutralize((UnityEventBase)ev);
            ev.RemoveAllListeners();
        }

        internal static bool TryParse(string s, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim().Replace(',', '.');
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        internal static string Fmt(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
