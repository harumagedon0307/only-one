using UnityEngine;
using UnityEngine.EventSystems;

public class ShapeMoveHandle : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private RectTransform target;
    private Canvas canvas;

    public void Initialize(RectTransform targetRect, Canvas parentCanvas)
    {
        target = targetRect;
        canvas = parentCanvas;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        eventData.Use();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (target == null) return;
        float scale = (canvas != null) ? canvas.scaleFactor : 1f;
        target.anchoredPosition += eventData.delta / scale;
    }
}
