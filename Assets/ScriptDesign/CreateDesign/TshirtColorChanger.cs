using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TShirtColorChanger : MonoBehaviour
{
    [SerializeField] private GameObject colorPanel;

    void Start()
    {
        colorPanel.SetActive(false);
    }

    public void OpenColorPanel()
    {
        colorPanel.SetActive(true);
    }

    public void CloseColorPanel()
    {
        colorPanel.SetActive(false);
    }

    // ▼ 共通：HEXカラーで色を設定
    public void SetHexColor(string hex)
    {
        Color c;
        if (ColorUtility.TryParseHtmlString(hex, out c))
        {
            ChangeColor(c);
        }
        else
        {
            Debug.LogWarning("HEXカラーコードが不正です: " + hex);
        }
    }

    // ▼ Tシャツに色を適用する本体
    public void ChangeColor(Color c)
    {
        Transform currentLayer = LayerManager.Instance.GetCurrentLayer();
        Image tshirtImage = currentLayer.GetComponentInChildren<Image>();

        if (tshirtImage != null)
        {
            tshirtImage.color = c;
        }
    }

    // ▼ ここから9色ボタン（HEX版）
    public void SetRed() { SetHexColor("#FF0000"); }
    public void SetLightBlue() { SetHexColor("#00FFFF"); }
    public void SetYellow() { SetHexColor("#FFFF00"); }
    public void SetBlue() { SetHexColor("#0000FF"); }
    public void SetLightGreen() { SetHexColor("#00FF00"); }
    public void SetWhite() { SetHexColor("#FFFFFF"); }
    public void SetBlack() { SetHexColor("#000000"); }
    public void SetBrown() { SetHexColor("#5F3200"); }
    public void SetPink() { SetHexColor("#FF00FF"); }
}
