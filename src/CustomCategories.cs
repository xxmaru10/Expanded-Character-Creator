using RpgEngine.Characters;
using System;
using UnityEngine;
using SlickUi;

namespace CustomPartsMod
{
    /// <summary>
    /// Declares the mod's synthetic sub-tab category buttons and ensures they exist in the creator.
    /// Currently just <b>Olhos</b> — a sub-tab inside the Head category's tab row (eyes live on the head).
    /// The <b>Sapato</b> category is handled separately by <see cref="ShoeButton"/> (a standalone button
    /// shown only in the feet context), because the native top-level category row is a script-driven
    /// <c>BasicTabSystem</c> that can't be cloned into safely. "Pés" is the native Feet category itself.
    /// </summary>
    internal static class CustomCategories
    {
        private static readonly CategoryTabButton Eyes = new CategoryTabButton(
            "EyesCategoryButton", EyesCategory.Path, EyeIcon.Get,
            "Categoria Olhos. Importe um olho ou escolha um da lista.", "Head/TabHeaders");

        private static readonly CategoryTabButton Brincos = new CategoryTabButton(
            "BrincosCategoryButton", new[] { "CustomCharacters", "RiggedBodyParts", "extras", "attachments", "brincos" }, EarringIcon.Get,
            "Brincos. Escolha um brinco da lista.", "Head/TabHeaders");

        private static readonly CategoryTabButton Colares = new CategoryTabButton(
            "ColaresCategoryButton", new[] { "CustomCharacters", "RiggedBodyParts", "extras", "attachments", "colares" }, NecklaceIcon.Get,
            "Colares. Escolha um colar da lista.", "Head/TabHeaders");

        internal static void EnsureAll(CharacterCreator creator)
        {
            // Diagnostics to identify the exact path of the Accessories tab row
            try
            {
                var canvas = creator.createNew.GetComponentInParent<Canvas>();
                var root = canvas != null && canvas.rootCanvas != null ? canvas.rootCanvas.transform : creator.transform;
                foreach (var b in root.GetComponentsInChildren<UiButton>(true))
                {
                    var ev = b.onLeftMouseClick;
                    if (ev == null) continue;
                    for (int i = 0; i < ev.GetPersistentEventCount(); i++)
                    {
                        if (ev.GetPersistentTarget(i) == creator.itemTabsLoader)
                        {
                            string m = ev.GetPersistentMethodName(i);
                            if (!string.IsNullOrEmpty(m) && m.IndexOf("Path", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                Plugin.Log.LogInfo($"[cat-diag] {b.gameObject.name} em: {CategoryTabButton.FullPath(b.transform)}");
                            }
                        }
                    }
                }
            }
            catch {}

            Eyes.Ensure(creator);
            Brincos.Ensure(creator);
            Colares.Ensure(creator);
        }
    }
}
