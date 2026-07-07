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

            // Feet/shoe: ask left or right first (the engine keeps them as separate lower-leg sockets),
            // defaulting to the last-used side, then import with the chosen slot. Other categories go
            // straight to the file browser.
            if (FootSide.AppliesTo(category))
            {
                var canvas = creator.createNew != null ? creator.createNew.GetComponentInParent<Canvas>() : null;
                Transform canvasT = canvas != null ? canvas.rootCanvas.transform : creator.transform;
                GameObject buttonTemplate = creator.createNew != null ? creator.createNew.gameObject : null;
                FootSidePrompt.Open(buttonTemplate, canvasT, ScaleStore.GetLastFootSideLeft(), left =>
                {
                    ScaleStore.SetLastFootSideLeft(left);
                    RiggedAttachType sideSlot = left ? RiggedAttachType.legLowerL : RiggedAttachType.legLowerR;
                    MeshImporter.LoadMesh(rpg => OnMeshLoaded(rpg, category, sideSlot), new[] { ".obj" });
                });
                return;
            }

            // MeshImporter opens SimpleFileBrowser and calls us back with the loaded mesh.
            MeshImporter.LoadMesh(rpg => OnMeshLoaded(rpg, category, slot), new[] { ".obj" });
        }

        private static void OnMeshLoaded(RPGMesh rpg, string[] category, RiggedAttachType slot)
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

            // Hybrid sizing. Base = normalized so any OBJ (often tiny) is visible and never 100x off.
            // If this exact model was tuned before, use its saved absolute scale/offset; otherwise
            // start from normalized * the user's last-used multiplier (their preference carries over
            // to new models without breaking on differently-scaled ones).
            float normalized = NormalizeScale(mesh);
            bool hasSaved = ScaleStore.TryGet(sourceKey, out PartTransform saved);

            float startScale;
            Vector3 startOffset, startScaleAxis, startEuler;
            string startGender;
            if (hasSaved)
            {
                // Re-importing a file that was tuned before: reproduce its exact saved values.
                startScale = saved.scale;
                startOffset = saved.offset;
                startScaleAxis = saved.scaleAxis;
                startEuler = saved.euler;
                startGender = saved.gender ?? "";
            }
            else if (ScaleStore.TryGetCategoryDefault(category, out float catMult, out Vector3 catAxis, out Vector3 catEuler, out Vector3 catOffset))
            {
                // A NEW model in a tab that has a saved default ("all heads look like this"):
                // scale = normalized × the category multiplier so different-sized meshes still land right.
                startScale = normalized * catMult;
                startScaleAxis = catAxis;
                startEuler = catEuler;
                startOffset = catOffset;
                startGender = "";
            }
            else
            {
                // No category default yet: fall back to the global last-used multiplier/offset (P1b).
                ScaleStore.GetLast(out float lastMult, out Vector3 lastOffset);
                startScale = normalized * lastMult;
                // Eyes sit slightly IN FRONT of the face by default (user-requested Z ≈ 0.12); the
                // unrelated last-used offset from other parts doesn't apply to them.
                startOffset = EyesCategory.Is(category) ? new Vector3(0f, 0f, 0.12f) : lastOffset;
                startScaleAxis = Vector3.one;
                startEuler = Vector3.zero;
                startGender = "";
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
            // also add more from the panel (P13 variant boxes).
            string modelPath = FileBrowser.Result != null && FileBrowser.Result.Length > 0 ? FileBrowser.Result[0] : null;

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
                string single = !string.IsNullOrEmpty(savedTexPath) && File.Exists(savedTexPath)
                    ? savedTexPath : TextureLoader.FindSibling(modelPath);
                if (!string.IsNullOrEmpty(single)) variants.Add(single);
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
                var canvas = creator.createNew.GetComponentInParent<Canvas>();
                Transform canvasT = canvas != null ? canvas.rootCanvas.transform : creator.transform;
                GameObject inputTemplate = creator.characterName != null ? creator.characterName.gameObject : null;
                ScaleSession.Open(creator.createNew.gameObject, inputTemplate, canvasT, custom,
                    sourceKey, part.DisplayName, startScale, startOffset, startEuler, startScaleAxis, startGender);
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
