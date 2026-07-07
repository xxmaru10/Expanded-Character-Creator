using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SlickUi;

namespace CustomPartsMod
{
    /// <summary>
    /// Small floating panel shown before importing into a feet/shoe category: asks whether the model is
    /// for the LEFT or RIGHT foot (the engine keeps left/right as separate lower-leg sockets). Defaults to
    /// the last-used side (<see cref="ScaleStore.GetLastFootSideLeft"/>). On pick it calls back with the
    /// chosen side (true = left) and the caller maps it to legLowerL / legLowerR.
    /// </summary>
    internal static class FootSidePrompt
    {
        private static GameObject _panel;

        private static readonly Color PickHi = new Color(0.20f, 0.55f, 0.28f, 1f);

        internal static void Open(GameObject buttonTemplate, Transform canvas, bool defaultLeft, Action<bool> onPick)
        {
            Close();
            if (buttonTemplate == null || canvas == null) { onPick?.Invoke(defaultLeft); return; }

            var go = new GameObject("FootSidePrompt", typeof(RectTransform));
            _panel = go;
            PanelUi.BuildShell(go, canvas, buttonTemplate, new Vector2(360f, 150f), new Vector2(0f, -80f), "Lado do pé");

            PanelUi.SmallLabel(go.transform, "Este modelo é para qual pé?", new Vector2(14f, -48f), new Vector2(332f, 24f));

            var left = PanelUi.Button(buttonTemplate, go.transform, "◀ Esquerda", new Vector2(14f, -80f), new Vector2(162f, 50f), () => Pick(true, onPick));
            var right = PanelUi.Button(buttonTemplate, go.transform, "Direita ▶", new Vector2(184f, -80f), new Vector2(162f, 50f), () => Pick(false, onPick));

            // Highlight the remembered default so a quick double-take confirms the usual side.
            Highlight(defaultLeft ? left : right);
        }

        private static void Highlight(UiButton btn)
        {
            var img = btn != null ? btn.GetComponent<Image>() : null;
            if (img != null) img.color = PickHi;
        }

        private static void Pick(bool left, Action<bool> onPick)
        {
            Close();
            onPick?.Invoke(left);
        }

        internal static void Close()
        {
            if (_panel != null) { UnityEngine.Object.Destroy(_panel); _panel = null; }
        }
    }
}
