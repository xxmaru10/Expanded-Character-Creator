using System;
using UnityEngine;
using SlickUi;
using UnityUtils;
using RpgEngine;             // CharacterCreatorCamera
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// Adds two zoom buttons (+ / –) beside the navigation arrows in the creator, zooming the preview
    /// camera. There is no native zoom (CharacterCreatorCamera.FocusOn* are empty stubs), so we adjust
    /// the camera's field of view (or orthographic size), which is independent of the camera's
    /// panel-shift Move() logic. The buttons are stacked below the right arrow and reuse the arrows'
    /// <see cref="NavArrowFollow"/> so they track the model.
    /// </summary>
    internal static class ZoomButtons
    {
        private static ZoomButton _in, _out;

        internal static void Ensure(CharacterCreator creator)
        {
            if (_in != null && _out != null) return;
            if (creator == null) return;

            var cam = creator.creatorCam != null ? creator.creatorCam : UniqueMono<CharacterCreatorCamera>.instance;
            UiButton panel = cam != null ? cam.inputPanel : null;
            Transform parent = panel != null ? panel.transform : null;
            if (parent == null) return;

            _in = Make(parent, plus: true, pos: new Vector2(140f, -84f), () => Zoom(+1f));
            _out = Make(parent, plus: false, pos: new Vector2(140f, -140f), () => Zoom(-1f));

            // Track the model beside the arrows via the arrows' follower (created by NavArrows.Ensure).
            var follow = parent.GetComponentInChildren<NavArrowFollow>();
            if (follow != null) { follow.zoomIn = _in.rectTransform; follow.zoomOut = _out.rectTransform; }

            Plugin.Log.LogInfo("Botoes de zoom (+/-) adicionados ao lado das setas.");
        }

        private static ZoomButton Make(Transform parent, bool plus, Vector2 pos, Action onClick)
        {
            var go = new GameObject(plus ? "ZoomIn" : "ZoomOut", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);

            var btn = go.AddComponent<ZoomButton>();
            btn.plus = plus;
            btn.color = new Color(1f, 1f, 1f, 0.9f);
            btn.raycastTarget = true;
            btn.onClick = onClick;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(44f, 44f);
            rt.anchoredPosition = pos;
            go.transform.SetAsLastSibling(); // draw over the preview
            return btn;
        }

        /// <summary>dir &gt; 0 zooms in, dir &lt; 0 zooms out. FOV for perspective, size for orthographic.</summary>
        private static void Zoom(float dir)
        {
            var cc = UniqueMono<CharacterCreatorCamera>.instance;
            var cam = cc != null ? cc.cam : null;
            if (cam == null) return;

            if (cam.orthographic)
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * (dir > 0f ? 0.88f : 1f / 0.88f), 0.15f, 50f);
            else
                cam.fieldOfView = Mathf.Clamp(cam.fieldOfView - dir * 4f, 8f, 70f);
        }
    }
}
