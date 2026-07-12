using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using RiggedAttachType = RpgEngine.Characters.CharacterCreatorEnums.RiggedAttachType;

namespace CustomPartsMod
{
    /// <summary>
    /// Saved per-model record: placement (scale/offset), texture, and enough to REBUILD the part
    /// on a later session (source model path + category + slot).
    /// </summary>
    internal struct PartTransform
    {
        public float scale;
        public Vector3 scaleAxis;  // per-axis multiplier on top of scale (P4); (0,0,0) => treated as (1,1,1)
        public Vector3 offset;
        public Vector3 euler;      // typed rotation (P5)
        public string gender;      // "", "Feminine" or "Masculine" (P3)
        public string channel;     // paint channel override, e.g. "_Color_Skin" ("" = auto by category) (P2)
        public string texturePath;
        public string[] textureVariants; // P13 — all texture paths for this model (null/empty = none)
        public int activeVariant;        // P13 — index of the selected variant
        public string modelPath;   // source .obj/.glb on disk (null for the global __last__ entry)
        public string[] category;  // savePath prefix of the tab it belongs to
        public string slot;        // RiggedAttachType name
        public int additive;       // P14 attach mode override: 0=auto, 1=accessory(additive), 2=replace
        public string tag;         // P10 — user tag/theme within the category ("" = none)
        public string link;        // auto-equip group ID
    }

    // Top-level PUBLIC [Serializable] types with public fields — the JsonUtility pattern that
    // reliably serializes a List. (Private nested classes serialize as "{}" — that was the bug
    // that made nothing persist to disk.)
    [Serializable]
    public class ScaleEntry
    {
        public string key;
        public float scale = 1f;
        public float sx = 1f, sy = 1f, sz = 1f; // per-axis scale multiplier (P4)
        public float px, py, pz;                // offset
        public float rx, ry, rz;                // rotation (P5)
        public string gender;                   // "", "Feminine" or "Masculine" (P3)
        public string channel;                  // paint channel override (P2)
        public string tex;
        public string[] texVariants;            // all texture paths (P13)
        public int texVariant;                  // selected variant index (P13)
        public string model;
        public string[] category;
        public string slot;
        public int additive;                    // attach mode override (P14): 0=auto,1=accessory,2=replace
        public string tag;                      // user tag/theme within the category (P10)
        public string link;                     // auto-equip group ID
    }

    /// <summary>
    /// Persists per-model records (scale/offset/texture + how to rebuild) so parts survive across
    /// sessions. JSON at <c>…\The RPG Engine\CustomParts\scales.json</c>, keyed by source file name.
    /// </summary>
    internal static class ScaleStore
    {
        private static readonly Dictionary<string, PartTransform> Cache =
            new Dictionary<string, PartTransform>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;
        private static bool _deferSave;

        internal static string CustomPartsDir
        {
            get
            {
                // Application.dataPath = …\The RPG Engine\The_RPG_Engine_Data
                string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                return Path.Combine(root, "CustomParts");
            }
        }

        private static string FilePath => Path.Combine(CustomPartsDir, "scales.json");

        /// <summary>Per-axis scale is a multiplier; a stored 0 (missing/old record) means "no stretch" = 1.</summary>
        private static float Axis(float v) => v > 1e-4f ? v : 1f;

        private static string NormalizeKey(string k)
        {
            if (k == null) return "";
            return k.ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
        }

        internal static PartTransform Get(string key, float fallbackScale)
        {
            EnsureLoaded();
            if (TryGet(key, out var t)) return t;
            return new PartTransform { scale = fallbackScale, offset = Vector3.zero };
        }

        internal static bool TryGet(string key, out PartTransform value)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(key))
            {
                value = default;
                return false;
            }
            if (Cache.TryGetValue(key, out value)) return true;

            // Fallback: search with normalized key to bridge hyphen/underscore differences
            string norm = NormalizeKey(key);
            foreach (var kv in Cache)
            {
                if (NormalizeKey(kv.Key) == norm)
                {
                    value = kv.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        // Global calibrated scale, stored as a MULTIPLIER over each mesh's normalized size (so it carries
        // to the next, possibly differently-scaled, model and always lands visible — the normalize step
        // sizes the mesh to ~DefaultScale first, then this multiplier scales from there). Calibrated by
        // the scale panel's "Confirmar" (with the category checkbox OFF) and reused by every new import.
        // A shared offset rides along. Reserved key "__global__"; multiplier defaults to 1 when unset.
        private const string GlobalKey = "__global__";

        internal static void GetGlobalMult(out float multiplier, out Vector3 offset)
        {
            EnsureLoaded();
            if (Cache.TryGetValue(GlobalKey, out var t) && t.scale > 1e-6f)
            {
                multiplier = t.scale;
                offset = t.offset;
            }
            else
            {
                multiplier = 1f;
                offset = Vector3.zero;
            }
        }

        internal static void SetGlobalMult(float multiplier, Vector3 offset)
        {
            EnsureLoaded();
            Cache[GlobalKey] = new PartTransform { scale = multiplier > 1e-6f ? multiplier : 1f, offset = offset };
            Save();
        }

        // Remembered left/right side for the LAST import of each sided kind (feet, hands, ...), so the
        // side prompt defaults to it. Stored per-kind (reserved "__side__<kind>" record's slot field, "L"
        // or "R") so picking a hand side doesn't disturb the remembered foot side and vice versa.
        private const string SideKeyPrefix = "__side__";

        internal static bool GetLastSideLeft(string kind)
        {
            EnsureLoaded();
            return Cache.TryGetValue(SideKeyPrefix + kind, out var t) && t.slot == "L";
        }

        internal static void SetLastSideLeft(string kind, bool left)
        {
            EnsureLoaded();
            Cache[SideKeyPrefix + kind] = new PartTransform { slot = left ? "L" : "R" };
            Save();
        }

        // Per-category override, keyed by the exact tab (savePath prefix) so each tab has its own default
        // (e.g. "Faces" and "Heads" are independent even though both attach to the head bone). Set only
        // when the user ticks the "Salvar como padrão desta categoria" checkbox — it OVERRIDES the global
        // factor for this category. Every NEW model imported into that tab inherits these placement
        // values. Scale is a MULTIPLIER over each mesh's normalized size (so differently-sized models
        // still land right); axis/rotation/offset are absolute. Reserved key prefix "__cat__".
        // Gender/texture excluded (those stay per-model).
        private const string CatPrefix = "__cat__";

        private static string CategoryKey(string[] category, RiggedAttachType slot, string gender)
        {
            if (category == null || category.Length == 0) return null;
            string baseKey = CatPrefix + string.Join("/", category);

            // If the category is sided, append the side suffix (L or R) to have separate defaults
            var kind = SidedCategory.KindOf(category);
            if (kind != SidedCategory.Kind.None)
            {
                bool left = slot.ToString().EndsWith("L") || slot.ToString().EndsWith("l");
                baseKey += left ? "__L" : "__R";
            }

            // Append gender suffix if specified
            if (!string.IsNullOrEmpty(gender))
            {
                if (gender.Equals("Masculine", StringComparison.OrdinalIgnoreCase)) baseKey += "__M";
                else if (gender.Equals("Feminine", StringComparison.OrdinalIgnoreCase)) baseKey += "__F";
            }

            return baseKey;
        }

        internal static void SetCategoryDefault(string[] category, RiggedAttachType slot, string gender, float multiplier, Vector3 axis, Vector3 euler, Vector3 offset)
        {
            string key = CategoryKey(category, slot, gender);
            if (key == null) return;
            EnsureLoaded();
            Cache[key] = new PartTransform
            {
                scale = multiplier > 1e-6f ? multiplier : 1f,
                scaleAxis = axis,
                euler = euler,
                offset = offset,
            };
            _deferSave = false;
            Save();
        }

        internal static bool TryGetCategoryDefault(string[] category, RiggedAttachType slot, string gender, out float multiplier, out Vector3 axis, out Vector3 euler, out Vector3 offset)
        {
            multiplier = 1f; axis = Vector3.one; euler = Vector3.zero; offset = Vector3.zero;
            if (category == null || category.Length == 0) return false;
            EnsureLoaded();

            // 1. Try exact sided + gender key first (e.g. __cat__...__L__F or __cat__...__L__M)
            string key = CategoryKey(category, slot, gender);
            if (Cache.TryGetValue(key, out var t))
            {
                multiplier = t.scale > 1e-6f ? t.scale : 1f;
                axis = new Vector3(Axis(t.scaleAxis.x), Axis(t.scaleAxis.y), Axis(t.scaleAxis.z));
                euler = t.euler;
                offset = t.offset;
                return true;
            }

            // 2. Fallback to sided without gender suffix (Both gender default)
            string sidedNoGenderKey = CategoryKey(category, slot, "");
            if (Cache.TryGetValue(sidedNoGenderKey, out t))
            {
                multiplier = t.scale > 1e-6f ? t.scale : 1f;
                axis = new Vector3(Axis(t.scaleAxis.x), Axis(t.scaleAxis.y), Axis(t.scaleAxis.z));
                euler = t.euler;
                offset = t.offset;
                return true;
            }

            // 3. Fallback: if sided, try the other side's key with gender and mirror it
            var kind = SidedCategory.KindOf(category);
            if (kind != SidedCategory.Kind.None)
            {
                bool left = slot.ToString().EndsWith("L") || slot.ToString().EndsWith("l");
                RiggedAttachType otherSlot = left ? SidedCategory.RightSlot(kind) : SidedCategory.LeftSlot(kind);
                
                // Try other side with gender
                string otherSidedGenderKey = CategoryKey(category, otherSlot, gender);
                if (Cache.TryGetValue(otherSidedGenderKey, out t))
                {
                    multiplier = t.scale > 1e-6f ? t.scale : 1f;
                    axis = new Vector3(Axis(t.scaleAxis.x), Axis(t.scaleAxis.y), Axis(t.scaleAxis.z));
                    euler = t.euler;
                    offset = t.offset;
                    SidedCategory.MirrorTransformForLeft(left ? slot : otherSlot, ref offset, ref euler);
                    return true;
                }

                // Try other side without gender
                string otherSidedNoGenderKey = CategoryKey(category, otherSlot, "");
                if (Cache.TryGetValue(otherSidedNoGenderKey, out t))
                {
                    multiplier = t.scale > 1e-6f ? t.scale : 1f;
                    axis = new Vector3(Axis(t.scaleAxis.x), Axis(t.scaleAxis.y), Axis(t.scaleAxis.z));
                    euler = t.euler;
                    offset = t.offset;
                    SidedCategory.MirrorTransformForLeft(left ? slot : otherSlot, ref offset, ref euler);
                    return true;
                }
            }

            // 4. Fallback: try base category key without side/gender suffixes (for older records or non-sided)
            string baseKey = CatPrefix + string.Join("/", category);
            // Try base key with gender
            string baseGenderKey = baseKey + (string.IsNullOrEmpty(gender) ? "" : (gender.Equals("Masculine", StringComparison.OrdinalIgnoreCase) ? "__M" : "__F"));
            if (Cache.TryGetValue(baseGenderKey, out t))
            {
                multiplier = t.scale > 1e-6f ? t.scale : 1f;
                axis = new Vector3(Axis(t.scaleAxis.x), Axis(t.scaleAxis.y), Axis(t.scaleAxis.z));
                euler = t.euler;
                offset = t.offset;
                
                bool left = slot.ToString().EndsWith("L") || slot.ToString().EndsWith("l");
                if (left) SidedCategory.MirrorTransformForLeft(slot, ref offset, ref euler);
                return true;
            }

            // Try base key without gender
            if (Cache.TryGetValue(baseKey, out t))
            {
                multiplier = t.scale > 1e-6f ? t.scale : 1f;
                axis = new Vector3(Axis(t.scaleAxis.x), Axis(t.scaleAxis.y), Axis(t.scaleAxis.z));
                euler = t.euler;
                offset = t.offset;
                
                bool left = slot.ToString().EndsWith("L") || slot.ToString().EndsWith("l");
                if (left) SidedCategory.MirrorTransformForLeft(slot, ref offset, ref euler);
                return true;
            }

            return false;
        }

        internal static void Set(string key, PartTransform value)
        {
            if (string.IsNullOrEmpty(key)) return;
            EnsureLoaded();

            // Clean up any existing key that has the same normalized name to avoid duplicates
            string norm = NormalizeKey(key);
            List<string> toRemove = null;
            foreach (var kv in Cache)
            {
                if (NormalizeKey(kv.Key) == norm && !string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    (toRemove ?? (toRemove = new List<string>())).Add(kv.Key);
                }
            }
            if (toRemove != null)
            {
                foreach (var oldKey in toRemove) Cache.Remove(oldKey);
            }

            Cache[key] = value;
            Save();
        }

        /// <summary>
        /// Batches disk writes. While a batch is open, Set/SetGlobalMult/etc. update the in-memory cache but
        /// skip writing the file; <see cref="Flush"/> writes it once at the end. Mass import wraps its
        /// loop in this so a 600-item folder rewrites scales.json ONCE instead of once per item — the
        /// per-item full-file rewrite is O(n) over the whole (growing) store, i.e. O(n²) for the batch.
        /// </summary>
        internal static void BeginBatch()
        {
            EnsureLoaded();
            _deferSave = true;
        }

        /// <summary>Ends a batch opened by <see cref="BeginBatch"/> and writes the file a single time.</summary>
        internal static void Flush()
        {
            _deferSave = false;
            Save();
        }

        /// <summary>P13 — patch just the texture-variant fields of an EXISTING record (so switching/
        /// adding a texture in the variant bar persists without a full "Salvar padrão"). Returns false
        /// when there is no saved record yet — the change then stays in memory until the panel confirms.</summary>
        internal static bool TryUpdateVariants(string key, string[] variants, int activeVariant, string activePath)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(key)) return false;

            string actualKey = key;
            if (!Cache.ContainsKey(key))
            {
                string norm = NormalizeKey(key);
                bool found = false;
                foreach (var k in Cache.Keys)
                {
                    if (NormalizeKey(k) == norm)
                    {
                        actualKey = k;
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }

            var t = Cache[actualKey];
            t.textureVariants = variants;
            t.activeVariant = activeVariant;
            t.texturePath = activePath;
            Cache[actualKey] = t;
            Save();
            return true;
        }

        /// <summary>P10 — patch just the tag field of an EXISTING record (e.g. when a tag is deleted
        /// entirely). Returns false when there is no saved record yet — the in-memory part is still
        /// updated by the caller, it just won't survive a reload.</summary>
        internal static bool TryUpdateTag(string key, string tag)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(key)) return false;

            string actualKey = key;
            if (!Cache.ContainsKey(key))
            {
                string norm = NormalizeKey(key);
                bool found = false;
                foreach (var k in Cache.Keys)
                {
                    if (NormalizeKey(k) == norm)
                    {
                        actualKey = k;
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }

            var t = Cache[actualKey];
            t.tag = tag ?? "";
            Cache[actualKey] = t;
            Save();
            return true;
        }

        /// <summary>Saved models that can be rebuilt on load (have a model path). The reserved records
        /// (__global__, __cat__…, __side__…) carry no modelPath, so this filter skips them.</summary>
        internal static IEnumerable<KeyValuePair<string, PartTransform>> AllModels()
        {
            EnsureLoaded();
            foreach (var kv in Cache)
                if (!string.IsNullOrEmpty(kv.Value.modelPath))
                    yield return kv;
        }

        internal static void Remove(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            EnsureLoaded();

            string actualKey = key;
            if (!Cache.ContainsKey(key))
            {
                string norm = NormalizeKey(key);
                foreach (var k in Cache.Keys)
                {
                    if (NormalizeKey(k) == norm)
                    {
                        actualKey = k;
                        break;
                    }
                }
            }

            if (Cache.Remove(actualKey)) Save();
        }

        // One JSON object per LINE (JSONL). A single flat [Serializable] object always round-trips
        // through JsonUtility — unlike a List/nested-class wrapper, which silently produced "{}".
        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    ScaleEntry e;
                    try { e = JsonUtility.FromJson<ScaleEntry>(line); }
                    catch { continue; }
                    if (e == null || string.IsNullOrEmpty(e.key)) continue;
                    Cache[e.key] = new PartTransform
                    {
                        scale = e.scale,
                        // Old records (pre-P4) have no sx/sy/sz -> JsonUtility leaves 0; treat 0 as 1.
                        scaleAxis = new Vector3(Axis(e.sx), Axis(e.sy), Axis(e.sz)),
                        offset = new Vector3(e.px, e.py, e.pz),
                        euler = new Vector3(e.rx, e.ry, e.rz),
                        gender = e.gender,
                        channel = e.channel,
                        texturePath = e.tex,
                        textureVariants = e.texVariants,
                        activeVariant = e.texVariant,
                        modelPath = e.model,
                        category = e.category,
                        slot = e.slot,
                        additive = e.additive,
                        tag = e.tag,
                        link = e.link
                    };
                }
                Plugin.Log.LogInfo($"[store] carregados {Cache.Count} registro(s) de scales.json");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Nao consegui ler scales.json: " + e.Message);
            }
        }

        private static void Save()
        {
            if (_deferSave) return; // inside a batch: BeginBatch/Flush writes once at the end
            try
            {
                Directory.CreateDirectory(CustomPartsDir);
                var sb = new StringBuilder();
                foreach (var kv in Cache)
                {
                    Vector3 axis = kv.Value.scaleAxis;
                    var e = new ScaleEntry
                    {
                        key = kv.Key,
                        scale = kv.Value.scale,
                        sx = Axis(axis.x), sy = Axis(axis.y), sz = Axis(axis.z),
                        px = kv.Value.offset.x,
                        py = kv.Value.offset.y,
                        pz = kv.Value.offset.z,
                        rx = kv.Value.euler.x,
                        ry = kv.Value.euler.y,
                        rz = kv.Value.euler.z,
                        gender = kv.Value.gender,
                        channel = kv.Value.channel,
                        tex = kv.Value.texturePath,
                        texVariants = kv.Value.textureVariants,
                        texVariant = kv.Value.activeVariant,
                        model = kv.Value.modelPath,
                        category = kv.Value.category,
                        slot = kv.Value.slot,
                        additive = kv.Value.additive,
                        tag = kv.Value.tag,
                        link = kv.Value.link
                    };
                    sb.AppendLine(JsonUtility.ToJson(e)); // one compact JSON per line
                }
                File.WriteAllText(FilePath, sb.ToString());
                Plugin.Log.LogInfo($"[store] gravados {Cache.Count} registro(s) em scales.json");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Nao consegui salvar scales.json: " + e.Message);
            }
        }
    }
}
