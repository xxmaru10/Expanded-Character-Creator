using UnityUtils;   // ConvertToSprite
using RpgEngine;    // BuildTabsButton
using UnityEngine;

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

            if (!CustomPartCatalog.TryGet(id, out var part))
            {
                // Restore native button state (recycled buttons need their native icon active and text inactive)
                button.icon.SetActive(true);
                button.text.SetActive(false);
                return;
            }

            // Load the saved portrait from disk lazily, the first time this part's button is drawn (the
            // scroll pool only inits visible buttons), instead of eagerly at registration — that keeps
            // opening the creator cheap with a large library. Probed once; a portrait made later this
            // session (Thumbnailer) sets part.Thumbnail directly and shows without another disk hit.
            if (part.Thumbnail == null && !part.ThumbnailProbed)
            {
                part.ThumbnailProbed = true;
                // Load a portrait already saved for this model (from an earlier apply/cycle). We do NOT
                // generate here — portraits come from actually applying the part (on click, or via the
                // "Gerar miniaturas" auto-cycle), which photographs the real attachment reliably.
                if (ThumbnailStore.TryLoad(part.SourceKey, out var thumb)) part.Thumbnail = thumb;
            }

            if (part.Thumbnail == null)
            {
                // No portrait yet: show the name until the part is applied (which generates one).
                button.icon.SetActive(false);
                button.text.SetActive(true);
                return;
            }

            if (part.ThumbnailSprite == null)
                part.ThumbnailSprite = part.Thumbnail.ConvertToSprite();

            button.icon.SetImage(part.ThumbnailSprite);
            button.icon.SetActive(true);
            button.text.SetActive(false);
        }
    }
}

