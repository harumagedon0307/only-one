using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonScript : MonoBehaviour
{
    private const string TargetScenePath = "Assets/Scenes/wear_to_3d.unity";
    private bool isLoading = false;

    // Called from UI button OnClick
    public void OnClick()
    {
        if (isLoading) return;

        if (!Application.CanStreamedLevelBeLoaded(TargetScenePath))
        {
            Debug.LogError($"Scene '{TargetScenePath}' is not enabled in Build Settings.");
            return;
        }

        isLoading = true;
        SceneManager.LoadScene(TargetScenePath);
    }
}
