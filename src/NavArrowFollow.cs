using UnityEngine;
using UnityUtils;
using RpgEngine;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// Keeps the P12 nav arrows flanking the character on screen. The creator camera shifts the model
    /// sideways when a side panel opens (<see cref="CharacterCreatorCamera.Move"/>), so anchoring the
    /// arrows to a fixed point drifts them off the model. Each frame this projects the character's
    /// world position through the preview camera and repositions both arrows at ±dx around it.
    /// </summary>
    internal class NavArrowFollow : MonoBehaviour
    {
        public RectTransform panel;   // the preview panel rect (arrows' parent)
        public RectTransform prev, next;
        public RectTransform zoomIn, zoomOut; // optional zoom buttons, stacked below the right arrow
        public float dx = 140f;

        private Camera _cam;
        private Transform _target;

        private void Resolve()
        {
            var cc = UniqueMono<CharacterCreatorCamera>.instance;
            _cam = cc != null ? cc.cam : null;
            var creator = UniqueMono<CharacterCreator>.instance;
            _target = creator != null && creator.dummy != null ? creator.dummy.transform : null;
        }

        private void LateUpdate()
        {
            if (_cam == null || _target == null)
            {
                Resolve();
                if (_cam == null || _target == null) return;
            }
            if (panel == null || prev == null || next == null) return;

            // Body-middle world point → viewport (0..1 across the render texture = across the panel).
            Vector3 vp = _cam.WorldToViewportPoint(_target.position + Vector3.up);
            if (vp.z <= 0f) return; // behind the camera

            Rect r = panel.rect;
            float cx = (vp.x - 0.5f) * r.width; // horizontal offset from the panel centre to the model

            prev.anchoredPosition = new Vector2(cx - dx, 0f);
            next.anchoredPosition = new Vector2(cx + dx, 0f);

            // Zoom buttons: stacked just below the right arrow so they stay beside the arrows.
            if (zoomIn != null) zoomIn.anchoredPosition = new Vector2(cx + dx, -84f);
            if (zoomOut != null) zoomOut.anchoredPosition = new Vector2(cx + dx, -140f);
        }
    }
}
