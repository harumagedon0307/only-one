using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Editable : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [SerializeField] private float holdTime = 1.0f;
    [SerializeField] private float scaleSpeed = 0.005f;
    [SerializeField] private float rotationSpeed = 1.0f;
    [SerializeField] private float minScale = 0.5f;
    [SerializeField] private float maxScale = 2.0f;

    public GameObject editMenuPrefab;

    private float pointerDownTimer = 0f;
    private bool isPointerDown = false;
    private bool isEditing = false;

    private float prevPinchDistance = 0f;
    private float prevPinchAngle = 0f;

    private GameObject currentMenu;
    private GameObject inputBlocker;

    private RectTransform rectTransform;
    private Canvas canvas;

    private static Editable currentEditingTarget = null;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void SetEditing(bool value)
    {
        isEditing = value;
    }

    private void Update()
    {
        if (isPointerDown)
        {
            pointerDownTimer += Time.deltaTime;
            if (pointerDownTimer >= holdTime)
            {
                LongPressSuccess();
            }
        }

        HandlePinchZoomAndRotate();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPointerDown = true;
        pointerDownTimer = 0f;
        eventData.Use();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ResetPress();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isEditing || currentEditingTarget != this || Input.touchCount > 1)
        {
            return;
        }

        if (rectTransform == null)
        {
            return;
        }

        float scale = (canvas != null && canvas.scaleFactor > 0f) ? canvas.scaleFactor : 1f;
        rectTransform.anchoredPosition += eventData.delta / scale;
    }

    private void LongPressSuccess()
    {
        isPointerDown = false;
        ActivateEditingTarget();
        OpenEditMenu();
    }

    private void ResetPress()
    {
        isPointerDown = false;
        pointerDownTimer = 0f;
    }

    private void OpenEditMenu()
    {
        if (currentMenu != null)
        {
            return;
        }

        if (editMenuPrefab == null)
        {
            Debug.LogWarning("Editable: editMenuPrefab is missing. Drag-only edit mode is enabled.");
            return;
        }

        CreateInputBlocker();

        currentMenu = Instantiate(editMenuPrefab, transform.parent);
        currentMenu.transform.position = transform.position;

        EditMenu menu = currentMenu.GetComponent<EditMenu>();
        if (menu != null)
        {
            menu.SetTarget(gameObject);
        }
        else
        {
            Debug.LogWarning("Editable: EditMenu component is missing on editMenuPrefab.");
        }
    }

    private void ActivateEditingTarget()
    {
        if (currentEditingTarget != null && currentEditingTarget != this)
        {
            currentEditingTarget.CloseEditMenuFromScript();
        }

        isEditing = true;
        currentEditingTarget = this;
    }

    private void HandlePinchZoomAndRotate()
    {
        if (!isEditing || currentEditingTarget != this)
        {
            return;
        }

        if (Input.touchCount != 2)
        {
            prevPinchDistance = 0f;
            prevPinchAngle = 0f;
            return;
        }

        if (rectTransform == null)
        {
            return;
        }

        Touch t1 = Input.GetTouch(0);
        Touch t2 = Input.GetTouch(1);

        Vector2 touchDir = t2.position - t1.position;
        float currentDistance = Vector2.Distance(t1.position, t2.position);
        float currentAngle = Mathf.Atan2(touchDir.y, touchDir.x) * Mathf.Rad2Deg;

        if (prevPinchDistance > 0f)
        {
            float delta = currentDistance - prevPinchDistance;
            float scaleDelta = delta * scaleSpeed;

            Vector3 newScale = rectTransform.localScale + Vector3.one * scaleDelta;
            newScale.x = Mathf.Clamp(newScale.x, minScale, maxScale);
            newScale.y = Mathf.Clamp(newScale.y, minScale, maxScale);
            newScale.z = 1f;
            rectTransform.localScale = newScale;
        }

        if (prevPinchAngle != 0f)
        {
            float deltaAngle = Mathf.DeltaAngle(prevPinchAngle, currentAngle);
            rectTransform.localEulerAngles += new Vector3(0f, 0f, deltaAngle * rotationSpeed);
        }

        prevPinchDistance = currentDistance;
        prevPinchAngle = currentAngle;
    }

    private void CreateInputBlocker()
    {
        if (inputBlocker != null)
        {
            return;
        }

        if (canvas == null)
        {
            Debug.LogWarning("Editable: Canvas was not found. Input blocker is skipped.");
            return;
        }

        inputBlocker = new GameObject("InputBlocker");
        var img = inputBlocker.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;

        inputBlocker.transform.SetParent(canvas.transform, false);

        var rt = inputBlocker.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        inputBlocker.transform.SetSiblingIndex(transform.GetSiblingIndex());
    }

    public void CloseEditMenuFromScript()
    {
        if (currentMenu != null)
        {
            Destroy(currentMenu);
        }

        currentMenu = null;
        isEditing = false;

        if (currentEditingTarget == this)
        {
            currentEditingTarget = null;
        }

        prevPinchDistance = 0f;
        prevPinchAngle = 0f;

        if (inputBlocker != null)
        {
            Destroy(inputBlocker);
        }
    }
}
