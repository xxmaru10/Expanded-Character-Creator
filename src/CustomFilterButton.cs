using SlickUi;
using TMPro;
using UnityEngine;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// P9 — a toggle button ("Só custom: SIM/NÃO") stacked under the import buttons that flips the
    /// custom-only tab filter (<see cref="CustomFilter"/>). Cloned from an engine button like the others.
    /// </summary>
    internal static class CustomFilterButton
    {
        private static UiButton _button;

        /// <summary>The button's own RectTransform, so other injected UI (e.g. TagBar) can anchor
        /// right beside it instead of guessing a fixed offset.</summary>
        internal static RectTransform Rect => _button != null ? _button.GetComponent<RectTransform>() : null;

        internal static void Ensure(CharacterCreator creator)
        {
            if (_button != null) { UpdateLabel(); return; }

            UiButton template = creator.createNew;
            if (template == null || template.transform == null)
            {
                Plugin.Log.LogWarning("Nao achei um botao modelo (createNew) para clonar (Só custom).");
                return;
            }

            // Anchor it ABOVE the whole creator window (CharacterCreator.uiWindow) so it floats just over
            // the top edge, clear of the title bar / close button / search field. It moves with the
            // window. Falls back to the tab list if uiWindow isn't available.
            RectTransform windowRt = creator.uiWindow != null ? creator.uiWindow.transform as RectTransform : null;
            var tabs = creator.itemTabsLoader != null ? creator.itemTabsLoader.tabSystem : null;
            Transform parent = windowRt != null ? (Transform)windowRt
                             : tabs != null ? tabs.transform : template.transform.parent;

            _button = UiFactory.TextButton(
                template.gameObject, parent,
                Label(), _ => { CustomFilter.Toggle(); UpdateLabel(); });

            if (_button == null)
            {
                Plugin.Log.LogWarning("Falha ao criar o botao de filtro (clone sem UiButton).");
                return;
            }
            _button.gameObject.name = "CustomOnlyFilterButton";

            var rt = _button.GetComponent<RectTransform>();
            if (rt != null)
            {
                // uiWindow is a FULL-SCREEN container: anchor to its top-LEFT corner and drop the button
                // into the empty strip ABOVE the item panel (which is docked top-left), aligned with the
                // panel's left edge. Visible, hides with the creator, overlaps nothing.
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(175f, 30f);
                rt.anchoredPosition = new Vector2(18f, -22f);
                _button.transform.SetAsLastSibling();
            }

            LocButtons.Register(UpdateLabel); // re-localizes on runtime language change (label depends on state)
            Plugin.Log.LogInfo("Botao 'Só custom' (P9) posicionado acima do painel de itens.");
        }

        private static string Label() => CustomFilter.CustomOnly ? "Só custom: SIM" : "Só custom: NÃO";

        private static void UpdateLabel()
        {
            if (_button == null) return;
            var txt = _button.GetComponentInChildren<TMP_Text>(true);
            if (txt != null) txt.text = Loc.T(Label());
        }
    }
}
