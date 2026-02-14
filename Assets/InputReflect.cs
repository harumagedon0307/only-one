using UnityEngine;
using UnityEngine.UI;

public class InputReflectLegacy : MonoBehaviour
{
    public InputField inputField; // “ü—Í—“
    public Text targetText;       // ”½‰fæText

    void Start()
    {
        inputField.onValueChanged.AddListener(OnInputChanged);
    }

    void OnInputChanged(string value)
    {
        targetText.text = value;
    }
}
