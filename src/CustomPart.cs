using System.Collections.Generic;
using System.IO;
using UnityEngine;
using RiggedAttachType = RpgEngine.Characters.CharacterCreatorEnums.RiggedAttachType;

namespace CustomPartsMod
{
    /// <summary>
    /// One imported body part. Parts rebuilt across sessions register from metadata alone and import
    /// their mesh + texture from disk lazily, the first time they are applied (see <see cref="EnsureLoaded"/>),
    /// so opening the creator stays fast no matter how large the saved library is.
    /// </summary>
    internal class CustomPart
    {
        public string PartId;            // unique dictionary key / savedId (language-independent)
        public string SourceKey;         // stable per-model key (source filename) for saved scale
        public string ModelPath;         // source .obj/.glb path on disk (for reload across sessions)
        public string DisplayName;       // shown on the tab button
        public string[] CategoryPath;    // savePath prefix of the category it was imported into
        public RiggedAttachType Slot;    // bone socket it attaches to
        public string ChannelId;         // colour channel this part is painted by (P2), e.g. "_Color_Primary"
        public string Tag = "";          // P10 — user tag/theme within the category ("" = none)
        public string LinkGroupId = "";  // Auto-equip group ID (parts sharing this ID auto-equip together)

        public Mesh Mesh;                // ready mesh (OBJ path)
        public Texture2D Texture;        // optional albedo (the ACTIVE variant, P13)
        public string TexturePath;       // source path of Texture (active variant; for persistence)

        // P7 — snapshot portrait used as the tab button icon (instead of the text name). Captured on
        // Confirm and cached to disk (CustomParts\thumbs\<sourceKey>.png). ThumbnailSprite is the lazily
        // built Sprite so we don't re-allocate one on every pooled button re-init.
        public Texture2D Thumbnail;
        public Sprite ThumbnailSprite;
        public bool ThumbnailProbed;     // disk was already checked for a saved portrait (once per part)

        // P13 — texture variants: several textures for the same mesh, selectable via numbered boxes.
        // No-gap list (index = box); ActiveVariant indexes it. TexturePath/Texture mirror the active one.
        public List<string> TextureVariants = new List<string>();
        public int ActiveVariant;

        public float Scale = 1f;
        public float NormalizedScale = 1f; // scale that makes this mesh ~target size (hybrid base)
        public Vector3 ScaleAxis = Vector3.one; // per-axis multiplier on top of Scale (P4)
        public Vector3 LocalPosition = Vector3.zero;
        public Vector3 LocalEuler = Vector3.zero; // typed rotation (P5)
        public string GenderTag = "";    // "", "Feminine" or "Masculine" (P3)
        public bool Additive;            // eyes/accessories: attach WITHOUT replacing the slot (no RemoveAllChildren)
        public int AdditiveOverride;     // P14 user override: 0=auto, 1=accessory(additive), 2=replace

        private bool _loaded;

        /// <summary>
        /// Imports the mesh (and its active texture) from <see cref="ModelPath"/> the first time this
        /// part is applied. Parts rebuilt across sessions register from metadata only — no OBJ is parsed
        /// at startup — so this defers that cost to the click that actually needs the geometry. A no-op
        /// once done, or when the mesh is already in hand (fresh imports arrive with it). Idempotent and
        /// safe to call on every <see cref="CustomBodyPartAttachment.Build"/>.
        /// </summary>
        internal void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            if (Mesh != null) return; // fresh import: mesh + texture + NormalizedScale already set

            if (string.IsNullOrEmpty(ModelPath) || !File.Exists(ModelPath))
            {
                Compat.ShowError("Arquivo do modelo ausente: " + ModelPath);
                return;
            }

            string fileType = Path.GetExtension(ModelPath).TrimStart('.').ToLowerInvariant();
            string fileName = Path.GetFileNameWithoutExtension(ModelPath);

            var rpg = Compat.ImportMesh(fileName, ModelPath, fileType);
            if (rpg == null || rpg.mesh == null || rpg.mesh.vertexCount == 0)
            {
                // OBJ/STL load a ready mesh; GLB needs the async path (skipped on reload, matching P0).
                Plugin.Log.LogWarning("[lazy] nao consegui carregar a malha de: " + ModelPath);
                return;
            }

            Mesh = rpg.mesh;
            Mesh.RecalculateBounds();
            if (Mesh.normals == null || Mesh.normals.Length == 0) Mesh.RecalculateNormals();
            NormalizedScale = ImportFlow.NormalizeScale(Mesh); // only needed when re-editing scale

            // Active texture variant, loaded on demand (the selection PersistenceLoader used to do eagerly).
            if (Texture == null)
            {
                string texPath = null;
                if (TextureVariants != null && TextureVariants.Count > 0)
                    texPath = TextureVariants[Mathf.Clamp(ActiveVariant, 0, TextureVariants.Count - 1)];
                else if (!string.IsNullOrEmpty(TexturePath))
                    texPath = TexturePath;

                var t = TextureLoader.LoadFromFile(texPath);
                if (t != null) { Texture = t; TexturePath = texPath; }
            }
        }

        /// <summary>True once the mesh has been imported (so a background thumbnail pass can tell whether
        /// it loaded the mesh itself, and free it afterwards, vs. finding it already in use).</summary>
        internal bool IsMeshLoaded => _loaded;

        /// <summary>Drops the imported mesh + active texture and lets <see cref="EnsureLoaded"/> re-import
        /// them on next real use. Called after the background worker photographs a part it wasn't applying,
        /// so a full-library backfill doesn't hold thousands of meshes in memory. Only safe when the part
        /// is NOT currently applied to a character (the caller checks). The thumbnail is kept.</summary>
        internal void UnloadMesh()
        {
            Mesh = null;
            Texture = null;
            _loaded = false;
        }
    }
}
