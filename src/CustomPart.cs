using System.Collections.Generic;
using UnityEngine;
using RiggedAttachType = RpgEngine.Characters.CharacterCreatorEnums.RiggedAttachType;

namespace CustomPartsMod
{
    /// <summary>
    /// One imported body part held in memory. In Phase 2 these will be reconstructed
    /// from files on disk so they persist across sessions.
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

        public Mesh Mesh;                // ready mesh (OBJ path)
        public Texture2D Texture;        // optional albedo (the ACTIVE variant, P13)
        public string TexturePath;       // source path of Texture (active variant; for persistence)

        // P7 — snapshot portrait used as the tab button icon (instead of the text name). Captured on
        // Confirm and cached to disk (CustomParts\thumbs\<sourceKey>.png). ThumbnailSprite is the lazily
        // built Sprite so we don't re-allocate one on every pooled button re-init.
        public Texture2D Thumbnail;
        public Sprite ThumbnailSprite;

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
    }
}
