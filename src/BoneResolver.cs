using System.Collections.Generic;
using UnityEngine;
using RpgEngine.Characters;
using RiggedAttachType = RpgEngine.Characters.CharacterCreatorEnums.RiggedAttachType;

namespace CustomPartsMod
{
    /// <summary>
    /// Finds the actual ANIMATED skeleton bone for a slot (from PickupableCharacter.boneHelper),
    /// so a rigid part parented to it follows the animation — the AttachmentPoint sockets are
    /// static (native parts animate via skinning, not via that parent). Falls back to null so
    /// the caller can use the AttachmentPoint socket. Logs the bone names once for tuning.
    /// </summary>
    internal static class BoneResolver
    {
        private static bool _loggedKeys;

        // Substrings to look for in a bone's name, per slot (checked in order, lower-cased).
        private static readonly Dictionary<RiggedAttachType, string[]> Names =
            new Dictionary<RiggedAttachType, string[]>
        {
            { RiggedAttachType.head,      new[] { "head" } },
            { RiggedAttachType.face,      new[] { "head" } },
            { RiggedAttachType.hair,      new[] { "head" } },
            { RiggedAttachType.beard,     new[] { "jaw", "head" } },
            { RiggedAttachType.helmet,    new[] { "head" } },
            { RiggedAttachType.torso,     new[] { "chest", "spine", "torso" } },
            { RiggedAttachType.hip,       new[] { "hips", "pelvis" } },
            { RiggedAttachType.handR,     new[] { "hand" } },
            { RiggedAttachType.handL,     new[] { "hand" } },
            { RiggedAttachType.armUpperR, new[] { "upperarm", "arm" } },
            { RiggedAttachType.armUpperL, new[] { "upperarm", "arm" } },
            { RiggedAttachType.armLowerR, new[] { "forearm", "lowerarm", "elbow" } },
            { RiggedAttachType.armLowerL, new[] { "forearm", "lowerarm", "elbow" } },
            { RiggedAttachType.legLowerR, new[] { "shin", "calf", "lowerleg", "knee" } },
            { RiggedAttachType.legLowerL, new[] { "shin", "calf", "lowerleg", "knee" } },
            { RiggedAttachType.shoulderR, new[] { "shoulder", "clavicle" } },
            { RiggedAttachType.shoulderL, new[] { "shoulder", "clavicle" } },
        };

        internal static Transform Resolve(PickupableCharacter character, RiggedAttachType slot)
        {
            Dictionary<string, Transform> bones;
            try { bones = character.boneHelper; }
            catch { return null; }
            if (bones == null || bones.Count == 0) return null;

            if (!_loggedKeys)
            {
                _loggedKeys = true;
                Plugin.Log.LogInfo("[bones] " + string.Join(", ", bones.Keys));
            }

            if (!Names.TryGetValue(slot, out var subs)) return null;

            string s = slot.ToString();
            bool wantR = s.EndsWith("R");
            bool wantL = s.EndsWith("L");

            foreach (var sub in subs)
            {
                foreach (var kv in bones)
                {
                    if (kv.Value == null) continue;
                    string k = kv.Key.ToLowerInvariant();
                    if (!k.Contains(sub)) continue;
                    if (!SideOk(k, wantR, wantL)) continue;
                    return kv.Value;
                }
            }
            return null;
        }

        private static bool SideOk(string k, bool wantR, bool wantL)
        {
            if (!wantR && !wantL) return true;
            bool isR = k.Contains("right") || k.Contains(".r") || k.Contains("_r") || k.Contains("-r");
            bool isL = k.Contains("left") || k.Contains(".l") || k.Contains("_l") || k.Contains("-l");
            if (wantR) return isR && !isL;
            return isL && !isR;
        }
    }
}
