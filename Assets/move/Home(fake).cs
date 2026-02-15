using System;
using System.Collections;
using Mediapipe.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ToFakehome : MonoBehaviour
{
    private const string TargetSceneName = "New home";
    private bool isLoading = false;

    // Called from UI button OnClick
    public void Fakehome()
    {
        if (isLoading) return;

        if (!Application.CanStreamedLevelBeLoaded(TargetSceneName))
        {
            Debug.LogError($"Scene '{TargetSceneName}' is not enabled in Build Settings.");
            return;
        }

        StartCoroutine(LoadSceneSafely());
    }

    private IEnumerator LoadSceneSafely()
    {
        isLoading = true;

        StopMediapipeSafely();

        // Give camera/native resources a couple of frames to release before scene swap.
        yield return null;
        yield return null;

        SceneManager.LoadScene(TargetSceneName);
    }

    private static void StopMediapipeSafely()
    {
        var solutions = UnityEngine.Object.FindObjectsOfType<Solution>(true);
        foreach (var solution in solutions)
        {
            if (solution == null) continue;

            try
            {
                solution.Stop();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ToFakehome] Failed to stop solution: " + e.Message);
            }
        }

        var imageSource = ImageSourceProvider.ImageSource;
        if (imageSource != null)
        {
            try
            {
                imageSource.Stop();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ToFakehome] Failed to stop image source: " + e.Message);
            }
        }

    }
}
