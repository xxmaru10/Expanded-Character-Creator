using System;
using System.IO;
using UnityEngine;
using UnityUtils;
using RpgEngine.Characters;
using RiggedAttachType = RpgEngine.Characters.CharacterCreatorEnums.RiggedAttachType;

namespace CustomPartsMod
{
    /// <summary>
    /// P0 — persistence across sessions. On the first time the creator opens, rebuilds every saved
    /// model (those confirmed via the panel) from its file on disk, so custom parts survive a
    /// restart. Silent: reloads the mesh through MeshImporter.ImportNew (no file browser).
    /// </summary>
    internal static class PersistenceLoader
    {
        private static bool _loaded;

        internal static void LoadAll(CharacterCreator creator)
        {
            if (_loaded) return; // once per session; parts stay registered afterwards
            _loaded = true;

            if (creator == null || creator.itemTabsLoader == null) return;

            int ok = 0, fail = 0;
            foreach (var kv in ScaleStore.AllModels())
            {
                try { if (Reload(kv.Key, kv.Value)) ok++; else fail++; }
                catch (Exception e) { fail++; Plugin.Log.LogWarning("[persist] '" + kv.Key + "': " + e.Message); }
            }

            if (ok > 0) creator.itemTabsLoader.Refresh();
            Plugin.Log.LogInfo($"[persist] {ok} modelo(s) recarregado(s), {fail} pulado(s).");
        }

        private static bool Reload(string sourceKey, PartTransform rec)
        {
            if (string.IsNullOrEmpty(rec.modelPath) || !File.Exists(rec.modelPath))
            {
                Plugin.Log.LogWarning("[persist] arquivo do modelo ausente: " + rec.modelPath);
                return false;
            }
            if (rec.category == null || rec.category.Length == 0)
                return false; // pre-P0 record without category; user re-confirms once to upgrade it

            string fileType = Path.GetExtension(rec.modelPath).TrimStart('.').ToLowerInvariant();
            string fileName = Path.GetFileNameWithoutExtension(rec.modelPath);

            RPGMesh rpg = Compat.ImportMesh(fileName, rec.modelPath, fileType);
            if (rpg == null || rpg.mesh == null || rpg.mesh.vertexCount == 0)
            {
                // obj/stl load a ready mesh; glb needs the async path — skip those on reload for now.
                Plugin.Log.LogWarning("[persist] nao consegui recarregar a malha de: " + rec.modelPath);
                return false;
            }

            Mesh mesh = rpg.mesh;
            mesh.RecalculateBounds();
            if (mesh.normals == null || mesh.normals.Length == 0) mesh.RecalculateNormals();

            RiggedAttachType slot = RiggedAttachType.head;
            if (!string.IsNullOrEmpty(rec.slot)) Enum.TryParse(rec.slot, out slot);

            // P13 — restore the texture variant list + active index; load the active one.
            var variants = new System.Collections.Generic.List<string>();
            if (rec.textureVariants != null)
                foreach (var v in rec.textureVariants) if (!string.IsNullOrEmpty(v)) variants.Add(v);
            if (variants.Count == 0 && !string.IsNullOrEmpty(rec.texturePath)) variants.Add(rec.texturePath);
            int activeVariant = variants.Count > 0 ? Mathf.Clamp(rec.activeVariant, 0, variants.Count - 1) : 0;

            string activePath = variants.Count > 0 ? variants[activeVariant] : null;
            Texture2D tex = !string.IsNullOrEmpty(activePath) ? TextureLoader.LoadFromFile(activePath) : null;

            var part = new CustomPart
            {
                PartId = ImportFlow.MakeId(sourceKey),
                SourceKey = sourceKey,
                ModelPath = rec.modelPath,
                DisplayName = fileName,
                CategoryPath = rec.category,
                Slot = slot,
                ChannelId = !string.IsNullOrEmpty(rec.channel) ? rec.channel : ChannelMap.ForCategory(rec.category), // P2
                Tag = rec.tag ?? "", // P10 user tag/theme
                Mesh = mesh,
                Texture = tex,
                TexturePath = tex != null ? activePath : null,
                TextureVariants = variants,
                ActiveVariant = activeVariant,
                Scale = rec.scale,
                NormalizedScale = ImportFlow.NormalizeScale(mesh),
                ScaleAxis = rec.scaleAxis,
                LocalPosition = rec.offset,
                LocalEuler = rec.euler,
                GenderTag = rec.gender ?? "",
                AdditiveOverride = rec.additive,
                Additive = AccessoryMap.ResolveAdditive(rec.category, rec.additive), // eyes/accessories stay additive on reload (P14)
            };

            CustomPartCatalog.Register(part);                     // -> attachmentPaths, becomes an option
            Compat.AddTabInitializer(UniqueMono<CharacterCreator>.instance.itemTabsLoader, part.PartId);
            return true;
        }
    }
}
