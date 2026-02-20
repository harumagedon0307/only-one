using Mediapipe.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WearClothVisibilityUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private PointListAnnotation pointListAnnotation;

    [Header("UI")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Text legacyLabelText;
    [SerializeField] private string clothVisibleLabel = "Hide Cloth";
    [SerializeField] private string clothHiddenLabel = "Show Cloth";

    private void Awake()
    {
        TryResolveTarget();

        if (toggleButton == null)
        {
            toggleButton = GetComponent<Button>();
        }

        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(ToggleClothVisibility);
            toggleButton.onClick.AddListener(ToggleClothVisibility);
        }

        if (labelText == null && toggleButton != null)
        {
            labelText = toggleButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (legacyLabelText == null && toggleButton != null)
        {
            legacyLabelText = toggleButton.GetComponentInChildren<Text>(true);
        }

        RefreshLabel();
    }

    public void Initialize(PointListAnnotation target, Button button, TMP_Text tmpLabel, Text legacyLabel)
    {
        pointListAnnotation = target;
        toggleButton = button;
        labelText = tmpLabel;
        legacyLabelText = legacyLabel;
        RefreshLabel();
    }

    public void SetPointListAnnotation(PointListAnnotation target, bool forceShow)
    {
        pointListAnnotation = target;
        if (forceShow && pointListAnnotation != null)
        {
            pointListAnnotation.SetClothVisible(true);
        }
        RefreshLabel();
    }

    public void ToggleClothVisibility()
    {
        TryResolveTarget();
        if (pointListAnnotation == null)
        {
            return;
        }

        pointListAnnotation.ToggleClothVisible();
        RefreshLabel();
    }

    public void ShowCloth()
    {
        TryResolveTarget();
        if (pointListAnnotation == null)
        {
            return;
        }

        pointListAnnotation.SetClothVisible(true);
        RefreshLabel();
    }

    public void HideCloth()
    {
        TryResolveTarget();
        if (pointListAnnotation == null)
        {
            return;
        }

        pointListAnnotation.SetClothVisible(false);
        RefreshLabel();
    }

    public void RefreshLabel()
    {
        TryResolveTarget();
        bool isVisible = pointListAnnotation == null || pointListAnnotation.IsClothVisible();
        string next = isVisible ? clothVisibleLabel : clothHiddenLabel;

        if (labelText != null)
        {
            labelText.text = next;
        }

        if (legacyLabelText != null)
        {
            legacyLabelText.text = next;
        }
    }

    private void TryResolveTarget()
    {
        if (pointListAnnotation != null)
        {
            return;
        }

#if UNITY_2020_1_OR_NEWER
        pointListAnnotation = FindObjectOfType<PointListAnnotation>(true);
#else
        pointListAnnotation = FindObjectOfType<PointListAnnotation>();
#endif
    }
}
