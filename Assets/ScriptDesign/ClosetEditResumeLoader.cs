using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ClosetEditResumeLoader
{
    private const string TargetSceneName = "DesignClot_ituki";
    private const string SelectedIdKey = "SelectedTopsId";
    private const string ClosetDirName = "Closet";
    private const string FrontMark = "\u524D\u670D";
    private const string BackMark = "\u5F8C\u670D";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplySelectedDesignOnSceneLoad()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.name, TargetSceneName, StringComparison.Ordinal))
        {
            return;
        }

        string selectedId = PlayerPrefs.GetString(SelectedIdKey, string.Empty);
        if (string.IsNullOrEmpty(selectedId))
        {
            return;
        }

        string closetDir = Path.Combine(Application.persistentDataPath, ClosetDirName);
        if (!Directory.Exists(closetDir))
        {
            Debug.LogWarning("[ClosetEditResume] Closet directory not found: " + closetDir);
            return;
        }

        RectTransform frontCloth = FindRectTransformInScene(scene, FrontMark, "Front", "FrontLayer");
        RectTransform backCloth = FindRectTransformInScene(scene, BackMark, "Back", "BackLayer");

        if (frontCloth == null && backCloth == null)
        {
            Debug.LogWarning("[ClosetEditResume] Front/Back cloth targets were not found in scene.");
            return;
        }

        string frontPath = FindDesignImagePath(closetDir, selectedId, true);
        string backPath = FindDesignImagePath(closetDir, selectedId, false);
        if (string.IsNullOrEmpty(backPath))
        {
            backPath = frontPath;
        }

        bool frontApplied = ApplyTextureToRect(frontCloth, frontPath);
        bool backApplied = ApplyTextureToRect(backCloth, backPath);

        if (frontApplied || backApplied)
        {
            Debug.Log($"[ClosetEditResume] Restored design id={selectedId} front={frontApplied} back={backApplied}");
        }
        else
        {
            Debug.LogWarning($"[ClosetEditResume] Failed to restore design id={selectedId}");
        }
    }

    private static RectTransform FindRectTransformInScene(Scene scene, params string[] preferredNames)
    {
        RectTransform[] allRects = Resources.FindObjectsOfTypeAll<RectTransform>();

        foreach (string preferred in preferredNames)
        {
            if (string.IsNullOrEmpty(preferred))
            {
                continue;
            }

            foreach (RectTransform rect in allRects)
            {
                if (rect == null || !rect.gameObject.scene.IsValid() || rect.gameObject.scene.handle != scene.handle)
                {
                    continue;
                }

                if (string.Equals(rect.gameObject.name, preferred, StringComparison.OrdinalIgnoreCase))
                {
                    return rect;
                }
            }
        }

        foreach (string preferred in preferredNames)
        {
            if (string.IsNullOrEmpty(preferred))
            {
                continue;
            }

            foreach (RectTransform rect in allRects)
            {
                if (rect == null || !rect.gameObject.scene.IsValid() || rect.gameObject.scene.handle != scene.handle)
                {
                    continue;
                }

                if (rect.gameObject.name.IndexOf(preferred, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return rect;
                }
            }
        }

        return null;
    }

    private static string FindDesignImagePath(string dir, string id, bool wantFront)
    {
        string[] exactCandidates = wantFront
            ? new[]
            {
                FrontMark + "Tops_" + id + ".png",
                "FrontTops_" + id + ".png",
                "frontTops_" + id + ".png"
            }
            : new[]
            {
                BackMark + "Tops_" + id + ".png",
                "BackTops_" + id + ".png",
                "backTops_" + id + ".png"
            };

        foreach (string fileName in exactCandidates)
        {
            string fullPath = Path.Combine(dir, fileName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        string[] files = Directory.GetFiles(dir, "*_" + id + ".png");
        if (files.Length == 0)
        {
            files = Directory.GetFiles(dir, "*" + id + ".png");
        }
        foreach (string filePath in files)
        {
            string fileName = Path.GetFileName(filePath);
            string lower = fileName.ToLowerInvariant();

            if (wantFront && (fileName.Contains(FrontMark) || lower.Contains("front")))
            {
                return filePath;
            }

            if (!wantFront && (fileName.Contains(BackMark) || lower.Contains("back")))
            {
                return filePath;
            }
        }

        if (files.Length > 0)
        {
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            if (!wantFront && files.Length > 1)
            {
                return files[1];
            }

            return files[0];
        }

        return null;
    }

    private static bool ApplyTextureToRect(RectTransform target, string imagePath)
    {
        if (target == null || string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(imagePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ClosetEditResume] Failed to read image: " + imagePath + " | " + e.Message);
            return false;
        }

        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(bytes))
        {
            Debug.LogWarning("[ClosetEditResume] Failed to decode image: " + imagePath);
            return false;
        }

        Image image = target.GetComponent<Image>();
        if (image != null)
        {
            Sprite sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            image.sprite = sprite;
            image.color = Color.white;
            return true;
        }

        RawImage rawImage = target.GetComponent<RawImage>();
        if (rawImage != null)
        {
            rawImage.texture = tex;
            rawImage.color = Color.white;
            return true;
        }

        Debug.LogWarning("[ClosetEditResume] Target has no Image/RawImage: " + target.name);
        return false;
    }
}
