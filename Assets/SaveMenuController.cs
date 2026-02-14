using UnityEngine;

public class SaveMenuController : MonoBehaviour
{
    public GameObject saveMenuPanel;           // SaveMenuPanel を入れる

    // 保存メニューを開く
    public void OpenSaveMenu()
    {
        saveMenuPanel.SetActive(true);
    }

    // 保存メニューを閉じる
    public void CloseSaveMenu()
    {
        saveMenuPanel.SetActive(false);
    }
}
