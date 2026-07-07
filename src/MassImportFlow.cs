using System;
using System.Collections;
using System.IO;
using SimpleFileBrowser;
using UnityEngine;
using RpgEngine;
using RpgEngine.Characters;
using UnityUtils;
using UnityUtils.Async;
using RiggedAttachType = RpgEngine.Characters.CharacterCreatorEnums.RiggedAttachType;

namespace CustomPartsMod
{
    /// <summary>Conventions the user fixes once (MassImportConfig) and every model in the folder
    /// inherits — gender filter, paint channel, uniform scale ×, per-axis scale, rotation, position.
    /// Texture is excluded (paired automatically per model).</summary>
    internal struct MassImportSettings
    {
        public float ScaleMultiplier;  // × over each model's normalized size (1 = default size)
        public Vector3 ScaleAxis;      // per-axis multiplier (P4); 1/1/1 = no stretch
        public Vector3 Euler;          // rotation degrees (P5)
        public Vector3 Offset;         // world-unit position offset
        public string Gender;          // "", "Feminine", "Masculine" (P3)
        public string Channel;         // paint channel id (P2)
        public bool FootSideLeft;      // feet/shoe categories: whole folder goes to the left (else right) foot
    }

    /// <summary>
    /// P1 — drives the "Importar Pasta" button. First opens the conventions panel (MassImportConfig);
    /// on confirm, picks a whole folder, silently imports every .obj in it into the currently open
    /// category applying those conventions, pairs a same-named texture automatically, and persists
    /// each one immediately (mirrors "Confirmar") so the whole batch survives a restart without a
    /// per-item step. GLB is excluded because reload-on-restart (P0) doesn't support it yet.
    /// </summary>
    internal static class MassImportFlow
    {
        internal static void OnImportFolderClicked()
        {
            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator == null || creator.itemTabsLoader == null)
            {
                Compat.ShowError("Criador de personagens indisponivel.");
                return;
            }

            string[] category = Compat.GetPathFilter(creator.itemTabsLoader);
            if (category == null || (!CustomCategory.IsSynthetic(category) && category.Length < 3))
            {
                Compat.ShowError("Abra uma categoria (ex.: Cabecas, Olhos ou Pés) antes de importar.");
                return;
            }

            RiggedAttachType baseSlot = CategoryMap.ToSocket(category);
            bool footCategory = FootSide.AppliesTo(category); // feet/shoe: the panel offers a left/right choice

            // Open the conventions panel; the actual folder pick + import happens on confirm.
            var canvas = creator.createNew != null ? creator.createNew.GetComponentInParent<Canvas>() : null;
            Transform canvasT = canvas != null ? canvas.rootCanvas.transform : creator.transform;
            GameObject buttonTemplate = creator.createNew != null ? creator.createNew.gameObject : null;
            GameObject inputTemplate = creator.characterName != null ? creator.characterName.gameObject : null;

            MassImportConfig.Open(buttonTemplate, inputTemplate, canvasT, category, settings =>
            {
                RiggedAttachType slot = baseSlot;
                if (footCategory)
                {
                    ScaleStore.SetLastFootSideLeft(settings.FootSideLeft); // remember for next time
                    slot = settings.FootSideLeft ? RiggedAttachType.legLowerL : RiggedAttachType.legLowerR;
                }
                RunImport(category, slot, settings).StartCoroutine();
            });
        }

        private static IEnumerator RunImport(string[] category, RiggedAttachType slot, MassImportSettings settings)
        {
            yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Folders, allowMultiSelection: false,
                null, null, Loc.T("Selecionar pasta com .obj"), Loc.T("Importar pasta"));

            string[] result = FileBrowser.Result;
            if (result == null || result.Length == 0) yield break;

            string[] files;
            try
            {
                files = Directory.GetFiles(result[0], "*.obj", SearchOption.TopDirectoryOnly);
            }
            catch (Exception e)
            {
                Compat.ShowError(Loc.T("Nao consegui ler a pasta:") + " " + e.Message);
                yield break;
            }

            if (files.Length == 0)
            {
                Compat.ShowError("Nenhum .obj encontrado nessa pasta.");
                yield break;
            }

            int ok = 0, fail = 0;
            foreach (var path in files)
            {
                bool imported;
                try { imported = ImportOne(path, category, slot, settings); }
                catch (Exception e)
                {
                    imported = false;
                    Plugin.Log.LogWarning("[mass-import] '" + path + "': " + e.Message);
                }
                if (imported) ok++; else fail++;
            }

            var creator = UniqueMono<CharacterCreator>.instance;
            if (ok > 0 && creator != null && creator.itemTabsLoader != null) creator.itemTabsLoader.Refresh();

            if (ok > 0) Compat.ShowSuccess(Loc.T("Pasta importada:") + $" {ok} " + Loc.T(fail > 0 ? "parte(s), alguns com erro." : "parte(s)."));
            else Compat.ShowError("Nenhum .obj da pasta pode ser importado.");
        }

        private static bool ImportOne(string modelPath, string[] category, RiggedAttachType slot, MassImportSettings settings)
        {
            string fileName = Path.GetFileNameWithoutExtension(modelPath);
            RPGMesh rpg = Compat.ImportMesh(fileName, modelPath, "obj");
            if (rpg == null || rpg.mesh == null || rpg.mesh.vertexCount == 0) return false;

            Mesh mesh = rpg.mesh;
            mesh.RecalculateBounds();
            if (mesh.normals == null || mesh.normals.Length == 0) mesh.RecalculateNormals();

            string sourceKey = string.IsNullOrEmpty(rpg.id) ? fileName : rpg.id;
            string id = ImportFlow.MakeId(sourceKey);

            // Every model in the batch follows the user's fixed conventions. Scale is normalized per
            // mesh then multiplied, so differently-sized OBJs still land at a consistent world size.
            float normalized = ImportFlow.NormalizeScale(mesh);
            float scale = normalized * settings.ScaleMultiplier;

            // Pair a texture automatically: a sibling of the model (.mtl map_Kd or same-named PNG/JPG).
            string texPath = TextureLoader.FindSibling(modelPath);
            Texture2D tex = TextureLoader.LoadFromFile(texPath);
            if (tex == null && rpg.texture != null && rpg.texture.width > 1) tex = rpg.texture;

            // P13 — one variant to start (the auto-paired texture); the user can add more in the panel.
            var variants = new System.Collections.Generic.List<string>();
            if (tex != null && !string.IsNullOrEmpty(texPath)) variants.Add(texPath);

            var part = new CustomPart
            {
                PartId = id,
                SourceKey = sourceKey,
                DisplayName = string.IsNullOrEmpty(rpg.id) ? fileName : rpg.id,
                CategoryPath = category,
                Slot = slot,
                ChannelId = settings.Channel,
                Mesh = mesh,
                Texture = tex,
                TexturePath = tex != null ? texPath : null,
                TextureVariants = variants,
                ActiveVariant = 0,
                ModelPath = modelPath,
                Scale = scale,
                NormalizedScale = normalized,
                ScaleAxis = settings.ScaleAxis,
                LocalPosition = settings.Offset,
                LocalEuler = settings.Euler,
                GenderTag = settings.Gender ?? "",
                AdditiveOverride = 0, // mass import uses the auto rule (eyes + accessory categories)
                Additive = AccessoryMap.ResolveAdditive(category, 0), // P14
                Tag = TagManager.ActiveTag, // P10 — batch gets the active tag
            };

            CustomPartCatalog.Register(part);
            Compat.AddTabInitializer(UniqueMono<CharacterCreator>.instance.itemTabsLoader, id);

            // Persist right away (same record ScaleSession.Confirm would write) so the batch survives
            // a restart without the user opening each part's panel one by one.
            ScaleStore.Set(sourceKey, new PartTransform
            {
                scale = scale,
                scaleAxis = settings.ScaleAxis,
                offset = settings.Offset,
                euler = settings.Euler,
                gender = settings.Gender ?? "",
                channel = settings.Channel,
                texturePath = part.TexturePath,
                textureVariants = variants.ToArray(),
                activeVariant = 0,
                modelPath = modelPath,
                category = category,
                slot = slot.ToString(),
                additive = 0,
                tag = part.Tag,
            });

            return true;
        }
    }
}
