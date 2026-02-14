using UnityEngine;
using UnityEngine.EventSystems;

public class ShapeRotateHandle : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private RectTransform target;
    private Canvas canvas;
    private float startAngle;
    private float startRotationZ;

    public void Initialize(RectTransform targetRect, Canvas parentCanvas)
    {
        target = targetRect;
        canvas = parentCanvas;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (target == null) return;
        startAngle = GetPointerAngle(eventData);
        startRotationZ = target.localEulerAngles.z;
        eventData.Use();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (target == null) return;
        float currentAngle = GetPointerAngle(eventData);
        float delta = currentAngle - startAngle;
        target.localEulerAngles = new Vector3(0f, 0f, startRotationZ + delta);
    }

    private float GetPointerAngle(PointerEventData eventData)
    {
        Vector2 center = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, target.position);
        Vector2 dir = eventData.position - center;
        return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
    }
}
