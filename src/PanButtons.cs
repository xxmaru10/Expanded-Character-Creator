using System;
using UnityEngine;
using SlickUi;
using UnityUtils;
using RpgEngine;             // CharacterCreatorCamera
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// Two ▲ ▼ buttons that raise/lower the creator camera (<see cref="CameraPan"/>), mirroring the
    /// zoom +/- on the model's OTHER side (left column). Reuses the nav arrows' <see cref="NavArrowFollow"/>
    /// so they track the model when a side panel shifts the camera.
    /// </summary>
    internal static class PanButtons
    {
        private static PanButton _up, _down;

        internal static void Ensure(CharacterCreator creator)
        {
            if (_up != null && _down != null) return;
            if (creator == null) return;

            var cam = creator.creatorCam != null ? creator.creatorCam : UniqueMono<CharacterCreatorCamera>.instance;
            UiButton panel = cam != null ? cam.inputPanel : null;
            Transform parent = panel != null ? panel.transform : null;
            if (parent == null) return;

            _up = Make(parent, up: true, pos: new Vector2(-140f, -84f), () => CameraPan.Pan(+1f));
            _down = Make(parent, up: false, pos: new Vector2(-140f, -140f), () => CameraPan.Pan(-1f));

            var follow = parent.GetComponentInChildren<NavArrowFollow>();
            if (follow != null) { follow.panUp = _up.rectTransform; follow.panDown = _down.rectTransform; }

            Plugin.Log.LogInfo("Botoes de camera (subir/descer) adicionados a esquerda do boneco.");
        }

        private static PanButton Make(Transform parent, bool up, Vector2 pos, Action onClick)
        {
            var go = new GameObject(up ? "PanUp" : "PanDown", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);

            var btn = go.AddComponent<PanButton>();
            btn.pointUp = up;
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
    }
}
