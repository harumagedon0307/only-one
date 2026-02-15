using UnityEngine;

public class CircleSpawner : MonoBehaviour
{
    public GameObject circlePrefab;
    public GameObject editMenuPrefab;

    public void SpawnCircle()
    {
        if (circlePrefab == null || LayerManager.Instance == null)
        {
            Debug.LogWarning("CircleSpawner: circlePrefab or LayerManager is missing.");
            return;
        }

        GameObject newCircle = Instantiate(circlePrefab, LayerManager.Instance.GetCurrentLayer());

        RectTransform rect = newCircle.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = new Vector3(1f, 1f, 4f);
        }

        Editable editable = newCircle.GetComponent<Editable>();
        if (editable == null)
        {
            editable = newCircle.AddComponent<Editable>();
        }
        editable.editMenuPrefab = editMenuPrefab;
    }
}
