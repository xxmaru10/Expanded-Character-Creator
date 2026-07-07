using System;

namespace CustomPartsMod
{
    /// <summary>
    /// The engine splits legs/feet into left and right (legLowerL / legLowerR). When importing into a
    /// feet or shoe category the user must pick a side (see <see cref="FootSidePrompt"/>); this identifies
    /// those categories (language-independent, by savePath segment) so the side prompt appears only there.
    /// </summary>
    internal static class FootSide
    {
        internal static bool AppliesTo(string[] category)
        {
            if (category == null) return false;
            foreach (var s in category)
                if (string.Equals(s, "feet", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s, "shoes", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
