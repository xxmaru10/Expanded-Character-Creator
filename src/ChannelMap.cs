using System;
using System.Collections.Generic;

namespace CustomPartsMod
{
    /// <summary>
    /// P2 — maps a part's category (savePath segments) to the character colour channel it should be
    /// painted by, per the user's spec. Language-independent: keys off stable savePath segments, not
    /// the localized display name. The returned string is the shader colour property id used by the
    /// engine (see Colors.Part.GetColorId): torso→primary, legs→secondary, boots→leatherA, etc.
    /// </summary>
    internal static class ChannelMap
    {
        internal const string Hair = "_Color_Hair";
        internal const string Skin = "_Color_Skin";
        internal const string Eyes = "_Color_Eyes";
        internal const string Primary = "_Color_Primary";       // torso
        internal const string Secondary = "_Color_Secondary";   // legs
        internal const string LeatherA = "_Color_Leather_Primary";   // boots
        internal const string LeatherB = "_Color_Leather_Secondary"; // knees/shoulders/elbows
        internal const string MetalA = "_Color_Metal_Primary";       // arm/forearm
        internal const string MetalB = "_Color_Metal_Secondary";     // hands
        internal const string MetalDark = "_Color_Metal_Dark";       // helmet
        internal const string Emission = "_Emission";                // accessories, horns

        internal static string ForCategory(string[] categoryPath)
        {
            var seg = new HashSet<string>(categoryPath ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            // Order matters: check the most specific segment first.
            if (seg.Contains("hair") || seg.Contains("B_Hair") || seg.Contains("eyebrows")
                || seg.Contains("beard") || seg.Contains("E_Beard")) return Hair;
            if (seg.Contains("eyes")) return Eyes;
            if (seg.Contains("Full_Helmets") || seg.Contains("helmet")) return MetalDark;
            if (seg.Contains("hands")) return MetalB;
            if (seg.Contains("elbows") || seg.Contains("knees") || seg.Contains("shoulders")) return LeatherB;
            if (seg.Contains("uppers") || seg.Contains("wrists") || seg.Contains("arms")) return MetalA;
            if (seg.Contains("feet") || seg.Contains("shoes")) return LeatherA;
            if (seg.Contains("legs")) return Secondary;
            if (seg.Contains("torso") || seg.Contains("hips")) return Primary;
            if (seg.Contains("attachments") || seg.Contains("extras") || seg.Contains("back")) return Emission;
            if (seg.Contains("faces") || seg.Contains("heads") || seg.Contains("race")) return Skin;

            return Primary; // sensible default
        }
    }
}
