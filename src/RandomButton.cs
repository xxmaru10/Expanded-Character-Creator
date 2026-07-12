using System.Collections.Generic;
using SlickUi;
using UnityEngine;
using UnityUtils;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// P15 — injects the randomize buttons, stacked BELOW "Resetar câmera" (so they no longer overlap
    /// the import buttons). Both roll ONLY imported (custom) parts:
    ///   • "Aleatório"    → opens the <see cref="RandomPanel"/> (per-category locks).
    ///   • "Aleatorizar"  → a direct one-click roll (<see cref="Randomizer.Randomize"/>).
    /// Cloned from an engine button like the others.
    /// </summary>
    internal static class RandomButton
    {
        private static readonly List<UiButton> _buttons = new List<UiButton>();

        internal static void Ensure(CharacterCreator creator)
        {
            if (_buttons.Count > 0) return;

            UiButton template = creator.createNew;
            if (template == null || template.transform == null)
            {
                Plugin.Log.LogWarning("Nao achei um botao modelo (createNew) para clonar (Aleatório).");
                return;
            }

            // Stacked below "Resetar câmera" (-188). One row = 34px. Both roll ONLY imported (custom)
            // parts: "Aleatório" opens the lock panel, "Aleatorizar" is a direct one-click roll.
            Make(template, "Aleatório", -224f, "RandomButton", OnOpenPanel);
            Make(template, "Aleatorizar", -258f, "RandomCustomButton", Randomizer.Randomize);

            Plugin.Log.LogInfo("Botoes de aleatorizar injetados no criador (P15).");
        }

        private static void Make(UiButton template, string label, float y, string name, System.Action onClick)
        {
            var btn = UiFactory.TextButton(template.gameObject, template.transform.parent, label, _ => onClick());
            if (btn == null)
            {
                Plugin.Log.LogWarning("Falha ao criar o botao '" + label + "' (clone sem UiButton).");
                return;
            }
            btn.gameObject.name = name;

            var rt = btn.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 170f), Mathf.Max(rt.sizeDelta.y, 34f));
                rt.anchoredPosition += new Vector2(-230f, y);
            }

            LocButtons.Register(btn, label); // keep it localized on runtime language change
            _buttons.Add(btn);
        }

        private static void OnOpenPanel()
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
