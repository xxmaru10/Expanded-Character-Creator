using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RpgEngine;

namespace CustomPartsMod
{
    /// <summary>
    /// Fixes a latent engine crash when saving a character (or prop/mesh/token) as a LOCAL MODEL.
    ///
    /// User symptom: clicking "Confirmar / Salvar modelo local" (or the overwrite prompt) does
    /// nothing — the dialog won't close, the model isn't saved to the tabs, and it never shows up in
    /// the build-menu ("menu de construção") search for recently saved models.
    ///
    /// Root cause (confirmed in Player.log):
    ///   NullReferenceException
    ///     at UnityEngine.Behaviour.get_isActiveAndEnabled(...)
    ///     at RpgEngine.BuildTabsBase.AddToTabs&lt;T&gt;(Action`1)
    ///     at RpgEngine.BuildTabsBase.AddToTabs_RPGCharacter(string)
    ///     at RpgEngine.PrefabUploader.&lt;SaveToLocal&gt;b__0()
    ///     at RpgEngine.Prompt.onYes(...)
    ///
    /// <c>AddToTabs&lt;T&gt;</c> iterates the static <c>BuildTabsBase.tabSystems</c> HashSet and, for
    /// each entry that is an <c>IBuildTabs_Character</c> (Props / CharacterTemplates loader), runs the
    /// initializer and then reads <c>tabSystem.isActiveAndEnabled</c>. A DESTROYED loader still lingers
    /// in the set (a closed templates/props menu whose OnDestroy never pruned it): it still satisfies
    /// the <c>is T</c> type check, so the loop reaches <c>isActiveAndEnabled</c> on a dead Unity object
    /// and throws. The throw propagates out of <c>Prompt.onYes</c>, aborting the whole save AFTER the
    /// disk write (<c>RpgCharacterCache.Write</c> runs first) but BEFORE the tab insert + success popup
    /// — so the file is on disk yet invisible in the live build menu until a reload.
    ///
    /// Fix: right before the engine iterates, drop every destroyed/null entry from the (public static)
    /// <c>tabSystems</c> set. Unity's overloaded <c>==</c> reports destroyed objects as null, so
    /// <c>RemoveWhere(t =&gt; t == null)</c> catches them. This is pure defensive housekeeping on an
    /// engine registry the mod never writes to; it only removes entries the engine's own OnDestroy
    /// should already have removed, letting the save finish and the model appear in the build menu.
    /// Not mod-specific — it also protects vanilla prop/mesh/token saves that hit the same path.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_SaveModelTabPrune
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(BuildTabsBase), "AddToTabs_RPGCharacter");
            yield return AccessTools.Method(typeof(BuildTabsBase), "AddToTabs_RPGProp");
            yield return AccessTools.Method(typeof(BuildTabsBase), "AddToTabs_Mesh");
            yield return AccessTools.Method(typeof(BuildTabsBase), "AddToTabs_Token");
        }

        private static void Prefix()
        {
            var set = BuildTabsBase.tabSystems;
            if (set == null) return;
            // Unity's == treats a destroyed MonoBehaviour as null; RemoveWhere strips those dead
            // entries so the engine's foreach never touches isActiveAndEnabled on a dead object.
            int removed = set.RemoveWhere(t => t == null);
            if (removed > 0)
                Plugin.Log.LogInfo($"[save-fix] {removed} aba(s) destruida(s) removida(s) de tabSystems antes de salvar.");
        }
    }
}
