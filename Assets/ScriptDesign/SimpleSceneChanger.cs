using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleSceneChanger : MonoBehaviour
{
    [Header("移動先のシーン名")]
    public string targetSceneName = "ToDesignCloth_ituki";

    [Header("新規作成として開くか？")]
    public bool openAsNewDesign = true;

    /// <summary>
    /// ボタンのOnClickに登録するメソッド
    /// </summary>
    public void ChangeScene()
    {
        if (openAsNewDesign)
        {
            // IDを消去して、確実に「新規作成モード」にする
            PlayerPrefs.DeleteKey("SelectedTopsId");
            PlayerPrefs.Save();
            Debug.Log("【SceneChanger】新規作成モードでシーン遷移します。");
        }

        SceneManager.LoadScene(targetSceneName);
    }
}
