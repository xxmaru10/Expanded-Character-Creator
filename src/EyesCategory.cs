using System;

namespace CustomPartsMod
{
    /// <summary>
    /// P2 Fatia 3 — a dedicated "Olhos" (Eyes) category. The engine has no native eyes mesh
    /// category (eyes are only a colour applied via SetEyesColor), so we invent a unique savePath
    /// prefix that no native part uses. Its own navigable tab button (see <see cref="CategoryTabButton"/>)
    /// filters the item list to exactly these parts via <c>itemTabsLoader.SetPathFilter(Path)</c>.
    /// The "eyes" segment keeps <see cref="CategoryMap"/>/<see cref="ChannelMap"/> language-independent
    /// (they key off stable segments): eyes → face socket, "_Color_Eyes" paint channel.
    /// </summary>
    internal static class EyesCategory
    {
        // Unique top-level prefix + an "eyes" segment the category/channel maps recognise.
        internal static readonly string[] Path = { "CustomParts", "eyes" };

        /// <summary>True when a category path is (or lives under) the eyes category.</summary>
        internal static bool Is(string[] category)
        {
            if (category == null) return false;
            foreach (var seg in category)
                if (string.Equals(seg, "eyes", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(seg, "olho", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(seg, "olhos", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
