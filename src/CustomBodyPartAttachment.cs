using System.Collections.Generic;
using UnityEngine;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// A rigid (non-skinned) custom body part: a MeshRenderer parented to an animated bone so
    /// it follows the animation. Level A of DESIGN.md. Being a <see cref="CharacterAttachment"/>
    /// means the engine's colour propagation reaches it for free.
    /// The pivot is centered on the mesh, so scaling grows in place; position is a world-unit
    /// offset from the bone that the user can type in.
    /// </summary>
    internal class CustomBodyPartAttachment : CharacterAttachment
    {
        private Vector3 _meshCenter;
        private PickupableCharacter _character;

        // P2 painting: for TEXTURED parts we own a lit material whose _BaseColor tints the texture.
        // Default white = pure texture; painting the part's channel multiplies the texture by the colour.
        private Material _tintMat;
        private Color _paintColor = Color.white;

        /// <summary>True when this part is painted via our own tint path (textured parts).</summary>
        internal bool HasTint => _tintMat != null;
        internal bool Painted { get; private set; }

        /// <summary>Tints the texture by <paramref name="c"/> (texture × colour). No-op for untextured
        /// parts, which the engine paints through the shared character material directly.</summary>
        internal void ApplyPaint(Color c)
        {
            if (_tintMat == null) return;
            Painted = true;
            _paintColor = c;
            if (_tintMat.HasProperty("_BaseColor")) _tintMat.SetColor("_BaseColor", c);
            if (_tintMat.HasProperty("_Color")) _tintMat.SetColor("_Color", c);
        }

        /// <summary>Reverts to the pure texture (white tint). Called when the edit panel reopens.</summary>
        internal void ResetPaint()
        {
            Painted = false;
            _paintColor = Color.white;
            if (_tintMat == null) return;
            if (_tintMat.HasProperty("_BaseColor")) _tintMat.SetColor("_BaseColor", Color.white);
            if (_tintMat.HasProperty("_Color")) _tintMat.SetColor("_Color", Color.white);
        }

        public override CharacterAttachment Instantiate(PickupableCharacter character, bool delOther = true) => this;

        public override bool ContainedIn(PickupableCharacter character, string partId)
            => character.attachedItems.ContainsKey(partId);

        internal CustomPart Part { get; private set; }
        internal float UserScale { get; private set; } = 1f;
        internal Vector3 UserScaleAxis { get; private set; } = Vector3.one; // per-axis multiplier (P4)
        internal Vector3 UserOffset { get; private set; } = Vector3.zero;
        internal Vector3 UserEuler { get; private set; } = Vector3.zero;    // typed rotation (P5)

        internal void SetUserScale(float scale)
        {
            UserScale = Mathf.Clamp(scale, 0.001f, 10000f);
            if (Part != null) Part.Scale = UserScale;
            Reapply();
        }

        internal void SetUserScaleAxis(Vector3 axis)
        {
            UserScaleAxis = SanitizeAxis(axis);
            if (Part != null) Part.ScaleAxis = UserScaleAxis;
            Reapply();
        }

        internal void SetUserOffset(Vector3 offset)
        {
            UserOffset = offset;
            if (Part != null) Part.LocalPosition = offset;
            Reapply();
        }

        /// <summary>Adds a WORLD-space delta to the offset (used by the drag-to-move modeling mode).
        /// UserOffset lives in the bone's local frame (world-unit magnitude — see <see cref="Reapply"/>),
        /// so the world delta is rotated into the bone frame before being added.</summary>
        internal void AddWorldOffset(Vector3 worldDelta)
        {
            Transform parent = transform.parent;
            Vector3 local = parent != null ? Quaternion.Inverse(parent.rotation) * worldDelta : worldDelta;
            SetUserOffset(UserOffset + local);
        }

        internal void SetUserEuler(Vector3 euler)
        {
            UserEuler = euler;
            if (Part != null) Part.LocalEuler = euler;
            Reapply();
        }

        /// <summary>Per-axis scale is a multiplier; guard against 0/negative that would flatten the mesh.</summary>
        private static Vector3 SanitizeAxis(Vector3 a)
            => new Vector3(
                a.x > 1e-4f ? a.x : 1f,
                a.y > 1e-4f ? a.y : 1f,
                a.z > 1e-4f ? a.z : 1f);

        /// <summary>Applies a texture live (rebuilds the material with a shader that shows it).</summary>
        internal void SetTexture(Texture2D tex)
        {
            if (Part != null) Part.Texture = tex;
            var r = GetComponent<Renderer>();
            if (r != null && Part != null)
            {
                r.sharedMaterial = BuildMaterial(Part, _character);
                _tintMat = Part.Texture != null ? r.sharedMaterial : null;
                if (Painted) ApplyPaint(_paintColor); // keep the paint if we rebuilt while tinted
            }
        }

        /// <summary>
        /// Places the mesh so its CENTER sits at (bone + UserOffset) in world units, at UserScale
        /// world size, cancelling the bone's accumulated scale. Recomputed whenever scale/offset
        /// change so the mesh scales in place and the offset behaves like world units.
        /// </summary>
        private void Reapply()
        {
            Transform parent = transform.parent;
            Vector3 b = parent != null ? parent.lossyScale : Vector3.one;
            Vector3 invB = new Vector3(1f / NonZero(b.x), 1f / NonZero(b.y), 1f / NonZero(b.z));

            // Effective world-space scale = uniform UserScale * per-axis multiplier (P4).
            Vector3 s = new Vector3(UserScale * UserScaleAxis.x, UserScale * UserScaleAxis.y, UserScale * UserScaleAxis.z);
            Quaternion rot = Quaternion.Euler(UserEuler); // typed rotation relative to the bone (P5)

            transform.localScale = Vector3.Scale(s, invB);
            transform.localRotation = rot;

            // Keep the mesh CENTER pinned at (bone + UserOffset) in world units even under
            // rotation/non-uniform scale, so the part scales/rotates in place instead of flying off.
            Vector3 centerWorld = rot * Vector3.Scale(s, _meshCenter);
            Vector3 term = UserOffset - centerWorld;
            transform.localPosition = Vector3.Scale(term, invB);
        }

        internal static CustomBodyPartAttachment Build(CustomPart part, PickupableCharacter character)
        {
            // Create the GameObject + component FIRST so this method never returns null
            // (SpawnAlongside dereferences .gameObject on the result).
            var go = new GameObject("CustomPart_" + part.PartId);
            var attachment = go.AddComponent<CustomBodyPartAttachment>();
            attachment.Part = part;
            attachment._character = character;
            attachment.UserScale = Mathf.Clamp(part.Scale, 0.001f, 10000f);
            attachment.UserScaleAxis = SanitizeAxis(part.ScaleAxis);
            attachment.UserOffset = part.LocalPosition;
            attachment.UserEuler = part.LocalEuler;

            try
            {
                Transform attachSocket = character.riggedAttachments != null
                    ? character.riggedAttachments.AttachmentPoint(part.Slot)
                    : null;

                // Prefer the animated bone so the part follows the body; fall back to the socket.
                Transform bone = BoneResolver.Resolve(character, part.Slot);
                Transform parent = bone ?? attachSocket ?? character.transform;

                // Replace, don't stack: clear the vanilla part (under the attach socket) and any
                // prior custom part (under the bone), the way the engine does (RemoveAllChildren).
                // Done before parenting our own object so it is not removed too.
                // Additive parts (eyes/accessories) skip this so they add ON TOP of the head/face
                // instead of replacing it.
                if (!part.Additive)
                {
                    if (attachSocket != null) attachment.RemoveAllChildren(attachSocket, character);
                    if (parent != null && parent != attachSocket) attachment.RemoveAllChildren(parent, character);
                }

                go.transform.SetParent(parent, worldPositionStays: false);
                go.transform.localEulerAngles = Vector3.zero;
                go.layer = parent.gameObject.layer;

                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = part.Mesh;
                attachment._meshCenter = part.Mesh != null ? part.Mesh.bounds.center : Vector3.zero;

                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = BuildMaterial(part, character);
                // Textured parts get our tintable material (P2 paint); untextured use the game material.
                attachment._tintMat = part.Texture != null ? renderer.sharedMaterial : null;

                attachment.Reapply();
                LogDiagnostics(part, parent, renderer);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError("Falha ao montar a malha da parte '" + part.PartId + "': " + e);
                // attachment is still a valid, empty object — no crash downstream.
            }

            return attachment;
        }

        private static float NonZero(float v) => Mathf.Approximately(v, 0f) ? 1f : v;

        /// <summary>Numbers to diagnose placement without seeing the game.</summary>
        private static void LogDiagnostics(CustomPart part, Transform parent, Renderer renderer)
        {
            try
            {
                Mesh m = part.Mesh;
                Bounds local = m != null ? m.bounds : default;
                Bounds world = renderer.bounds;
                string shader = renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null
                    ? renderer.sharedMaterial.shader.name
                    : "<none>";

                Plugin.Log.LogInfo(
                    $"[diag] '{part.PartId}' slot={part.Slot} parent='{parent.name}' " +
                    $"parentLossyScale={parent.lossyScale} parentEuler={parent.rotation.eulerAngles} | " +
                    $"mesh verts={(m != null ? m.vertexCount : 0)} localSize={local.size} localCenter={local.center} | " +
                    $"WORLD size={world.size} center={world.center} | shader='{shader}'");
            }
            catch { /* diagnostics must never break the attach */ }
        }

        private static Material BuildMaterial(CustomPart part, PickupableCharacter character)
        {
            Material material;

            if (part.Texture != null)
            {
                // Textured: a simple lit shader shows the PNG with its own colours.
                material = new Material(FallbackShader());
                ApplyTexture(material, part.Texture);
            }
            else if (Plugin.UseGameShader.Value && TryGetReferenceMaterial(character, out var reference))
            {
                // Untextured: reuse the character shader so the game colour pickers paint it.
                material = new Material(reference);
            }
            else
            {
                material = new Material(FallbackShader());
            }

            return material;
        }

        private static void ApplyTexture(Material m, Texture2D tex)
        {
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
        }

        private static bool TryGetReferenceMaterial(PickupableCharacter character, out Material material)
        {
            foreach (KeyValuePair<string, CharacterAttachment> kv in character.attachedItems)
            {
                if (kv.Value == null) continue;
                var r = kv.Value.GetComponent<Renderer>();
                if (r != null && r.sharedMaterial != null)
                {
                    material = r.sharedMaterial;
                    return true;
                }
            }
            material = null;
            return false;
        }

        private static Shader FallbackShader()
            => Shader.Find("Universal Render Pipeline/Lit")
               ?? Shader.Find("Standard")
               ?? Shader.Find("Sprites/Default");
    }
}
