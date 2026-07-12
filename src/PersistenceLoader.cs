using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityUtils;
using RpgEngine.Characters;
using RiggedAttachType = RpgEngine.Characters.CharacterCreatorEnums.RiggedAttachType;

namespace CustomPartsMod
{
    /// <summary>
    /// P0 — persistence across sessions. On the first time the creator opens, re-registers every saved
    /// model (those confirmed via the panel) so custom parts survive a restart. Registration uses the
    /// saved record's METADATA only — no OBJ is parsed and no PNG is decoded here. The mesh + texture are
    /// imported lazily by <see cref="CustomPart.EnsureLoaded"/> the first time the part is applied, which
    /// keeps opening the creator fast even with thousands of saved parts.
    /// </summary>
    internal static class PersistenceLoader
    {
        private static bool _loaded;
        private static HashSet<string> _existingFiles;
        // Cache de resolução caminho-do-registro -> caminho-real (ou null se ausente), pra não re-globar
        // o mesmo arquivo no pre-scan e no Register. Limpo ao fim do LoadAll.
        private static Dictionary<string, string> _resolveCache;

        /// <summary>
        /// Resolve o caminho de um modelo salvo para o arquivo REAL no disco, tolerando a corrupção de
        /// encoding do scales.json: acentos foram gravados como Latin-1 (0xE7 = 'ç') e, lidos como UTF-8,
        /// viram o caractere de substituição U+FFFD ('�'). Nesses casos o caminho direto não existe, então
        /// fazemos um glob na pasta trocando cada '�' por '*' (o resto do nome, com o hash, é específico o
        /// bastante). Retorna false só quando o arquivo realmente não está lá.
        /// </summary>
        private static bool TryResolveModel(string modelPath, out string resolved)
        {
            resolved = null;
            if (string.IsNullOrEmpty(modelPath)) return false;

            if (_resolveCache != null && _resolveCache.TryGetValue(modelPath, out resolved))
                return resolved != null;

            string result = null;
            try
            {
                string full = Path.GetFullPath(modelPath);
                if (_existingFiles != null && _existingFiles.Contains(full)) result = modelPath;
                else if (File.Exists(modelPath)) result = modelPath;
                else if (modelPath.IndexOf('�') >= 0)
                {
                    string dir = Path.GetDirectoryName(modelPath);
                    string name = Path.GetFileName(modelPath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        string pattern = name.Replace('�', '*');
                        while (pattern.Contains("**")) pattern = pattern.Replace("**", "*");
                        var matches = Directory.GetFiles(dir, pattern);
                        if (matches.Length > 0) result = matches[0];
                    }
                }
            }
            catch { result = null; }

            if (_resolveCache == null) _resolveCache = new Dictionary<string, string>(StringComparer.Ordinal);
            _resolveCache[modelPath] = result;
            resolved = result;
            return result != null;
        }

        private static int GetSlotPriority(RiggedAttachType slot)
        {
            switch (slot)
            {
                case RiggedAttachType.torso: return 10;
                case RiggedAttachType.hip: return 9;
                case RiggedAttachType.kneeL: return 8;
                case RiggedAttachType.legLowerL: return 7;
                case RiggedAttachType.handL: return 6;
                case RiggedAttachType.armUpperL: return 5;
                case RiggedAttachType.armLowerL: return 4;
                case RiggedAttachType.head: return 3;
                
                case RiggedAttachType.kneeR: return 2;
                case RiggedAttachType.legLowerR: return 2;
                case RiggedAttachType.handR: return 2;
                case RiggedAttachType.armUpperR: return 2;
                case RiggedAttachType.armLowerR: return 2;
                
                default: return 0;
            }
        }

        internal static void LoadAll(CharacterCreator creator)
        {
            if (_loaded) return; // once per session; parts stay registered afterwards
            _loaded = true;

            if (creator == null || creator.itemTabsLoader == null) return;

            // Cache all files in CustomParts recursively to avoid slow File.Exists calls
            _existingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string modelsDir = Path.Combine(ScaleStore.CustomPartsDir, "models");
                if (Directory.Exists(modelsDir))
                {
                    foreach (string file in Directory.GetFiles(modelsDir, "*.obj", SearchOption.AllDirectories))
                    {
                        _existingFiles.Add(Path.GetFullPath(file));
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("[persist] Falha ao pre-carregar cache de arquivos: " + ex.Message);
            }

            // Pre-scan para eleger a peça gatilho principal de cada LinkGroupId. O gatilho vira o ÚNICO
            // item do grupo com botão na aba — então precisa ser uma peça cujo ARQUIVO EXISTE. Um garment
            // do CAS costuma ter torso + braços + antebraços no MESMO link/slot(torso); se o gatilho eleito
            // fosse só "o primeiro" e calhasse de ser um braço/antebraço com acento (caminho corrompido no
            // scales.json, ver TryResolveModel), o Register falhava e o garment INTEIRO sumia da aba.
            // Pontuação: existir domina (1000) sobre a prioridade de slot (0-10), então o torso real
            // (arquivo ok) sempre ganha do braço com nome quebrado.
            var linkGroupTriggers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var linkGroupBestScore = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in ScaleStore.AllModels())
            {
                string linkId = kv.Value.link ?? "";
                if (string.IsNullOrEmpty(linkId)) continue;

                RiggedAttachType slot = SidedCategory.ResolveSlot(kv.Value.category, kv.Key, kv.Value.slot);
                int priority = GetSlotPriority(slot);
                bool exists = TryResolveModel(kv.Value.modelPath, out _);
                int score = (exists ? 1000 : 0) + priority;

                // Group by linkId + the ACTUAL slot (L and R kept separate) so each side gets its own
                // button/icon in the list. Only true same-slot duplicates collapse to one trigger; the
                // left/right counterpart still auto-equips via the LinkGroupId when either is applied.
                string groupKey = linkId + "_" + slot;

                if (!linkGroupBestScore.TryGetValue(groupKey, out int bestScore) || score > bestScore)
                {
                    linkGroupBestScore[groupKey] = score;
                    linkGroupTriggers[groupKey] = kv.Key;
                }
            }

            int ok = 0, fail = 0;
            foreach (var kv in ScaleStore.AllModels())
            {
                try { if (Register(kv.Key, kv.Value, linkGroupTriggers)) ok++; else fail++; }
                catch (Exception e) { fail++; Plugin.Log.LogWarning("[persist] '" + kv.Key + "': " + e.Message); }
            }

            // Fazer a propagação em lote de forma O(N) e de forma 100% preguiçosa (sem carregar PNGs do disco)
            if (ok > 0)
            {
                CustomPartCatalog.PropagateAllVariants(eagerLoadTextures: false);
            }

            if (ok > 0) creator.itemTabsLoader.Refresh();
            Plugin.Log.LogInfo($"[persist] {ok} modelo(s) registrado(s), {fail} pulado(s).");

            _resolveCache = null; // só precisava durante o registro; a peça já guarda o caminho resolvido
        }

        private static bool Register(string sourceKey, PartTransform rec, Dictionary<string, string> linkGroupTriggers)
        {
            if (rec.category == null || rec.category.Length == 0)
                return false; // pre-P0 record without category; user re-confirms once to upgrade it

            // Cheap existence check (a stat, not a parse) so a moved/deleted model doesn't leave a dead
            // button in the tab. The actual geometry/texture load is deferred to EnsureLoaded. Uses the
            // encoding-tolerant resolver so accented filenames that were corrupted in scales.json still
            // resolve to their real file on disk (and the part gets the CORRECTED path).
            if (string.IsNullOrEmpty(rec.modelPath))
                return false;

            if (!TryResolveModel(rec.modelPath, out string modelPath))
            {
                Plugin.Log.LogWarning("[persist] arquivo do modelo ausente: " + rec.modelPath);
                return false;
            }

            RiggedAttachType slot = SidedCategory.ResolveSlot(rec.category, sourceKey, rec.slot);

            // P13 — restore the texture variant list + active index. Paths only; the active texture is
            // decoded lazily on first apply (EnsureLoaded).
            var variants = new System.Collections.Generic.List<string>();
            if (rec.textureVariants != null)
                foreach (var v in rec.textureVariants) if (!string.IsNullOrEmpty(v)) variants.Add(v);
            if (variants.Count == 0 && !string.IsNullOrEmpty(rec.texturePath)) variants.Add(rec.texturePath);
            int activeVariant = variants.Count > 0 ? Mathf.Clamp(rec.activeVariant, 0, variants.Count - 1) : 0;
            string activePath = variants.Count > 0 ? variants[activeVariant] : null;

            var part = new CustomPart
            {
                PartId = ImportFlow.MakeId(sourceKey),
                SourceKey = sourceKey,
                ModelPath = modelPath, // resolved real path (handles the scales.json accent corruption)
                DisplayName = Path.GetFileNameWithoutExtension(modelPath),
                CategoryPath = rec.category,
                Slot = slot,
                ChannelId = !string.IsNullOrEmpty(rec.channel) ? rec.channel : ChannelMap.ForCategory(rec.category), // P2
                Tag = rec.tag ?? "", // P10 user tag/theme
                Mesh = null,          // imported lazily on first apply
                Texture = null,       // decoded lazily on first apply
                TexturePath = activePath,
                TextureVariants = variants,
                ActiveVariant = activeVariant,
                Scale = rec.scale,
                NormalizedScale = 1f, // recomputed with the mesh in EnsureLoaded (only used when re-editing)
                ScaleAxis = rec.scaleAxis,
                LocalPosition = rec.offset,
                LocalEuler = rec.euler,
                GenderTag = rec.gender ?? "",
                AdditiveOverride = rec.additive,
                LinkGroupId = rec.link ?? "",
                Additive = AccessoryMap.ResolveAdditive(rec.category, rec.additive), // eyes/accessories stay additive on reload (P14)
            };

            CustomPartCatalog.Register(part, deferPropagation: true); // -> attachmentPaths, defer variant check
            
            // Um item ganha botão na UI se for peça avulsa OU se for o gatilho principal eleito do seu grupo.
            // groupKey usa o slot REAL (L e R separados) pra cada lado ter seu próprio ícone na lista.
            string groupKey = part.LinkGroupId + "_" + part.Slot;
            bool isTrigger = string.IsNullOrEmpty(part.LinkGroupId) || 
                             (linkGroupTriggers.TryGetValue(groupKey, out var triggerKey) && triggerKey == sourceKey);

            if (isTrigger)
            {
                Compat.AddTabInitializer(UniqueMono<CharacterCreator>.instance.itemTabsLoader, part.PartId);
            }
            return true;
        }
    }
}
