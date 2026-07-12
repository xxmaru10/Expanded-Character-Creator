using System.Collections.Generic;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// In-memory registry of imported parts, plus the bridge into the engine's
    /// character-part catalog (CharacterCreator.attachmentPaths). Registering a part
    /// makes it a first-class, selectable entry that the tab UI can display.
    /// </summary>
    internal static class CustomPartCatalog
    {
        private static readonly Dictionary<string, CustomPart> Parts =
            new Dictionary<string, CustomPart>(System.StringComparer.Ordinal);

        internal static bool IsCustom(string partId) => Parts.ContainsKey(partId);

        internal static bool TryGet(string partId, out CustomPart part) => Parts.TryGetValue(partId, out part);

        /// <summary>All registered custom parts (P15 — random build groups these by category).</summary>
        internal static IEnumerable<CustomPart> AllParts() => Parts.Values;

        internal static void Register(CustomPart part, bool deferPropagation = false)
        {
            Parts[part.PartId] = part;

            // Propagate and merge texture variants among parts in the same link group
            if (!deferPropagation && !string.IsNullOrEmpty(part.LinkGroupId))
            {
                List<string> bestVariants = part.TextureVariants ?? new List<string>();
                foreach (var p in Parts.Values)
                {
                    if (p.LinkGroupId == part.LinkGroupId && p.TextureVariants != null && p.TextureVariants.Count > bestVariants.Count)
                    {
                        bestVariants = p.TextureVariants;
                    }
                }

                if (bestVariants.Count > 0)
                {
                    if (part.TextureVariants == null || part.TextureVariants.Count < bestVariants.Count)
                    {
                        part.TextureVariants = new List<string>(bestVariants);
                        if (part.TextureVariants.Count > 0)
                        {
                            part.TexturePath = part.TextureVariants[0];
                            part.Texture = TextureLoader.LoadFromFile(part.TexturePath);
                        }
                    }

                    foreach (var p in Parts.Values)
                    {
                        if (p.LinkGroupId == part.LinkGroupId && (p.TextureVariants == null || p.TextureVariants.Count < bestVariants.Count))
                        {
                            p.TextureVariants = new List<string>(bestVariants);
                            if (p.TextureVariants.Count > 0)
                            {
                                p.ActiveVariant = 0;
                                p.TexturePath = p.TextureVariants[0];
                                p.Texture = TextureLoader.LoadFromFile(p.TexturePath);
                            }
                        }
                    }
                }
            }

            // P7 — the saved portrait (if any) is loaded from disk lazily by IconButton the first time
            // this part's button is actually drawn, not here: registering thousands of parts must not
            // decode thousands of PNGs up front.

            // Synthetic database entry so the creator treats it like any native part.
            // savePath = category prefix + a unique leaf; fullPath is derived from it
            // (never used for custom parts because AddPart is intercepted).
            var savePath = new List<string>(part.CategoryPath) { part.PartId };

            var data = new PropDatabaseData
            {
                savedId = part.PartId,
                inGameName = part.DisplayName,
                visible = true,          // clickable regardless of DLC edition
                savePath = savePath,
            };

            // Gender (P3): the creator's gender toggle EXCLUDES parts by tag ("Feminine"/"Masculine").
            if (!string.IsNullOrEmpty(part.GenderTag)) data.tags.Add(part.GenderTag);

            CharacterCreator.attachmentPaths[part.PartId] = data;
        }

        internal static void PropagateAllVariants(bool eagerLoadTextures = false)
        {
            // 1. Encontrar a melhor lista de variantes de textura para cada LinkGroupId
            var bestVariantsMap = new Dictionary<string, List<string>>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var part in Parts.Values)
            {
                if (string.IsNullOrEmpty(part.LinkGroupId)) continue;

                if (!bestVariantsMap.TryGetValue(part.LinkGroupId, out var currentBest) || 
                    (part.TextureVariants != null && part.TextureVariants.Count > (currentBest?.Count ?? 0)))
                {
                    bestVariantsMap[part.LinkGroupId] = part.TextureVariants;
                }
            }

            // 2. Propagar a melhor lista para todas as partes do mesmo LinkGroupId de forma rápida (e sem carregar arquivos se for lazy)
            foreach (var part in Parts.Values)
            {
                if (string.IsNullOrEmpty(part.LinkGroupId)) continue;

                if (bestVariantsMap.TryGetValue(part.LinkGroupId, out var bestVariants) && bestVariants != null && bestVariants.Count > 0)
                {
                    if (part.TextureVariants == null || part.TextureVariants.Count < bestVariants.Count)
                    {
                        part.TextureVariants = new List<string>(bestVariants);
                        part.ActiveVariant = 0;
                        part.TexturePath = bestVariants[0];
                        if (eagerLoadTextures)
                        {
                            part.Texture = TextureLoader.LoadFromFile(part.TexturePath);
                        }
                    }
                }
            }
        }

        /// <summary>P3 — sets a part's gender tag live. Caller should Refresh the tab so the
        /// gender filter re-evaluates. genderTag is "", "Feminine" or "Masculine".</summary>
        internal static void SetGender(string partId, string genderTag)
        {
            if (Parts.TryGetValue(partId, out var part)) part.GenderTag = genderTag ?? "";

            if (CharacterCreator.attachmentPaths.TryGetValue(partId, out var data))
            {
                data.tags.RemoveAll(t => t == "Feminine" || t == "Masculine");
                if (!string.IsNullOrEmpty(genderTag)) data.tags.Add(genderTag);
            }
        }

        /// <summary>Removes a custom part from the in-memory registry and the engine catalog.</summary>
        internal static void Remove(string partId)
        {
            Parts.Remove(partId);
            if (CharacterCreator.attachmentPaths.ContainsKey(partId))
                CharacterCreator.attachmentPaths.Remove(partId);
        }
    }
}
