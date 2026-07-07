using UnityEngine;
using RpgEngine; // BuildTabsButton

namespace CustomPartsMod
{
    /// <summary>
    /// Sizing helper so our per-item overlay buttons (edit "E" / delete "X") match the engine's own
    /// badges on the same item — the favourite star (<c>favouriteToggle</c>) and the hide/block toggle
    /// (<c>hideToggle</c>). Reads their runtime size instead of hardcoding, so we stay paired with them.
    /// </summary>
    internal static class ItemBadge
    {
        internal const float Fallback = 16f;

        /// <summary>Side length (px) to draw our badges at, matched to the native favourite/hide icons.</summary>
        internal static float Size(BuildTabsButton button)
        {
            float s = ReadHeight(button != null ? button.favouriteToggle : null);
            if (s <= 1f) s = ReadHeight(button != null ? button.hideToggle : null);
            if (s <= 1f) return Fallback;
            return Mathf.Clamp(s, 11f, 20f);
        }

        private static float ReadHeight(Component c)
        {
            if (c == null) return 0f;
            var rt = c.GetComponent<RectTransform>();
            if (rt == null) return 0f;
            float h = rt.rect.height;
            if (h <= 1f) h = rt.sizeDelta.y;
            return h;
        }
    }
}
