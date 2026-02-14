using UnityEngine;
using UnityEngine.UI;

public class ServerEndpointRuntimeSwitcher : MonoBehaviour
{
    [Header("Optional UI")]
    [SerializeField] private InputField baseUrlInputField;
    [SerializeField] private Text statusText;

    private void Start()
    {
        RefreshUI();
    }

    public void ApplyFromInputField()
    {
        if (baseUrlInputField == null)
        {
            SetStatus("InputField is not assigned.");
            return;
        }

        ApplyBaseUrl(baseUrlInputField.text);
    }

    public void ApplyBaseUrl(string baseUrl)
    {
        ServerEndpointSettings.SetBaseUrl(baseUrl);
        RefreshUI();
    }

    public void ResetToDefault()
    {
        ServerEndpointSettings.ResetToDefault();
        RefreshUI();
    }

    public void RefreshUI()
    {
        string current = ServerEndpointSettings.GetBaseUrl();
        if (baseUrlInputField != null) baseUrlInputField.text = current;
        SetStatus("Server: " + current);
    }

    private void SetStatus(string message)
    {
        Debug.Log("[ServerEndpointRuntimeSwitcher] " + message);
        if (statusText != null) statusText.text = message;
    }
}
