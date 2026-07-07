using SlickUi;
using UnityEngine;
using UnityEngine.UI;
using UnityUtils;
using RpgEngine;            // BuildTabsButton
using RpgEngine.Characters; // CharacterCreator

namespace CustomPartsMod
{
    /// <summary>
    /// P6 — adds a small "E" (edit) button next to the trash button on CUSTOM part item buttons,
    /// which reopens the scale/position panel for that part. Pool-safe, mirroring
    /// <see cref="TrashButton"/>: reused/rewired for custom ids, hidden when recycled for a native.
    /// </summary>
    internal static class EditButton
    {
        private const string Name = "CustomEditBtn";

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

                btn = UiFactory.TextButton(creator.createNew.gameObject, button.transform, "E", null);
                if (btn == null) return;
                btn.gameObject.name = Name;

                var rt = btn.GetComponent<RectTransform>();
                if (rt != null)
                {
                    float s = ItemBadge.Size(button); // match the native favourite/hide badge size
                    rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(1f, 1f);
                    rt.sizeDelta = new Vector2(s, s);
                    rt.anchoredPosition = new Vector2(-(s + 2f), -2f); // just left of the trash "X"
                }
                var img = btn.GetComponent<Image>();
                if (img != null) img.color = new Color(0.18f, 0.42f, 0.62f, 0.98f);
            }
            if (btn == null) return;

            btn.transform.SetAsLastSibling(); // keep it on top of the item
            btn.onLeftMouseClick.RemoveAllListeners();
            btn.onLeftMouseClick.AddListener(_ => PartEditor.Reopen(id));
        }
    }
}
