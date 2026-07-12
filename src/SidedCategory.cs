using System;
using RiggedAttachType = RpgEngine.Characters.CharacterCreatorEnums.RiggedAttachType;

namespace CustomPartsMod
{
    /// <summary>
    /// The engine splits some limbs into separate left/right sockets (legLowerL/R for legs+feet,
    /// handL/R for hands). When importing into one of these categories the user must pick a side
    /// (see <see cref="SidePrompt"/>); this identifies those categories (language-independent, by
    /// savePath segment) and maps each to its left/right slot pair. Feet and shoes share one "Feet" kind
    /// (a shoe stacks on the chosen foot); hands are their own kind — each kind remembers its own
    /// last-used side independently (see <see cref="ScaleStore.GetLastSideLeft"/>).
    /// </summary>
    internal static class SidedCategory
    {
        internal enum Kind { None, Feet, Hands, ArmUpper, ArmLower, Knees }

        internal static Kind KindOf(string[] category)
        {
            if (category == null) return Kind.None;
            var seg = new System.Collections.Generic.HashSet<string>(category, StringComparer.OrdinalIgnoreCase);

            if (seg.Contains("knees")) return Kind.Knees;
            if (seg.Contains("feet") || seg.Contains("shoes") || seg.Contains("lowerlegs") || seg.Contains("legs") || seg.Contains("foot")) return Kind.Feet;
            if (seg.Contains("hands")) return Kind.Hands;
            if (seg.Contains("uppers")) return Kind.ArmUpper;
            if (seg.Contains("wrists") || seg.Contains("elbows") || seg.Contains("arms")) return Kind.ArmLower;

            return Kind.None;
        }

        internal static bool IsLeftName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string k = name.ToLowerInvariant();
            return k.Contains("esquerd") 
                || k.Contains("left") 
                || k.Contains("_l_") 
                || k.EndsWith("_l") 
                || k.Contains("-l-") 
                || k.EndsWith("-l")
                || k.Contains("_e_")
                || k.EndsWith("_e")
                || k.Contains("-e-")
                || k.EndsWith("-e");
        }

        internal static bool IsLeftSlot(string slotName)
        {
            if (string.IsNullOrEmpty(slotName)) return false;
            return slotName.EndsWith("L") || slotName.EndsWith("l");
        }

        internal static void MirrorTransformForLeft(RiggedAttachType slot, ref UnityEngine.Vector3 offset, ref UnityEngine.Vector3 euler)
        {
            if (slot == RiggedAttachType.kneeL)
            {
                offset.x = -offset.x;
                offset.y = -offset.y;
                euler.x = -euler.x;
                euler.y = -euler.y;
            }
            else if (slot == RiggedAttachType.legLowerL || slot == RiggedAttachType.handL || slot == RiggedAttachType.armUpperL || slot == RiggedAttachType.armLowerL || slot == RiggedAttachType.shoulderL)
            {
                offset.y = -offset.y;
                offset.z = -offset.z;
                euler.y = -euler.y;
                euler.z = -euler.z;
            }
        }

        internal static bool AppliesTo(string[] category) => KindOf(category) != Kind.None;

        internal static RiggedAttachType ResolveSlot(string[] category, string sourceKey, string savedSlotName = null)
        {
            RiggedAttachType baseSlot = CategoryMap.ToSocket(category);
            var kind = SidedCategory.KindOf(category);
            if (kind == Kind.None) return baseSlot;

            bool left = false;
            if (!string.IsNullOrEmpty(savedSlotName))
            {
                left = IsLeftSlot(savedSlotName);
            }
            else
            {
                left = IsLeftName(sourceKey);
            }

            return left ? LeftSlot(kind) : RightSlot(kind);
        }

        internal static RiggedAttachType LeftSlot(Kind kind)
        {
            switch (kind)
            {
                case Kind.Hands:    return RiggedAttachType.handL;
                case Kind.ArmUpper: return RiggedAttachType.armUpperL;
                case Kind.ArmLower: return RiggedAttachType.armLowerL;
                case Kind.Knees:    return RiggedAttachType.kneeL;
                default:            return RiggedAttachType.legLowerL; // Feet
            }
        }

        internal static RiggedAttachType RightSlot(Kind kind)
        {
            switch (kind)
            {
                case Kind.Hands:    return RiggedAttachType.handR;
                case Kind.ArmUpper: return RiggedAttachType.armUpperR;
                case Kind.ArmLower: return RiggedAttachType.armLowerR;
                case Kind.Knees:    return RiggedAttachType.kneeR;
                default:            return RiggedAttachType.legLowerR; // Feet
            }
        }

        /// <summary>Stable key (independent of language) used to remember each kind's last-used side
        /// separately, so choosing one limb's side doesn't affect the others' remembered side.</summary>
        internal static string StoreKey(Kind kind)
        {
            switch (kind)
            {
                case Kind.Hands:    return "hands";
                case Kind.ArmUpper: return "armupper";
                case Kind.ArmLower: return "armlower";
                case Kind.Knees:    return "knees";
                default:            return "feet";
            }
        }

        internal static string PanelTitle(Kind kind)
        {
            switch (kind)
            {
                case Kind.Hands:    return "Lado da mão";
                case Kind.ArmUpper: return "Lado do braço";
                case Kind.ArmLower: return "Lado do antebraço";
                case Kind.Knees:    return "Lado do joelho";
                default:            return "Lado do pé";
            }
        }

        internal static string Question(Kind kind)
        {
            switch (kind)
            {
                case Kind.Hands:    return "Este modelo é para qual mão?";
                case Kind.ArmUpper: return "Este modelo é para qual braço?";
                case Kind.ArmLower: return "Este modelo é para qual antebraço?";
                case Kind.Knees:    return "Este modelo é para qual joelho/coxa?";
                default:            return "Este modelo é para qual pé?";
            }
        }

        private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
