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

        /// <summary>A category's draw pool: its custom parts, narrowed to the selected tag when a tag
        /// filter is active (so "Aleatorizar" rolls only within that theme). No tag selected ⇒ all parts.</summary>
        private static List<string> PoolFor(Group g)
        {
            if (!TagManager.FilterActive) return g.PartIds;
            string tag = TagManager.SelectedTag;
            var pool = new List<string>();
            foreach (var id in g.PartIds)
                if (CustomPartCatalog.TryGet(id, out var p) &&
                    string.Equals(p.Tag, tag, StringComparison.CurrentCultureIgnoreCase))
                    pool.Add(id);
            return pool;
        }

        /// <summary>Re-rolls every UNLOCKED category, applying one random custom part in each. Locked
        /// categories are left exactly as they are. When a tag is selected, only parts of that tag are
        /// drawn (a category with none in the tag is skipped). Only IMPORTED (custom) parts are ever
        /// drawn — native game parts are never touched.</summary>
        internal static void Randomize()
        {
            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator == null || creator.dummy == null)
            {
                Compat.ShowError("Criador indisponível para aleatorizar.");
                return;
            }

            var groups = Groups();
            int unlocked = 0, poolTotal = 0;
            foreach (var g in groups)
            {
                if (_locked.Contains(g.Key) || g.PartIds.Count == 0) continue;
                unlocked++;

                var pool = PoolFor(g);
                poolTotal += pool.Count;
                if (pool.Count == 0) continue; // nothing of the selected tag in this category

                // The custom part currently applied in this category (any tag), so we can swap it out.
                string applied = g.PartIds.FirstOrDefault(id => creator.dummy.attachedItems.ContainsKey(id));
                string pick = pool[UnityEngine.Random.Range(0, pool.Count)];
                if (pick == applied) continue; // already showing the drawn part

                // Explicitly swap: remove the old custom part (covers additive parts like eyes, which the
                // slot-replace logic in Patch_AddPart intentionally does NOT auto-remove), then add the pick.
                if (!string.IsNullOrEmpty(applied) && creator.dummy.Contains(applied))
                    creator.SpawnAlongside(applied);
                if (!creator.dummy.Contains(pick))
                    creator.SpawnAlongside(pick);
            }

            if (unlocked > 0 && poolTotal == 0)
                Compat.ShowError(TagManager.FilterActive
                    ? $"Nenhuma peça custom com a tag \"{TagManager.SelectedTag}\" para sortear."
                    : "Importe peças custom primeiro — não há nada para sortear.");
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
