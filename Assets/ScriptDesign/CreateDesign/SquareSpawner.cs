using UnityEngine;
using UnityEngine.UI;

public class SquareSpawner : MonoBehaviour
{
    public GameObject squarePrefab;
    public GameObject editMenuPrefab;

    public void Squarespawner()
    {
        if (squarePrefab == null || LayerManager.Instance == null)
        {
            Debug.LogWarning("SquareSpawner: squarePrefab or LayerManager is missing.");
            return;
        }

        GameObject newSquare = Instantiate(squarePrefab, LayerManager.Instance.GetCurrentLayer());

        RectTransform rect = newSquare.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(100f, 100f);
        }

        Editable editable = newSquare.GetComponent<Editable>();
        if (editable == null)
        {
            editable = newSquare.AddComponent<Editable>();
        }
        editable.editMenuPrefab = editMenuPrefab;
    }
}
