using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CustomPartsMod
{
    /// <summary>
    /// P7 — persists per-model thumbnail portraits to disk so a part shows its picture instead of
    /// its name across sessions. PNGs live in <c>…\The RPG Engine\CustomParts\thumbs\</c>, one per
    /// model, named from the source key. Loaded textures are cached in memory by key.
    /// </summary>
    internal static class ThumbnailStore
    {
        private static readonly Dictionary<string, Texture2D> Cache =
            new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        private static string ThumbsDir => Path.Combine(ScaleStore.CustomPartsDir, "thumbs");

        private static string PathFor(string sourceKey) => Path.Combine(ThumbsDir, Sanitize(sourceKey) + ".png");

        /// <summary>Writes the portrait to disk and updates the in-memory cache.</summary>
        internal static void Save(string sourceKey, Texture2D tex)
        {
            if (string.IsNullOrEmpty(sourceKey) || tex == null) return;
            try
            {
                Directory.CreateDirectory(ThumbsDir);
                byte[] png = tex.EncodeToPNG();
                if (png == null || png.Length == 0) return;
                File.WriteAllBytes(PathFor(sourceKey), png);
                Cache[sourceKey] = tex;
                Plugin.Log.LogInfo($"[thumb] salvo retrato de '{sourceKey}' ({png.Length} bytes)");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Nao consegui salvar a miniatura: " + e.Message);
            }
        }

        /// <summary>Loads the saved portrait for a model (from cache, else from disk). Returns false
        /// when there is none.</summary>
        internal static bool TryLoad(string sourceKey, out Texture2D tex)
        {
            tex = null;
            if (string.IsNullOrEmpty(sourceKey)) return false;
            if (Cache.TryGetValue(sourceKey, out tex) && tex != null) return true;

            string path = PathFor(sourceKey);
            if (!File.Exists(path)) return false;
            try
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
                if (!t.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.Destroy(t); return false; }
                Cache[sourceKey] = t;
                tex = t;
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Nao consegui ler a miniatura: " + e.Message);
                return false;
            }
        }

        /// <summary>Deletes the saved portrait (when a part is removed).</summary>
        internal static void Delete(string sourceKey)
        {
            if (string.IsNullOrEmpty(sourceKey)) return;
            Cache.Remove(sourceKey);
            try { if (File.Exists(PathFor(sourceKey))) File.Delete(PathFor(sourceKey)); }
            catch (Exception e) { Plugin.Log.LogWarning("Nao consegui apagar a miniatura: " + e.Message); }
        }

        private static string Sanitize(string key)
        {
            var sb = new StringBuilder(key.Length);
            foreach (char c in key)
                sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            return sb.ToString();
        }
    }
}
