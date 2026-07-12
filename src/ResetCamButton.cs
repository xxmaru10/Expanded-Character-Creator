using SlickUi;
using UnityEngine;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// A standalone "Resetar câmera" button (OUTSIDE the paint panel, always visible in the creator),
    /// stacked below "Pincel". Clicking it returns the character-creator camera to its default pose via
    /// <see cref="CameraReset"/> — the working replacement for the engine's empty FocusOnFull stub.
    /// </summary>
    internal static class ResetCamButton
    {
        private static UiButton _button;

        internal static void Ensure(CharacterCreator creator)
        {
            if (_button != null) return;

            UiButton template = creator.createNew;
            if (template == null || template.transform == null)
            {
                Plugin.Log.LogWarning("Nao achei um botao modelo (createNew) para clonar (Resetar câmera).");
                return;
            }

            _button = UiFactory.TextButton(
                template.gameObject, template.transform.parent,
                "Resetar câmera", _ => CameraReset.Reset());

            if (_button == null)
            {
                Plugin.Log.LogWarning("Falha ao criar o botao Resetar câmera (clone sem UiButton).");
                return;
            }
            _button.gameObject.name = "ResetCamButton";

            var rt = _button.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 170f), Mathf.Max(rt.sizeDelta.y, 34f));
                rt.anchoredPosition += new Vector2(-230f, -188f); // one row below "Pincel" (-152)
            }

            LocButtons.Register(_button, "Resetar câmera");
            Plugin.Log.LogInfo("Botao 'Resetar câmera' injetado no criador.");
        }
    }
}
