using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CustomPartsMod
{
    /// <summary>
    /// A clickable up/down arrow drawn as a filled triangle with a dark halo (vertical sibling of
    /// <see cref="NavArrowButton"/>). Used by <see cref="PanButtons"/> to raise/lower the creator camera.
    /// </summary>
    internal class PanButton : MaskableGraphic, IPointerClickHandler
    {
        public bool pointUp = true;
        public Action onClick;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();

            AddTriangle(vh, r, new Color(0f, 0f, 0f, 0.5f), pointUp);
            const float pad = 5f;
            var inner = new Rect(r.x + pad, r.y + pad, r.width - 2f * pad, r.height - 2f * pad);
            AddTriangle(vh, inner, color, pointUp);
        }

        private static void AddTriangle(VertexHelper vh, Rect r, Color col, bool up)
        {
            int i = vh.currentVertCount;
            float midX = r.x + r.width * 0.5f;
            Vector2 a, b, c;
            if (up)
            {
                a = new Vector2(r.x, r.y);                    // bottom-left
                b = new Vector2(r.x + r.width, r.y);          // bottom-right
                c = new Vector2(midX, r.y + r.height);        // top tip
            }
            else
            {
                a = new Vector2(r.x, r.y + r.height);         // top-left
                b = new Vector2(r.x + r.width, r.y + r.height); // top-right
                c = new Vector2(midX, r.y);                   // bottom tip
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
