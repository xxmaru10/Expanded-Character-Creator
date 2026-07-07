using SlickUi;
using UnityEngine;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// P1 — injects a single "Importar Pasta" text button next to "Importar Parte", cloned the
    /// same way (inherits click wiring/style from an existing engine button).
    /// </summary>
    internal static class MassImportButton
    {
        private static UiButton _button;

        internal static void Ensure(CharacterCreator creator)
        {
            if (_button != null) return;

            UiButton template = creator.createNew;
            if (template == null || template.transform == null)
            {
                Plugin.Log.LogWarning("Nao achei um botao modelo (createNew) para clonar (Importar Pasta).");
                return;
            }

            _button = UiFactory.TextButton(
                template.gameObject, template.transform.parent,
                "Importar Pasta", _ => MassImportFlow.OnImportFolderClicked());

            if (_button == null)
            {
                Plugin.Log.LogWarning("Falha ao criar o botao de import em massa (clone sem UiButton).");
                return;
            }
            _button.gameObject.name = "MassImportButton";

            // Same size as "Importar Parte", stacked directly below it.
            var rt = _button.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 170f), Mathf.Max(rt.sizeDelta.y, 34f));
                rt.anchoredPosition += new Vector2(-230f, -78f);
            }

            LocButtons.Register(_button, "Importar Pasta"); // keep it localized on runtime language change
            Plugin.Log.LogInfo("Botao 'Importar Pasta' injetado no criador (P1).");
        }
    }
}
