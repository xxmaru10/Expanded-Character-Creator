using System.Collections.Generic;
using SlickUi;
using SimpleFileBrowser;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityUtils;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// P13 — top-center bar of texture-variant controls for the currently selected custom part.
    /// Appears as soon as a custom part is applied (SpawnAlongside). Up to 5 variants render as
    /// numbered boxes (click a box to switch, "+" to add). With MORE than 5 (e.g. a head baked under
    /// every skin tone) it collapses to a compact spinner "&lt; i / N &gt;" whose arrows step through the
    /// variants, keeping the bar small. Changes persist immediately onto the model's saved record.
    /// </summary>
    internal class VariantBar : MonoBehaviour
    {
        private static VariantBar _current;

        private CustomBodyPartAttachment _attachment;
        private RectTransform _rt;
        private GameObject _buttonTemplate;
        private readonly List<UiButton> _boxes = new List<UiButton>();

        private const float MaxBarWidth = 1000f;
        private const float Gap = 4f;
        private const float MinBox = 16f;
        private const float MaxBox = 46f;
        private const float BoxHeight = 34f;

        // Above this many variants the per-box UI gets unwieldy, so switch to the arrow spinner.
        private const int SpinnerThreshold = 5;

        private static readonly Color BoxActive = new Color(0.20f, 0.55f, 0.28f, 1f);
        private static readonly Color BoxFilled = new Color(0.16f, 0.17f, 0.20f, 0.98f);
        private static readonly Color BoxAdd = new Color(0.15f, 0.30f, 0.42f, 1f);
        private static readonly Color BoxDelete = new Color(0.62f, 0.16f, 0.16f, 1f); // the "x" delete button

        private const float DelSize = 15f; // corner "x" button size (boxes mode)

        /// <summary>The custom part currently selected/shown (what the paint brush should target).
        /// Null when no custom part is active.</summary>
        internal static CustomBodyPartAttachment CurrentAttachment =>
            _current != null ? _current._attachment : null;

        /// <summary>Register a freshly PAINTED texture (already written to <paramref name="pngPath"/>)
        /// as a NEW variant on the current part, make it active, and refresh the bar. Mirrors the
        /// file-picker path (OnPicked) but the source is the brush canvas, not a chosen file — so the
        /// original texture stays untouched and the paint becomes just another selectable option.</summary>
        internal static void AddPaintedVariant(string pngPath, Texture2D tex)
        {
            if (_current == null) return;
            var att = _current._attachment;
            var part = att != null ? att.Part : null;
            if (part == null || string.IsNullOrEmpty(pngPath) || tex == null) return;

            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator != null && creator.dummy != null && !string.IsNullOrEmpty(part.LinkGroupId))
            {
                foreach (var kv in creator.dummy.attachedItems)
                {
                    if (kv.Value is CustomBodyPartAttachment c && c.Part != null && c.Part.LinkGroupId == part.LinkGroupId)
                    {
                        c.Part.TextureVariants.Add(pngPath);
                        c.Part.ActiveVariant = c.Part.TextureVariants.Count - 1;
                        c.Part.TexturePath = pngPath;
                        c.Part.Texture = tex;
                        c.SetTexture(tex);
                        ScaleStore.TryUpdateVariants(c.Part.SourceKey, c.Part.TextureVariants.ToArray(), c.Part.ActiveVariant, c.Part.TexturePath);
                    }
                }
            }
            else
            {
                part.TextureVariants.Add(pngPath);
                part.ActiveVariant = part.TextureVariants.Count - 1;
                part.TexturePath = pngPath;
                part.Texture = tex;
                att.SetTexture(tex);
            }

            SyncCounterpart(part, part.ActiveVariant, part.TexturePath, tex, part.TextureVariants);

            _current.Persist();
            _current.Rebuild();
        }

        /// <summary>Show (or refresh) the bar for a freshly selected custom part.</summary>
        internal static void ShowFor(CustomBodyPartAttachment attachment)
        {
            if (attachment == null || attachment.Part == null) { Hide(); return; }
            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator == null || creator.createNew == null) return;

            if (_current == null)
            {
                var canvas = creator.createNew.GetComponentInParent<Canvas>();
                Transform canvasT = canvas != null ? canvas.rootCanvas.transform : creator.transform;

                var go = new GameObject("CustomVariantBar", typeof(RectTransform));
                go.transform.SetParent(canvasT, worldPositionStays: false);
                _current = go.AddComponent<VariantBar>();
                _current._rt = go.GetComponent<RectTransform>();
                _current._rt.anchorMin = _current._rt.anchorMax = new Vector2(0.5f, 1f);
                _current._rt.pivot = new Vector2(0.5f, 1f);
                _current._rt.anchoredPosition = new Vector2(0f, -8f); // just under the top edge, centered
            }

            _current._buttonTemplate = creator.createNew.gameObject;
            _current._attachment = attachment;
            _current.transform.SetAsLastSibling();
            _current.Rebuild();
        }

        internal static void Hide()
        {
            if (_current != null) { Destroy(_current.gameObject); _current = null; }
        }

        private void OnDisable()
        {
            Hide();
        }

        private void Rebuild()
        {
            foreach (var b in _boxes) if (b != null) Destroy(b.gameObject);
            _boxes.Clear();

            var part = _attachment != null ? _attachment.Part : null;
            if (part == null) { Hide(); return; }

            int count = part.TextureVariants.Count;
            if (count > SpinnerThreshold) RebuildSpinner(part, count);
            else RebuildBoxes(part, count);
        }

        // Up to SpinnerThreshold variants: one numbered box each, plus the "+" box.
        private void RebuildBoxes(CustomPart part, int count)
        {
            int total = count + 1; // + the "add" box

            // Shrink boxes so all of them (however many) fit within the max bar width.
            float boxW = Mathf.Clamp(MaxBarWidth / total - Gap, MinBox, MaxBox);
            float barW = total * (boxW + Gap) - Gap;
            _rt.sizeDelta = new Vector2(barW, BoxHeight);

            for (int i = 0; i < total; i++)
            {
                int idx = i;
                bool isAdd = i == count;
                string label = isAdd ? "+" : (i + 1).ToString();

                var btn = UiFactory.TextButton(_buttonTemplate, transform, label, _ => OnBox(idx));
                if (btn == null) continue;

                var brt = btn.GetComponent<RectTransform>();
                brt.anchorMin = brt.anchorMax = new Vector2(0f, 0.5f);
                brt.pivot = new Vector2(0f, 0.5f);
                brt.sizeDelta = new Vector2(boxW, BoxHeight);
                brt.anchoredPosition = new Vector2(i * (boxW + Gap), 0f);

                var img = btn.GetComponent<Image>();
                if (img != null) img.color = isAdd ? BoxAdd : (i == part.ActiveVariant ? BoxActive : BoxFilled);

                _boxes.Add(btn);

                // A small "x" at the box's top-right corner deletes that texture variant.
                if (!isAdd) AddDeleteX(idx, i * (boxW + Gap), boxW);
            }
        }

        // Small red "x" pinned to a box's top-right corner; click removes that variant.
        private void AddDeleteX(int idx, float boxX, float boxW)
        {
            var x = UiFactory.TextButton(_buttonTemplate, transform, "x", _ => DeleteVariant(idx));
            if (x == null) return;

            var xrt = x.GetComponent<RectTransform>();
            xrt.anchorMin = xrt.anchorMax = new Vector2(0f, 0.5f);
            xrt.pivot = new Vector2(0f, 0.5f);
            xrt.sizeDelta = new Vector2(DelSize, DelSize);
            xrt.anchoredPosition = new Vector2(boxX + boxW - DelSize, BoxHeight / 2f - DelSize / 2f);

            var img = x.GetComponent<Image>();
            if (img != null) img.color = BoxDelete;
            x.transform.SetAsLastSibling(); // sit on top of the number box
            _boxes.Add(x);
        }

        // More than SpinnerThreshold variants: a compact "< i / N >" stepper (+ the "add" box), so a
        // model baked under dozens of skin tones doesn't spray dozens of boxes across the screen.
        private void RebuildSpinner(CustomPart part, int count)
        {
            const float ArrowW = 34f, NumW = 90f, AddW = 34f, DelW = 34f;
            float barW = ArrowW + Gap + NumW + Gap + ArrowW + Gap + AddW + Gap + DelW;
            _rt.sizeDelta = new Vector2(barW, BoxHeight);

            int shown = Mathf.Clamp(part.ActiveVariant, 0, count - 1) + 1;

            float x = 0f;
            x = AddSpinnerBox("<", x, ArrowW, BoxFilled, _ => Step(-1));
            x = AddSpinnerBox(shown + " / " + count, x, NumW, BoxActive, _ => Step(1)); // click number = next
            x = AddSpinnerBox(">", x, ArrowW, BoxFilled, _ => Step(1));
            x = AddSpinnerBox("+", x, AddW, BoxAdd, _ => AddVariant());
            // "x" deletes the CURRENTLY-SHOWN variant (the one whose number is displayed).
            AddSpinnerBox("x", x, DelW, BoxDelete, _ => DeleteVariant(CurrentActive()));
        }

        private int CurrentActive()
        {
            var part = _attachment != null ? _attachment.Part : null;
            return part == null ? -1 : part.ActiveVariant;
        }

        // Remove a texture variant from the part (list-only; the PNG file on disk is kept). Keeps at
        // least one variant, fixes the active index, re-applies the now-active texture, and persists.
        private void DeleteVariant(int i)
        {
            var part = _attachment != null ? _attachment.Part : null;
            if (part == null) return;
            if (i < 0 || i >= part.TextureVariants.Count) return;
            if (part.TextureVariants.Count <= 1)
            {
                Compat.ShowError("Precisa manter ao menos uma textura.");
                return;
            }

            part.TextureVariants.RemoveAt(i);
            if (part.ActiveVariant == i) part.ActiveVariant = Mathf.Clamp(i, 0, part.TextureVariants.Count - 1);
            else if (part.ActiveVariant > i) part.ActiveVariant--;

            string path = part.TextureVariants[part.ActiveVariant];
            var tex = TextureLoader.LoadFromFile(path);
            if (tex != null) { part.TexturePath = path; _attachment.SetTexture(tex); }

            Persist();
            SyncCounterpart(part, part.ActiveVariant, part.TexturePath, tex, part.TextureVariants);
            Rebuild();
        }

        // Creates one positioned spinner control and returns the x for the next one.
        private float AddSpinnerBox(string label, float x, float w, Color color, UnityAction<PointerEventData> onClick)
        {
            var btn = UiFactory.TextButton(_buttonTemplate, transform, label, onClick);
            if (btn == null) return x + w + Gap;

            var brt = btn.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 0.5f);
            brt.pivot = new Vector2(0f, 0.5f);
            brt.sizeDelta = new Vector2(w, BoxHeight);
            brt.anchoredPosition = new Vector2(x, 0f);

            var img = btn.GetComponent<Image>();
            if (img != null) img.color = color;

            _boxes.Add(btn);
            return x + w + Gap;
        }

        // Move the active variant by delta, wrapping around, and apply it.
        private void Step(int delta)
        {
            var part = _attachment != null ? _attachment.Part : null;
            if (part == null || part.TextureVariants.Count == 0) return;
            int n = part.TextureVariants.Count;
            int next = ((part.ActiveVariant + delta) % n + n) % n;
            Select(next);
        }

        private void OnBox(int i)
        {
            var part = _attachment != null ? _attachment.Part : null;
            if (part == null) return;
            if (i < part.TextureVariants.Count) Select(i);
            else AddVariant();
        }

        private void Select(int i)
        {
            var part = _attachment.Part;
            string path = part.TextureVariants[i];
            var tex = TextureLoader.LoadFromFile(path);
            if (tex == null) { Compat.ShowError("Nao consegui carregar essa textura."); return; }

            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator != null && creator.dummy != null && !string.IsNullOrEmpty(part.LinkGroupId))
            {
                foreach (var kv in creator.dummy.attachedItems)
                {
                    if (kv.Value is CustomBodyPartAttachment c && c.Part != null && c.Part.LinkGroupId == part.LinkGroupId)
                    {
                        c.Part.ActiveVariant = i;
                        c.Part.TexturePath = path;
                        c.Part.Texture = tex;
                        c.SetTexture(tex);
                        ScaleStore.TryUpdateVariants(c.Part.SourceKey, c.Part.TextureVariants.ToArray(), c.Part.ActiveVariant, c.Part.TexturePath);
                    }
                }
            }
            else
            {
                part.ActiveVariant = i;
                part.TexturePath = path;
                _attachment.SetTexture(tex);
                Persist();
            }

            SyncCounterpart(part, part.ActiveVariant, part.TexturePath, tex, part.TextureVariants);
            Rebuild();
        }

        private void AddVariant()
        {
            FileBrowser.SetFilters(false, new FileBrowser.Filter("Imagem", ".png", ".jpg", ".jpeg"));
            FileBrowser.ShowLoadDialog(OnPicked, null, FileBrowser.PickMode.Files,
                allowMultiSelection: false, initialPath: null, initialFilename: null,
                title: "Adicionar textura (PNG/JPG)", loadButtonText: "Adicionar");
        }

        private void OnPicked(string[] paths)
        {
            var part = _attachment != null ? _attachment.Part : null;
            if (part == null || paths == null || paths.Length == 0) return;
            var tex = TextureLoader.LoadFromFile(paths[0]);
            if (tex == null) { Compat.ShowError("Nao consegui carregar essa imagem."); return; }

            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator != null && creator.dummy != null && !string.IsNullOrEmpty(part.LinkGroupId))
            {
                foreach (var kv in creator.dummy.attachedItems)
                {
                    if (kv.Value is CustomBodyPartAttachment c && c.Part != null && c.Part.LinkGroupId == part.LinkGroupId)
                    {
                        if (!c.Part.TextureVariants.Contains(paths[0])) c.Part.TextureVariants.Add(paths[0]);
                        c.Part.ActiveVariant = c.Part.TextureVariants.Count - 1;
                        c.Part.TexturePath = paths[0];
                        c.Part.Texture = tex;
                        c.SetTexture(tex);
                        ScaleStore.TryUpdateVariants(c.Part.SourceKey, c.Part.TextureVariants.ToArray(), c.Part.ActiveVariant, c.Part.TexturePath);
                    }
                }
            }
            else
            {
                part.TextureVariants.Add(paths[0]);
                part.ActiveVariant = part.TextureVariants.Count - 1;
                part.TexturePath = paths[0];
                _attachment.SetTexture(tex);
                Persist();
            }

            SyncCounterpart(part, part.ActiveVariant, part.TexturePath, tex, part.TextureVariants);
            Rebuild();
        }

        /// <summary>Update the model's saved record variant fields if it has one (imported+persisted).
        /// Un-persisted parts keep the change in memory until "Salvar padrão" in the edit panel.</summary>
        private void Persist()
        {
            var part = _attachment != null ? _attachment.Part : null;
            if (part == null || string.IsNullOrEmpty(part.SourceKey)) return;
            string activePath = part.ActiveVariant >= 0 && part.ActiveVariant < part.TextureVariants.Count
                ? part.TextureVariants[part.ActiveVariant] : null;
            ScaleStore.TryUpdateVariants(part.SourceKey, part.TextureVariants.ToArray(), part.ActiveVariant, activePath);
        }

        private static bool IsCounterpart(string nameA, string nameB)
        {
            if (string.IsNullOrEmpty(nameA) || string.IsNullOrEmpty(nameB)) return false;
            string normA = NormalizeName(nameA);
            string normB = NormalizeName(nameB);

            if (normA.StartsWith("coxa_direita") && normB.StartsWith("coxa_esquerda"))
                return normA.Substring("coxa_direita".Length) == normB.Substring("coxa_esquerda".Length);
            if (normA.StartsWith("coxa_esquerda") && normB.StartsWith("coxa_direita"))
                return normA.Substring("coxa_esquerda".Length) == normB.Substring("coxa_direita".Length);

            if (normA.StartsWith("panturrilha_direita") && normB.StartsWith("panturrilha_esquerda"))
                return normA.Substring("panturrilha_direita".Length) == normB.Substring("panturrilha_esquerda".Length);
            if (normA.StartsWith("panturrilha_esquerda") && normB.StartsWith("panturrilha_direita"))
                return normA.Substring("panturrilha_esquerda".Length) == normB.Substring("panturrilha_direita".Length);

            return false;
        }

        private static string NormalizeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.ToLowerInvariant()
                    .Replace("á", "a").Replace("ã", "a").Replace("â", "a").Replace("à", "a")
                    .Replace("é", "e").Replace("ê", "e")
                    .Replace("í", "i")
                    .Replace("ó", "o").Replace("õ", "o").Replace("ô", "o")
                    .Replace("ú", "u")
                    .Replace("ç", "c");
        }

        private static void SyncCounterpart(CustomPart part, int activeIdx, string activePath, Texture2D tex, List<string> textureVariants)
        {
            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator == null) return;

            foreach (var p in CustomPartCatalog.AllParts())
            {
                if (p != part && IsCounterpart(part.DisplayName, p.DisplayName))
                {
                    p.TextureVariants = new List<string>(textureVariants);
                    p.ActiveVariant = Mathf.Clamp(activeIdx, 0, p.TextureVariants.Count - 1);
                    p.TexturePath = activePath;
                    p.Texture = tex;

                    if (creator.dummy != null && creator.dummy.attachedItems.TryGetValue(p.PartId, out var attached))
                    {
                        if (attached is CustomBodyPartAttachment counterpartAtt)
                        {
                            counterpartAtt.SetTexture(tex);
                        }
                    }

                    ScaleStore.TryUpdateVariants(p.SourceKey, p.TextureVariants.ToArray(), p.ActiveVariant, p.TexturePath);
                }
            }
        }

        private void Update()
        {
            var creator = UniqueMono<CharacterCreator>.instance;
            // The CharacterCreator is a persistent singleton whose root stays active even on the map;
            // the actual creator UI is toggled via uiWindow. Checking the root left the bar stuck on the
            // map after closing the creator (texture-variant boxes floating over the scene). Track
            // uiWindow so the bar hides the moment the creator window closes.
            bool creatorOpen = creator != null &&
                (creator.uiWindow != null ? creator.uiWindow.activeInHierarchy
                                          : creator.gameObject.activeInHierarchy);
            if (_attachment == null || !creatorOpen)
            {
                Hide();
            }
        }
    }
}
