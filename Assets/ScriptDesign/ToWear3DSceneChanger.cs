using UnityEngine;
using UnityEngine.SceneManagement;

public class ToWear3DSceneChanger : MonoBehaviour
{
    [Header("遷移先のシーン名（またはパス）")]
    public string sceneName = "Assets/Scenes/wear_to_3d.unity";

    /// <summary>
    /// ボタンの OnClick() などに登録して呼び出してください。
    /// </summary>
    public void ChangeToWear3D()
    {
        Debug.Log($"【SceneChanger】{sceneName} シーンへの遷移を開始します...");
        
        // シーンが存在するかチェック（Build Settings に登録されている必要がある）
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError($"【SceneChanger】エラー: シーン '{sceneName}' が Build Settings に登録されていないか、見つかりません。");
            
            // 部分一致でシーン名を検索して代替案を出すヒント
            Debug.LogWarning("Build Settings を確認し、目的のシーン（Assets/Scenes/wear_to_3d.unity など）が登録されているか確認してください。");
        }
    }
}
