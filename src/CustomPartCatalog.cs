using System.Collections.Generic;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// In-memory registry of imported parts, plus the bridge into the engine's
    /// character-part catalog (CharacterCreator.attachmentPaths). Registering a part
    /// makes it a first-class, selectable entry that the tab UI can display.
    /// </summary>
    internal static class CustomPartCatalog
    {
        private static readonly Dictionary<string, CustomPart> Parts =
            new Dictionary<string, CustomPart>(System.StringComparer.Ordinal);

        internal static bool IsCustom(string partId) => Parts.ContainsKey(partId);

        internal static bool TryGet(string partId, out CustomPart part) => Parts.TryGetValue(partId, out part);

        /// <summary>All registered custom parts (P15 — random build groups these by category).</summary>
        internal static IEnumerable<CustomPart> AllParts() => Parts.Values;

        internal static void Register(CustomPart part)
        {
            Parts[part.PartId] = part;

            // P7 — if this model was snapshotted before (this or a past session), show its saved portrait
            // as the button icon right away.
            if (part.Thumbnail == null && ThumbnailStore.TryLoad(part.SourceKey, out var thumb))
                part.Thumbnail = thumb;

            // Synthetic database entry so the creator treats it like any native part.
            // savePath = category prefix + a unique leaf; fullPath is derived from it
            // (never used for custom parts because AddPart is intercepted).
            var savePath = new List<string>(part.CategoryPath) { part.PartId };

            var data = new PropDatabaseData
            {
                savedId = part.PartId,
                inGameName = part.DisplayName,
                visible = true,          // clickable regardless of DLC edition
                savePath = savePath,
            };

            // Gender (P3): the creator's gender toggle EXCLUDES parts by tag ("Feminine"/"Masculine").
            if (!string.IsNullOrEmpty(part.GenderTag)) data.tags.Add(part.GenderTag);

            CharacterCreator.attachmentPaths[part.PartId] = data;
        }

        /// <summary>P3 — sets a part's gender tag live. Caller should Refresh the tab so the
        /// gender filter re-evaluates. genderTag is "", "Feminine" or "Masculine".</summary>
        internal static void SetGender(string partId, string genderTag)
        {
            if (Parts.TryGetValue(partId, out var part)) part.GenderTag = genderTag ?? "";

            if (CharacterCreator.attachmentPaths.TryGetValue(partId, out var data))
            {
                data.tags.RemoveAll(t => t == "Feminine" || t == "Masculine");
                if (!string.IsNullOrEmpty(genderTag)) data.tags.Add(genderTag);
            }
        }

        /// <summary>Removes a custom part from the in-memory registry and the engine catalog.</summary>
        internal static void Remove(string partId)
        {
            Parts.Remove(partId);
            if (CharacterCreator.attachmentPaths.ContainsKey(partId))
                CharacterCreator.attachmentPaths.Remove(partId);
        }
    }
}
