using System;
using System.Collections.Generic;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// Resolves a NATIVE category's savePath prefix from a stable segment (e.g. "uppers", "hands",
    /// "torso") by scanning the engine's loaded part catalog at runtime. This lets the shared-folder
    /// import route a part into the right body category WITHOUT that tab being open — language-independent
    /// and with no hardcoded paths (game-data changes are picked up automatically). Successful lookups
    /// are cached; misses are not (so a segment that loads later can still resolve).
    /// </summary>
    internal static class NativeCategory
    {
        private static readonly Dictionary<string, string[]> Cache =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Tries each candidate segment in order; returns the first native category path found.</summary>
        internal static bool TryFindPath(string[] segments, out string[] path)
        {
            if (segments != null)
                foreach (var seg in segments)
                    if (TryFindOne(seg, out path)) return true;
            path = null;
            return false;
        }

        private static bool TryFindOne(string seg, out string[] path)
        {
            if (Cache.TryGetValue(seg, out path)) return true; // only successes are cached
            path = ScanFor(seg);
            if (path != null) Cache[seg] = path;
            return path != null;
        }

        // First native part whose savePath contains the segment; the category prefix is the savePath up
        // to and including that segment (which is exactly the tab's pathPartsFilter for that folder).
        private static string[] ScanFor(string seg)
        {
            foreach (var kv in CharacterCreator.attachmentPaths)
            {
                if (CustomPartCatalog.IsCustom(kv.Key)) continue; // native parts only
                var sp = kv.Value != null ? kv.Value.savePath : null;
                if (sp == null) continue;
                for (int i = 0; i < sp.Count; i++)
                {
                    if (!string.Equals(sp[i], seg, StringComparison.OrdinalIgnoreCase)) continue;
                    var prefix = new string[i + 1];
                    for (int j = 0; j <= i; j++) prefix[j] = sp[j];
                    return prefix;
                }
            }
            return null;
        }
    }
}
