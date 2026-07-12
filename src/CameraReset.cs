using System.Reflection;
using UnityEngine;
using UnityUtils;
using RpgEngine;

namespace CustomPartsMod
{
    /// <summary>
    /// Resets the character-creator camera to its default pose. IMPORTANT: the engine's own
    /// <c>FocusOnHead()</c>/<c>FocusOnFull()</c> are EMPTY stubs (verified by decompile), so they do
    /// nothing. The rig is <c>rotCore</c> (orbit pivot; <c>MouseDrag</c> writes its rotation) → <c>cam</c>
    /// child (fixed local pose, offset back on Z). Zoom is FOV (untouched here → reset keeps the zoom).
    ///
    /// The old code zeroed <c>rotCore.localRotation</c> to <c>identity</c>, which faced the BACK (the real
    /// default is ~(20,190,0), NOT identity — confirmed by logging). So we capture rotCore's pristine
    /// local rotation+position on the FIRST creator open (<see cref="CaptureHome"/>, from the EditProp
    /// postfix, before any orbit/pan) and restore THAT — which also undoes <see cref="CameraPan"/>'s
    /// vertical offset.
    /// </summary>
    internal static class CameraReset
    {
        private static readonly FieldInfo BasePosField =
            typeof(CharacterCreatorCamera).GetField("basePos", BindingFlags.NonPublic | BindingFlags.Instance);

        internal static bool HomeCaptured { get; private set; }
        internal static Vector3 HomeRotCorePos { get; private set; }
        internal static Quaternion HomeRotCoreRot { get; private set; }

        /// <summary>Record the camera rig's pristine default ONCE, on the first creator open (before the
        /// user has orbited or panned).</summary>
        internal static void CaptureHome()
        {
            if (HomeCaptured) return; // first (pristine) capture wins
            var cc = UniqueMono<CharacterCreatorCamera>.instance;
            if (cc == null || cc.rotCore == null) return;
            HomeRotCorePos = cc.rotCore.localPosition;
            HomeRotCoreRot = cc.rotCore.localRotation;
            HomeCaptured = true;
        }

        internal static void Reset()
        {
            var cc = UniqueMono<CharacterCreatorCamera>.instance;
            if (cc == null || cc.cam == null)
            {
                Plugin.Log.LogWarning("Reset câmera: câmera do criador indisponível.");
                return;
            }

            if (BasePosField != null && BasePosField.GetValue(cc) is PropTransform bp)
            {
                cc.cam.transform.localPosition = bp.position;
                cc.cam.transform.localRotation = bp.rotation;
            }
            if (cc.rotCore != null)
            {
                if (HomeCaptured)
                {
                    cc.rotCore.localRotation = HomeRotCoreRot; // real front-facing default (not identity)
                    cc.rotCore.localPosition = HomeRotCorePos; // undo vertical pan
                }
                else cc.rotCore.localRotation = Quaternion.identity; // fallback if home wasn't captured
            }
        }
    }
}
