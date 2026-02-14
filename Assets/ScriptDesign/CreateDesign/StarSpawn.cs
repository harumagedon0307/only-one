using UnityEngine;
using UnityEngine.UI;

public class StarUIController : MonoBehaviour
{
    public Button spawnButton;
    public GameObject starPrefab; // Canvas 上の Image で作った星の Prefab
    public GameObject editMenuPrefab;

    void Start()
    {
        if (spawnButton != null)
            spawnButton.onClick.AddListener(SpawnStar);
    }

    public void SpawnStar()
    {
        GameObject star = Instantiate(starPrefab);

        star.transform.SetParent(LayerManager.Instance.GetCurrentLayer(), false);
        star.transform.localPosition = Vector3.zero;

        // RectTransform でサイズ変更
        RectTransform rect = star.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(200f, 200f); // ← 好きな初期サイズ
        }

        // Editable をアタッチ
        Editable editable = star.AddComponent<Editable>();
        editable.editMenuPrefab = editMenuPrefab;
    }

}
