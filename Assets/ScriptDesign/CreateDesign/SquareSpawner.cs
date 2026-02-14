using UnityEngine;
using UnityEngine.UI;

public class SquareSpawner : MonoBehaviour
{
    public GameObject squarePrefab;
    public GameObject editMenuPrefab;

    public void Squarespawner()
    {
        GameObject newSquare = Instantiate(squarePrefab, LayerManager.Instance.GetCurrentLayer());

        RectTransform rect = newSquare.GetComponent<RectTransform>();
        rect.anchoredPosition = Vector2.zero;

        // rect.localScale = new Vector3(0.25f, 0.25f, 1f); ←削除
        rect.sizeDelta = new Vector2(100f, 100f); // 好きな初期サイズ

        Editable editable = newSquare.AddComponent<Editable>();
        editable.editMenuPrefab = editMenuPrefab;
    }
}



