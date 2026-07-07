using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityUtils;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// P15 — "Modo Aleatório Personalizado": builds the character by drawing ONLY custom parts
    /// (never native ones), one per category. Categories the user has locked keep their current part;
    /// unlocked ones are re-rolled on each Randomize. Only categories with at least one custom part
    /// take part in the draw. Lock state lives in the session only (it is a usage preference, not
    /// part data — nothing to persist to disk).
    /// </summary>
    internal static class Randomizer
    {
        /// <summary>One randomizable category: a stable key, a friendly label and its custom part ids.</summary>
        internal sealed class Group
        {
            public string Key;
            public string Label;
            public List<string> PartIds = new List<string>();
        }

        // Locked category keys (session only).
        private static readonly HashSet<string> _locked = new HashSet<string>(StringComparer.Ordinal);

        internal static bool IsLocked(string key) => _locked.Contains(key);

        internal static void ToggleLock(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!_locked.Remove(key)) _locked.Add(key);
        }

        /// <summary>Custom parts grouped by category (savePath prefix), ordered by label. Categories with
        /// no custom part never appear (there'd be nothing to draw — and we never fall back to native).</summary>
        internal static List<Group> Groups()
        {
            var byKey = new Dictionary<string, Group>(StringComparer.Ordinal);
            foreach (var p in CustomPartCatalog.AllParts())
            {
                if (p == null || p.CategoryPath == null || p.CategoryPath.Length == 0) continue;
                string key = string.Join("/", p.CategoryPath);
                if (!byKey.TryGetValue(key, out var g))
                {
                    g = new Group { Key = key, Label = FriendlyName(p.CategoryPath) };
                    byKey[key] = g;
                }
                g.PartIds.Add(p.PartId);
            }
            return byKey.Values.OrderBy(g => g.Label, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        /// <summary>Re-rolls every UNLOCKED category, applying one random custom part in each. Locked
        /// categories are left exactly as they are.</summary>
        internal static void Randomize()
        {
            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator == null || creator.dummy == null)
            {
                Compat.ShowError("Criador indisponível para aleatorizar.");
                return;
            }

            int changed = 0;
            foreach (var g in Groups())
            {
                if (_locked.Contains(g.Key) || g.PartIds.Count == 0) continue;

                // The custom part currently applied in this category (if any).
                string applied = g.PartIds.FirstOrDefault(id => creator.dummy.attachedItems.ContainsKey(id));
                string pick = g.PartIds[UnityEngine.Random.Range(0, g.PartIds.Count)];
                if (pick == applied) continue; // already showing the drawn part

                // Explicitly swap: remove the old custom part (covers additive parts like eyes, which the
                // slot-replace logic in Patch_AddPart intentionally does NOT auto-remove), then add the pick.
                if (!string.IsNullOrEmpty(applied) && creator.dummy.Contains(applied))
                    creator.SpawnAlongside(applied);
                if (!creator.dummy.Contains(pick))
                    creator.SpawnAlongside(pick);
                changed++;
            }

            if (changed == 0 && Groups().Count == 0)
                Compat.ShowError("Importe peças custom primeiro — não há nada para sortear.");
        }

        /// <summary>Friendly PT label for a category from its savePath segments; falls back to the last
        /// segment. Language-independent input (stable savePath), localized output for the lock rows.</summary>
        private static string FriendlyName(string[] categoryPath)
        {
            var seg = new HashSet<string>(categoryPath, StringComparer.OrdinalIgnoreCase);
            if (seg.Contains("eyes")) return "Olhos";
            if (seg.Contains("Full_Helmets") || seg.Contains("helmet")) return "Capacete";
            if (seg.Contains("hands")) return "Mãos";
            if (seg.Contains("shoulders")) return "Ombros";
            if (seg.Contains("uppers")) return "Braço (superior)";
            if (seg.Contains("wrists") || seg.Contains("elbows")) return "Antebraço";
            if (seg.Contains("arms")) return "Braços";
            if (seg.Contains("torso")) return "Torso";
            if (seg.Contains("hips")) return "Quadril";
            if (seg.Contains("knees")) return "Joelhos";
            if (seg.Contains("feet") || seg.Contains("legs")) return "Pernas";
            if (seg.Contains("beard") || seg.Contains("E_Beard")) return "Barba";
            if (seg.Contains("hair") || seg.Contains("B_Hair")) return "Cabelo";
            if (seg.Contains("faces") || seg.Contains("race")) return "Rosto";
            if (seg.Contains("heads")) return "Cabeça";
            if (seg.Contains("attachments") || seg.Contains("extras")) return "Acessórios";
            return categoryPath[categoryPath.Length - 1];
        }
    }
}
