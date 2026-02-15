using UnityEngine;
using UnityEngine.UI;

public class StarUIController : MonoBehaviour
{
    public Button spawnButton;
    public GameObject starPrefab;
    public GameObject editMenuPrefab;

    void Start()
    {
        if (spawnButton != null)
        {
            spawnButton.onClick.AddListener(SpawnStar);
        }
    }

    public void SpawnStar()
    {
        if (starPrefab == null || LayerManager.Instance == null)
        {
            Debug.LogWarning("StarUIController: starPrefab or LayerManager is missing.");
            return;
        }

        GameObject star = Instantiate(starPrefab);
        star.transform.SetParent(LayerManager.Instance.GetCurrentLayer(), false);
        star.transform.localPosition = Vector3.zero;

        RectTransform rect = star.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(200f, 200f);
        }

        Editable editable = star.GetComponent<Editable>();
        if (editable == null)
        {
            editable = star.AddComponent<Editable>();
        }
        editable.editMenuPrefab = editMenuPrefab;
    }
}
