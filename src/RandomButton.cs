using SlickUi;
using UnityEngine;
using UnityUtils;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// P15 — injects a single "Aleatório" text button (stacked with the import buttons) that opens the
    /// <see cref="RandomPanel"/>. Cloned from an engine button like the others.
    /// </summary>
    internal static class RandomButton
    {
        private static UiButton _button;

        internal static void Ensure(CharacterCreator creator)
        {
            if (_button != null) return;

            UiButton template = creator.createNew;
            if (template == null || template.transform == null)
            {
                Plugin.Log.LogWarning("Nao achei um botao modelo (createNew) para clonar (Aleatório).");
                return;
            }

            _button = UiFactory.TextButton(
                template.gameObject, template.transform.parent,
                "Aleatório", _ => OnClick());

            if (_button == null)
            {
                Plugin.Log.LogWarning("Falha ao criar o botao Aleatório (clone sem UiButton).");
                return;
            }
            _button.gameObject.name = "RandomButton";

            // Same size as the other import buttons, stacked below "Importar Pasta".
            var rt = _button.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 170f), Mathf.Max(rt.sizeDelta.y, 34f));
                rt.anchoredPosition += new Vector2(-230f, -116f);
            }

            LocButtons.Register(_button, "Aleatório"); // keep it localized on runtime language change
            Plugin.Log.LogInfo("Botao 'Aleatório' injetado no criador (P15).");
        }

        private static void OnClick()
        {
            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator == null) { Compat.ShowError("Criador indisponível."); return; }

            var canvas = creator.createNew != null ? creator.createNew.GetComponentInParent<Canvas>() : null;
            Transform canvasT = canvas != null ? canvas.rootCanvas.transform : creator.transform;
            GameObject buttonTemplate = creator.createNew != null ? creator.createNew.gameObject : null;

            RandomPanel.Open(buttonTemplate, canvasT);
        }
    }
}
