using System;
using System.Collections.Generic;

namespace CustomPartsMod
{
    /// <summary>
    /// P14 — decides whether an imported part attaches ADDITIVELY (like an accessory worn on top of the
    /// body, Sims-style: it doesn't replace the base part and stacks with other accessories) or
    /// REPLACES its slot (the default for base body parts like heads/torsos).
    ///
    /// Auto rule keys off stable savePath segments (language-independent). The user can override it
    /// per-model from the edit panel; that override is persisted in scales.json (see PartTransform.additive).
    /// </summary>
    internal static class AccessoryMap
    {
        // Category savePath segments that are "accessories": worn over the body, so additive by default.
        private static readonly HashSet<string> AccessorySegments = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "Full_Helmets", "helmet", "helmetAdditions", "helmetAddition",
            "shoulders", "attachments", "extras",
            "shoes", // the shoe subcategory stacks ON TOP of the feet (additive), like an accessory
        };

        internal static bool IsAccessory(string[] categoryPath)
        {
            if (categoryPath == null) return false;
            foreach (var seg in categoryPath)
                if (seg != null && AccessorySegments.Contains(seg)) return true;
            return false;
        }

        /// <summary>Effective additive decision. <paramref name="overrideMode"/>: 0 = auto (eyes +
        /// accessory categories), 1 = force accessory (additive), 2 = force replace.</summary>
        internal static bool ResolveAdditive(string[] categoryPath, int overrideMode)
        {
            if (overrideMode == 1) return true;
            if (overrideMode == 2) return false;
            return EyesCategory.Is(categoryPath) || IsAccessory(categoryPath);
        }
    }
}
