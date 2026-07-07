using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CustomPartsMod
{
    /// <summary>
    /// A clickable zoom button drawn in code (no font glyph, no sprite): a dark rounded backdrop with
    /// white bars forming "+" (plus) or "–" (minus). Same approach as <see cref="NavArrowButton"/> so it
    /// renders reliably over the 3D preview and receives clicks via the EventSystem.
    /// </summary>
    internal class ZoomButton : MaskableGraphic, IPointerClickHandler
    {
        public bool plus = true;
        public Action onClick;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();

            // Dark backdrop for contrast against the preview.
            AddQuad(vh, r, new Color(0f, 0f, 0f, 0.5f));

            float cx = r.x + r.width * 0.5f;
            float cy = r.y + r.height * 0.5f;
            float len = r.width * 0.5f;
            float thick = Mathf.Max(3f, r.height * 0.14f);

            // Horizontal bar (both + and –); vertical bar only for +.
            AddQuad(vh, new Rect(cx - len * 0.5f, cy - thick * 0.5f, len, thick), color);
            if (plus)
                AddQuad(vh, new Rect(cx - thick * 0.5f, cy - len * 0.5f, thick, len), color);
        }

        private static void AddQuad(VertexHelper vh, Rect r, Color col)
        {
            int i = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.color = col;
            v.position = new Vector2(r.x, r.y); vh.AddVert(v);
            v.position = new Vector2(r.x, r.y + r.height); vh.AddVert(v);
            v.position = new Vector2(r.x + r.width, r.y + r.height); vh.AddVert(v);
            v.position = new Vector2(r.x + r.width, r.y); vh.AddVert(v);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        public void OnPointerClick(PointerEventData eventData) => onClick?.Invoke();
    }
}
