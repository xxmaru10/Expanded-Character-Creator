using System;
using System.Collections.Generic;
using SlickUi;
using UnityEngine;
using UnityUtils;
using RpgEngine;             // CharacterCreatorCamera
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// P12 — ◀ ▶ arrows that step through the current category's visible options (native AND
    /// custom), applying each one live. Placed flanking the character preview: one on the left
    /// edge, one on the right edge of the preview panel (<c>CharacterCreatorCamera.inputPanel</c>).
    /// Reuses the tab system's own filter predicate (<c>tabSystem.selector</c>) over
    /// <c>CharacterCreator.attachmentPaths</c> so the list and order match what is on screen.
    /// </summary>
    internal static class NavArrows
    {
        private static NavArrowButton _prev, _next;

        internal static void Ensure(CharacterCreator creator)
        {
            if (_prev != null && _next != null) return;
            if (creator == null) return;

            // Anchor to the character preview panel so the arrows flank the model.
            var cam = creator.creatorCam != null ? creator.creatorCam : UniqueMono<CharacterCreatorCamera>.instance;
            UiButton panel = cam != null ? cam.inputPanel : null;
            Transform parent = panel != null ? panel.transform : FallbackCanvas(creator);
            if (parent == null) return;

            _prev = Make(parent, pointRight: false, left: true, () => Step(-1));
            _next = Make(parent, pointRight: true, left: false, () => Step(1));

            // Keep the arrows glued to the model even when the camera shifts (side panel open/close).
            var followGo = new GameObject("NavArrowFollow", typeof(RectTransform));
            followGo.transform.SetParent(parent, worldPositionStays: false);
            var follow = followGo.AddComponent<NavArrowFollow>();
            follow.panel = parent as RectTransform;
            follow.prev = _prev.rectTransform;
            follow.next = _next.rectTransform;
            follow.dx = 140f;

            Plugin.Log.LogInfo("Setas de navegacao (P12) posicionadas ao lado do boneco.");
        }

        private static Transform FallbackCanvas(CharacterCreator creator)
        {
            var canvas = creator.createNew != null ? creator.createNew.GetComponentInParent<Canvas>() : null;
            return canvas != null ? canvas.rootCanvas.transform : creator.transform;
        }

        private static NavArrowButton Make(Transform parent, bool pointRight, bool left, Action onClick)
        {
            var go = new GameObject(left ? "NavArrow_Left" : "NavArrow_Right", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);

            var arrow = go.AddComponent<NavArrowButton>();
            arrow.pointRight = pointRight;
            arrow.color = new Color(1f, 1f, 1f, 0.9f);
            arrow.raycastTarget = true;
            arrow.onClick = onClick;

            var rt = go.GetComponent<RectTransform>();
            // Anchored to the CENTER of the preview (≈ the character) and offset a little to each side,
            // so the two arrows sit right beside the model instead of at the far screen edges.
            const float dx = 140f;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(52f, 72f);
            rt.anchoredPosition = new Vector2(left ? -dx : dx, 0f);
            go.transform.SetAsLastSibling();          // draw over the preview
            return arrow;
        }

        /// <summary>Applies the next (dir=+1) or previous (dir=-1) visible option in the current category.</summary>
        private static void Step(int dir)
        {
            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator == null || creator.itemTabsLoader == null || creator.dummy == null) return;

            var pool = creator.itemTabsLoader.tabSystem;
            Predicate<string> sel = pool != null ? pool.selector : null;
            if (sel == null)
            {
                Compat.ShowError("Abra uma categoria antes de navegar.");
                return;
            }

            // Ordered list of what is currently shown = attachmentPaths keys that pass the filter.
            var list = new List<string>();
            foreach (var key in CharacterCreator.attachmentPaths.Keys)
                if (sel(key)) list.Add(key);
            if (list.Count == 0)
            {
                Compat.ShowError("Nenhuma opcao nesta categoria.");
                return;
            }

            // Current = first shown id that is applied to the preview.
            int cur = -1;
            for (int i = 0; i < list.Count; i++)
                if (creator.dummy.attachedItems.ContainsKey(list[i])) { cur = i; break; }

            int target = cur < 0 ? (dir > 0 ? 0 : list.Count - 1)
                                 : (int)Mathf.Repeat(cur + dir, list.Count);

            string curId = cur >= 0 ? list[cur] : null;
            string targetId = list[target];

            // SpawnAlongside toggles: switch the current off (to keep attachedItems clean) then the
            // target on. If there is only one option, curId == targetId and it stays applied.
            if (!string.IsNullOrEmpty(curId) && curId != targetId && creator.dummy.Contains(curId))
                creator.SpawnAlongside(curId);
            if (!creator.dummy.Contains(targetId))
                creator.SpawnAlongside(targetId);
        }
    }
}
