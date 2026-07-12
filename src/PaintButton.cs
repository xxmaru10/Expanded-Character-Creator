using SlickUi;
using UnityEngine;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// Injects a single "Pincel" text button, stacked below the import/random buttons. Toggling it
    /// starts/stops a <see cref="PaintSession"/> on the currently selected custom part: the user paints
    /// a region with an RGB brush (adjustable size + eraser) and, on confirm, the painted result is
    /// saved as a NEW texture variant (the original is never overwritten — see VariantBar).
    /// Cloned from an engine button like the other injected buttons.
    /// </summary>
    internal static class PaintButton
    {
        private static UiButton _button;

        /// <summary>The injected "Pincel" button's RectTransform, so the paint panel can dock right
        /// below it (null until injected / if the clone failed).</summary>
        internal static RectTransform Rect => _button != null ? _button.GetComponent<RectTransform>() : null;

        internal static void Ensure(CharacterCreator creator)
        {
            if (_button != null) return;

            UiButton template = creator.createNew;
            if (template == null || template.transform == null)
            {
                Plugin.Log.LogWarning("Nao achei um botao modelo (createNew) para clonar (Pincel).");
                return;
            }

            _button = UiFactory.TextButton(
                template.gameObject, template.transform.parent,
                "Pincel", _ => PaintSession.Toggle());

            if (_button == null)
            {
                Plugin.Log.LogWarning("Falha ao criar o botao Pincel (clone sem UiButton).");
                return;
            }
            _button.gameObject.name = "PaintButton";

            // Same size as the other import buttons, stacked one row BELOW "Aleatório" (which is at -116).
            var rt = _button.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 170f), Mathf.Max(rt.sizeDelta.y, 34f));
                rt.anchoredPosition += new Vector2(-230f, -152f);
            }

            LocButtons.Register(_button, "Pincel"); // keep it localized on runtime language change
            Plugin.Log.LogInfo("Botao 'Pincel' injetado no criador.");
        }
    }
}
