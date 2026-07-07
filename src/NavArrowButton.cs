using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CustomPartsMod
{
    /// <summary>
    /// A clickable arrow drawn as a filled triangle (no font glyph, no sprite asset) with a dark
    /// halo for contrast against the 3D preview. Used by <see cref="NavArrows"/> (P12).
    /// </summary>
    internal class NavArrowButton : MaskableGraphic, IPointerClickHandler
    {
        public bool pointRight = true;
        public Action onClick;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();

            // Dark backdrop triangle (halo) + the coloured arrow slightly inset on top.
            AddTriangle(vh, r, new Color(0f, 0f, 0f, 0.5f), pointRight);
            const float pad = 5f;
            var inner = new Rect(r.x + pad, r.y + pad, r.width - 2f * pad, r.height - 2f * pad);
            AddTriangle(vh, inner, color, pointRight);
        }

        private static void AddTriangle(VertexHelper vh, Rect r, Color col, bool right)
        {
            int i = vh.currentVertCount;
            Vector2 a, b, c;
            float midY = r.y + r.height * 0.5f;
            if (right)
            {
                a = new Vector2(r.x, r.y);                 // bottom-left
                b = new Vector2(r.x, r.y + r.height);      // top-left
                c = new Vector2(r.x + r.width, midY);      // right tip
            }
            else
            {
                a = new Vector2(r.x + r.width, r.y);       // bottom-right
                b = new Vector2(r.x + r.width, r.y + r.height); // top-right
                c = new Vector2(r.x, midY);                // left tip
            }

            var v = UIVertex.simpleVert;
            v.color = col;
            v.position = a; vh.AddVert(v);
            v.position = b; vh.AddVert(v);
            v.position = c; vh.AddVert(v);
            vh.AddTriangle(i, i + 1, i + 2);
        }

        public void OnPointerClick(PointerEventData eventData) => onClick?.Invoke();
    }
}
