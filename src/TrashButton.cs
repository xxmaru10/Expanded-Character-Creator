using SlickUi;
using UnityEngine;
using UnityEngine.UI;
using UnityUtils;
using RpgEngine;            // BuildTabsButton
using RpgEngine.Characters; // CharacterCreator

namespace CustomPartsMod
{
    /// <summary>
    /// Adds a small red "X" (delete) button to the top-right corner of a tab item button, but
    /// only for CUSTOM parts. Pool-safe: the button is reused/rewired for custom ids and hidden
    /// when a pooled item button is recycled for a native part.
    /// </summary>
    internal static class TrashButton
    {
        private const string Name = "CustomTrashBtn";

        internal static void Apply(BuildTabsButton button, string id)
        {
            if (button == null) return;
            Transform existing = button.transform.Find(Name);

            if (!CustomPartCatalog.IsCustom(id))
            {
                if (existing != null) existing.gameObject.SetActive(false);
                return;
            }

            UiButton btn;
            if (existing != null)
            {
                btn = existing.GetComponent<UiButton>();
                existing.gameObject.SetActive(true);
            }
            else
            {
                var creator = UniqueMono<CharacterCreator>.instance;
                if (creator == null || creator.createNew == null) return;

                btn = UiFactory.TextButton(creator.createNew.gameObject, button.transform, "X", null);
                if (btn == null) return;
                btn.gameObject.name = Name;

                var rt = btn.GetComponent<RectTransform>();
                if (rt != null)
                {
                    float s = ItemBadge.Size(button); // match the native favourite/hide badge size
                    rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(1f, 1f);
                    rt.sizeDelta = new Vector2(s, s);
                    rt.anchoredPosition = new Vector2(-2f, -2f);
                }
                var img = btn.GetComponent<Image>();
                if (img != null) img.color = new Color(0.72f, 0.16f, 0.16f, 0.98f);
            }
            if (btn == null) return;

            btn.transform.SetAsLastSibling(); // keep it on top of the item
            btn.onLeftMouseClick.RemoveAllListeners();
            btn.onLeftMouseClick.AddListener(_ => PartsAdmin.Delete(id));
        }
    }
}
