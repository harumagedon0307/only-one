using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Editable : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    private float holdTime = 1.0f; // 長押し時間
    private float pointerDownTimer = 0f;
    private bool isPointerDown = false;

    public GameObject editMenuPrefab;
    private GameObject currentMenu;

    private RectTransform rectTransform;
    private Canvas canvas;

    // 他UIへイベントを通さないブロッカー
    private GameObject inputBlocker;

    private bool isEditing = false;

    // ====== 追加：現在編集中の Editable（1つだけ） ======
    private static Editable currentEditingTarget = null;

    // ---------- ピンチ操作用 ----------
    private float prevPinchDistance = 0f;
    [SerializeField] private float scaleSpeed = 0.005f;
    [SerializeField] private float minScale = 0.5f;
    [SerializeField] private float maxScale = 2.0f;
    // ----------------------------------

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void SetEditing(bool value)
    {
        isEditing = value;
    }

    void Update()
    {
        if (isPointerDown)
        {
            pointerDownTimer += Time.deltaTime;
            if (pointerDownTimer >= holdTime)
            {
                LongPressSuccess();
            }
        }

        // ピンチ拡大縮小（編集中の1つだけ）
        HandlePinchZoom();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPointerDown = true;
        pointerDownTimer = 0f;

        // 現在のイベントを消費
        eventData.Use();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ResetPress();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isEditing && currentEditingTarget == this && Input.touchCount <= 1)
        {
            if (rectTransform == null) return;
            float scale = (canvas != null && canvas.scaleFactor > 0f) ? canvas.scaleFactor : 1f;
            rectTransform.anchoredPosition += eventData.delta / scale;
        }
    }

    void LongPressSuccess()
    {
        isPointerDown = false;
        ActivateEditingTarget();
        OpenEditMenu();
    }

    void ResetPress()
    {
        isPointerDown = false;
        pointerDownTimer = 0f;
    }

    void OpenEditMenu()
    {
        if (currentMenu != null) return;
        if (editMenuPrefab == null)
        {
            Debug.LogWarning("Editable: editMenuPrefab is missing. Drag-only edit mode is enabled.");
            return;
        }

        CreateInputBlocker();  // 後ろの UI を完全遮断

        currentMenu = Instantiate(editMenuPrefab, transform.parent);
        currentMenu.transform.position = transform.position;

        EditMenu menu = currentMenu.GetComponent<EditMenu>();
        if (menu != null)
        {
            menu.SetTarget(this.gameObject);
        }
        else
        {
            Debug.LogWarning("Editable: EditMenu component is missing on editMenuPrefab.");
        }
    }

    void ActivateEditingTarget()
    {
        // 他が編集中なら解除（1つだけ編集）
        if (currentEditingTarget != null && currentEditingTarget != this)
        {
            currentEditingTarget.CloseEditMenuFromScript();
        }

        isEditing = true;
        currentEditingTarget = this;
    }

    // ---------- ピンチ拡大縮小 ----------
    void HandlePinchZoom()
    {
        if (!isEditing) return;
        if (currentEditingTarget != this) return;

        if (Input.touchCount != 2)
        {
            prevPinchDistance = 0f;
            return;
        }

        Touch t1 = Input.GetTouch(0);
        Touch t2 = Input.GetTouch(1);

        float currentDistance = Vector2.Distance(t1.position, t2.position);

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

        prevPinchDistance = currentDistance;
    }
    // ----------------------------------

    // --------- UI ブロッカー生成 ---------
    void CreateInputBlocker()
    {
        if (inputBlocker != null) return;
        if (canvas == null)
        {
            Debug.LogWarning("Editable: Canvas was not found. Input blocker is skipped.");
            return;
        }

        inputBlocker = new GameObject("InputBlocker");
        var img = inputBlocker.AddComponent<Image>();

        img.color = new Color(0, 0, 0, 0);
        img.raycastTarget = true;

        inputBlocker.transform.SetParent(canvas.transform, false);

        var rt = inputBlocker.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // EditMenu より手前に出さない
        inputBlocker.transform.SetSiblingIndex(transform.GetSiblingIndex());
    }

    // メニューから呼ばれる閉じ処理
    public void CloseEditMenuFromScript()
    {
        if (currentMenu != null)
            Destroy(currentMenu);

        currentMenu = null;
        isEditing = false;

        if (currentEditingTarget == this)
            currentEditingTarget = null;

        if (inputBlocker != null)
            Destroy(inputBlocker);
    }
}
