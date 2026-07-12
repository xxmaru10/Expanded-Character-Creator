using System;
using System.Globalization;
using System.Text;

namespace CustomPartsMod
{
    /// <summary>
    /// Reads a CAS-export part name (e.g. "antebraço_direito_ymTop_..._464C4B8F") and decides which body
    /// category/slot/side it belongs to, purely from its leading Portuguese part token. Used by the
    /// shared-folder import to auto-route each .obj without any tab being open. Accent-insensitive and
    /// ordered most-specific-first (antebraço before braço; perna/peito before a bare "pé"). Unknown
    /// prefixes return <see cref="Route.Ok"/> = false so the caller can skip instead of mis-routing.
    /// </summary>
    internal static class PartNameRouter
    {
        internal struct Route
        {
            public bool Ok;
            public string[] Segments;       // native category segments to search, most-specific first
            public SidedCategory.Kind Kind; // None => not sided (torso, hips)
            public bool Left;               // meaningful only when Kind != None
            public string Label;            // human label for logs
        }

        internal static Route Resolve(string fileBaseName)
        {
            string n = Normalize(fileBaseName);
            bool isLeft = SidedCategory.IsLeftName(n);

            // Most-specific prefixes first.
            if (n.StartsWith("antebraco")) return Sided(new[] { "wrists", "elbows", "arms" }, SidedCategory.Kind.ArmLower, isLeft, "antebraço");
            if (n.StartsWith("braco"))     return Sided(new[] { "uppers" }, SidedCategory.Kind.ArmUpper, isLeft, "braço");
            if (n.StartsWith("mao"))       return Sided(new[] { "hands" }, SidedCategory.Kind.Hands, isLeft, "mão");
            // Bottoms split in 3 (the engine has NO legUpper slot): the single upper piece (pelvis/thighs)
            // anchors at the central hip; each calf (panturrilha) is a sided lower leg.
            if (n.StartsWith("panturrilha")) return Sided(new[] { "feet", "foot", "shoes", "shoe", "calves", "lowerlegs", "legs", "leg" }, SidedCategory.Kind.Feet, isLeft, "panturrilha");
            if (n.StartsWith("coxa") || n.StartsWith("perna_superior"))
                return Sided(new[] { "knees" }, SidedCategory.Kind.Knees, isLeft, "coxa");
            if (n.StartsWith("parte_de_cima") || n.StartsWith("partedecima")
                || n.StartsWith("quadril") || n.StartsWith("cintura"))
                return Fixed(new[] { "hips", "hip", "pelvis", "pants", "bottoms", "legsUpper" }, "parte de cima");
            if (n.StartsWith("peito") || n.StartsWith("torso")) return Fixed(new[] { "torso" }, "torso");
            if (n.StartsWith("hip"))                            return Fixed(new[] { "hips" }, "hip");
            if (n.StartsWith("barba"))                          return Fixed(new[] { "ears", "F_Ears" }, "barba");
            if (n.StartsWith("cabelo"))                         return Fixed(new[] { "hair", "B_Hair" }, "cabelo");
            if (n.StartsWith("orelha"))                         return Fixed(new[] { "ears", "F_Ears" }, "orelha");
            // Generic leg/foot fallbacks (sided lower leg) for names that don't use the 3-part split.
            if (n.StartsWith("perna") || n.StartsWith("pe_") || n == "pe")
                return Sided(new[] { "feet", "foot", "shoes", "shoe", "calves", "lowerlegs", "legs", "leg" }, SidedCategory.Kind.Feet, isLeft, "perna/pé");

            return new Route { Ok = false };
        }

        private static Route Sided(string[] segs, SidedCategory.Kind kind, bool left, string label)
            => new Route { Ok = true, Segments = segs, Kind = kind, Left = left, Label = label };

        private static Route Fixed(string[] segs, string label)
            => new Route { Ok = true, Segments = segs, Kind = SidedCategory.Kind.None, Left = false, Label = label };

        // Lowercase + strip accents so "antebraço_direito" -> "antebraco_direito", "mão" -> "mao".
        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string formD = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);
            foreach (char c in formD)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        internal static string ResolveLinkGroupId(string fileBaseName, string folderName)
        {
            if (string.IsNullOrEmpty(fileBaseName)) return folderName ?? "";

            string n = fileBaseName;
            
            // List of prefixes to strip (ordered by length/specificity so longer ones match first)
            string[] prefixes = new[]
            {
                "antebraco_esquerdo_", "antebraço_esquerdo_",
                "antebraco_direito_", "antebraço_direito_",
                "braco_esquerdo_", "braço_esquerdo_",
                "braco_direito_", "braço_direito_",
                "mao_esquerda_", "mão_esquerda_",
                "mao_direita_", "mão_direita_",
                "sapato_esquerdo_", "sapato_direito_",
                "coxa_esquerda_", "coxa_direita_",
                "panturrilha_esquerda_", "panturrilha_direita_",
                "perna_superior_esquerda_", "perna_superior_direita_",
                "perna_esquerda_", "perna_direita_",
                "pe_esquerdo_", "pe_direito_",
                "parte_de_cima_", "partedecima_",
                "antebraco_", "antebraço_",
                "braco_", "braço_",
                "mao_", "mão_",
                "sapato_",
                "coxa_",
                "panturrilha_",
                "perna_superior_",
                "perna_",
                "peito_",
                "torso_",
                "quadril_",
                "cintura_"
            };

            foreach (var prefix in prefixes)
            {
                if (n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    n = n.Substring(prefix.Length);
                    break;
                }
            }

            if (!string.IsNullOrEmpty(n)) return n;
            return folderName ?? "";
        }
    }
}
