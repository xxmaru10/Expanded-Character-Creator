using System;
using System.Collections.Generic;
using System.Text;
using SlickUi;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using RpgEngine;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// Injects a navigable custom-category button (Olhos / Pés / Sapato) INSIDE the creator panel, beside
    /// the native part-category icons. Generalized from the original one-off "Olhos" injector so several
    /// synthetic categories share the exact same clone-and-wire logic and coordinate their "selected"
    /// underline: at most one custom button OR one native tab looks selected at a time.
    ///
    /// The creator's category buttons are scene-wired (their click carries a persistent call to
    /// <c>itemTabsLoader.SetRootPath/SetPathFilter</c>). Several exist — some on the clickable MANNEQUIN,
    /// some in the PANEL row — so we anchor to the one whose transform is closest (deepest shared ancestor)
    /// to the item list (<c>itemTabsLoader.tabSystem</c>): the panel row, not the mannequin. We clone it (to
    /// inherit the exact size/style/white-icon slot), strip its navigation, drop in our white icon, reparent
    /// it as a sibling, and wire it to point the item list at the synthetic category.
    /// </summary>
    internal sealed class CategoryTabButton
    {
        // Every custom category button, so clicking one can clear the others' underlines (and a native
        // tab click can clear all of them). Populated as buttons are constructed.
        private static readonly List<CategoryTabButton> Instances = new List<CategoryTabButton>();

        // The native head sub-tabs sharing our row; their triggers clear our underlines. Re-discovered when
        // the creator is reopened (the cached modules become destroyed objects — see IntegrateSelection).
        private static readonly List<SimpleTabSystem.Module> Natives = new List<SimpleTabSystem.Module>();

        private readonly string _goName;
        private readonly string[] _path;
        private readonly Func<Sprite> _iconFactory;
        private readonly string _successMsg;

        private UiButton _button;
        private GameObject _indicator; // our own "selected" underline (clone of the anchor's)

        internal CategoryTabButton(string goName, string[] path, Func<Sprite> iconFactory, string successMsg)
        {
            _goName = goName;
            _path = path;
            _iconFactory = iconFactory;
            _successMsg = successMsg;
            Instances.Add(this);
        }

        /// <summary>Clone the category button closest to the item list — the panel sub-tab row
        /// (All/Face/Race/Hair) — and integrate our underline with that row's selection.</summary>
        internal void Ensure(CharacterCreator creator)
        {
            if (_button != null) return; // Unity fake-null rebuilds it if the clone was destroyed
            if (creator == null || creator.itemTabsLoader == null || creator.createNew == null) return;

            Transform reference = creator.itemTabsLoader.tabSystem != null
                ? creator.itemTabsLoader.tabSystem.transform
                : creator.itemTabsLoader.transform;

            UiButton anchor = PickClosest(FindCategoryButtons(creator), reference);
            if (anchor == null)
            {
                Plugin.Log.LogWarning($"[cat:{_goName}] nenhum botao de categoria (Set*Path*) encontrado para ancorar.");
                return;
            }

            var go = UnityEngine.Object.Instantiate(anchor.gameObject, anchor.transform.parent);
            go.name = _goName;
            go.transform.SetAsLastSibling();

            _button = go.GetComponent<UiButton>();
            if (_button == null) { UnityEngine.Object.Destroy(go); return; }

            NeutralizeClick(_button);                          // drop the cloned navigation
            _button.onLeftMouseClick.AddListener(_ => Open(creator));
            SetIcon(go, _iconFactory());
            IntegrateSelection(creator, go, anchor);           // fix the "two selected" look

            Plugin.Log.LogInfo($"[cat:{_goName}] ancorado em: {FullPath(go.transform)}");
        }

        private void Open(CharacterCreator creator)
        {
            if (creator == null || creator.itemTabsLoader == null) return;
            creator.itemTabsLoader.SetPathFilter(_path); // shows only this category's parts
            Select();
            Compat.ShowSuccess(_successMsg);
        }

        /// <summary>Light our underline; clear every other custom button's and every native tab's.</summary>
        private void Select()
        {
            if (_indicator != null) _indicator.SetActive(true);
            foreach (var inst in Instances)
                if (inst != this && inst._indicator != null) inst._indicator.SetActive(false);
            foreach (var m in Natives) m.indicator?.SetActive(false);
        }

        private static void ClearAllCustomIndicators()
        {
            foreach (var inst in Instances)
                if (inst._indicator != null) inst._indicator.SetActive(false);
        }

        /// <summary>
        /// Part-category buttons = UiButtons whose click is scene-wired to <c>itemTabsLoader.Set*Path*</c>.
        /// (Gender/search also target itemTabsLoader but via FilterExcludeByTags/etc., so the "Path" method
        /// filter excludes them.) Our own clones are excluded — their navigation was neutralized but the
        /// (now-disabled) persistent listeners still count, so match them by name instead.
        /// </summary>
        private static List<UiButton> FindCategoryButtons(CharacterCreator creator)
        {
            var list = new List<UiButton>();
            Transform root = RootUnder(creator);
            foreach (var b in root.GetComponentsInChildren<UiButton>(true))
            {
                if (b == null || IsOurs(b.gameObject)) continue;
                if (IsCategoryButton(b, creator.itemTabsLoader)) list.Add(b);
            }
            return list;
        }

        private static bool IsOurs(GameObject go)
        {
            foreach (var inst in Instances)
                if (go.name == inst._goName) return true;
            return false;
        }

        /// <summary>Pick the candidate in the SAME panel as the item list: the one sharing the deepest
        /// common ancestor with <paramref name="reference"/> (tabSystem). Ties prefer a visible button.</summary>
        private static UiButton PickClosest(List<UiButton> candidates, Transform reference)
        {
            UiButton best = null;
            int bestDepth = -1;
            var refChain = Ancestors(reference);
            foreach (var b in candidates)
            {
                int depth = SharedDepth(refChain, Ancestors(b.transform));
                bool better = depth > bestDepth
                    || (depth == bestDepth && best != null && !best.gameObject.activeInHierarchy && b.gameObject.activeInHierarchy);
                if (better) { best = b; bestDepth = depth; }
            }
            return best;
        }

        private static Transform RootUnder(CharacterCreator creator)
        {
            var canvas = creator.createNew.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.rootCanvas != null) return canvas.rootCanvas.transform;
            return creator.transform;
        }

        private static bool IsCategoryButton(UiButton b, UnityEngine.Object itemLoader)
        {
            var ev = b.onLeftMouseClick;
            if (ev == null) return false;
            for (int i = 0; i < ev.GetPersistentEventCount(); i++)
            {
                if (ev.GetPersistentTarget(i) != itemLoader) continue;
                string m = ev.GetPersistentMethodName(i);
                if (!string.IsNullOrEmpty(m) && m.IndexOf("Path", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static void NeutralizeClick(UiButton b)
        {
            var ev = b.onLeftMouseClick;
            try
            {
                for (int i = 0; i < ev.GetPersistentEventCount(); i++)
                    ev.SetPersistentListenerState(i, UnityEventCallState.Off);
            }
            catch { }
            ev.RemoveAllListeners();
        }

        /// <summary>Replace the cloned button's icon with our white glyph.</summary>
        private static void SetIcon(GameObject go, Sprite icon)
        {
            var tabsButton = go.GetComponent<BuildTabsButton>();
            if (tabsButton != null && tabsButton.icon != null && tabsButton.icon.image != null)
            {
                tabsButton.icon.SetActive(true);
                tabsButton.icon.image.sprite = icon;
                tabsButton.icon.image.color = Color.white;
                tabsButton.text?.SetActive(false);
                return;
            }

            Image target = null;
            foreach (var img in go.GetComponentsInChildren<Image>(true))
            {
                if (img.gameObject == go) continue; // skip the button's own background
                if (img.gameObject.name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0) { target = img; break; }
                if (target == null && img.sprite != null) target = img;
            }
            if (target == null) target = go.GetComponent<Image>();
            if (target != null)
            {
                target.enabled = true;
                target.sprite = icon;
                target.color = Color.white;
            }
        }

        /// <summary>
        /// The head sub-tabs (All/Face/Race/Hair) are modules of a <see cref="SimpleTabSystem"/>; the
        /// "selected" look is each module's <c>indicator</c> (the underline). Our clone isn't a module, so
        /// without this both the last native tab AND the clone look selected. We only manage the underline
        /// (never touch module content, so items are never hidden): our own underline = the clone's copy of
        /// the anchor module's indicator; clicking any native tab clears all custom underlines (wired once).
        /// </summary>
        private void IntegrateSelection(CharacterCreator creator, GameObject go, UiButton anchor)
        {
            try
            {
                foreach (var bts in RootUnder(creator).GetComponentsInChildren<BasicTabSystem>(true))
                {
                    var sys = bts.tabSystem;
                    if (sys == null || sys._modules == null) continue;

                    var rowNatives = new List<SimpleTabSystem.Module>();
                    SimpleTabSystem.Module anchorModule = null;
                    foreach (var m in sys._modules)
                    {
                        if (m == null || m.trigger == null) continue;
                        if (m.trigger.transform.parent != go.transform.parent) continue; // our row only
                        rowNatives.Add(m);
                        if (m.trigger.gameObject == anchor.gameObject) anchorModule = m;
                    }
                    if (rowNatives.Count == 0) continue; // not the system that owns our row

                    // Our own underline = the clone's copy of the anchor module's indicator (if it was a child).
                    if (anchorModule != null && anchorModule.indicator != null)
                    {
                        string rel = RelPath(anchor.transform, anchorModule.indicator.transform);
                        var t = rel != null ? go.transform.Find(rel) : null;
                        if (t != null) _indicator = t.gameObject;
                    }
                    if (_indicator != null) _indicator.SetActive(false); // start deselected

                    // Wire the native tabs to clear every custom underline. Do it once per creator session:
                    // the first custom button to integrate wires them; the others see the same live triggers
                    // and skip. After a close/reopen the cached trigger is a destroyed object (Unity == is
                    // false vs the new one), so we re-discover and re-wire against the fresh tabs.
                    bool sameSession = Natives.Count > 0 && Natives[0].trigger == rowNatives[0].trigger;
                    if (!sameSession)
                    {
                        Natives.Clear();
                        Natives.AddRange(rowNatives);
                        foreach (var m in rowNatives)
                        {
                            var trig = m.trigger;
                            if (trig != null) trig.onLeftMouseClick.AddListener(_ => ClearAllCustomIndicators());
                        }
                    }

                    Plugin.Log.LogInfo($"[cat:{_goName}] selecao integrada ({rowNatives.Count} abas irmas; underline {(_indicator != null ? "ok" : "nao encontrado")}).");
                    return;
                }
                Plugin.Log.LogWarning($"[cat:{_goName}] SimpleTabSystem das abas de cabeca nao encontrado; selecao nao integrada.");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[cat:{_goName}] IntegrateSelection: " + e.Message); }
        }

        /// <summary>Slash-path of <paramref name="target"/> relative to <paramref name="root"/>, or null
        /// when target is not a descendant of root.</summary>
        private static string RelPath(Transform root, Transform target)
        {
            if (target == root) return "";
            var parts = new List<string>();
            for (var t = target; t != null; t = t.parent)
            {
                if (t == root) { parts.Reverse(); return string.Join("/", parts); }
                parts.Add(t.name);
            }
            return null;
        }

        // ---- transform helpers ----

        private static List<Transform> Ancestors(Transform t)
        {
            var chain = new List<Transform>();
            for (var p = t; p != null; p = p.parent) chain.Add(p);
            chain.Reverse(); // root-first
            return chain;
        }

        private static int SharedDepth(List<Transform> a, List<Transform> b)
        {
            int i = 0;
            while (i < a.Count && i < b.Count && a[i] == b[i]) i++;
            return i;
        }

        private static string FullPath(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
