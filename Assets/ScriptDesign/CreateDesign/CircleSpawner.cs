using UnityEngine;

public class CircleSpawner : MonoBehaviour
{
    public GameObject circlePrefab;
    public GameObject editMenuPrefab;

    public void SpawnCircle()
    {
        GameObject newCircle = Instantiate(circlePrefab, LayerManager.Instance.GetCurrentLayer());

        RectTransform rect = newCircle.GetComponent<RectTransform>();
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = new Vector3(1f, 1f, 4f);

        Editable editable = newCircle.AddComponent<Editable>();
        editable.editMenuPrefab = editMenuPrefab;
    }

}
