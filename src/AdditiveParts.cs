using System;
using System.Collections.Generic;
using UnityEngine;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// Tracks which ADDITIVE custom parts (eyes/accessories) the user currently wants ON, and re-adds
    /// them when the engine drops them as collateral. Placing a native head/face runs the engine's
    /// <c>RemoveAllChildren</c> on that socket, which destroys any custom part living under it (our eye).
    /// The user's intent is toggled only by clicking the part itself (SpawnAlongside); every other
    /// removal is collateral, so after any part placement we re-apply the ones that went missing.
    /// </summary>
    internal static class AdditiveParts
    {
        private static readonly HashSet<string> Active = new HashSet<string>(StringComparer.Ordinal);
        private static bool _reapplying;

        /// <summary>The user explicitly turned an additive part on/off (via its tab item / arrows).</summary>
        internal static void SetIntent(string id, bool on)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (on) Active.Add(id); else Active.Remove(id);
        }

        internal static void Clear() => Active.Clear();

        /// <summary>Re-add any wanted additive part that isn't currently on the character. Guarded so the
        /// re-add (which itself calls AddPart → this postfix) doesn't recurse.</summary>
        internal static void Reapply(PickupableCharacter character)
        {
            if (_reapplying || character == null || Active.Count == 0) return;
            _reapplying = true;
            try
            {
                // Iterate a snapshot: AddPart below re-enters other patches that may call SetIntent,
                // mutating Active mid-enumeration (throws "Collection was modified" and, during async
                // map load, aborts the whole LoadBook). The copy makes re-adds safe.
                foreach (var id in new List<string>(Active))
                {
                    if (character.attachedItems.ContainsKey(id)) continue;
                    if (!CustomPartCatalog.IsCustom(id)) continue;
                    try
                    {
                        var att = character.AddPart(id);          // our prefix rebuilds the mesh
                        // Match the re-added part to the layer of the bone it hangs on — NOT a hardcoded 21.
                        // Build() already sets that (go.layer = parent bone layer): in the creator the bone
                        // is on layer 21 (preview camera), so this stays 21; on a MAP character the bone is
                        // on the default layer. Forcing 21 here made re-added accessories (glasses, and an
                        // additive shoe) invisible on the map — the map camera doesn't render layer 21 —
                        // even though they were still attached (they showed in the creator, vanished on the
                        // map). Inherit the parent's layer so map characters keep the accessory visible.
                        if (att != null)
                        {
                            int layer = att.transform.parent != null
                                ? att.transform.parent.gameObject.layer
                                : att.gameObject.layer;
                            SetLayer(att.gameObject, layer);
                        }
                    }
                    catch (Exception e) { Plugin.Log.LogWarning("[additive] reapply " + id + ": " + e.Message); }
                }
            }
            finally { _reapplying = false; }
        }

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayer(c.gameObject, layer);
        }
    }
}
