using System;
using System.IO;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityUtils;
using RpgEngine;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// Interactive texture brush for the currently selected custom part (approach "A": paint pixels
    /// directly on the part's albedo). Toggled by <see cref="PaintButton"/>.
    ///
    /// Painting works by raycasting the part's MeshCollider and reading <c>RaycastHit.textureCoord</c>.
    /// CRITICAL: the creator renders the character with its OWN camera (<c>CharacterCreatorCamera.cam</c>)
    /// into a RenderTexture (<c>.preview</c>) shown in a UI RawImage — NOT straight to the screen. So the
    /// ray must come from that camera, using the mouse position mapped into the preview image's rect
    /// (viewport coords), not <c>Camera.main.ScreenPointToRay</c> (which never hits the model — the old
    /// "não pinta" bug).
    ///
    /// "Confirmar" writes the canvas to a PNG under CustomParts\painted\ and registers it as a NEW
    /// texture variant (original untouched — <see cref="VariantBar.AddPaintedVariant"/>); the painted
    /// texture stays applied. "Cancelar" restores the original active texture.
    /// </summary>
    internal class PaintSession : MonoBehaviour
    {
        private static PaintSession _current;
        internal static bool Active => _current != null;

        private CustomBodyPartAttachment _attachment;
        private System.Collections.Generic.List<MeshCollider> _colliders = new System.Collections.Generic.List<MeshCollider>();
        private CharacterCreatorCamera _cc;   // creator camera (renders the model into .preview)
        private RawImage _previewImage;       // UI element showing the RenderTexture

        private Texture2D _originalTex;
        private Texture2D _canvas;
        private Color32[] _pixels;
        private Color32[] _basePixels;
        private int _texW, _texH;
        private bool _dirty;
        private bool _handedOff;

        private Color _color = new Color(0f, 0f, 0f, 1f);
        private int _brushRadius = 8;
        private float _opacity = 1f;   // 0..1 — how strongly each stamp blends toward the brush colour
        private bool _erasing;

        // ---- RGB colour picker state ----
        // HSV is the picker's MASTER value (so hue survives at black/white where RGB would lose it);
        // _color is always kept in sync from it. _hexBuf backs the editable hex TextField.
        private float _h, _s, _v;
        private string _hexBuf = "000000";
        private bool _picking;          // eyedropper armed: next model click samples a colour, not paints
        private bool _cursorArmed;      // the OS cursor is currently the eyedropper icon
        private Texture2D _svTex;       // saturation/value square for the current hue (rebuilt on hue change)
        private float _svTexHue = -1f;
        private static Texture2D _hueTex;

        private static readonly Color[] _presets =
        {
            new Color(0f, 0f, 0f),       new Color(1f, 1f, 1f),       new Color(0.5f, 0.5f, 0.5f),
            new Color(0.86f, 0.20f, 0.18f), new Color(0.95f, 0.61f, 0.07f), new Color(0.95f, 0.85f, 0.15f),
            new Color(0.30f, 0.69f, 0.31f), new Color(0.20f, 0.60f, 0.86f), new Color(0.61f, 0.35f, 0.71f),
            new Color(1.00f, 0.87f, 0.77f), new Color(0.80f, 0.60f, 0.46f), new Color(0.45f, 0.30f, 0.20f),
        };

        private Rect _win = new Rect(24f, 320f, 264f, 300f);
        private const int WinId = 0x5A17;
        private bool _pointerOverPanel;
        private bool _pointerOverModel;
        private Vector2 _lastCursor;

        internal static void Toggle()
        {
            if (_current != null) { _current.Stop(false); return; }

            var att = VariantBar.CurrentAttachment;
            if (att == null || att.Part == null)
            {
                Compat.ShowError("Selecione uma parte custom antes de pintar.");
                return;
            }

            var host = new GameObject("CustomPaintSession");
            _current = host.AddComponent<PaintSession>();
            if (!_current.Begin(att)) { Destroy(host); _current = null; }
        }

        private bool Begin(CustomBodyPartAttachment att)
        {
            _attachment = att;
            _colliders.Clear();

            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator != null && creator.dummy != null && att.Part != null && !string.IsNullOrEmpty(att.Part.LinkGroupId))
            {
                foreach (var kv in creator.dummy.attachedItems)
                {
                    if (kv.Value is CustomBodyPartAttachment c && c.Part != null && c.Part.LinkGroupId == att.Part.LinkGroupId)
                    {
                        var mfc = c.GetComponent<MeshFilter>();
                        var colc = c.GetComponent<MeshCollider>();
                        if (colc == null && mfc != null && mfc.sharedMesh != null)
                        {
                            colc = c.gameObject.AddComponent<MeshCollider>();
                            colc.sharedMesh = mfc.sharedMesh;
                        }
                        if (colc != null) _colliders.Add(colc);
                    }
                }
            }
            else
            {
                var mf = att.GetComponent<MeshFilter>();
                var col = att.GetComponent<MeshCollider>();
                if (col == null && mf != null && mf.sharedMesh != null)
                {
                    col = att.gameObject.AddComponent<MeshCollider>();
                    col.sharedMesh = mf.sharedMesh;
                }
                if (col != null) _colliders.Add(col);
            }

            if (_colliders.Count == 0) { Compat.ShowError("Essa parte nao tem malha para pintar."); return false; }

            _cc = creator != null ? creator.creatorCam : UniqueMono<CharacterCreatorCamera>.instance;
            if (_cc == null || _cc.cam == null) { Compat.ShowError("Nao achei a camera do criador."); return false; }
            _previewImage = FindPreviewImage(_cc.preview);

            _originalTex = att.Part.Texture;
            _texW = _originalTex != null ? _originalTex.width : 1024;
            _texH = _originalTex != null ? _originalTex.height : 1024;
            if (_texW < 4) _texW = 1024;
            if (_texH < 4) _texH = 1024;

            _canvas = new Texture2D(_texW, _texH, TextureFormat.RGBA32, false);
            _pixels = ReadPixels(_originalTex, _texW, _texH);
            _basePixels = (Color32[])_pixels.Clone();
            _canvas.SetPixels32(_pixels);
            _canvas.Apply();

            if (creator != null && creator.dummy != null && _attachment.Part != null && !string.IsNullOrEmpty(_attachment.Part.LinkGroupId))
            {
                foreach (var kv in creator.dummy.attachedItems)
                {
                    if (kv.Value is CustomBodyPartAttachment c && c.Part != null && c.Part.LinkGroupId == _attachment.Part.LinkGroupId)
                    {
                        c.SetTexture(_canvas);
                    }
                }
            }
            else
            {
                _attachment.SetTexture(_canvas);
            }
            _dirty = false;
            Color.RGBToHSV(_color, out _h, out _s, out _v);
            _hexBuf = Hex(_color);

            PositionPanelUnderButton();
            Plugin.Log.LogInfo($"[paint] '{att.Part.PartId}' {_texW}x{_texH}; previewImage={(_previewImage != null)}.");
            return true;
        }

        /// <summary>Dock the panel just below the injected "Pincel" button (its initial spot — the user
        /// can still drag it after). Falls back to the default rect if the button isn't found.</summary>
        private void PositionPanelUnderButton()
        {
            var rt = PaintButton.Rect;
            if (rt == null) return;
            Canvas canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera : null;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners); // [0]=BL [1]=TL [2]=TR [3]=BR
            Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 br = RectTransformUtility.WorldToScreenPoint(cam, corners[3]);
            float leftScreen = Mathf.Min(bl.x, br.x);
            float bottomScreen = Mathf.Min(bl.y, br.y);              // bottom edge of the button (y-up)
            _win.x = Mathf.Clamp(leftScreen, 0f, Screen.width - _win.width);
            _win.y = Mathf.Clamp(Screen.height - bottomScreen + 6f, 0f, Screen.height - 80f); // GUI y is top-down
        }

        private void Update()
        {
            var creator = UniqueMono<CharacterCreator>.instance;
            if (_attachment == null || creator == null || !creator.gameObject.activeInHierarchy)
            {
                Stop(false);
                return;
            }

            SyncEyedropperCursor();

            if (_picking)
            {
                // Eyedropper: sample the actual RENDERED pixel under the cursor — works on ANYTHING in
                // the preview (this part, other parts, the body, the background), not just our own mesh.
                _pointerOverModel = false;
                _lastCursor = Input.mousePosition;
                if (Input.GetMouseButtonDown(0) && !_pointerOverPanel)
                {
                    if (PickColorFromPreview(out Color picked))
                    {
                        Color.RGBToHSV(picked, out _h, out _s, out _v);
                        SyncColorFromHsv();
                    }
                    _picking = false;
                }
            }
            else
            {
                _pointerOverModel = TryGetModelUV(out Vector2 uv);
                if (Input.GetMouseButton(0) && !_pointerOverPanel && _pointerOverModel)
                    Stamp(uv);
            }

            if (_dirty) { _canvas.SetPixels32(_pixels); _canvas.Apply(false); _dirty = false; }
        }

        /// <summary>Ray from the creator camera through the cursor (mapped into the preview rect).</summary>
        private bool TryGetModelUV(out Vector2 uv)
        {
            uv = default;
            Camera cam = _cc != null ? _cc.cam : null;
            if (cam == null) return false;
            _lastCursor = Input.mousePosition;

            Vector2 vp; // viewport point [0..1] within the camera's render target
            if (_previewImage != null)
            {
                Canvas canvas = _previewImage.canvas;
                Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    ? canvas.worldCamera : null;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _previewImage.rectTransform, Input.mousePosition, uiCam, out Vector2 local))
                    return false;
                Rect r = _previewImage.rectTransform.rect;
                float nx = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
                float ny = Mathf.InverseLerp(r.yMin, r.yMax, local.y);
                if (nx < 0f || nx > 1f || ny < 0f || ny > 1f) return false;
                vp = new Vector2(nx, ny);
            }
            else
            {
                vp = new Vector2(Input.mousePosition.x / Screen.width, Input.mousePosition.y / Screen.height);
            }

            Ray ray = cam.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));
            // RaycastAll (not Raycast): other parts/body now have colliders too, so the closest hit may
            // not be ours — scan all hits for our collider instead of being occluded.
            var hits = Physics.RaycastAll(ray, 5000f);
            for (int i = 0; i < hits.Length; i++)
            {
                if (_colliders.Contains(hits[i].collider as MeshCollider)) { uv = hits[i].textureCoord; return true; }
            }
            return false;
        }

        private void Stamp(Vector2 uv)
        {
            int cx = Mathf.Clamp(Mathf.RoundToInt(uv.x * (_texW - 1)), 0, _texW - 1);
            int cy = Mathf.Clamp(Mathf.RoundToInt(uv.y * (_texH - 1)), 0, _texH - 1);
            int r = _brushRadius, r2 = r * r;
            Color32 col = _color;

            for (int dy = -r; dy <= r; dy++)
            {
                int y = cy + dy;
                if (y < 0 || y >= _texH) continue;
                int row = dy * dy;
                for (int dx = -r; dx <= r; dx++)
                {
                    if (row + dx * dx > r2) continue;
                    int x = cx + dx;
                    if (x < 0 || x >= _texW) continue;
                    int idx = y * _texW + x;
                    // Opacity: blend toward the brush colour (erase restores the untouched base). At 1
                    // it fully replaces; lower values lay the colour down translucently.
                    _pixels[idx] = _erasing
                        ? _basePixels[idx]
                        : (_opacity >= 0.999f ? col : Color32.Lerp(_pixels[idx], col, _opacity));
                }
            }
            _dirty = true;
        }

        private void OnGUI()
        {
            // brush cursor ring over the model (approximate screen size from the preview scale)
            if (_pointerOverModel && !_pointerOverPanel)
            {
                float perTexel = PreviewScreenWidth() / Mathf.Max(1, _texW);
                float rad = Mathf.Clamp(_brushRadius * perTexel, 3f, 300f);
                var c = new Rect(_lastCursor.x - rad, (Screen.height - _lastCursor.y) - rad, rad * 2f, rad * 2f);
                var old = GUI.color; GUI.color = _erasing ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 1f, 1f, 0.95f);
                GUI.DrawTexture(c, Ring());
                GUI.color = old;
            }

            _win = GUILayout.Window(WinId, _win, DrawWindow, "Pincel");
            _pointerOverPanel = _win.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
        }

        private void DrawWindow(int id)
        {
            _erasing = GUILayout.Toggle(_erasing, _erasing ? " Borracha (apagando)" : " Borracha");
            GUILayout.Space(4f);

            DrawColorPicker();

            GUILayout.Space(4f);
            if (GUILayout.Button("Preencher Tudo"))
            {
                FillActiveMeshUVs();
                _dirty = true;
            }

            GUILayout.Space(4f);
            GUILayout.Label($"Tamanho: {_brushRadius} px");
            _brushRadius = Mathf.RoundToInt(GUILayout.HorizontalSlider(_brushRadius, 1f, 80f));

            GUILayout.Space(4f);
            GUILayout.Label($"Opacidade: {Mathf.RoundToInt(_opacity * 100f)}%");
            _opacity = GUILayout.HorizontalSlider(_opacity, 0.05f, 1f);

            GUILayout.Space(8f);
            if (GUILayout.Button("Confirmar (salvar variante)")) Save();
            GUILayout.Space(2f);
            if (GUILayout.Button("Cancelar")) Stop(false);

            GUI.DragWindow(new Rect(0, 0, 10000, 20)); // drag by the title bar
        }

        // ---- RGB colour picker (IMGUI) -----------------------------------------------------

        /// <summary>Saturation/Value square + hue bar + editable hex + presets + eyedropper.</summary>
        private void DrawColorPicker()
        {
            // Live swatch + eyedropper toggle.
            GUILayout.BeginHorizontal();
            Rect sw = GUILayoutUtility.GetRect(40f, 20f, GUILayout.Width(40f), GUILayout.Height(20f));
            if (Event.current.type == EventType.Repaint)
            {
                var oc = GUI.color;
                GUI.color = _color; GUI.DrawTexture(sw, Texture2D.whiteTexture); GUI.color = oc;
                Outline(sw, new Color(0f, 0f, 0f, 0.6f));
            }
            GUILayout.Space(6f);
            _picking = GUILayout.Toggle(_picking, _picking ? " Clique no modelo…" : " Conta-gotas",
                                        "Button", GUILayout.Height(20f));
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);

            // Saturation (x) / Value (y) square for the current hue.
            Rect sv = GUILayoutUtility.GetRect(1f, 118f, GUILayout.ExpandWidth(true), GUILayout.Height(118f));
            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(sv, SvTex());
                Marker(sv.x + _s * sv.width, sv.y + (1f - _v) * sv.height);
            }
            HandleSv(sv);

            GUILayout.Space(3f);

            // Hue bar.
            Rect hue = GUILayoutUtility.GetRect(1f, 16f, GUILayout.ExpandWidth(true), GUILayout.Height(16f));
            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(hue, HueTex());
                float hx = hue.x + _h * hue.width;
                var old = GUI.color;
                GUI.color = Color.white; GUI.DrawTexture(new Rect(hx - 1.5f, hue.y - 1f, 3f, hue.height + 2f), Texture2D.whiteTexture);
                GUI.color = Color.black; GUI.DrawTexture(new Rect(hx - 0.5f, hue.y, 1f, hue.height), Texture2D.whiteTexture);
                GUI.color = old;
            }
            HandleHue(hue);

            GUILayout.Space(5f);

            // Editable hex + live RGB readout.
            GUILayout.BeginHorizontal();
            GUILayout.Label("#", GUILayout.Width(12f));
            string prev = _hexBuf;
            _hexBuf = GUILayout.TextField(_hexBuf, 7, GUILayout.Width(78f));
            if (_hexBuf != prev && ParseHex(_hexBuf, out Color hc))
            { Color.RGBToHSV(hc, out _h, out _s, out _v); _color = hc; _color.a = 1f; }
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{Mathf.RoundToInt(_color.r * 255)}, {Mathf.RoundToInt(_color.g * 255)}, {Mathf.RoundToInt(_color.b * 255)}");
            GUILayout.EndHorizontal();

            // Preset palette (click to set).
            GUILayout.Space(4f);
            const int perRow = 6;
            for (int i = 0; i < _presets.Length; i++)
            {
                if (i % perRow == 0) GUILayout.BeginHorizontal();
                if (Swatch(_presets[i], 22f))
                { Color.RGBToHSV(_presets[i], out _h, out _s, out _v); SyncColorFromHsv(); }
                if (i % perRow == perRow - 1 || i == _presets.Length - 1)
                { GUILayout.FlexibleSpace(); GUILayout.EndHorizontal(); }
            }
        }

        private void HandleSv(Rect r)
        {
            var e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && r.Contains(e.mousePosition))
            {
                _s = Mathf.Clamp01((e.mousePosition.x - r.x) / r.width);
                _v = Mathf.Clamp01(1f - (e.mousePosition.y - r.y) / r.height);
                SyncColorFromHsv();
                e.Use();
            }
        }

        private void HandleHue(Rect r)
        {
            var e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && r.Contains(e.mousePosition))
            {
                _h = Mathf.Clamp01((e.mousePosition.x - r.x) / r.width);
                SyncColorFromHsv();
                e.Use();
            }
        }

        private bool Swatch(Color c, float size)
        {
            Rect r = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            if (Event.current.type == EventType.Repaint)
            {
                var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old;
                Outline(r, new Color(0f, 0f, 0f, 0.5f));
            }
            var e = Event.current;
            if (e.type == EventType.MouseDown && r.Contains(e.mousePosition)) { e.Use(); return true; }
            return false;
        }

        /// <summary>Map the cursor into the preview image's rect as viewport coords [0..1] (bottom-left
        /// origin, matching the camera's render target). Falls back to full-screen when no RawImage.</summary>
        private bool CursorToPreviewViewport(out Vector2 vp)
        {
            vp = default;
            if (_previewImage != null)
            {
                Canvas canvas = _previewImage.canvas;
                Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    ? canvas.worldCamera : null;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _previewImage.rectTransform, Input.mousePosition, uiCam, out Vector2 local))
                    return false;
                Rect r = _previewImage.rectTransform.rect;
                float nx = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
                float ny = Mathf.InverseLerp(r.yMin, r.yMax, local.y);
                if (nx < 0f || nx > 1f || ny < 0f || ny > 1f) return false;
                vp = new Vector2(nx, ny);
                return true;
            }
            vp = new Vector2(Input.mousePosition.x / Screen.width, Input.mousePosition.y / Screen.height);
            return true;
        }

        /// <summary>Eyedropper: read the colour of the pixel the cursor is over straight from the creator
        /// camera's RenderTexture. This is whatever is VISIBLE there — the painted part, another part,
        /// the body, even the background — so the user can sample any colour they can see.</summary>
        private bool PickColorFromPreview(out Color color)
        {
            color = Color.white;
            RenderTexture rt = _cc != null ? _cc.preview : null;
            if (rt == null) return false;
            if (!CursorToPreviewViewport(out Vector2 vp)) return false;

            int px = Mathf.Clamp(Mathf.RoundToInt(vp.x * (rt.width - 1)), 0, rt.width - 1);
            int py = Mathf.Clamp(Mathf.RoundToInt(vp.y * (rt.height - 1)), 0, rt.height - 1);

            RenderTexture keep = RenderTexture.active;
            var tmp = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = rt;
                tmp.ReadPixels(new Rect(px, py, 1, 1), 0, 0);
                tmp.Apply(false);
                color = tmp.GetPixel(0, 0);
                color.a = 1f;
                return true;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("[paint] conta-gotas falhou: " + e.Message);
                return false;
            }
            finally
            {
                RenderTexture.active = keep;
                Destroy(tmp);
            }
        }

        private void SyncColorFromHsv()
        {
            _color = Color.HSVToRGB(_h, _s, _v);
            _color.a = 1f;
            _hexBuf = Hex(_color);
        }

        /// <summary>Turn the OS mouse cursor into a little eyedropper while the picker is armed, and put
        /// it back to normal the moment it disarms. Hotspot = the pipette tip (where the colour is read).</summary>
        private void SyncEyedropperCursor()
        {
            if (_picking && !_cursorArmed)
            {
                Cursor.SetCursor(EyedropperCursor(), EyedropperHotspot, CursorMode.Auto);
                _cursorArmed = true;
            }
            else if (!_picking && _cursorArmed)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); // restore the default arrow
                _cursorArmed = false;
            }
        }

        // Cursor icon: a two-tone (dark outline + light fill) pipette drawn once and cached. The tip sits
        // at the lower-left so the hotspot lands exactly on the sampled pixel.
        private static readonly Vector2 EyedropperHotspot = new Vector2(3f, 28f);
        private static Texture2D _eyedropperCursor;

        private static Texture2D EyedropperCursor()
        {
            if (_eyedropperCursor != null) return _eyedropperCursor;
            const int S = 32;
            _eyedropperCursor = new Texture2D(S, S, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };

            var px = new Color32[S * S];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            // Image-space coordinates (x→right, y↓down); the pipette runs from the tip (lower-left) up to
            // a round bulb (upper-right).
            Vector2 tip = new Vector2(3.5f, 28.5f);
            Vector2 shaftEnd = new Vector2(20f, 12f);
            Vector2 bulb = new Vector2(23.5f, 8.5f);
            const float bulbR = 5.5f;
            var dark = new Color32(24, 24, 28, 255);
            var light = new Color32(238, 240, 248, 255);

            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float dShaft = DistToSeg(p, tip, shaftEnd);
                    float dBulb = Vector2.Distance(p, bulb);
                    float outer = Mathf.Min(dShaft - 3.1f, dBulb - bulbR);          // <=0 => inside outline
                    float inner = Mathf.Min(dShaft - 1.6f, dBulb - (bulbR - 2.1f)); // <=0 => inside fill
                    int idx = (S - 1 - y) * S + x; // Texture2D y is bottom-up; flip so the icon is upright
                    if (inner <= 0f) px[idx] = light;
                    else if (outer <= 0f) px[idx] = dark;
                }

            _eyedropperCursor.SetPixels32(px);
            _eyedropperCursor.Apply(false);
            return _eyedropperCursor;
        }

        private static float DistToSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + t * ab);
        }

        private Texture2D SvTex()
        {
            if (_svTex != null && Mathf.Approximately(_svTexHue, _h)) return _svTex;
            const int n = 64;
            if (_svTex == null)
                _svTex = new Texture2D(n, n, TextureFormat.RGB24, false)
                { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = Color.HSVToRGB(_h, x / (n - 1f), y / (n - 1f));
            _svTex.SetPixels32(px);
            _svTex.Apply(false);
            _svTexHue = _h;
            return _svTex;
        }

        private static Texture2D HueTex()
        {
            if (_hueTex != null) return _hueTex;
            const int n = 128;
            _hueTex = new Texture2D(n, 1, TextureFormat.RGB24, false)
            { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n];
            for (int x = 0; x < n; x++) px[x] = Color.HSVToRGB(x / (n - 1f), 1f, 1f);
            _hueTex.SetPixels32(px);
            _hueTex.Apply(false);
            return _hueTex;
        }

        private static void Marker(float x, float y)
        {
            var t = Texture2D.whiteTexture; var old = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(x - 5f, y - 1f, 10f, 2f), t);
            GUI.DrawTexture(new Rect(x - 1f, y - 5f, 2f, 10f), t);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x - 4f, y - 0.5f, 8f, 1f), t);
            GUI.DrawTexture(new Rect(x - 0.5f, y - 4f, 1f, 8f), t);
            GUI.color = old;
        }

        private static void Outline(Rect r, Color c)
        {
            var t = Texture2D.whiteTexture; var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), t);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1f, r.width, 1f), t);
            GUI.DrawTexture(new Rect(r.x, r.y, 1f, r.height), t);
            GUI.DrawTexture(new Rect(r.xMax - 1f, r.y, 1f, r.height), t);
            GUI.color = old;
        }

        private static string Hex(Color c) =>
            Mathf.RoundToInt(c.r * 255).ToString("X2") +
            Mathf.RoundToInt(c.g * 255).ToString("X2") +
            Mathf.RoundToInt(c.b * 255).ToString("X2");

        private static bool ParseHex(string s, out Color c)
        {
            c = Color.black;
            if (string.IsNullOrEmpty(s)) return false;
            s = s.Trim().TrimStart('#');
            if (s.Length != 6) return false;
            try
            {
                int r = Convert.ToInt32(s.Substring(0, 2), 16);
                int g = Convert.ToInt32(s.Substring(2, 2), 16);
                int b = Convert.ToInt32(s.Substring(4, 2), 16);
                c = new Color(r / 255f, g / 255f, b / 255f, 1f);
                return true;
            }
            catch { return false; }
        }

        private void Save()
        {
            try
            {
                if (_dirty) { _canvas.SetPixels32(_pixels); _canvas.Apply(false); _dirty = false; }
                byte[] png = _canvas.EncodeToPNG();
                string dir = Path.Combine(ScaleStore.CustomPartsDir, "painted");
                Directory.CreateDirectory(dir);
                string baseName = Sanitize(_attachment.Part.PartId);
                string file = Path.Combine(dir, baseName + "_" + NextIndex(dir, baseName) + ".png");
                File.WriteAllBytes(file, png);

                _handedOff = true;
                VariantBar.AddPaintedVariant(file, _canvas);
                Plugin.Log.LogInfo("[paint] salvo como variante: " + file);
                Stop(true);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError("[paint] falha ao salvar: " + e);
                Compat.ShowError("Nao consegui salvar a textura pintada.");
            }
        }

        private void Stop(bool kept)
        {
            if (_cursorArmed) { Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); _cursorArmed = false; }
            if (!kept && _attachment != null) _attachment.SetTexture(_originalTex);
            if (_canvas != null && !_handedOff) Destroy(_canvas);
            if (_svTex != null) Destroy(_svTex);
            _canvas = null;
            _current = null;
            if (this != null && gameObject != null) Destroy(gameObject);
        }

        // ---- helpers ------------------------------------------------------------------------

        private float PreviewScreenWidth()
        {
            if (_previewImage == null) return Screen.width;
            var c = new Vector3[4];
            _previewImage.rectTransform.GetWorldCorners(c); // overlay canvas → screen px
            return Mathf.Abs(c[2].x - c[0].x);
        }

        private static RawImage FindPreviewImage(RenderTexture preview)
        {
            if (preview == null) return null;
            foreach (var ri in Resources.FindObjectsOfTypeAll<RawImage>())
                if (ri != null && ri.texture == preview && ri.gameObject.scene.IsValid())
                    return ri;
            return null;
        }

        private static Color32[] ReadPixels(Texture2D src, int w, int h)
        {
            if (src == null)
            {
                var white = new Color32[w * h];
                for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);
                return white;
            }
            if (src.isReadable)
            {
                try { return src.GetPixels32(); } catch { }
            }
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            RenderTexture keep = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tmp.Apply(false);
            RenderTexture.active = keep;
            RenderTexture.ReleaseTemporary(rt);
            Color32[] px = tmp.GetPixels32();
            Destroy(tmp);
            return px;
        }

        private static Texture2D _ring;
        private static Texture2D Ring()
        {
            if (_ring != null) return _ring;
            const int s = 64;
            _ring = new Texture2D(s, s, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[s * s];
            float c = (s - 1) / 2f, rOut = c, rIn = c - 3f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    px[y * s + x] = (d <= rOut && d >= rIn) ? new Color32(255, 255, 255, 235) : new Color32(0, 0, 0, 0);
                }
            _ring.SetPixels32(px);
            _ring.Apply(false);
            return _ring;
        }

        private static int NextIndex(string dir, string baseName)
        {
            int i = 1;
            while (File.Exists(Path.Combine(dir, baseName + "_" + i + ".png"))) i++;
            return i;
        }

        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "paint";
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        private void FillActiveMeshUVs()
        {
            if (_attachment == null || _attachment.Part == null || _attachment.Part.Mesh == null) return;
            var mesh = _attachment.Part.Mesh;
            Vector2[] uvs = mesh.uv;
            int[] tris = mesh.triangles;
            if (uvs == null || uvs.Length == 0 || tris == null || tris.Length == 0) return;

            bool[] mask = new bool[_texW * _texH];

            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector2 uv1 = uvs[tris[i]];
                Vector2 uv2 = uvs[tris[i+1]];
                Vector2 uv3 = uvs[tris[i+2]];

                Vector2 p1 = new Vector2(uv1.x * _texW, uv1.y * _texH);
                Vector2 p2 = new Vector2(uv2.x * _texW, uv2.y * _texH);
                Vector2 p3 = new Vector2(uv3.x * _texW, uv3.y * _texH);

                int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p1.x, Mathf.Min(p2.x, p3.x))), 0, _texW - 1);
                int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p1.x, Mathf.Max(p2.x, p3.x))), 0, _texW - 1);
                int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p1.y, Mathf.Min(p2.y, p3.y))), 0, _texH - 1);
                int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p1.y, Mathf.Max(p2.y, p3.y))), 0, _texH - 1);

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                        if (IsPointInTriangle(p, p1, p2, p3)) mask[y * _texW + x] = true;
                    }
                }
            }

            bool[] dilated = new bool[_texW * _texH];
            for (int y = 0; y < _texH; y++)
            {
                for (int x = 0; x < _texW; x++)
                {
                    if (mask[y * _texW + x])
                    {
                        dilated[y * _texW + x] = true;
                        if (x > 0) dilated[y * _texW + (x - 1)] = true;
                        if (x < _texW - 1) dilated[y * _texW + (x + 1)] = true;
                        if (y > 0) dilated[(y - 1) * _texW + x] = true;
                        if (y < _texH - 1) dilated[(y + 1) * _texW + x] = true;
                    }
                }
            }

            for (int i = 0; i < dilated.Length; i++)
            {
                if (dilated[i]) _pixels[i] = _color;
            }
        }

        private bool IsPointInTriangle(Vector2 pt, Vector2 v1, Vector2 v2, Vector2 v3)
        {
            float d1 = Sign(pt, v1, v2);
            float d2 = Sign(pt, v2, v3);
            float d3 = Sign(pt, v3, v1);
            bool has_neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool has_pos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            return !(has_neg && has_pos);
        }

        private float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }
    }

    /// <summary>
    /// While the brush is active, a LEFT-drag paints — so suppress the creator camera's rotation for the
    /// left button (fixes: dragging the brush sliders spun the camera, and painting rotated the model).
    /// Other buttons still rotate, so the user can still look around with a right-drag.
    /// </summary>
    [HarmonyPatch(typeof(CharacterCreatorCamera), "MouseDrag")]
    internal static class PaintCameraDragGuard
    {
        private static bool Prefix(PointerEventData mouse)
        {
            if (!PaintSession.Active) return true;
            if (mouse != null && mouse.button == PointerEventData.InputButton.Left) return false;
            return true;
        }
    }
}
