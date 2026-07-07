using System.Reflection;
using HarmonyLib;
using UnityUtils;
using RpgEngine;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// P9 — "Só customizados" filter. When on, the character creator's tab shows only imported custom
    /// parts (native parts hidden), within whatever category is open. Implemented by post-filtering the
    /// engine's own tab selector predicate (<c>BuildTabsWithPathButtons.Filter</c>), so it composes
    /// with the path/gender/search filters already in place and NavArrows (P12) steps through the same
    /// reduced list for free. Session-only state (not persisted).
    /// </summary>
    internal static class CustomFilter
    {
        internal static bool CustomOnly;

        /// <summary>Flip the filter and refresh the current tab so it re-evaluates.</summary>
        internal static void Toggle()
        {
            CustomOnly = !CustomOnly;
            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator != null && creator.itemTabsLoader != null) creator.itemTabsLoader.Refresh();
        }
    }

    /// <summary>
    /// Postfix on the character tab's selector predicate: when the filter is on, drop any id that is
    /// not one of ours. Gated to the character creator's own loader so other tab systems (map objects,
    /// etc.) are untouched. Runs after the engine's own Filter, so a part already hidden stays hidden.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_CustomOnlyFilter
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(typeof(BuildTabsWithPathButtons), "Filter", new[] { typeof(string) });

        private static void Postfix(BuildTabsWithPathButtons __instance, string id, ref bool __result)
        {
            if (!CustomFilter.CustomOnly || !__result) return;
            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator == null || __instance != creator.itemTabsLoader) return; // only the character tabs
            if (!CustomPartCatalog.IsCustom(id)) __result = false;
        }
    }
}
