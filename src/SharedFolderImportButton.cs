using SlickUi;
using UnityEngine;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// Injects "Importar Pasta (texturas compartilhadas)" below "Importar Pasta". One click: pick a folder
    /// (subfolders included); every .obj is auto-routed to its body category and shares its own folder's
    /// textures. See <see cref="SharedFolderImportFlow"/>.
    /// </summary>
    internal static class SharedFolderImportButton
    {
        private static UiButton _button;

        internal static void Ensure(CharacterCreator creator)
        {
            if (_button != null) return;

            UiButton template = creator.createNew;
            if (template == null || template.transform == null)
            {
                Plugin.Log.LogWarning("Nao achei um botao modelo (createNew) para clonar (texturas compartilhadas).");
                return;
            }

            _button = UiFactory.TextButton(
                template.gameObject, template.transform.parent,
                "Importar Pasta (texturas compartilhadas)", _ => SharedFolderImportFlow.OnClicked());

            if (_button == null)
            {
                Plugin.Log.LogWarning("Falha ao criar o botao de import compartilhado (clone sem UiButton).");
                return;
            }
            _button.gameObject.name = "SharedFolderImportButton";

            // Same style, stacked just below "Importar Pasta" (which sits at -78). Wider to fit the label.
            var rt = _button.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 240f), Mathf.Max(rt.sizeDelta.y, 34f));
                rt.anchoredPosition += new Vector2(-230f, -116f);
            }

            LocButtons.Register(_button, "Importar Pasta (texturas compartilhadas)");
            Plugin.Log.LogInfo("Botao 'Importar Pasta (texturas compartilhadas)' injetado.");
        }
    }
}
