using UnityEngine;
using UnityEngine.EventSystems;

namespace CustomPartsMod
{
    /// <summary>
    /// Makes a UI element draggable: while dragging anywhere on this (raycastable) graphic, moves
    /// <see cref="target"/>'s RectTransform by the pointer delta. Children that consume their own
    /// drag (input fields) keep working; empty areas / labels move the panel.
    /// </summary>
    internal class DragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public RectTransform target;
        private Canvas _canvas;

        public void OnBeginDrag(PointerEventData e)
        {
            if (target != null) _canvas = target.GetComponentInParent<Canvas>();
        }

        public void OnDrag(PointerEventData e)
        {
            if (target == null) return;
            float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            target.anchoredPosition += e.delta / scale;
        }
    }
}
