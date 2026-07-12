using System.Reflection;
using HarmonyLib;
using SlickUi;
using UnityEngine;
using RpgEngine;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// The "Sapato" (shoe) category entry point. The native top-level category row is a script-driven
    /// <see cref="BasicTabSystem"/> whose buttons stretch to fill the whole strip (cloning into it produced
    /// a giant white overlay), so instead this is a standalone mod button — built the proven way (clone the
    /// creator's <c>createNew</c> text button, like <see cref="ImportButton"/>) — that appears ONLY while the
    /// Feet category is open. Clicking it filters the item list to the synthetic shoe category
    /// (<see cref="ShoeCategory"/>); imports there stack additively on the chosen foot (<see cref="SidedCategory"/>).
    /// </summary>
    internal static class ShoeButton
    {
        private static UiButton _button;

        internal static void Ensure(CharacterCreator creator)
        {
            if (_button != null) return; // Unity fake-null rebuilds it if the previous clone was destroyed

            UiButton template = creator.createNew;
            if (template == null || template.transform == null) return;

            _button = UiFactory.TextButton(
                template.gameObject, template.transform.parent,
                "Sapato (calçado)", _ => Open(creator));
            if (_button == null) return;
            _button.gameObject.name = "ShoeCategoryButton";

            var rt = _button.GetComponent<RectTransform>();
            if (rt != null)
            {
                // Same top-right mod-button column as Importar Parte/Pasta/Aleatório, in the next free slot.
                rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 170f), Mathf.Max(rt.sizeDelta.y, 34f));
                rt.anchoredPosition += new Vector2(-230f, -292f);
            }

            LocButtons.Register(_button, "Sapato (calçado)"); // keep it localized on runtime language change

            Plugin.Log.LogInfo("Botao 'Sapato' injetado.");
        }

        private static void Open(CharacterCreator creator)
        {
            if (creator == null || creator.itemTabsLoader == null) return;
            creator.itemTabsLoader.SetPathFilter(ShoeCategory.Path);
            Compat.ShowSuccess("Categoria Sapato. Importe um sapato (escolha o lado) ou escolha um da lista.");
        }
    }
}
