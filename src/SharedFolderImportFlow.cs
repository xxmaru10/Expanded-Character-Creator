using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SimpleFileBrowser;
using UnityEngine;
using RpgEngine;
using RpgEngine.Characters;
using UnityUtils;
using UnityUtils.Async;
using RiggedAttachType = RpgEngine.Characters.CharacterCreatorEnums.RiggedAttachType;

namespace CustomPartsMod
{
    /// <summary>
    /// "Importar Pasta (texturas compartilhadas)" — the CAS-export workflow. Picks a folder, walks it
    /// RECURSIVELY, and for EACH subfolder (one garment) shares that folder's textures across all of its
    /// .obj parts. Each part is auto-routed to the right body category/slot/side by its filename prefix
    /// (torso / braço / antebraço / mão / perna / pé …), so selecting the top-level "Top" folder imports
    /// every garment into the correct categories in one click. Placement uses each category's saved
    /// default (P6). Persists in one batch (Fix B) and yields (Fix C) to keep the UI alive. Textures are
    /// taken as-is from each folder — the user keeps only the unique swatches, no per-part copies.
    /// </summary>
    internal class ImportLoadingUI : MonoBehaviour
    {
        public int Total;
        public int Current;

        private void OnGUI()
        {
            var r = new Rect(Screen.width / 2f - 200f, Screen.height / 2f - 30f, 400f, 60f);
            GUI.Box(r, "");
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 24 };
            style.normal.textColor = Color.white;
            GUI.Label(r, $"Importando OBJs: {Current} de {Total}...", style);
        }
    }

    internal static class SharedFolderImportFlow
    {
        private static readonly string[] ImageExts = { ".png", ".jpg", ".jpeg" };

        internal static void OnClicked()
        {
            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator == null || creator.itemTabsLoader == null)
            {
                Compat.ShowError("Criador de personagens indisponivel.");
                return;
            }
            RunImport().StartCoroutine();
        }

        private static IEnumerator RunImport()
        {
            yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Folders, allowMultiSelection: false,
                null, null, Loc.T("Selecionar pasta (subpastas incluídas)"), Loc.T("Importar pasta"));

            string[] result = FileBrowser.Result;
            if (result == null || result.Length == 0) yield break;

            string[] files;
            try { files = Directory.GetFiles(result[0], "*.obj", SearchOption.AllDirectories); }
            catch (Exception e) { Compat.ShowError(Loc.T("Nao consegui ler a pasta:") + " " + e.Message); yield break; }

            if (files.Length == 0) { Compat.ShowError("Nenhum .obj encontrado nessa pasta."); yield break; }
            if (files.Length > 1400)
            {
                Compat.ShowError(string.Format("Muitos arquivos detectados ({0}). O limite máximo por importação é 1400 para evitar travamentos ou falta de memória. Certifique-se de não selecionar a pasta raiz (ex. CAS_Export) por engano.", files.Length));
                yield break;
            }

            // Group by containing folder so texture-sharing is scoped per garment (never across garments).
            var byDir = files.GroupBy(f => Path.GetDirectoryName(f));

            int ok = 0, fail = 0, skipped = 0, seen = 0;
            var loadingGo = new GameObject("ImportLoading");
            var loadingUi = loadingGo.AddComponent<ImportLoadingUI>();
            loadingUi.Total = files.Length;

            ScaleStore.BeginBatch();
            try
            {
                foreach (var group in byDir)
                {
                    List<string> shared = SharedTextures(group.Key);
                    foreach (var objPath in group)
                    {
                        int r;
                        try { r = ImportOne(objPath, shared); }
                        catch (Exception e) { r = -1; Plugin.Log.LogWarning("[shared-import] '" + objPath + "': " + e.Message); }
                        if (r == 1) ok++; else if (r == 0) skipped++; else fail++;

                        seen++;
                        loadingUi.Current = seen;
                        Plugin.Log.LogInfo($"[shared-import] {seen}/{files.Length}...");
                        yield return null;
                    }
                }
            }
            finally 
            { 
                ScaleStore.Flush(); 
                UnityEngine.Object.Destroy(loadingGo);
            }

            var creator = UniqueMono<CharacterCreator>.instance;
            if (ok > 0 && creator != null && creator.itemTabsLoader != null) creator.itemTabsLoader.Refresh();

            if (ok > 0)
                Compat.ShowSuccess(Loc.T("Pasta importada:") + $" {ok} " + Loc.T("parte(s).") +
                    (skipped > 0 ? $" ({skipped} " + Loc.T("puladas") + ")" : ""));
            else
                Compat.ShowError("Nenhum .obj da pasta pode ser importado.");
        }

        /// <returns>1 = imported, 0 = skipped (unknown prefix / no native category), -1 = failed.</returns>
        private static int ImportOne(string modelPath, List<string> sharedTextures)
        {
            string fileName = Path.GetFileNameWithoutExtension(modelPath);

            var route = PartNameRouter.Resolve(fileName);
            if (!route.Ok)
            {
                Plugin.Log.LogWarning("[shared-import] prefixo desconhecido, pulado: " + fileName);
                return 0;
            }
            if (!NativeCategory.TryFindPath(route.Segments, out string[] category))
            {
                Plugin.Log.LogWarning("[shared-import] categoria nativa nao encontrada (" + route.Label + "), pulado: " + fileName);
                return 0;
            }

            RiggedAttachType slot = route.Kind != SidedCategory.Kind.None
                ? (route.Left ? SidedCategory.LeftSlot(route.Kind) : SidedCategory.RightSlot(route.Kind))
                : CategoryMap.ToSocket(category);

            RPGMesh rpg = Compat.ImportMesh(fileName, modelPath, "obj");
            if (rpg == null || rpg.mesh == null || rpg.mesh.vertexCount == 0) return -1;

            Mesh mesh = rpg.mesh;
            mesh.RecalculateBounds();
            if (mesh.normals == null || mesh.normals.Length == 0) mesh.RecalculateNormals();

            string sourceKey = string.IsNullOrEmpty(rpg.id) ? fileName : rpg.id;
            string id = ImportFlow.MakeId(sourceKey);
            string gender = "";
            string k = fileName.ToLowerInvariant();
            if (k.Contains("yf") || k.Contains("female") || k.Contains("feminino") || k.Contains("mulher"))
                gender = "Feminine";
            else if (k.Contains("ym") || k.Contains("male") || k.Contains("masculino") || k.Contains("homem"))
                gender = "Masculine";

            // Placement from the target category's override (P6) when present, else the global factor —
            // both MULTIPLIERS over each mesh's normalized size, so every body category keeps its own
            // tuned look across the batch and always lands visible.
            float normalized = ImportFlow.NormalizeScale(mesh);
            Vector3 axis = Vector3.one, euler = Vector3.zero, offset = Vector3.zero;
            float scale;
            if (ScaleStore.TryGetCategoryDefault(category, slot, gender, out float catMult, out Vector3 a, out Vector3 e, out Vector3 o))
            {
                scale = normalized * catMult; axis = a; euler = e; offset = o;
            }
            else
            {
                ScaleStore.GetGlobalMult(out float globalMult, out offset);
                scale = normalized * globalMult;
            }

            // Shared texture set for this garment folder, variant 0 active. Every part of the folder points
            // at the SAME files — no copies, no per-part naming.
            var variants = new List<string>(sharedTextures);
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
                Plugin.Log.LogWarning($"[copy-self-contained-shared] Falha ao copiar arquivos para a pasta do mod: {ex.Message}");
            }

            string texPath = variants.Count > 0 ? variants[0] : null;
            Texture2D tex = TextureLoader.LoadFromFile(texPath);
            if (tex == null && rpg.texture != null && rpg.texture.width > 1) tex = rpg.texture;

            string channel = ChannelMap.ForCategory(category);

            var part = new CustomPart
            {
                PartId = id,
                SourceKey = sourceKey,
                DisplayName = fileName,
                CategoryPath = category,
                Slot = slot,
                ChannelId = channel,
                Mesh = mesh,
                Texture = tex,
                TexturePath = tex != null ? texPath : null,
                TextureVariants = variants,
                ActiveVariant = 0,
                ModelPath = modelPath,
                Scale = scale,
                NormalizedScale = normalized,
                ScaleAxis = axis,
                LocalPosition = offset,
                LocalEuler = euler,
                GenderTag = gender,
                AdditiveOverride = 0,
                LinkGroupId = linkGroupId,
                Additive = AccessoryMap.ResolveAdditive(category, 0),
                Tag = TagManager.ActiveTag,
            };

            CustomPartCatalog.Register(part);
            Compat.AddTabInitializer(UniqueMono<CharacterCreator>.instance.itemTabsLoader, id);

            ScaleStore.Set(sourceKey, new PartTransform
            {
                scale = scale,
                scaleAxis = axis,
                offset = offset,
                euler = euler,
                gender = gender,
                channel = channel,
                texturePath = part.TexturePath,
                textureVariants = variants.ToArray(),
                activeVariant = 0,
                modelPath = modelPath,
                category = category,
                slot = slot.ToString(),
                additive = 0,
                tag = part.Tag,
                link = linkGroupId
            });

            return 1;
        }

        // All images in a folder, ordered by swatch index (base = 0, then the numbered swatches). The
        // order is derived from the images' longest common filename prefix, so it stays correct even when
        // the garment hash ends in a digit.
        private static List<string> SharedTextures(string dir)
        {
            var imgs = new List<string>();
            try
            {
                if (string.IsNullOrEmpty(dir)) return imgs;
                foreach (var p in Directory.GetFiles(dir))
                    if (Array.IndexOf(ImageExts, Path.GetExtension(p).ToLowerInvariant()) >= 0)
                        imgs.Add(p);
            }
            catch (Exception e) { Plugin.Log.LogWarning("[shared-import] lista de texturas falhou: " + e.Message); return imgs; }

            if (imgs.Count <= 1) return imgs;

            var names = imgs.Select(Path.GetFileNameWithoutExtension).ToList();
            string prefix = LongestCommonPrefix(names);
            imgs.Sort((x, y) =>
            {
                int ix = SwatchIndex(Path.GetFileNameWithoutExtension(x), prefix);
                int iy = SwatchIndex(Path.GetFileNameWithoutExtension(y), prefix);
                return ix != iy ? ix.CompareTo(iy) : string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            });
            return imgs;
        }

        private static int SwatchIndex(string name, string prefix)
        {
            string rest = !string.IsNullOrEmpty(prefix) && name.Length >= prefix.Length ? name.Substring(prefix.Length) : name;
            if (string.IsNullOrEmpty(rest)) return 0;                 // base swatch (no trailing number)
            return int.TryParse(rest, out int v) ? v : int.MaxValue;   // non-numeric tail sinks to the end
        }

        private static string LongestCommonPrefix(List<string> names)
        {
            if (names == null || names.Count == 0) return "";
            string p = names[0];
            foreach (var n in names)
            {
                int k = 0;
                while (k < p.Length && k < n.Length && char.ToLowerInvariant(p[k]) == char.ToLowerInvariant(n[k])) k++;
                p = p.Substring(0, k);
                if (p.Length == 0) break;
            }
            return p;
        }
    }
}
