using System;

namespace CustomPartsMod
{
    /// <summary>
    /// The "Sapato" (Shoe) custom category — a top-level button injected next to the native "Feet"
    /// button (see <see cref="CategoryTabButton"/> top-level mode). "Pés" is simply the engine's native
    /// Feet category (it already accepts custom imports), so there is no synthetic feet category; only the
    /// shoe one is invented. Its unique savePath prefix under "CustomParts" is used nowhere native. Shoes
    /// attach ADDITIVELY over the lower-leg socket (stack on top of the foot — see <see cref="AccessoryMap"/>),
    /// and left/right is chosen at import time (<see cref="SidedCategory"/>) → legLowerL / legLowerR.
    /// </summary>
    internal static class ShoeCategory
    {
        internal static readonly string[] Path = { "CustomParts", "shoes" };
    }

    /// <summary>
    /// Identifies the mod's SYNTHETIC categories (eyes/shoes). Their prefixes are shorter than any native
    /// leaf category (3+ deep), so the import guards that reject "too shallow" paths must accept these
    /// explicitly. Exact-path match keeps it from misfiring on native parts that share a segment name.
    /// </summary>
    internal static class CustomCategory
    {
        internal static bool IsSynthetic(string[] category)
            => PathEquals(category, EyesCategory.Path)
            || PathEquals(category, ShoeCategory.Path);

        private static bool PathEquals(string[] a, string[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
            return true;
        }
    }
}
