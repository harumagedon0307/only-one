using System.IO;
using UnityEngine;

public static class LatestOutfitModelRegistry
{
    private const string LastPathKey = "HNW_LAST_OUTFIT_GLB_PATH";

    public static void SetPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string fullPath = path;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch
        {
            // Keep original value if path normalization fails.
        }

        PlayerPrefs.SetString(LastPathKey, fullPath);
        PlayerPrefs.Save();
        Debug.Log("[LatestOutfitModelRegistry] Updated: " + fullPath);
    }

    public static string GetExistingPath()
    {
        string saved = PlayerPrefs.GetString(LastPathKey, string.Empty);
        if (string.IsNullOrWhiteSpace(saved))
        {
            return null;
        }

        if (File.Exists(saved))
        {
            return saved;
        }

        return null;
    }
}
