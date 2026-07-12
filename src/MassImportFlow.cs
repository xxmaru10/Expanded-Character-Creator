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
    /// <summary>Conventions every model in the folder inherits — gender filter, paint channel, uniform
    /// scale ×, per-axis scale, rotation, position. Texture is excluded (paired automatically per model).
    /// The user dials these in on a LIVE preview of the folder's first piece (P15), so what they see is
    /// what the whole batch gets.</summary>
    internal struct MassImportSettings
    {
        public float ScaleMultiplier;  // × over each model's normalized size (1 = default size)
        public Vector3 ScaleAxis;      // per-axis multiplier (P4); 1/1/1 = no stretch
        public Vector3 Euler;          // rotation degrees (P5)
        public Vector3 Offset;         // world-unit position offset
        public string Gender;          // "", "Feminine", "Masculine" (P3)
        public string Channel;         // paint channel id (P2)
        public bool SideLeft;          // sided categories (feet/shoe, hands): whole folder goes to the left side
    }

    /// <summary>Carries the pending folder batch through the live preview: the category/slot/side chosen
    /// up front and the .obj files still to import once the user approves the previewed piece's values.</summary>
    internal sealed class FolderImportContext
    {
        public string[] Category;
        public RiggedAttachType Slot;
        public string[] RemainingFiles; // every .obj except the previewed first piece
        public bool SideLeft;
        public int TotalCount;          // whole folder count (previewed piece + remaining)
    }

    /// <summary>
    /// P1/P15 — drives the "Importar Pasta" button. Picks the side (for sided categories) and the
    /// folder, then imports the folder's FIRST .obj as a live preview and opens the normal scale panel
    /// with an extra "Aplicar a toda a pasta" button. The values the user dials in there are applied to
    /// every remaining .obj in one go (texture paired automatically per model), each persisted
    /// immediately so the batch survives a restart. GLB is excluded (reload-on-restart doesn't support
    /// it yet).
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
            var kind = SidedCategory.KindOf(category); // feet/shoe or hands: pick a left/right side once

            // Sided category: ask the side FIRST (the whole folder goes to it), then pick the folder and
            // preview. Non-sided goes straight to the folder pick.
            if (kind != SidedCategory.Kind.None)
            {
                var canvas = creator.createNew != null ? creator.createNew.GetComponentInParent<Canvas>() : null;
                Transform canvasT = canvas != null ? canvas.rootCanvas.transform : creator.transform;
                GameObject buttonTemplate = creator.createNew != null ? creator.createNew.gameObject : null;
                string storeKey = SidedCategory.StoreKey(kind);
                SidePrompt.Open(buttonTemplate, canvasT, kind, ScaleStore.GetLastSideLeft(storeKey), left =>
                {
                    ScaleStore.SetLastSideLeft(storeKey, left);
                    RiggedAttachType slot = left ? SidedCategory.LeftSlot(kind) : SidedCategory.RightSlot(kind);
                    PickFolderAndPreview(category, slot, left).StartCoroutine();
                });
                return;
            }

            PickFolderAndPreview(category, baseSlot, false).StartCoroutine();
        }

        /// <summary>Picks the folder, then imports its FIRST .obj live and opens the scale panel in
        /// folder-preview mode. The rest of the folder waits in the <see cref="FolderImportContext"/> until
        /// the user presses "Aplicar a toda a pasta".</summary>
        private static IEnumerator PickFolderAndPreview(string[] category, RiggedAttachType slot, bool sideLeft)
        {
            yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Folders, allowMultiSelection: false,
                null, null, Loc.T("Selecionar pasta com .obj"), Loc.T("Importar pasta"));

            string[] result = FileBrowser.Result;
            if (result == null || result.Length == 0) yield break;

            string[] files;
            try
            {
                // Recursive: a folder of subfolders (each a garment) imports in one go. Each .obj still
                // pairs with the textures in its OWN folder, and the whole tree goes to the open category
                // with the chosen side — use "Importar Pasta (texturas compartilhadas)" for mixed-part
                // trees that must auto-route to different categories.
                files = Directory.GetFiles(result[0], "*.obj", SearchOption.AllDirectories);
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
            if (files.Length > 1400)
            {
                Compat.ShowError(string.Format("Muitos arquivos detectados ({0}). O limite máximo por importação é 1400 para evitar travamentos ou falta de memória. Certifique-se de não selecionar a pasta raiz (ex. CAS_Export) por engano.", files.Length));
                yield break;
            }

            // Import the first piece live so the user sizes/positions a REAL model before committing the
            // whole folder to those values.
            string first = files[0];
            RPGMesh rpg = Compat.ImportMesh(Path.GetFileNameWithoutExtension(first), first, "obj");
            if (rpg == null || rpg.mesh == null || rpg.mesh.vertexCount == 0)
            {
                Compat.ShowError("Nao consegui importar a primeira peca da pasta (use .obj validos).");
                yield break;
            }

            string[] rest = new string[files.Length - 1];
            Array.Copy(files, 1, rest, 0, rest.Length);

            var ctx = new FolderImportContext
            {
                Category = category,
                Slot = slot,
                RemainingFiles = rest,
                SideLeft = sideLeft,
                TotalCount = files.Length,
            };

            // Spawns the first part, opens the scale panel with the "Aplicar a toda a pasta" button.
            ImportFlow.ImportLoadedMesh(rpg, category, slot, first, ctx);
        }

        /// <summary>Called by the scale panel's "Aplicar a toda a pasta" button: imports every remaining
        /// .obj using the previewed piece's values. Runs across frames so a big folder doesn't freeze.</summary>
        internal static void ImportRemaining(FolderImportContext ctx, MassImportSettings settings)
        {
            if (ctx == null) return;
            RunRemaining(ctx, settings).StartCoroutine();
        }

        private static IEnumerator RunRemaining(FolderImportContext ctx, MassImportSettings settings)
        {
            string[] files = ctx.RemainingFiles ?? new string[0];
            int ok = 0, fail = 0;
            // Fix B — batch the disk writes: the whole folder rewrites scales.json ONCE (on Flush).
            ScaleStore.BeginBatch();
            try
            {
                for (int i = 0; i < files.Length; i++)
                {
                    string path = files[i];
                    bool imported;
                    try { imported = ImportOne(path, ctx.Category, ctx.Slot, settings); }
                    catch (Exception e)
                    {
                        imported = false;
                        Plugin.Log.LogWarning("[mass-import] '" + path + "': " + e.Message);
                    }
                    if (imported) ok++; else fail++;

                    // Fix C — yield every few items so a big folder keeps the UI alive; progress -> log.
                    if ((i & 7) == 7)
                    {
                        Plugin.Log.LogInfo($"[mass-import] {i + 1}/{files.Length}...");
                        yield return null;
                    }
                }
            }
            finally
            {
                ScaleStore.Flush(); // single write of the whole batch, even if the loop threw
            }

            // Repaint the tab so the new parts show; IconButton generates each one's model icon on draw.
            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator != null && creator.itemTabsLoader != null) creator.itemTabsLoader.Refresh();

            int total = ok + 1; // + the previewed first piece (already imported + persisted)
            Compat.ShowSuccess(Loc.T("Pasta importada:") + $" {total} " + Loc.T(fail > 0 ? "parte(s), alguns com erro." : "parte(s)."));
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
            // mesh then multiplied, so differently-sized OBJs still land at a consistent, visible size.
            float normalized = ImportFlow.NormalizeScale(mesh);
            float scale = normalized * settings.ScaleMultiplier;

            // Pair textures automatically: variant 0 is the same-named sibling (.mtl map_Kd or
            // "<model>.png"), and the numbered siblings "<model>1.png", "<model>2.png", ... that the
            // CAS Batch Exporter writes for the other colour swatches fill the "+" variant slots.
            var variants = TextureLoader.FindVariants(modelPath);
            string linkGroupId = PartNameRouter.ResolveLinkGroupId(fileName, new DirectoryInfo(Path.GetDirectoryName(modelPath)).Name);
            string folderName = linkGroupId;
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
                Plugin.Log.LogWarning($"[copy-self-contained-mass] Falha ao copiar arquivos para a pasta do mod: {ex.Message}");
            }

            string texPath = variants.Count > 0 ? variants[0] : null;
            Texture2D tex = TextureLoader.LoadFromFile(texPath);
            if (tex == null && rpg.texture != null && rpg.texture.width > 1) tex = rpg.texture;

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
                LinkGroupId = linkGroupId,
                Additive = AccessoryMap.ResolveAdditive(category, 0), // P14
                Tag = TagManager.ActiveTag, // P10 — batch gets the active tag
            };

            CustomPartCatalog.Register(part);
            Compat.AddTabInitializer(UniqueMono<CharacterCreator>.instance.itemTabsLoader, id);

            // Portraits are NOT generated here — mass import stays fast. They fill in later from the real
            // preview: when the user applies a part, or via the "Gerar miniaturas" auto-cycle button.

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
                link = linkGroupId,
            });

            return true;
        }
    }
}
