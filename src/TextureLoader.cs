using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CustomPartsMod
{
    /// <summary>
    /// Loads a Texture2D from disk and finds a texture that belongs to an imported OBJ
    /// (the engine's OBJ importer loads geometry only). Looks at the model's .mtl (map_Kd)
    /// and a same-named image next to the file.
    /// </summary>
    internal static class TextureLoader
    {
        private static readonly string[] ImageExts = { ".png", ".jpg", ".jpeg" };

        internal static Texture2D LoadFromFile(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true);
                if (!tex.LoadImage(File.ReadAllBytes(path)))
                {
                    UnityEngine.Object.Destroy(tex);
                    return null;
                }
                tex.name = Path.GetFileNameWithoutExtension(path);
                tex.wrapMode = TextureWrapMode.Clamp;
                return tex;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Nao consegui carregar a textura '" + path + "': " + e.Message);
                return null;
            }
        }

        /// <summary>Best-effort search for a texture that ships with an OBJ. Returns a path or null.</summary>
        internal static string FindSibling(string modelPath)
        {
            try
            {
                if (string.IsNullOrEmpty(modelPath)) return null;
                string dir = Path.GetDirectoryName(modelPath);
                if (string.IsNullOrEmpty(dir)) return null;
                string baseName = Path.GetFileNameWithoutExtension(modelPath);

                // 1) .mtl (same name, then whatever the OBJ references via mtllib) -> map_Kd
                string fromMtl = TextureFromMtl(Path.Combine(dir, baseName + ".mtl"), dir)
                                 ?? TextureFromMtl(MtlLibReferenced(modelPath, dir), dir);
                if (fromMtl != null) return fromMtl;

                // 2) same-named image next to the model
                foreach (var ext in ImageExts)
                {
                    string p = Path.Combine(dir, baseName + ext);
                    if (File.Exists(p)) return p;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Busca de textura falhou: " + e.Message);
            }
            return null;
        }

        /// <summary>
        /// All textures that belong to a model, in variant order. Index 0 is the primary texture
        /// (the same-named sibling, or the OBJ's .mtl map_Kd); after it come the numbered siblings
        /// "&lt;base&gt;1", "&lt;base&gt;2", ... that the CAS Batch Exporter writes (one per colour swatch).
        /// Probing stops at the first missing index so the list stays contiguous, matching how the
        /// exporter numbers gap-free. Returns an empty list when nothing is found.
        /// </summary>
        internal static List<string> FindVariants(string modelPath)
        {
            var list = new List<string>();
            try
            {
                if (string.IsNullOrEmpty(modelPath)) return list;
                string dir = Path.GetDirectoryName(modelPath);
                if (string.IsNullOrEmpty(dir)) return list;
                string baseName = Path.GetFileNameWithoutExtension(modelPath);

                // Variant 0: the primary texture (same-named PNG or the .mtl's map_Kd).
                string primary = FindSibling(modelPath);
                if (!string.IsNullOrEmpty(primary)) list.Add(primary);

                // Variants 1..N: "<base>1.png", "<base>2.png", ... Stop at the first gap.
                for (int i = 1; ; i++)
                {
                    string found = null;
                    foreach (var ext in ImageExts)
                    {
                        string p = Path.Combine(dir, baseName + i + ext);
                        if (File.Exists(p)) { found = p; break; }
                    }
                    if (found == null) break;
                    if (!list.Contains(found)) list.Add(found);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Busca de variantes de textura falhou: " + e.Message);
            }
            return list;
        }

        private static string MtlLibReferenced(string objPath, string dir)
        {
            try
            {
                if (!File.Exists(objPath)) return null;
                foreach (var raw in File.ReadAllLines(objPath))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("mtllib ", StringComparison.OrdinalIgnoreCase))
                        return Path.Combine(dir, line.Substring(7).Trim());
                }
            }
            catch { }
            return null;
        }

        private static string TextureFromMtl(string mtlPath, string dir)
        {
            try
            {
                if (string.IsNullOrEmpty(mtlPath) || !File.Exists(mtlPath)) return null;
                foreach (var raw in File.ReadAllLines(mtlPath))
                {
                    string line = raw.Trim();
                    if (!line.StartsWith("map_Kd", StringComparison.OrdinalIgnoreCase)) continue;

                    // Last token is the file name (ignore options like -s/-o that may precede it).
                    string tex = line.Substring("map_Kd".Length).Trim();
                    int lastSpace = tex.LastIndexOf(' ');
                    if (lastSpace >= 0 && tex.StartsWith("-")) tex = tex.Substring(lastSpace + 1);

                    if (string.IsNullOrEmpty(tex)) continue;
                    string candidate = Path.IsPathRooted(tex) ? tex : Path.Combine(dir, tex);
                    if (File.Exists(candidate)) return candidate;

                    // map_Kd sometimes stores just a bare name in another folder — try by filename in dir.
                    string byName = Path.Combine(dir, Path.GetFileName(tex));
                    if (File.Exists(byName)) return byName;
                }
            }
            catch { }
            return null;
        }
    }
}
