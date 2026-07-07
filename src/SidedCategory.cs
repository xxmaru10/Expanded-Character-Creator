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
        internal enum Kind { None, Feet, Hands }

        internal static Kind KindOf(string[] category)
        {
            if (category == null) return Kind.None;
            foreach (var s in category)
            {
                if (Eq(s, "feet") || Eq(s, "shoes")) return Kind.Feet;
                if (Eq(s, "hands")) return Kind.Hands;
            }
            return Kind.None;
        }

        internal static bool AppliesTo(string[] category) => KindOf(category) != Kind.None;

        internal static RiggedAttachType LeftSlot(Kind kind)
            => kind == Kind.Hands ? RiggedAttachType.handL : RiggedAttachType.legLowerL;

        internal static RiggedAttachType RightSlot(Kind kind)
            => kind == Kind.Hands ? RiggedAttachType.handR : RiggedAttachType.legLowerR;

        /// <summary>Stable key (independent of language) used to remember each kind's last-used side
        /// separately, so choosing a hand side doesn't affect the remembered foot side and vice versa.</summary>
        internal static string StoreKey(Kind kind) => kind == Kind.Hands ? "hands" : "feet";

        internal static string PanelTitle(Kind kind) => kind == Kind.Hands ? "Lado da mão" : "Lado do pé";

        internal static string Question(Kind kind)
            => kind == Kind.Hands ? "Este modelo é para qual mão?" : "Este modelo é para qual pé?";

        private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
