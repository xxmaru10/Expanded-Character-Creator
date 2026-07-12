using UnityEngine;
using UnityUtils;
using RpgEngine;

namespace CustomPartsMod
{
    /// <summary>
    /// Raises/lowers the character-creator camera. The engine has NO vertical control — zoom is FOV
    /// (<see cref="ZoomButtons"/>) and drag only orbits (<c>rotCore.eulerAngles</c>), so you can't bring
    /// the head to frame centre to zoom into it. This translates the orbit pivot (<c>rotCore</c>) along
    /// its parent's Y, cranning the whole rig up/down while keeping zoom and rotation. The step is a
    /// fraction of the orbit radius so it feels the same at any zoom, and the total is clamped to ±radius
    /// of the captured home height so the model can't be lost off-frame. Reset restores the home height
    /// (see <see cref="CameraReset"/>).
    /// </summary>
    internal static class CameraPan
    {
        /// <summary>dir &gt; 0 raises the view (toward the head), dir &lt; 0 lowers it (toward the feet).</summary>
        internal static void Pan(float dir)
        {
            var cc = UniqueMono<CharacterCreatorCamera>.instance;
            if (cc == null || cc.cam == null || cc.rotCore == null) return;

            float radius = Vector3.Distance(cc.cam.transform.position, cc.rotCore.position);
            if (radius < 0.01f) radius = 1f;

            Vector3 p = cc.rotCore.localPosition;
            p.y += radius * 0.12f * Mathf.Sign(dir);
            if (CameraReset.HomeCaptured)
                p.y = Mathf.Clamp(p.y, CameraReset.HomeRotCorePos.y - radius, CameraReset.HomeRotCorePos.y + radius);
            cc.rotCore.localPosition = p;
        }
    }
}
