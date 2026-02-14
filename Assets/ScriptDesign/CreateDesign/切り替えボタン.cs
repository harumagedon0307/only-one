using UnityEngine;

public class LayerManager : MonoBehaviour
{
    public static LayerManager Instance;

    [Header("Layer Objects")]
    public Transform frontLayer;
    public Transform backLayer;

    [Header("Toggle Buttons")]
    public GameObject buttonF;  // 「前」ボタン
    public GameObject buttonB;  // 「後」ボタン

    public bool isFront = true;

    void Awake()
    {
        Instance = this;
    }

    // UIから呼ぶ
    public void Toggle()
    {
        isFront = !isFront;

        // 表示切替
        frontLayer.gameObject.SetActive(isFront);
        backLayer.gameObject.SetActive(!isFront);

        // ボタン切替
        buttonF.SetActive(isFront);
        buttonB.SetActive(!isFront);
    }

    public Transform GetCurrentLayer()
    {
        return isFront ? frontLayer : backLayer;
    }
}
