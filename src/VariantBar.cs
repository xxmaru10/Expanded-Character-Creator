using System.Collections.Generic;
using SlickUi;
using SimpleFileBrowser;
using UnityEngine;
using UnityEngine.UI;
using UnityUtils;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// P13 — top-center bar of numbered texture-variant boxes for the currently selected custom part.
    /// Appears as soon as a custom part is applied (SpawnAlongside); click a box to switch texture
    /// live, click "+" to add another. Unlimited variants: the boxes shrink to fit a fixed max width.
    /// Changes persist immediately onto the model's saved record when it already has one.
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

        private static readonly Color BoxActive = new Color(0.20f, 0.55f, 0.28f, 1f);
        private static readonly Color BoxFilled = new Color(0.16f, 0.17f, 0.20f, 0.98f);
        private static readonly Color BoxAdd = new Color(0.15f, 0.30f, 0.42f, 1f);

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

        private void Rebuild()
        {
            foreach (var b in _boxes) if (b != null) Destroy(b.gameObject);
            _boxes.Clear();

            var part = _attachment != null ? _attachment.Part : null;
            if (part == null) { Hide(); return; }

            int count = part.TextureVariants.Count;
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
            }
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
            part.ActiveVariant = i;
            part.TexturePath = path;
            _attachment.SetTexture(tex);
            Persist();
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
            part.TextureVariants.Add(paths[0]);
            part.ActiveVariant = part.TextureVariants.Count - 1;
            part.TexturePath = paths[0];
            _attachment.SetTexture(tex);
            Persist();
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

        private void Update()
        {
            // The shown part's attachment object was destroyed (part removed) => close the bar.
            if (_attachment == null) Hide();
        }
    }
}
