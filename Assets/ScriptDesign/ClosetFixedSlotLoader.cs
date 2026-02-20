using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ClosetFixedSlotLoader : MonoBehaviour
{
    public RawImage[] slotImages;
    public Button[] slotButtons;
    public string filePrefix = "\u524D\u670DTops_";
    public string nextSceneName = "DesignClot_ituki";
    public string[] editSceneFallbacks = { "DesignClot_ituki", "DesignCloth", "Tops" };
    public string muscleGlbPath = "\u30E0\u30AD\u30E0\u30AD.glb";
    public string pythonBatPath = "C:\\path\\to\\your\\start_fastapi.bat";
    public GameObject choicePanel;
    public Text messageText;

    private string pendingSelectedId = "";

    private static readonly Regex TopsIdRegex = new Regex(@"Tops_(\d+)\.png$", RegexOptions.IgnoreCase);
    private const string FrontMark = "\u524D\u670D";
    private const string BackMark = "\u5F8C\u670D";

    void Start()
    {
        EnsureSlotReferences();
        LoadItemsToSlots();
    }

    private void EnsureSlotReferences()
    {
        bool needImages = slotImages == null || slotImages.Length == 0;
        bool needButtons = slotButtons == null || slotButtons.Length == 0;
        if (!needImages && !needButtons)
        {
            return;
        }

        var imageList = new List<RawImage>();
        foreach (RawImage img in Resources.FindObjectsOfTypeAll<RawImage>())
        {
            if (img == null || img.transform == null) continue;
            if (!img.gameObject.scene.IsValid() || !img.gameObject.scene.isLoaded) continue;
            string n = img.gameObject.name;
            if (n.StartsWith("Image (") && n.EndsWith(")"))
            {
                imageList.Add(img);
            }
        }
        imageList.Sort((a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));

        if (needImages)
        {
            slotImages = imageList.ToArray();
        }

        if (needButtons)
        {
            var buttonList = new List<Button>(imageList.Count);
            foreach (RawImage img in imageList)
            {
                Button btn = img.GetComponentInChildren<Button>(true);
                if (btn != null)
                {
                    buttonList.Add(btn);
                }
            }
            slotButtons = buttonList.ToArray();
        }

        Debug.Log($"[ClosetFixedSlotLoader] Auto-bound slots: images={slotImages?.Length ?? 0}, buttons={slotButtons?.Length ?? 0}");
    }

    public void LoadItemsToSlots()
    {
        if (slotImages == null || slotButtons == null)
        {
            Debug.LogError("[ClosetFixedSlotLoader] slotImages/slotButtons is not assigned.");
            return;
        }

        foreach (RawImage img in slotImages)
        {
            if (img != null)
            {
                img.enabled = false;
                img.texture = null;
            }
        }

        foreach (Button btn in slotButtons)
        {
            if (btn != null)
            {
                btn.gameObject.SetActive(false);
                btn.onClick.RemoveAllListeners();
            }
        }

        string dir = Path.Combine(Application.persistentDataPath, "Closet");
        if (!Directory.Exists(dir))
        {
            Debug.LogWarning("[ClosetFixedSlotLoader] Closet directory does not exist: " + dir);
            return;
        }

        List<string> frontFiles = GetFrontImageFiles(dir);
        int index = 0;

        foreach (string filePath in frontFiles)
        {
            if (index >= slotImages.Length || index >= slotButtons.Length)
            {
                break;
            }

            string id = ExtractTopsId(filePath);
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            FillSlot(index, id, filePath);
            index++;
        }

        Debug.Log($"[ClosetFixedSlotLoader] Loaded slots: {index}");
    }

    private List<string> GetFrontImageFiles(string dir)
    {
        var result = new List<string>();
        string[] files = Directory.GetFiles(dir, "*Tops_*.png");

        foreach (string filePath in files)
        {
            string fileName = Path.GetFileName(filePath);
            if (!TopsIdRegex.IsMatch(fileName))
            {
                continue;
            }

            if (IsBackFileName(fileName))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(filePrefix) && fileName.StartsWith(filePrefix))
            {
                result.Insert(0, filePath);
            }
            else
            {
                result.Add(filePath);
            }
        }

        result.Sort((a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));
        return result;
    }

    private static bool IsFrontFileName(string fileName)
    {
        string lower = fileName.ToLowerInvariant();
        return fileName.Contains(FrontMark) || lower.Contains("front");
    }

    private static bool IsBackFileName(string fileName)
    {
        string lower = fileName.ToLowerInvariant();
        return fileName.Contains(BackMark) || lower.Contains("back");
    }

    private static string ExtractTopsId(string filePath)
    {
        Match match = TopsIdRegex.Match(Path.GetFileName(filePath));
        return match.Success ? match.Groups[1].Value : null;
    }

    private void FillSlot(int index, string id, string filePath)
    {
        Texture2D tex = LoadTexture(filePath);
        if (slotImages[index] != null)
        {
            slotImages[index].texture = tex;
            slotImages[index].enabled = tex != null;
        }

        if (slotButtons[index] != null)
        {
            slotButtons[index].gameObject.SetActive(true);
            slotButtons[index].onClick.RemoveAllListeners();
            slotButtons[index].onClick.AddListener(() =>
            {
                pendingSelectedId = id;
                if (messageText != null)
                {
                    messageText.text = "Select action";
                }

                if (choicePanel != null)
                {
                    choicePanel.SetActive(true);
                }
            });
        }
    }

    private static Texture2D LoadTexture(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        byte[] bytes = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2);
        return tex.LoadImage(bytes) ? tex : null;
    }

    public void SelectForEdit()
    {
        if (string.IsNullOrEmpty(pendingSelectedId))
        {
            Debug.LogWarning("[ClosetFixedSlotLoader] No item selected for edit.");
            return;
        }

        PlayerPrefs.SetString("SelectedTopsId", pendingSelectedId);
        PlayerPrefs.Save();
        if (!TryLoadEditScene())
        {
            Debug.LogError("[ClosetFixedSlotLoader] No editable scene found in Build Settings. Add DesignClot_ituki to Build Settings.");
        }
    }

    public void SelectForUpload()
    {
        if (string.IsNullOrEmpty(pendingSelectedId))
        {
            Debug.LogWarning("[ClosetFixedSlotLoader] No item selected for upload.");
            return;
        }

        StartCoroutine(ProcessAndUploadRoutine(pendingSelectedId));
    }

    private IEnumerator ProcessAndUploadRoutine(string id)
    {
        string dir = Path.Combine(Application.persistentDataPath, "Closet");
        Texture2D frontTex = LoadTexture(FindDesignImagePath(dir, id, true));
        Texture2D backTex = LoadTexture(FindDesignImagePath(dir, id, false));
        if (frontTex == null)
        {
            Debug.LogError($"[ClosetFixedSlotLoader] Front texture not found for Tops_{id}.");
            yield break;
        }

        yield return StartCoroutine(EnsureGlbFileLocal());

        string glbFullPath = Path.Combine(Application.persistentDataPath, muscleGlbPath);
#if UNITY_EDITOR
        if (!File.Exists(glbFullPath))
        {
            glbFullPath = Path.Combine(Application.streamingAssetsPath, muscleGlbPath);
        }
#endif

        GameObject model = GltfImportRuntimeHelper.LoadFromFileSafe(glbFullPath);
        if (model == null)
        {
            Debug.LogError("[ClosetFixedSlotLoader] Failed to load base GLB.");
            yield break;
        }

        ApplyTexturesToMuscle(model, frontTex, backTex);

        GlbExporter exporter = gameObject.AddComponent<GlbExporter>();
        exporter.target = model;
        exporter.outputFileName = "closet_selected_model.glb";
        string path = exporter.ExportGLB();

        SendGlbToServer sender = gameObject.AddComponent<SendGlbToServer>();
        yield return StartCoroutine(sender.Send(path));

        Destroy(sender);
        Destroy(model);
        Destroy(exporter);
    }

    private static string FindDesignImagePath(string dir, string id, bool wantFront)
    {
        if (!Directory.Exists(dir))
        {
            return null;
        }

        string[] exactCandidates = wantFront
            ? new[] { FrontMark + "Tops_" + id + ".png", "FrontTops_" + id + ".png", "frontTops_" + id + ".png" }
            : new[] { BackMark + "Tops_" + id + ".png", "BackTops_" + id + ".png", "backTops_" + id + ".png" };

        foreach (string fileName in exactCandidates)
        {
            string full = Path.Combine(dir, fileName);
            if (File.Exists(full))
            {
                return full;
            }
        }

        string[] files = Directory.GetFiles(dir, "*Tops_" + id + ".png");
        if (files == null || files.Length == 0)
        {
            return null;
        }

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            if (wantFront && IsFrontFileName(name))
            {
                return file;
            }

            if (!wantFront && IsBackFileName(name))
            {
                return file;
            }
        }

        return wantFront ? files[0] : null;
    }

    private static void ApplyTexturesToMuscle(GameObject model, Texture2D frontTex, Texture2D backTex)
    {
        if (model == null)
        {
            return;
        }

        foreach (Renderer rend in model.GetComponentsInChildren<Renderer>(true))
        {
            string name = rend.name.ToLowerInvariant();
            bool isFront = name.Contains(".002") || name.Contains("front");
            bool isBack = name.Contains(".001") || name.Contains("back");

            if (!isFront && !isBack)
            {
                continue;
            }

            Texture2D tex = isBack ? (backTex != null ? backTex : frontTex) : frontTex;
            if (tex == null)
            {
                continue;
            }

            Material[] mats = new Material[rend.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = new Material(Shader.Find("Standard"));
                m.SetFloat("_Mode", 3);
                m.EnableKeyword("_ALPHABLEND_ON");
                m.renderQueue = 3000;
                m.mainTexture = tex;
                mats[i] = m;
            }

            rend.materials = mats;
        }
    }

    private IEnumerator EnsureGlbFileLocal()
    {
        string destPath = Path.Combine(Application.persistentDataPath, muscleGlbPath);
#if !UNITY_EDITOR && UNITY_ANDROID
        if (!File.Exists(destPath))
        {
            string streamingPath = Path.Combine(Application.streamingAssetsPath, muscleGlbPath);
            var www = UnityEngine.Networking.UnityWebRequest.Get(streamingPath);
            yield return www.SendWebRequest();
            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                File.WriteAllBytes(destPath, www.downloadHandler.data);
            }
            else
            {
                Debug.LogError("[ClosetFixedSlotLoader] Failed to copy GLB from StreamingAssets: " + www.error);
            }
        }
#endif
        yield break;
    }

    private bool TryLoadEditScene()
    {
        string candidate = nextSceneName;
        if (candidate == "DressUpScene")
        {
            candidate = "DesignClot_ituki";
        }

        if (!string.IsNullOrEmpty(candidate) && Application.CanStreamedLevelBeLoaded(candidate))
        {
            SceneManager.LoadScene(candidate);
            return true;
        }

        if (editSceneFallbacks != null)
        {
            foreach (string fallback in editSceneFallbacks)
            {
                if (string.IsNullOrEmpty(fallback))
                {
                    continue;
                }

                if (Application.CanStreamedLevelBeLoaded(fallback))
                {
                    Debug.LogWarning($"[ClosetFixedSlotLoader] Scene '{candidate}' not available. Fallback to '{fallback}'.");
                    SceneManager.LoadScene(fallback);
                    return true;
                }
            }
        }

        return false;
    }
}
