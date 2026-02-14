using UnityEngine;
using UnityEngine.UI;

public class EditMenu : MonoBehaviour
{
    private GameObject targetSquare;

    public Button deleteButton;
    public Button toggleEditButton;

    [Header("Color Palette Buttons")]
    public Button[] colorButtons;       // パレット用ボタン
    public Color[] buttonColors;        // 各ボタンに対応する色

    private bool isEditing = false;

    public void SetTarget(GameObject square)
    {
        targetSquare = square;
    }

    void Start()
    {
        deleteButton.onClick.AddListener(DeleteSquare);
        toggleEditButton.onClick.AddListener(ToggleEditMode);

        // パレットボタンに色反映を登録
        for (int i = 0; i < colorButtons.Length; i++)
        {
            int index = i; // クロージャ用
            colorButtons[i].onClick.AddListener(() => SelectColor(buttonColors[index]));
        }
    }

    public void SelectColor(Color color)
    {
        if (targetSquare == null) return;

        var img = targetSquare.GetComponent<Image>();
        if (img != null)
        {
            img.color = color;
            return;
        }

        var renderer = targetSquare.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = color;
        }
    }

    public void DeleteSquare()
    {
        Destroy(targetSquare);
        CloseMenu();
    }

    public void ToggleEditMode()
    {
        if (targetSquare == null) return;

        var editable = targetSquare.GetComponent<Editable>();
        if (editable == null) return;

        isEditing = !isEditing;
        editable.SetEditing(isEditing);

        if (toggleEditButton != null)
        {
            var text = toggleEditButton.GetComponentInChildren<Text>();
            if (text != null)
                text.text = isEditing ? "移動OFF" : "移動ON";
        }

        if (!isEditing)
        {
            editable.CloseEditMenuFromScript();
            CloseMenu();
        }
    }

    public void CloseMenu()
    {
        Destroy(gameObject);
    }
}
