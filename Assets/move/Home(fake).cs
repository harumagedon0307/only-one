using UnityEngine;
using UnityEngine.SceneManagement;

public class ToFakehome : MonoBehaviour
{
    private const string TargetSceneName = "New home";

    // Called from UI button OnClick
    public void Fakehome()
    {
        if (!Application.CanStreamedLevelBeLoaded(TargetSceneName))
        {
            Debug.LogError($"Scene '{TargetSceneName}' is not enabled in Build Settings.");
            return;
        }

        SceneManager.LoadScene(TargetSceneName);
    }
}
