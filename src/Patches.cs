using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using RpgEngine;
using RpgEngine.Characters;
using RiggedAttachType = RpgEngine.Characters.CharacterCreatorEnums.RiggedAttachType;

namespace CustomPartsMod
{
    /// <summary>
    /// Intercepts PickupableCharacter.AddPart(string) for custom ids: builds the mesh
    /// attachment at runtime instead of the vanilla Resources.Load path, then reuses the
    /// engine's own private registration + colour application so behaviour matches vanilla.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_AddPart
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(typeof(PickupableCharacter), nameof(PickupableCharacter.AddPart), new[] { typeof(string) });

        private static bool Prefix(PickupableCharacter __instance, string partId, ref CharacterAttachment __result)
        {
            bool isCustom = CustomPartCatalog.TryGet(partId, out var part);

            // Replace, don't stack: clear any CUSTOM part already occupying this slot before adding.
            // A native part only clears its own attach socket; our custom part lives on the animated
            // bone, so selecting a native part in the same slot would otherwise leave the custom one on.
            //
            // BUT an ADDITIVE part (an accessory, P14) must add ON TOP of whatever is in the slot — e.g. a
            // crown/horns over an already-imported custom HEAD (same bone). So an additive incoming part
            // never clears the slot; it just stacks. (Removing an accessory is done by clicking it again.)
            bool incomingAdditive = isCustom && part.Additive;
            if (!incomingAdditive && TryGetIncomingSlot(partId, isCustom ? part : null, out var slot))
                RemoveCustomInSlot(__instance, slot, keepId: partId);

            if (!isCustom)
                return true; // not ours: run the original (it clears native parts itself)

            // Build never returns null; assign __result before the (reflection) registration so
            // that even if registration throws, SpawnAlongside's `.gameObject` deref is safe.
            var attachment = CustomBodyPartAttachment.Build(part, __instance);
            __result = attachment;

            try
            {
                Compat.InvokeSetAllOn(__instance, attachment);        // apply current colours
                Compat.InvokeAddPart(__instance, partId, attachment);  // register in attachedItems + prop
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Falha ao registrar parte customizada '" + partId + "': " + e);
            }

            return false; // skip original
        }

        /// <summary>Slot the incoming part occupies: from the custom part if ours, else derived from
        /// the native part's savePath. Returns false when the slot can't be identified confidently.</summary>
        private static bool TryGetIncomingSlot(string partId, CustomPart custom, out RiggedAttachType slot)
        {
            if (custom != null) { slot = custom.Slot; return true; }
            slot = default;
            return CharacterCreator.attachmentPaths.TryGetValue(partId, out var data)
                   && data.savePath != null
                   && CategoryMap.TryToSocket(data.savePath.ToArray(), out slot);
        }

        /// <summary>Unequips (from the preview only) any custom part sitting in <paramref name="slot"/>,
        /// except <paramref name="keepId"/> — so a newly selected part replaces it instead of stacking.</summary>
        private static void RemoveCustomInSlot(PickupableCharacter character, RiggedAttachType slot, string keepId)
        {
            List<string> toRemove = null;
            foreach (var kv in character.attachedItems)
            {
                if (kv.Key == keepId) continue;
                // Additive parts (eyes) are never auto-removed by placing another part — they only come
                // off when the user clicks them again. Skip them here.
                if (kv.Value is CustomBodyPartAttachment c && c.Part != null && !c.Part.Additive && c.Part.Slot == slot)
                    (toRemove ?? (toRemove = new List<string>())).Add(kv.Key);
            }
            if (toRemove == null) return;
            foreach (var id in toRemove)
            {
                try { character.RemovePart(id); }
                catch (Exception e) { Plugin.Log.LogWarning("substituir slot: " + e.Message); }
            }
        }
    }

    /// <summary>
    /// After ANY part is placed, re-add wanted additive parts (eyes) the engine may have removed as
    /// collateral (native head/face placement clears its socket via RemoveAllChildren). See
    /// <see cref="AdditiveParts"/>. Runs for native and custom AddPart alike; guarded against recursion.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_AddPart_Reapply
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(typeof(PickupableCharacter), nameof(PickupableCharacter.AddPart), new[] { typeof(string) });

        private static void Postfix(PickupableCharacter __instance) => AdditiveParts.Reapply(__instance);
    }

    /// <summary>
    /// The user's ON/OFF intent for an additive part is expressed by clicking it (SpawnAlongside toggles
    /// it). Record that intent BEFORE the toggle so collateral removals don't clear it.
    /// </summary>
    [HarmonyPatch(typeof(CharacterCreator), nameof(CharacterCreator.SpawnAlongside))]
    internal static class Patch_SpawnAlongside_Additive
    {
        private static void Prefix(CharacterCreator __instance, string item)
        {
            if (__instance.dummy == null) return;
            if (!CustomPartCatalog.TryGet(item, out var part) || !part.Additive) return;
            // Original toggles: Contains => it will be removed; else it will be added.
            AdditiveParts.SetIntent(item, !__instance.dummy.Contains(item));
        }
    }

    /// <summary>
    /// P13 — after a part is clicked (SpawnAlongside toggles it), show the top-center texture-variant
    /// bar for it when it is a custom part now applied; otherwise hide the bar (native part, or the
    /// custom part was toggled off). NavArrows' off-then-on pair ends with the target shown.
    /// </summary>
    [HarmonyPatch(typeof(CharacterCreator), nameof(CharacterCreator.SpawnAlongside))]
    internal static class Patch_SpawnAlongside_VariantBar
    {
        private static void Postfix(CharacterCreator __instance, string item)
        {
            if (__instance.dummy == null) return;
            if (__instance.dummy.attachedItems.TryGetValue(item, out var att) && att is CustomBodyPartAttachment c)
                VariantBar.ShowFor(c);
            else
                VariantBar.Hide();
        }
    }

    /// <summary>Reset (the "reset parts" button / new character) clears additive intent so eyes don't
    /// get re-applied onto a freshly reset character.</summary>
    [HarmonyPatch(typeof(CharacterCreator), nameof(CharacterCreator.ResetParts))]
    internal static class Patch_ResetParts_Additive
    {
        private static void Prefix() => AdditiveParts.Clear();
    }

    /// <summary>
    /// PickupableCharacter.Contains(string) would hit Resources.Load for a custom id and
    /// throw; answer from attachedItems instead.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_Contains
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(typeof(PickupableCharacter), nameof(PickupableCharacter.Contains), new[] { typeof(string) });

        private static bool Prefix(PickupableCharacter __instance, string partId, ref bool __result)
        {
            if (!CustomPartCatalog.IsCustom(partId))
                return true;

            __result = __instance.attachedItems.ContainsKey(partId);
            return false;
        }
    }

    /// <summary>
    /// P2 — user painting. PickupableCharacter.SetColor(string,Color) is the broadcast fired when the
    /// user moves a colour picker (NOT on the initial SetAllOn, which applies colours part-directly).
    /// So this fires only on deliberate paint: tint each textured custom part whose channel matches.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_Paint
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(typeof(PickupableCharacter), nameof(PickupableCharacter.SetColor), new[] { typeof(string), typeof(Color) });

        private static void Postfix(PickupableCharacter __instance, string target, Color color)
        {
            foreach (var kv in __instance.attachedItems)
                if (kv.Value is CustomBodyPartAttachment c && c.Part != null && c.Part.ChannelId == target)
                    c.ApplyPaint(color);
        }
    }

    /// <summary>
    /// Textured custom parts are painted via <see cref="Patch_Paint"/>; their lit material has no
    /// _Color_* channel properties. Skip the engine's per-channel SetColor for them (avoids setting
    /// missing properties). Untextured custom parts + native parts run the original.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_AttachmentColor
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(typeof(CharacterAttachment), "SetColor", new[] { typeof(string), typeof(Color) });

        private static bool Prefix(CharacterAttachment __instance)
            => !(__instance is CustomBodyPartAttachment c && c.HasTint);
    }

    /// <summary>
    /// After the creator window is populated, make sure the Import button exists.
    /// </summary>
    [HarmonyPatch(typeof(CharacterCreator), nameof(CharacterCreator.EditProp))]
    internal static class Patch_CreatorUi
    {
        private static void Postfix(CharacterCreator __instance)
        {
            ImportButton.Ensure(__instance);
            MassImportButton.Ensure(__instance);   // P1 — "Importar Pasta" (import em massa)
            RandomButton.Ensure(__instance);       // P15 — "Aleatório" (sorteia só peças custom)
            CustomFilterButton.Ensure(__instance); // P9 — "Só custom" toggle
            TagBar.Ensure(__instance);             // P10 — barra de tags "+ / chips", ao lado do "Só custom"
            CustomCategories.EnsureAll(__instance); // "Olhos" sub-tab category button
            ShoeButton.Ensure(__instance);          // "Sapato" button (shown only in the feet category)
            NavArrows.Ensure(__instance);          // P12 — ◀ ▶ step through category options
            ZoomButtons.Ensure(__instance);        // + / – zoom beside the arrows
            PersistenceLoader.LoadAll(__instance); // rebuild saved models (once per session)
        }
    }

    /// <summary>
    /// After each character tab item button is initialized, add a delete (trash) button to the
    /// custom ones so old imported models can be removed from the list.
    /// </summary>
    [HarmonyPatch(typeof(BuildTabsLoader_Characters), "InitialiseDataItemButton")]
    internal static class Patch_ItemTrash
    {
        private static void Postfix(BuildTabsButton button, string id)
        {
            IconButton.Apply(button, id); // P7 — show the snapshot portrait instead of the name
            TrashButton.Apply(button, id);
            EditButton.Apply(button, id); // P6 — reopen the edit panel for custom parts
        }
    }
}
