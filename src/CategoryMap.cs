using System;
using System.Collections.Generic;
using RiggedAttachType = RpgEngine.Characters.CharacterCreatorEnums.RiggedAttachType;

namespace CustomPartsMod
{
    /// <summary>
    /// Maps the creator's currently navigated category (a savePath prefix, e.g.
    /// ["CustomCharacters","RiggedBodyParts","heads","faces"]) to the bone socket the
    /// imported mesh should attach to. Language-independent: keys off stable savePath
    /// segments, never localized display names.
    /// </summary>
    internal static class CategoryMap
    {
        internal static RiggedAttachType ToSocket(string[] categoryPath)
            => TryToSocket(categoryPath, out var slot) ? slot : RiggedAttachType.head;

        /// <summary>Like <see cref="ToSocket"/> but returns false when no known body-part segment
        /// matched (instead of defaulting to head) — used to confidently identify a native part's
        /// slot for replace-don't-stack logic.</summary>
        internal static bool TryToSocket(string[] categoryPath, out RiggedAttachType slot)
        {
            slot = RiggedAttachType.head;
            var seg = new HashSet<string>(categoryPath ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            // Order matters: check the most specific segment first.
            if (seg.Contains("Full_Helmets") || seg.Contains("helmet")) { slot = RiggedAttachType.helmet; return true; }

            // Hats & earrings are worn ON THE HEAD, so they must follow the head bone. Their categories
            // live under extras/attachments, which would otherwise hit the generic "attachments/extras ->
            // back" rule below and parent them to the spine (Spine_03) — making them follow the torso
            // instead of the head (the reported desync). Map them to head-following slots first:
            //   Hats    -> helmet slot  -> BoneResolver -> "Head" bone
            //   brincos -> ears slot    -> BoneResolver -> "Head" bone
            if (seg.Contains("Hats") || seg.Contains("hat") || seg.Contains("hats") || seg.Contains("chapeu") || seg.Contains("chapeus")) { slot = RiggedAttachType.helmet; return true; }
            if (seg.Contains("brincos") || seg.Contains("brinco") || seg.Contains("earring") || seg.Contains("earrings")) { slot = RiggedAttachType.ears; return true; }
            if (seg.Contains("hands")) { slot = RiggedAttachType.handR; return true; }
            if (seg.Contains("shoulders")) { slot = RiggedAttachType.shoulderR; return true; }
            if (seg.Contains("uppers")) { slot = RiggedAttachType.armUpperR; return true; }
            if (seg.Contains("wrists") || seg.Contains("elbows")) { slot = RiggedAttachType.armLowerR; return true; }
            if (seg.Contains("arms")) { slot = RiggedAttachType.armLowerR; return true; }
            if (seg.Contains("torso") || seg.Contains("chest") || seg.Contains("shirts") || seg.Contains("tops") || seg.Contains("upper")) { slot = RiggedAttachType.torso; return true; }
            if (seg.Contains("hips") || seg.Contains("hip") || seg.Contains("pelvis") || seg.Contains("pants") || seg.Contains("bottoms") || seg.Contains("lower")) { slot = RiggedAttachType.hip; return true; }
            if (seg.Contains("knees")) { slot = RiggedAttachType.kneeR; return true; }
            if (seg.Contains("feet") || seg.Contains("foot") || seg.Contains("shoes") || seg.Contains("shoe") || seg.Contains("legs") || seg.Contains("leg") || seg.Contains("calves") || seg.Contains("lowerlegs")) { slot = RiggedAttachType.legLowerR; return true; }
            if (seg.Contains("beard") || seg.Contains("E_Beard")) { slot = RiggedAttachType.beard; return true; }
            if (seg.Contains("hair") || seg.Contains("B_Hair")) { slot = RiggedAttachType.hair; return true; }
            if (seg.Contains("ears") || seg.Contains("ear") || seg.Contains("F_Ears") || seg.Contains("orelha") || seg.Contains("orelhas")) { slot = RiggedAttachType.ears; return true; }
            if (seg.Contains("eyebrows") || seg.Contains("eyebrow") || seg.Contains("C_Eyebrow") || seg.Contains("sobrancelha") || seg.Contains("sobrancelhas")) { slot = RiggedAttachType.eyebrows; return true; }
            if (seg.Contains("waist")) { slot = RiggedAttachType.waist; return true; }
            if (seg.Contains("helmetAttachment") || seg.Contains("helmetAdditions") || seg.Contains("helmetAddition")) { slot = RiggedAttachType.helmetAttachment; return true; }
            if (seg.Contains("eyes") || seg.Contains("olho") || seg.Contains("olhos")) { slot = RiggedAttachType.face; return true; } // P2 Fatia 3 — eyes on the face
            if (seg.Contains("faces") || seg.Contains("race") || seg.Contains("heads")) { slot = RiggedAttachType.head; return true; }
            if (seg.Contains("attachments") || seg.Contains("extras")) { slot = RiggedAttachType.back; return true; }

            return false;
        }
    }
}
