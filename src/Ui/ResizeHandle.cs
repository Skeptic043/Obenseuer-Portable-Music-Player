using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PortableMusicPlayer.Ui
{
    // Dragging the corner grip scales the whole device, so the case keeps its proportions.
    internal sealed class ResizeHandle : MonoBehaviour, IDragHandler
    {
        private const float MinimumScale = 0.7f;
        private const float MaximumScale = 1.8f;
        private const float ScreenMargin = 48f;
        private RectTransform target;
        private Canvas canvas;
        private Action<float> scaled;

        internal void Initialize(RectTransform scaleTarget, Action<float> onScaled)
        {
            target = scaleTarget;
            canvas = scaleTarget.GetComponentInParent<Canvas>();
            scaled = onScaled;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (target == null) return;
            float canvasScale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Vector2 size = target.rect.size;
            // Down and to the right grows the device.
            float grow = (eventData.delta.x - eventData.delta.y) / canvasScale / (size.x + size.y);
            float scale = Mathf.Clamp(target.localScale.x + grow, MinimumScale, MaximumScale);
            if (target.parent is RectTransform surface)
                scale = Mathf.Max(MinimumScale, Mathf.Min(scale, (surface.rect.height - ScreenMargin) / size.y,
                    (surface.rect.width - ScreenMargin) / size.x));
            target.localScale = new Vector3(scale, scale, 1f);
            scaled?.Invoke(scale);
        }
    }
}
