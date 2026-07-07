using SlickUi;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CustomPartsMod
{
    /// <summary>
    /// Small helpers to build readable runtime UI by cloning an existing engine button
    /// (to inherit its click wiring/style) and turning it into a solid TEXT button, plus a
    /// plain TMP label. Shared by the Import button and the scale panel.
    /// </summary>
    internal static class UiFactory
    {
        private static readonly Color ButtonColor = new Color(0.16f, 0.17f, 0.20f, 0.98f);

        /// <summary>
        /// Clones <paramref name="template"/> (an icon-only UiButton), neutralizes its icon,
        /// gives it a solid background + centered label, and wires <paramref name="onClick"/>.
        /// The caller positions/sizes the returned button's RectTransform.
        /// </summary>
        internal static UiButton TextButton(GameObject template, Transform parent, string label, UnityAction<PointerEventData> onClick)
        {
            var clone = Object.Instantiate(template, parent);
            clone.name = "Btn_" + label;

            var button = clone.GetComponent<UiButton>();
            if (button == null)
            {
                Object.Destroy(clone);
                return null;
            }

            button.onLeftMouseClick.RemoveAllListeners();
            if (onClick != null) button.onLeftMouseClick.AddListener(onClick);

            // The button's own graphic is the template's icon; drop the sprite so it becomes a
            // plain, raycastable box instead of showing an unrelated icon.
            var selfImage = clone.GetComponent<Image>();
            if (selfImage != null)
            {
                selfImage.sprite = null;
                selfImage.color = ButtonColor;
            }
            foreach (var img in clone.GetComponentsInChildren<Image>(true))
                if (img.gameObject != clone) img.enabled = false;
            foreach (var raw in clone.GetComponentsInChildren<RawImage>(true))
                raw.enabled = false;

            // Reuse a label if the template had one, else add our own.
            var existing = clone.GetComponentInChildren<TMP_Text>(true);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                existing.enabled = true;
                existing.text = Loc.T(label);
                existing.color = Color.white;
                existing.alignment = TextAlignmentOptions.Center;
                existing.raycastTarget = false;
            }
            else
            {
                Label(clone.transform, label, fill: true);
            }

            return button;
        }

        /// <summary>
        /// Clones <paramref name="template"/> (an icon-only UiButton) and keeps it icon-based: sets the
        /// button's own graphic to <paramref name="sprite"/> instead of a text label. Used for the
        /// eyes category button (Fatia 3). The caller positions/sizes the returned button.
        /// </summary>
        internal static UiButton IconButton(GameObject template, Transform parent, Sprite sprite, UnityAction<PointerEventData> onClick)
        {
            var clone = Object.Instantiate(template, parent);
            clone.name = "IconBtn";

            var button = clone.GetComponent<UiButton>();
            if (button == null)
            {
                Object.Destroy(clone);
                return null;
            }

            button.onLeftMouseClick.RemoveAllListeners();
            if (onClick != null) button.onLeftMouseClick.AddListener(onClick);

            // The button's own graphic is the template's icon; repurpose it as our eye icon.
            var selfImage = clone.GetComponent<Image>();
            if (selfImage != null)
            {
                selfImage.sprite = sprite;
                selfImage.color = Color.white;
                selfImage.raycastTarget = true;
            }
            // Hide any child icon/text so only the button's own graphic shows the eye.
            foreach (var img in clone.GetComponentsInChildren<Image>(true))
                if (img.gameObject != clone) img.enabled = false;
            foreach (var raw in clone.GetComponentsInChildren<RawImage>(true))
                raw.enabled = false;
            foreach (var t in clone.GetComponentsInChildren<TMP_Text>(true))
                t.enabled = false;

            return button;
        }

        /// <summary>Adds a centered TMP label. When <paramref name="fill"/>, it fills the parent rect.</summary>
        internal static TMP_Text Label(Transform parent, string text, bool fill)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);

            var rt = go.GetComponent<RectTransform>();
            if (fill)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = Loc.T(text);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 8f;
            tmp.fontSizeMax = 22f;

            // A fresh TMP has no font asset -> renders nothing. Borrow one from the scene.
            var anyText = Object.FindObjectOfType<TMP_Text>();
            if (anyText != null && anyText.font != null)
                tmp.font = anyText.font;

            return tmp;
        }
    }
}
