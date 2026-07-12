using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SimpleFileBrowser;
using UnityEngine;
using RpgEngine;
using RpgEngine.Characters;
using UnityUtils;
using RiggedAttachType = RpgEngine.Characters.CharacterCreatorEnums.RiggedAttachType;

namespace CustomPartsMod
{
    /// <summary>
    /// Drives the "Import Part" button: reads the currently open category, opens the
    /// engine's own file browser via MeshImporter, then registers the result as a
    /// selectable custom part and applies it to the preview character.
    /// </summary>
    internal static class ImportFlow
    {
        internal static void OnImportClicked()
        {
            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator == null || creator.itemTabsLoader == null)
            {
                Compat.ShowError("Criador de personagens indisponivel.");
                return;
            }

            string[] category = Compat.GetPathFilter(creator.itemTabsLoader);
            // The synthetic categories (Olhos/Pés/Sapato) have short prefixes; accept them explicitly.
            // Every native leaf category is 3+ segments deep, so that guard stays for the rest.
            if (category == null || (!CustomCategory.IsSynthetic(category) && category.Length < 3))
            {
                Compat.ShowError("Abra uma categoria (ex.: Cabecas, Olhos ou Pés) antes de importar.");
                return;
            }

            RiggedAttachType slot = CategoryMap.ToSocket(category);

            // Sided categories (feet/shoe, hands): ask left or right first (the engine keeps them as
            // separate sockets), defaulting to that kind's last-used side, then import with the chosen
            // slot. Other categories go straight to the file browser.
            var kind = SidedCategory.KindOf(category);
            if (kind != SidedCategory.Kind.None)
            {
                var canvas = creator.createNew != null ? creator.createNew.GetComponentInParent<Canvas>() : null;
                Transform canvasT = canvas != null ? canvas.rootCanvas.transform : creator.transform;
                GameObject buttonTemplate = creator.createNew != null ? creator.createNew.gameObject : null;
                string storeKey = SidedCategory.StoreKey(kind);
                SidePrompt.Open(buttonTemplate, canvasT, kind, ScaleStore.GetLastSideLeft(storeKey), left =>
                {
                    ScaleStore.SetLastSideLeft(storeKey, left);
                    RiggedAttachType sideSlot = left ? SidedCategory.LeftSlot(kind) : SidedCategory.RightSlot(kind);
                    MeshImporter.LoadMesh(rpg => OnMeshLoaded(rpg, category, sideSlot), new[] { ".obj" });
                });
                return;
            }

            // MeshImporter opens SimpleFileBrowser and calls us back with the loaded mesh.
            MeshImporter.LoadMesh(rpg => OnMeshLoaded(rpg, category, slot), new[] { ".obj" });
        }

        private static void OnMeshLoaded(RPGMesh rpg, string[] category, RiggedAttachType slot)
            => ImportLoadedMesh(rpg, category, slot, null, null);

        /// <summary>Registers an already-loaded mesh as a custom part, applies it to the preview and opens
        /// the live scale panel. <paramref name="modelPathOverride"/> is the source .obj (for texture
        /// pairing + reload); when null it falls back to the file browser's last pick (single import).
        /// <paramref name="folderCtx"/> is non-null only for the mass-import preview: the panel then shows
        /// an "apply to the whole folder" button that imports the rest with the values chosen here.</summary>
        internal static void ImportLoadedMesh(RPGMesh rpg, string[] category, RiggedAttachType slot,
            string modelPathOverride, FolderImportContext folderCtx)
        {
            if (rpg == null || rpg.mesh == null || rpg.mesh.vertexCount == 0)
            {
                Compat.ShowError("Nao foi possivel importar (use um .obj valido).");
                return;
            }

            // Defensive: a bad/zero bounds makes Unity frustum-cull the mesh (fully invisible),
            // and missing normals make it shade black. Fix both before it is ever rendered.
            var mesh = rpg.mesh;
            mesh.RecalculateBounds();
            if (mesh.normals == null || mesh.normals.Length == 0)
                mesh.RecalculateNormals();

            // Stable id per model (source file name, no per-session counter): re-importing the same
            // file reuses the same entry instead of piling up duplicates in the tab.
            string sourceKey = string.IsNullOrEmpty(rpg.id) ? "part" : rpg.id;
            string id = MakeId(sourceKey);

            // Sizing. Base = normalized so any OBJ (often tiny in native units) is visible on import and
            // never 100x off. If this exact model was tuned before, use its saved absolute values;
            // otherwise start from normalized × a calibrated multiplier (a per-category override if the
            // checkbox pinned one, else the global factor) so the user's size preference carries to new
            // models without breaking on differently-scaled ones. Priority: saved → category → global.
            float normalized = NormalizeScale(mesh);
            bool hasSaved = ScaleStore.TryGet(sourceKey, out PartTransform saved);

            float startScale;
            Vector3 startOffset, startScaleAxis, startEuler;
            string startGender;

            if (hasSaved)
            {
                startGender = saved.gender ?? "";
            }
            else
            {
                string k = sourceKey.ToLowerInvariant();
                if (k.Contains("yf") || k.Contains("female") || k.Contains("feminino") || k.Contains("mulher"))
                    startGender = "Feminine";
                else if (k.Contains("ym") || k.Contains("male") || k.Contains("masculino") || k.Contains("homem"))
                    startGender = "Masculine";
                else
                    startGender = "";
            }

            if (hasSaved)
            {
                // Re-importing a file that was tuned before: reproduce its exact saved values.
                startScale = saved.scale;
                startOffset = saved.offset;
                startScaleAxis = saved.scaleAxis;
                startEuler = saved.euler;
            }
            else if (ScaleStore.TryGetCategoryDefault(category, slot, startGender, out float catMult, out Vector3 catAxis, out Vector3 catEuler, out Vector3 catOffset))
            {
                // A NEW model in a tab whose category default was pinned via the checkbox ("all heads
                // look like this"): scale = normalized × the category multiplier so different-sized
                // meshes still land right, overriding the global factor.
                startScale = normalized * catMult;
                startScaleAxis = catAxis;
                startEuler = catEuler;
                startOffset = catOffset;
            }
            else
            {
                // No category override: use the calibrated GLOBAL multiplier over the normalized base.
                ScaleStore.GetGlobalMult(out float globalMult, out Vector3 globalOffset);
                startScale = normalized * globalMult;
                // Eyes sit slightly IN FRONT of the face by default (user-requested Z ≈ 0.12); the
                // unrelated global offset from other parts doesn't apply to them.
                startOffset = EyesCategory.Is(category) ? new Vector3(0f, 0f, 0.12f) : globalOffset;
                startScaleAxis = Vector3.one;
                startEuler = Vector3.zero;
            }
            string savedTexPath = hasSaved ? saved.texturePath : null;
            // Paint channel (P2): saved per-model override wins, else auto by category.
            string startChannel = (hasSaved && !string.IsNullOrEmpty(saved.channel))
                ? saved.channel : ChannelMap.ForCategory(category);
            // Attach mode (P14): saved override wins, else auto (eyes + accessory categories).
            int startAdditive = hasSaved ? saved.additive : 0;
            // Tag (P10): saved tag wins on re-import, else the active import tag.
            string startTag = hasSaved && !string.IsNullOrEmpty(saved.tag) ? saved.tag : TagManager.ActiveTag;

            // The OBJ importer loads geometry only. Find a texture: the one saved for this model,
            // else a sibling of the model file (its .mtl map_Kd or a same-named PNG). The user can
            // also add more from the panel (P13 variant boxes). The path is the explicit source when
            // given (mass-import preview), else the file browser's last pick (single import).
            string modelPath = !string.IsNullOrEmpty(modelPathOverride)
                ? modelPathOverride
                : (FileBrowser.Result != null && FileBrowser.Result.Length > 0 ? FileBrowser.Result[0] : null);

            // P13 — texture variants. On re-import of a tuned model, restore its saved variant list;
            // otherwise start with the single auto-paired sibling texture as variant 0.
            var variants = new List<string>();
            int activeVariant = 0;
            if (hasSaved && saved.textureVariants != null && saved.textureVariants.Length > 0)
            {
                foreach (var v in saved.textureVariants) if (!string.IsNullOrEmpty(v)) variants.Add(v);
                activeVariant = Mathf.Clamp(saved.activeVariant, 0, Mathf.Max(0, variants.Count - 1));
            }
            else
            {
                // Fresh import: auto-pair the model's textures. Variant 0 is the same-named sibling
                // (the CAS Batch Exporter writes "<model>.png"); the numbered siblings
                // "<model>1.png", "<model>2.png", ... it writes for the other colour swatches fill the
                // "+" variant slots automatically. A saved single texture, if any, stays as variant 0.
                var found = TextureLoader.FindVariants(modelPath);
                if (!string.IsNullOrEmpty(savedTexPath) && File.Exists(savedTexPath))
                {
                    variants.Add(savedTexPath);
                    foreach (var v in found)
                        if (!variants.Contains(v)) variants.Add(v);
                }
                else
                {
                    variants.AddRange(found);
                }
            }

            // Copy model and texture files to CustomParts/models/<folderName>/ to make them self-contained
            string linkGroupId = PartNameRouter.ResolveLinkGroupId(sourceKey, "");
            string folderName = !string.IsNullOrEmpty(linkGroupId) ? linkGroupId : id;
            string destDir = Path.Combine(ScaleStore.CustomPartsDir, "models", folderName);
            try
            {
                Directory.CreateDirectory(destDir);
                string destObj = Path.Combine(destDir, Path.GetFileName(modelPath));
                if (File.Exists(modelPath) && !string.Equals(Path.GetFullPath(modelPath), Path.GetFullPath(destObj), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(modelPath, destObj, true);
                }
                modelPath = destObj;

                for (int i = 0; i < variants.Count; i++)
                {
                    string origTex = variants[i];
                    if (string.IsNullOrEmpty(origTex) || !File.Exists(origTex)) continue;
                    string destTex = Path.Combine(destDir, Path.GetFileName(origTex));
                    if (!string.Equals(Path.GetFullPath(origTex), Path.GetFullPath(destTex), StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(origTex, destTex, true);
                    }
                    variants[i] = destTex;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[copy-self-contained] Falha ao copiar arquivos para a pasta do mod: {ex.Message}");
            }

            string texPath = variants.Count > 0 ? variants[activeVariant] : null;
            Texture2D tex = TextureLoader.LoadFromFile(texPath);
            if (tex == null && rpg.texture != null && rpg.texture.width > 1) tex = rpg.texture; // e.g. GLB embeds one

            var part = new CustomPart
            {
                PartId = id,
                SourceKey = sourceKey,
                DisplayName = string.IsNullOrEmpty(rpg.id) ? id : rpg.id,
                CategoryPath = category,
                Slot = slot,
                ChannelId = startChannel, // P2 paint channel (saved override or auto)
                Tag = startTag,           // P10 user tag/theme
                Mesh = rpg.mesh,
                Texture = tex,
                TexturePath = tex != null ? texPath : null,
                TextureVariants = variants,
                ActiveVariant = variants.Count > 0 ? activeVariant : 0,
                ModelPath = modelPath,
                Scale = startScale,
                NormalizedScale = normalized,
                ScaleAxis = startScaleAxis,
                LocalPosition = startOffset,
                LocalEuler = startEuler,
                GenderTag = startGender,
                AdditiveOverride = startAdditive,
                LinkGroupId = linkGroupId,
                Additive = AccessoryMap.ResolveAdditive(category, startAdditive), // eyes/accessories add on top (P14)
            };

            CustomPartCatalog.Register(part);

            var creator = UniqueMono<CharacterCreator>.instance;
            Compat.AddTabInitializer(creator.itemTabsLoader, id); // make a button exist
            creator.itemTabsLoader.Refresh();                     // show it in the current tab
            creator.SpawnAlongside(id);                           // apply to the preview now (replaces slot)

            Compat.ShowSuccess(Loc.T("Parte importada:") + " " + part.DisplayName);

            // Open the live scale/position panel bound to the freshly placed attachment.
            if (creator.dummy != null
                && creator.dummy.attachedItems.TryGetValue(id, out var placed)
                && placed is CustomBodyPartAttachment custom
                && creator.createNew != null)
            {
                Thumbnailer.Capture(custom);

                var canvas = creator.createNew.GetComponentInParent<Canvas>();
                Transform canvasT = canvas != null ? canvas.rootCanvas.transform : creator.transform;
                GameObject inputTemplate = creator.characterName != null ? creator.characterName.gameObject : null;
                ScaleSession.Open(creator.createNew.gameObject, inputTemplate, canvasT, custom,
                    sourceKey, part.DisplayName, startScale, startOffset, startEuler, startScaleAxis, startGender,
                    folderCtx);
            }
        }

        /// <summary>
        /// Picks a starting scale so the mesh's largest dimension is about DefaultScale world units.
        /// Imported OBJs arrive at wildly different (often tiny) modelled scales; this makes any
        /// part visible on import instead of appearing as an invisible dot.
        /// </summary>
        internal static string MakeId(string sourceKey) => "CustomPart_" + Slug(sourceKey);

        internal static float NormalizeScale(UnityEngine.Mesh mesh)
        {
            Vector3 size = mesh.bounds.size;
            float meshMax = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            float target = Mathf.Max(0.01f, Plugin.DefaultScale.Value);
            float s = meshMax > 1e-6f ? target / meshMax : target;
            s = Mathf.Clamp(s, 0.001f, 10000f);
            Plugin.Log.LogInfo($"[normalize] meshMax={meshMax:0.#####} target={target} -> startScale={s:0.##}");
            return s;
        }

        internal static string Slug(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "part";
            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw)
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString();
        }
    }
}
