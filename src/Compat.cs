using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RpgEngine;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// Reflection glue for reaching non-public engine members without a publicized
    /// reference assembly. Reuses the game's own private methods so behaviour matches
    /// the vanilla code path exactly. All MethodInfos are resolved once.
    /// </summary>
    internal static class Compat
    {
        // PickupableCharacter.SetAllOn(CharacterAttachment)  -> applies all current colours to one part
        private static readonly MethodInfo MiSetAllOn =
            AccessTools.Method(typeof(PickupableCharacter), "SetAllOn", new[] { typeof(CharacterAttachment) });

        // PickupableCharacter.AddPart(string, CharacterAttachment)  -> registers the part in attachedItems + prop
        private static readonly MethodInfo MiAddPart2 =
            AccessTools.Method(typeof(PickupableCharacter), "AddPart", new[] { typeof(string), typeof(CharacterAttachment) });

        // BuildTabsBase.AddContentItemInitializer(string, bool)  -> registers a tab button for an id
        private static readonly MethodInfo MiAddInit =
            AccessTools.Method(typeof(BuildTabsBase), "AddContentItemInitializer", new[] { typeof(string), typeof(bool) });

        // BuildTabsWithPathButtons.pathPartsFilter  -> the currently navigated category (savePath prefix)
        private static readonly PropertyInfo PiPathFilter =
            AccessTools.Property(typeof(BuildTabsWithPathButtons), "pathPartsFilter");

        // PopupAlert (in-game toast). Optional; located by name to avoid a hard dependency.
        private static readonly Type TPopup = AccessTools.TypeByName("PopupAlert");

        // MeshImporter.ImportNew(fileName, filepath, fileType) -> loads a mesh WITHOUT the file
        // browser (dispatches obj/stl/glb by extension). Used to silently reload saved models.
        private static readonly MethodInfo MiImportNew =
            AccessTools.Method(typeof(RpgEngine.MeshImporter), "ImportNew",
                new[] { typeof(string), typeof(string), typeof(string) });

        internal static RPGMesh ImportMesh(string fileName, string path, string fileType)
            => MiImportNew?.Invoke(null, new object[] { fileName, path, fileType }) as RPGMesh;

        internal static void InvokeSetAllOn(PickupableCharacter c, CharacterAttachment part)
            => MiSetAllOn?.Invoke(c, new object[] { part });

        internal static void InvokeAddPart(PickupableCharacter c, string id, CharacterAttachment part)
            => MiAddPart2?.Invoke(c, new object[] { id, part });

        internal static void AddTabInitializer(BuildTabsBase tabs, string id)
            => MiAddInit?.Invoke(tabs, new object[] { id, false });

        internal static string[] GetPathFilter(BuildTabsWithPathButtons loader)
            => PiPathFilter?.GetValue(loader) as string[];

        internal static void ShowSuccess(string message)
        {
            Popup("ShowSuccess", Loc.T(message)); // show in the game's language; log stays original
            Plugin.Log.LogInfo(message);
        }

        internal static void ShowError(string message)
        {
            Popup("ShowError", Loc.T(message));
            Plugin.Log.LogWarning(message);
        }

        private static void Popup(string methodName, string message)
        {
            try
            {
                var mi = TPopup?
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == methodName
                                         && m.GetParameters().Length >= 1
                                         && m.GetParameters()[0].ParameterType == typeof(string));
                if (mi == null) return;

                var args = mi.GetParameters().Length == 1
                    ? new object[] { message }
                    : new object[] { message, 4f };
                mi.Invoke(null, args);
            }
            catch
            {
                // Non-fatal: the toast is a nicety; logging already happened.
            }
        }
    }
}
