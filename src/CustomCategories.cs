using RpgEngine.Characters;

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
            "Categoria Olhos. Importe um olho ou escolha um da lista.");

        internal static void EnsureAll(CharacterCreator creator)
        {
            Eyes.Ensure(creator);
        }
    }
}
