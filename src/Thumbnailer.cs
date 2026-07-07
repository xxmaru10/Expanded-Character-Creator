using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityUtils;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// P7 — captures a portrait to use as the part's tab-button icon. Renders JUST the imported part,
    /// framed tight on it: hides every other renderer on the dummy + the backdrop, points the creator
    /// camera at the part's bounds, and renders to a transparent 512×512 texture. Restores the camera
    /// and all renderers afterwards (single synchronous render, no visible flicker). Falls back to the
    /// engine's whole-character <see cref="CharacterCreator.SnapShot"/> if isolation can't run.
    /// </summary>
    internal static class Thumbnailer
    {
        internal static void Capture(CustomBodyPartAttachment attachment)
        {
            if (attachment == null) return;
            var part = attachment.Part;
            if (part == null || string.IsNullOrEmpty(part.SourceKey)) return;

            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator == null) return;

            Texture2D shot = CaptureIsolated(creator, attachment) ?? CaptureWhole(creator);
            if (shot == null || shot.height <= 1)
            {
                Plugin.Log.LogWarning("[thumb] snapshot vazio; miniatura nao gerada.");
                return;
            }

            part.Thumbnail = shot;
            part.ThumbnailSprite = null; // rebuilt lazily from the new texture
            ThumbnailStore.Save(part.SourceKey, shot);

            if (creator.itemTabsLoader != null) creator.itemTabsLoader.Refresh(); // repaint the button
        }

        /// <summary>Renders only the part, framed on its bounds, on a transparent background. Returns
        /// null (so the caller can fall back) if the camera or the part's renderers aren't available.</summary>
        private static Texture2D CaptureIsolated(CharacterCreator creator, CustomBodyPartAttachment attachment)
        {
            var ccam = creator.creatorCam;
            var cam = ccam != null ? ccam.cam : null;
            if (cam == null) return null;

            var partRenderers = attachment.GetComponentsInChildren<Renderer>(true);
            if (partRenderers == null || partRenderers.Length == 0) return null;

            // World bounds of the part (skip disabled/empty renderers).
            Bounds b = default; bool has = false;
            foreach (var r in partRenderers)
            {
                if (r == null) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            if (!has) return null;

            var partSet = new HashSet<Renderer>(partRenderers);
            var toggledOff = new List<Renderer>();

            // Save camera + scene state.
            Vector3 camPos = cam.transform.position;
            Quaternion camRot = cam.transform.rotation;
            RenderTexture prevTarget = cam.targetTexture;
            CameraClearFlags prevClear = cam.clearFlags;
            Color prevBg = cam.backgroundColor;
            float prevNear = cam.nearClipPlane;
            bool bgWasOn = creator.backgroundContent != null && creator.backgroundContent.gameObject.activeSelf;

            // Post-processing off (URP), matched to the engine's own DoSnapShot. Via reflection so we
            // don't need a compile-time URP reference.
            Component urp = FindUrpData(cam);
            PropertyInfo ppProp = urp != null ? urp.GetType().GetProperty("renderPostProcessing") : null;
            bool prevPP = false;
            if (ppProp != null && ppProp.CanRead) { try { prevPP = (bool)ppProp.GetValue(urp); } catch { ppProp = null; } }

            RenderTexture rt = null;
            Texture2D shot = null;
            try
            {
                // Hide everything else on the character + the dungeon backdrop, so only the part renders.
                if (creator.dummy != null)
                    foreach (var r in creator.dummy.GetComponentsInChildren<Renderer>(true))
                        if (r != null && r.enabled && !partSet.Contains(r)) { r.enabled = false; toggledOff.Add(r); }
                if (creator.backgroundContent != null) creator.backgroundContent.gameObject.SetActive(false);

                // Frame on the part: fit its LARGEST box dimension (not the diagonal sphere, which
                // overshoots and leaves the part tiny) to a target fraction of the frame. Keep the
                // current view direction. fill is user-tunable (ThumbnailFill).
                float half = Mathf.Max(0.02f, Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z)));
                float fill = Mathf.Clamp(Plugin.ThumbnailFill.Value, 0.1f, 1f);
                float vfov = Mathf.Max(0.1f, cam.fieldOfView * Mathf.Deg2Rad);
                float dist = half / (fill * Mathf.Tan(vfov * 0.5f));
                Vector3 dir = cam.transform.forward;
                cam.transform.position = b.center - dir * dist;
                cam.transform.LookAt(b.center, Vector3.up);
                cam.aspect = 1f;
                cam.nearClipPlane = Mathf.Min(prevNear, 0.01f);

                if (ppProp != null) ppProp.SetValue(urp, false);

                rt = new RenderTexture(512, 512, 24);
                cam.targetTexture = rt;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // transparent
                cam.Render();

                shot = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, mipChain: false);
                RenderTexture.active = rt;
                shot.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
                shot.Apply();
                RenderTexture.active = null;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[thumb] falha ao isolar a peca: " + e.Message);
                shot = null;
            }
            finally
            {
                // Restore camera.
                cam.targetTexture = prevTarget;
                cam.clearFlags = prevClear;
                cam.backgroundColor = prevBg;
                cam.nearClipPlane = prevNear;
                cam.transform.position = camPos;
                cam.transform.rotation = camRot;
                cam.ResetAspect();
                if (ppProp != null) { try { ppProp.SetValue(urp, prevPP); } catch { } }

                // Restore renderers + backdrop.
                foreach (var r in toggledOff) if (r != null) r.enabled = true;
                if (creator.backgroundContent != null && bgWasOn) creator.backgroundContent.gameObject.SetActive(true);

                if (rt != null) { RenderTexture.active = null; rt.Release(); UnityEngine.Object.Destroy(rt); }
            }

            return shot;
        }

        /// <summary>v1 fallback: the engine's own whole-character token portrait.</summary>
        private static Texture2D CaptureWhole(CharacterCreator creator)
        {
            try { return creator.SnapShot(); }
            catch (Exception e) { Plugin.Log.LogWarning("[thumb] SnapShot falhou: " + e.Message); return null; }
        }

        private static Component FindUrpData(Camera cam)
        {
            foreach (var c in cam.GetComponents<Component>())
                if (c != null && c.GetType().Name == "UniversalAdditionalCameraData") return c;
            return null;
        }
    }
}
