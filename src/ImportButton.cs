using SlickUi;
using UnityEngine;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// Injects a single "Import Part" text button into the character creator, cloned from an
    /// existing engine button so it inherits click wiring/style. Created once, when the
    /// creator opens (Unity fake-null rebuilds it if the previous clone was destroyed).
    /// </summary>
    internal static class ImportButton
    {
        private static UiButton _button;

        internal static void Ensure(CharacterCreator creator)
        {
            if (_button != null) return;

            UiButton template = creator.createNew;
            if (template == null || template.transform == null)
            {
                Plugin.Log.LogWarning("Nao achei um botao modelo (createNew) para clonar.");
                return;
            }

            _button = UiFactory.TextButton(
                template.gameObject, template.transform.parent,
                "Importar Parte", _ => ImportFlow.OnImportClicked());

            if (_button == null)
            {
                Plugin.Log.LogWarning("Falha ao criar o botao de import (clone sem UiButton).");
                return;
            }
            _button.gameObject.name = "ImportPartButton";

            // Widen it so the label fits, and move it off the template button — left (so it isn't
            // clipped at the right screen edge) and down.
            var rt = _button.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 170f), Mathf.Max(rt.sizeDelta.y, 34f));
                rt.anchoredPosition += new Vector2(-230f, -44f);
            }

            LocButtons.Register(_button, "Importar Parte"); // keep it localized on runtime language change
            Plugin.Log.LogInfo("Botao 'Importar Parte' injetado no criador (modo texto).");
        }
    }
}
