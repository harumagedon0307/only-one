using UnityEngine;
using UnityEngine.EventSystems;

public class EditablePattern : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    RectTransform rt;

    bool pointerDown = false;
    bool isHolding = false;

    float holdTime = 0.4f;
    float timer = 0f;

    float prevPinchDist;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    void Update()
    {
        // 長押し判定
        if (pointerDown)
        {
            timer += Time.deltaTime;
            if (!isHolding && timer >= holdTime)
            {
                isHolding = true;
                Debug.Log("★ 長押し成功！（Edit Mode）");
            }
        }

        // ピンチズーム
        if (isHolding && Input.touchCount == 2)
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            float dist = Vector2.Distance(t0.position, t1.position);

            if (prevPinchDist > 0f)
            {
                float diff = dist - prevPinchDist;
                float scale = diff * 0.001f;

                rt.localScale += Vector3.one * scale;
                rt.localScale = Vector3.Max(rt.localScale, Vector3.one * 0.2f);
            }

            prevPinchDist = dist;
        }
        else
        {
            prevPinchDist = 0f;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerDown = true;
        timer = 0f;
        isHolding = false;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerDown = false;
        isHolding = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isHolding) return;

        // 1本指のときだけ移動
        if (Input.touchCount <= 1)
        {
            rt.anchoredPosition += eventData.delta;
        }
    }
}
