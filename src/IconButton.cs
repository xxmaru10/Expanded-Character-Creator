using UnityUtils;   // ConvertToSprite
using RpgEngine;    // BuildTabsButton

namespace CustomPartsMod
{
    /// <summary>
    /// P7 — replaces a custom part's tab button TEXT with its snapshot portrait. Runs from the same
    /// per-item postfix as the trash/edit buttons, AFTER the engine's InitialiseButton has set the
    /// button to text mode (custom parts have no real Resources icon, so they default to text). Only
    /// touches custom parts that actually have a thumbnail; native parts and un-snapshotted customs are
    /// left as the engine drew them. Pool-safe: each re-init runs InitialiseButton first (resetting to
    /// text), so nothing needs undoing here.
    /// </summary>
    internal static class IconButton
    {
        internal static void Apply(BuildTabsButton button, string id)
        {
            if (button == null || button.icon == null || button.text == null) return;
            if (!CustomPartCatalog.TryGet(id, out var part) || part.Thumbnail == null) return;

            if (part.ThumbnailSprite == null)
                part.ThumbnailSprite = part.Thumbnail.ConvertToSprite();

            button.icon.SetImage(part.ThumbnailSprite);
            button.icon.SetActive(true);
            button.text.SetActive(false);
        }
    }
}
