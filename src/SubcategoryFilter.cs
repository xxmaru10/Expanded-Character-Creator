using System;
using System.Reflection;
using HarmonyLib;
using UnityUtils;
using RpgEngine;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// Keeps certain head sub-categories (earrings/necklaces = <c>brincos</c>/<c>colares</c>) OUT of the
    /// generic Accessories/Extras list. Those parts live under the <c>extras/attachments</c> savePath
    /// prefix so the engine's prefix filter would show them whenever the native Accessories region is open
    /// — but the user wants ONLY hats (chapéus) in Accessories, with earrings/necklaces reachable solely
    /// from their dedicated head sub-tab buttons (see <see cref="CustomCategories"/>).
    ///
    /// Rule: a part whose category leaf is one of <see cref="SubOnly"/> is hidden UNLESS the currently
    /// navigated category path (<c>pathPartsFilter</c>) explicitly names that leaf — i.e. the user clicked
    /// the Brincos/Colares sub-tab, which calls <c>SetPathFilter([… , "brincos"])</c>. Everywhere else
    /// (Accessories prefix, "All", etc.) they drop out. Code-only, so no scales.json migration is needed.
    ///
    /// Composes with the engine's own path/gender/search filters and with <see cref="CustomFilter"/> /
    /// <see cref="TagManager"/> (all are postfixes on the same <c>Filter</c> predicate). Gated to the
    /// character creator's own loader so other tab systems are untouched.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_SubcategoryFilter
    {
        // Category leaf segments that must appear ONLY when their own sub-tab is explicitly open.
        private static readonly string[] SubOnly = { "brincos", "colares" };

        private static MethodBase TargetMethod()
            => AccessTools.Method(typeof(BuildTabsWithPathButtons), "Filter", new[] { typeof(string) });

        private static void Postfix(BuildTabsWithPathButtons __instance, string id, ref bool __result)
        {
            if (!__result) return; // already hidden by the engine or an earlier postfix
            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator == null || __instance != creator.itemTabsLoader) return; // only the character tabs

            if (!CustomPartCatalog.TryGet(id, out var part) || part == null || part.CategoryPath == null) return;

            // Which "sub-only" leaf (if any) does this part belong to?
            string leaf = null;
            foreach (var seg in part.CategoryPath)
            {
                foreach (var s in SubOnly)
                    if (string.Equals(seg, s, StringComparison.OrdinalIgnoreCase)) { leaf = s; break; }
                if (leaf != null) break;
            }
            if (leaf == null) return; // ordinary part — leave the engine's decision alone

            // Show it only when the active category path explicitly targets that leaf (its own sub-tab).
            var path = Compat.GetPathFilter(__instance);
            bool explicitlyOpen = path != null &&
                Array.Exists(path, seg => string.Equals(seg, leaf, StringComparison.OrdinalIgnoreCase));
            if (!explicitlyOpen) __result = false;
        }
    }
}
