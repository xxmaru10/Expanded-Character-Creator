using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityUtils;
using RpgEngine;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// P10 — user tags (themes). The UI is an inline bar under the search box (<see cref="TagBar"/>):
    /// a "+" creates a tag; each tag is a clickable chip. Selecting a chip does BOTH things at once:
    /// it filters the open category to that tag AND makes new imports get stamped with it ("if you're
    /// inside that filter, the imported item goes into it"). So there is ONE selected tag, not separate
    /// import/view tags.
    ///
    /// Tags ride on the side of the part (<see cref="CustomPart.Tag"/>), never in the savePath (the
    /// engine won't auto-make sub-tabs), and persist per model. Selection is session-only.
    /// </summary>
    internal static class TagManager
    {
        // Tags created this session, so a brand-new tag shows as a chip before anything is imported.
        private static readonly HashSet<string> KnownTags = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

        /// <summary>The one selected tag: filters the list AND stamps new imports ("" = none/all).</summary>
        internal static string SelectedTag = "";

        internal static bool FilterActive => !string.IsNullOrEmpty(SelectedTag);

        /// <summary>New imports get this tag (= the selected chip).</summary>
        internal static string ActiveTag => SelectedTag;

        /// <summary>Create a tag from typed text and select it.</summary>
        internal static void CreateTag(string name)
        {
            string t = Normalize(name);
            if (string.IsNullOrEmpty(t)) return;
            KnownTags.Add(t);
            Select(t);
        }

        /// <summary>Select a tag (filter + import target). "" clears the selection (show all).</summary>
        internal static void Select(string tag)
        {
            SelectedTag = Normalize(tag);
            if (!string.IsNullOrEmpty(SelectedTag)) KnownTags.Add(SelectedTag);
            Refresh();
        }

        /// <summary>Click a chip: select it, or deselect if it was already the selected one.</summary>
        internal static void Toggle(string tag)
        {
            string t = Normalize(tag);
            Select(string.Equals(t, SelectedTag, StringComparison.CurrentCultureIgnoreCase) ? "" : t);
        }

        /// <summary>Permanently remove a tag: clears it from every part that carries it (persisting
        /// the change so it doesn't come back on reload) and forgets it from the known-tags set.
        /// Deselects it first if it was the active filter/import target.</summary>
        internal static void DeleteTag(string tag)
        {
            string t = Normalize(tag);
            if (string.IsNullOrEmpty(t)) return;

            if (string.Equals(SelectedTag, t, StringComparison.CurrentCultureIgnoreCase)) SelectedTag = "";
            KnownTags.RemoveWhere(k => string.Equals(k, t, StringComparison.CurrentCultureIgnoreCase));

            foreach (var p in CustomPartCatalog.AllParts())
            {
                if (p == null || !string.Equals(p.Tag, t, StringComparison.CurrentCultureIgnoreCase)) continue;
                p.Tag = "";
                if (!string.IsNullOrEmpty(p.SourceKey)) ScaleStore.TryUpdateTag(p.SourceKey, "");
            }

            Refresh();
        }

        /// <summary>Distinct tags: created this session plus those already on imported parts.</summary>
        internal static List<string> AllTags()
        {
            var set = new SortedSet<string>(KnownTags, StringComparer.CurrentCultureIgnoreCase);
            foreach (var p in CustomPartCatalog.AllParts())
                if (p != null && !string.IsNullOrEmpty(p.Tag)) set.Add(p.Tag);
            return new List<string>(set);
        }

        private static void Refresh()
        {
            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator != null && creator.itemTabsLoader != null) creator.itemTabsLoader.Refresh();
        }

        private static string Normalize(string s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim();
    }

    /// <summary>
    /// Postfix on the character tab's selector predicate: when a tag is selected, keep only custom
    /// parts of that tag (native parts and other tags drop out). Composes with the engine's own
    /// path/gender/search filters and with <see cref="CustomFilter"/>. Gated to the character loader.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_TagFilter
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(typeof(BuildTabsWithPathButtons), "Filter", new[] { typeof(string) });

        private static void Postfix(BuildTabsWithPathButtons __instance, string id, ref bool __result)
        {
            if (!TagManager.FilterActive || !__result) return;
            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator == null || __instance != creator.itemTabsLoader) return;

            if (!CustomPartCatalog.TryGet(id, out var part) || part.Tag != TagManager.SelectedTag)
                __result = false;
        }
    }
}
