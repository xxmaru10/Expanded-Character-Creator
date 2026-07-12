using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityUtils;
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

        private static bool _isAutoLinking = false;

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
            {
                if (partId != null && partId.StartsWith("CustomPart_"))
                {
                    // The catalog may not be populated yet (map loads before the Character Creator opens).
                    // Try to reconstruct this part on-demand from the persistence store (scales.json).
                    part = TryLoadOnDemand(partId);
                    if (part != null)
                    {
                        isCustom = true;
                        // fall through to the normal custom-part build path below
                    }
                    else
                    {
                        Plugin.Log.LogWarning($"Part '{partId}' not found in CustomPartCatalog and could not be loaded from persistence. Skipping.");
                        __result = null;
                        return false; // truly missing — skip to avoid crash
                    }
                }
                else
                {
                    return true; // not ours: run the original
                }
            }

            // Se for um acessório aditivo de categoria exclusiva (sapatos, cabelos, barbas, óculos, olhos),
            // removemos qualquer outro acessório customizado da mesma categoria no mesmo slot antes de equipar o novo.
            if (isCustom && part.Additive && part.CategoryPath != null)
            {
                string categoryGroup = null;
                foreach (var seg in part.CategoryPath)
                {
                    if (string.Equals(seg, "shoes", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(seg, "hair", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(seg, "B_Hair", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(seg, "beard", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(seg, "E_Beard", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(seg, "eyes", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(seg, "glasses", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(seg, "brincos", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(seg, "colares", StringComparison.OrdinalIgnoreCase))
                    {
                        categoryGroup = seg;
                        break;
                    }
                }

                if (categoryGroup != null)
                {
                    var toRemove = new List<string>();
                    foreach (var kv in __instance.attachedItems)
                    {
                        if (kv.Value is CustomBodyPartAttachment c && c.Part != null && c.Part.PartId != part.PartId)
                        {
                            bool sameSlot = c.Part.Slot == part.Slot;
                            bool sameGroup = c.Part.CategoryPath != null && Array.Exists(c.Part.CategoryPath, s => string.Equals(s, categoryGroup, StringComparison.OrdinalIgnoreCase));
                            if (sameSlot && sameGroup)
                            {
                                toRemove.Add(kv.Key);
                            }
                        }
                    }

                    foreach (var oldId in toRemove)
                    {
                        if (__instance.attachedItems.TryGetValue(oldId, out var oldAtt) && oldAtt != null)
                        {
                            AdditiveParts.SetIntent(oldId, false);
                            __instance.attachedItems.Remove(oldId);
                            if (oldAtt.gameObject != null) UnityEngine.Object.Destroy(oldAtt.gameObject);
                        }
                    }
                }
            }

            // Build never returns null; assign __result before the (reflection) registration so
            // that even if registration throws, SpawnAlongside's `.gameObject` deref is safe.
            var attachment = CustomBodyPartAttachment.Build(part, __instance);
            __result = attachment;

            try
            {
                Compat.InvokeSetAllOn(__instance, attachment);        // apply current colours
                // Idempotent add: the engine's AddPart does attachedItems.Add(id,...), which THROWS
                // "same key already added" if this id is already tracked. That happens with linked L/R
                // pairs (e.g. shoes) where equipping one side auto-equips the other and the partner is
                // already attached — the throw left the second shoe unregistered, so it appeared swapped
                // or missing. Drop any stale entry first so the (re)add is a clean replace.
                if (__instance.attachedItems != null && __instance.attachedItems.ContainsKey(partId))
                {
                    try { __instance.RemovePart(partId); } catch { }
                }
                Compat.InvokeAddPart(__instance, partId, attachment);  // register in attachedItems + prop
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Falha ao registrar parte customizada '" + partId + "': " + e);
            }

            // Auto-equip linked parts (children) when a trigger part is explicitly equipped
            if (!_isAutoLinking && !string.IsNullOrEmpty(part.LinkGroupId))
            {
                _isAutoLinking = true;
                try
                {
                    var creator = UniqueMono<CharacterCreator>.instance;
                    // Add linked partners to the SAME character that just equipped this part. In the
                    // creator that character IS creator.dummy (use SpawnAlongside so the preview + toggle
                    // state stay in sync); on a MAP character it is __instance. The old code always used
                    // creator.dummy when the creator existed, so loading a character on the map spawned its
                    // linked partners on the invisible creator preview instead of the map character — the
                    // "parts not importing from the creator to the engine" bug (95 parts built at the
                    // creator's -1999 location in the log).
                    bool onCreatorDummy = creator != null && creator.dummy == __instance;
                    foreach (var p in CustomPartCatalog.AllParts())
                    {
                        if (p.PartId != part.PartId && p.LinkGroupId == part.LinkGroupId)
                        {
                            // Skip a linked partner that is already attached: re-adding it would toggle
                            // it off (SpawnAlongside) or throw "same key already added" (AddPart).
                            bool alreadyAttached = __instance.attachedItems != null
                                                   && __instance.attachedItems.ContainsKey(p.PartId);
                            if (alreadyAttached) continue;

                            if (onCreatorDummy)
                            {
                                if (!creator.dummy.Contains(p.PartId)) creator.SpawnAlongside(p.PartId);
                            }
                            else
                            {
                                __instance.AddPart(p.PartId);
                            }
                        }
                    }
                }
                finally
                {
                    _isAutoLinking = false;
                }
            }

            return false; // skip original
        }

        /// <summary>Slot the incoming part occupies: from the custom part if ours, else derived from
        /// the native part's savePath. Returns false when the slot can't be identified confidently.</summary>
        internal static bool TryGetIncomingSlot(string partId, CustomPart custom, out RiggedAttachType slot)
        {
            if (custom != null) { slot = custom.Slot; return true; }
            slot = default;
            if (!CharacterCreator.attachmentPaths.TryGetValue(partId, out var data)
                || data.savePath == null
                || !CategoryMap.TryToSocket(data.savePath.ToArray(), out slot))
                return false;

            // CategoryMap defaults to the R variant for sided slots. If the native part's savePath
            // or partId contains a left-side indicator, flip to the L variant so RemoveCustomInSlot
            // matches the correct side.
            string path = string.Join("/", data.savePath).ToLowerInvariant();
            string pid = partId.ToLowerInvariant();
            bool isLeft = path.Contains("left") || path.Contains("_l/") || path.Contains("_l_")
                       || pid.Contains("left") || pid.Contains("_l_") || pid.EndsWith("_l");

            if (isLeft)
            {
                slot = FlipToLeft(slot);
            }
            return true;
        }

        /// <summary>Converts a right-side slot to its left counterpart.</summary>
        internal static RiggedAttachType FlipToLeft(RiggedAttachType slot)
        {
            switch (slot)
            {
                case RiggedAttachType.handR: return RiggedAttachType.handL;
                case RiggedAttachType.armUpperR: return RiggedAttachType.armUpperL;
                case RiggedAttachType.armLowerR: return RiggedAttachType.armLowerL;
                case RiggedAttachType.shoulderR: return RiggedAttachType.shoulderL;
                case RiggedAttachType.legLowerR: return RiggedAttachType.legLowerL;
                case RiggedAttachType.kneeR: return RiggedAttachType.kneeL;
                default: return slot;
            }
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

        /// <summary>Reconstructs a CustomPart from the persistence store (scales.json) when the
        /// catalog hasn't been populated yet (map loads before the Character Creator opens).
        /// Returns null if the part truly doesn't exist on disk.</summary>
        private static CustomPart TryLoadOnDemand(string partId)
        {
            // Find the scales.json record for this saved-character part id.
            //  1) EXACT: MakeId(key) == partId (fast, normal case).
            //  2) TOLERANT fallback: OLD saved characters store ids that predate the current key scheme
            //     — they lack the category prefix ("hat_"/"beard_"/"brac_"…) and/or keep accents
            //     ("braço" vs the stored "brac_braco"). Without this, those parts silently vanish on the
            //     map. Match on the stable, token-aligned suffix (the mesh name + 8-hex hash), which is
            //     unique per source part, so a differently-prefixed/accented key still resolves.
            string wantedNorm = NormalizeIdCore(partId);
            KeyValuePair<string, PartTransform>? chosen = null;
            bool fuzzy = false;

            foreach (var kv in ScaleStore.AllModels())
            {
                if (string.Equals(ImportFlow.MakeId(kv.Key), partId, StringComparison.Ordinal))
                {
                    chosen = kv; fuzzy = false; break; // exact match always wins
                }
                if (chosen == null && wantedNorm.Length >= 10)
                {
                    string candNorm = NormalizeIdCore(ImportFlow.MakeId(kv.Key));
                    if (candNorm == wantedNorm
                        || candNorm.EndsWith("_" + wantedNorm, StringComparison.Ordinal)
                        || wantedNorm.EndsWith("_" + candNorm, StringComparison.Ordinal))
                    {
                        chosen = kv; fuzzy = true; // remember first fuzzy hit; keep scanning for an exact
                    }
                }
            }

            if (chosen == null) return null;

            {
                string chosenKey = chosen.Value.Key;
                var rec = chosen.Value.Value;

                // File must still exist on disk.
                if (string.IsNullOrEmpty(rec.modelPath) || !System.IO.File.Exists(rec.modelPath))
                    return null;

                if (rec.category == null || rec.category.Length == 0)
                    return null;

                RiggedAttachType slot = SidedCategory.ResolveSlot(rec.category, chosenKey, rec.slot);

                var variants = new List<string>();
                if (rec.textureVariants != null)
                    foreach (var v in rec.textureVariants) if (!string.IsNullOrEmpty(v)) variants.Add(v);
                if (variants.Count == 0 && !string.IsNullOrEmpty(rec.texturePath)) variants.Add(rec.texturePath);
                int activeVariant = variants.Count > 0 ? Mathf.Clamp(rec.activeVariant, 0, variants.Count - 1) : 0;
                string activePath = variants.Count > 0 ? variants[activeVariant] : null;

                var part = new CustomPart
                {
                    PartId = partId,
                    SourceKey = chosenKey,
                    ModelPath = rec.modelPath,
                    DisplayName = System.IO.Path.GetFileNameWithoutExtension(rec.modelPath),
                    CategoryPath = rec.category,
                    Slot = slot,
                    ChannelId = !string.IsNullOrEmpty(rec.channel) ? rec.channel : ChannelMap.ForCategory(rec.category),
                    Tag = rec.tag ?? "",
                    Mesh = null,
                    Texture = null,
                    TexturePath = activePath,
                    TextureVariants = variants,
                    ActiveVariant = activeVariant,
                    Scale = rec.scale,
                    NormalizedScale = 1f,
                    ScaleAxis = rec.scaleAxis,
                    LocalPosition = rec.offset,
                    LocalEuler = rec.euler,
                    GenderTag = rec.gender ?? "",
                    AdditiveOverride = rec.additive,
                    LinkGroupId = rec.link ?? "",
                    Additive = AccessoryMap.ResolveAdditive(rec.category, rec.additive),
                };

                CustomPartCatalog.Register(part);
                Plugin.Log.LogInfo($"[on-demand] Part '{partId}' loaded from persistence for map character"
                    + (fuzzy ? $" (fallback -> '{chosenKey}')." : "."));
                return part;
            }
        }

        /// <summary>Normalizes a part id for tolerant matching: drops the "CustomPart_" prefix, lowercases
        /// and strips accents (ç->c, ã->a). Two ids that differ only by a category prefix or accents share
        /// the same token-aligned suffix once normalized, so an old saved-character id resolves to the
        /// current scales.json key.</summary>
        private static string NormalizeIdCore(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            string s = id;
            const string pfx = "CustomPart_";
            if (s.StartsWith(pfx, StringComparison.Ordinal)) s = s.Substring(pfx.Length);

            string formD = s.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(formD.Length);
            foreach (char c in formD)
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                    != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
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
            {
                VariantBar.ShowFor(c);
                // Auto-portrait on apply: a part with no thumbnail yet gets one photographed from the
                // real, correctly-placed attachment (the proven path). Newly imported parts thus get an
                // icon without any manual "generate" step.
                if (c.Part != null && c.Part.Thumbnail == null)
                {
                    Thumbnailer.Capture(c);
                }
            }
            else
            {
                VariantBar.Hide();
            }
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

    internal static class Patch_SpawnAlongside_State
    {
        internal static bool IsSpawning = false;
        internal static string SpawningPartId = null;
    }

    [HarmonyPatch(typeof(CharacterCreator), nameof(CharacterCreator.SpawnAlongside))]
    internal static class Patch_SpawnAlongside_Tracker
    {
        private static void Prefix(string item)
        {
            Patch_SpawnAlongside_State.IsSpawning = true;
            Patch_SpawnAlongside_State.SpawningPartId = item;
        }

        private static void Postfix()
        {
            Patch_SpawnAlongside_State.IsSpawning = false;
            Patch_SpawnAlongside_State.SpawningPartId = null;
        }
    }

    [HarmonyPatch(typeof(PickupableCharacter), nameof(PickupableCharacter.RemovePart), new[] { typeof(string) })]
    internal static class Patch_RemovePart_Guard
    {
        private static bool _isAutoRemoving = false;

        private static bool Prefix(PickupableCharacter __instance, string partId)
        {
            // If the removal is triggered during a SpawnAlongside call (UI button selection):
            if (Patch_SpawnAlongside_State.IsSpawning && !string.IsNullOrEmpty(Patch_SpawnAlongside_State.SpawningPartId))
            {
                string incomingId = Patch_SpawnAlongside_State.SpawningPartId;
                if (CustomPartCatalog.TryGet(partId, out var oldPart))
                {
                    if (CustomPartCatalog.TryGet(incomingId, out var newPart))
                    {
                        // Allow removal if they occupy the same socket type (even if different sides, like L vs R)
                        if (Patch_AddPart.FlipToLeft(oldPart.Slot) != Patch_AddPart.FlipToLeft(newPart.Slot))
                        {
                            return false; // skip the removal
                        }
                    }
                    else if (Patch_AddPart.TryGetIncomingSlot(incomingId, null, out var incomingSlot))
                    {
                        // Incoming is a native part; only allow removal if they share the same socket type
                        if (Patch_AddPart.FlipToLeft(oldPart.Slot) != Patch_AddPart.FlipToLeft(incomingSlot))
                        {
                            return false; // skip the removal
                        }
                    }
                }
            }
            return true;
        }

        private static void Postfix(PickupableCharacter __instance, string partId)
        {
            if (_isAutoRemoving) return;

            if (CustomPartCatalog.TryGet(partId, out var part) && !string.IsNullOrEmpty(part.LinkGroupId))
            {
                _isAutoRemoving = true;
                try
                {
                    var toRemove = new List<string>();
                    foreach (var kv in __instance.attachedItems)
                    {
                        if (kv.Value is CustomBodyPartAttachment c && c.Part != null && c.Part.PartId != partId && c.Part.LinkGroupId == part.LinkGroupId)
                        {
                            toRemove.Add(kv.Key);
                        }
                    }

                    foreach (var id in toRemove)
                    {
                        __instance.RemovePart(id);
                        AdditiveParts.SetIntent(id, false);
                    }
                }
                finally
                {
                    _isAutoRemoving = false;
                }
            }
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

    [HarmonyPatch(typeof(CharacterCreator), nameof(CharacterCreator.EditProp))]
    internal static class Patch_CreatorUi
    {
        private static void Postfix(CharacterCreator __instance)
        {
            // (Removido o dump de diagnóstico "[all-paths]": com 15k+ entradas em attachmentPaths ele
            // construía e escrevia milhares de linhas de log A CADA abertura do criador — custo real e
            // inútil em produção. Reintroduzir só sob um flag de debug se precisar investigar categorias.)

            CameraReset.CaptureHome();              // record the camera's default pose on first open (for reset)
            ImportButton.Ensure(__instance);
            MassImportButton.Ensure(__instance);   // P1 — "Importar Pasta" (import em massa)
            SharedFolderImportButton.Ensure(__instance); // "Importar Pasta (texturas compartilhadas)" — recursivo + auto-rota
            RandomButton.Ensure(__instance);       // P15 — "Aleatório" (sorteia só peças custom)
            PaintButton.Ensure(__instance);        // "Pincel" — pinta a textura da peça (salva como variante nova)
            ResetCamButton.Ensure(__instance);     // "Resetar câmera" — volta a câmera do criador ao padrão
            CustomFilterButton.Ensure(__instance); // P9 — "Só custom" toggle
            TagBar.Ensure(__instance);             // P10 — barra de tags "+ / chips", ao lado do "Só custom"
            CustomCategories.EnsureAll(__instance); // "Olhos" sub-tab category button
            ShoeButton.Ensure(__instance);          // "Sapato" button (shown only in the feet category)
            NavArrows.Ensure(__instance);          // P12 — ◀ ▶ step through category options
            ZoomButtons.Ensure(__instance);        // + / – zoom beside the arrows
            PanButtons.Ensure(__instance);         // ▲ / ▼ raise/lower the camera (left of the model)
            PersistenceLoader.LoadAll(__instance); // rebuild saved models (once per session)
            TagBar.Refresh();                      // parts (with their tags) are now registered — repopulate the tag chips
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

    /// <summary>
    /// Blocks camera drag rotation when modeling mode is active and the user is dragging
    /// with the left mouse button (to prevent camera rotation from competing with the modeling drag).
    /// Right/Middle mouse drags still work.
    /// </summary>
    [HarmonyPatch(typeof(CharacterCreatorCamera), "MouseDrag")]
    internal static class ScaleCameraDragGuard
    {
        private static bool Prefix(UnityEngine.EventSystems.PointerEventData mouse)
        {
            if (ScaleSession.IsModeling)
            {
                if (mouse != null && mouse.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left)
                    return false;
            }
            return true;
        }
    }

}
