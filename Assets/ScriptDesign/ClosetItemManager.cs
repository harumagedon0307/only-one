using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ClosetItemManager : MonoBehaviour
{
    [Header("UI References")]
    public Transform contentParent;
    public GameObject itemPrefab;

    [Header("Navigation")]
    public string nextSceneName = "DesignClot_ituki";
    public string[] editSceneFallbacks = { "DesignClot_ituki", "DesignCloth", "Tops" };
    public int maxItems = 6;

    [Header("Choice UI")]
    public GameObject choicePanel;
    public Text messageText;
    public string choiceMessage = "\u64CD\u4F5C\u3092\u9078\u629E\u3057\u3066\u304F\u3060\u3055\u3044";

    [Header("GLB Synthesis")]
    public string muscleGlbPath = "\u30E0\u30AD\u30E0\u30AD.glb";
    public string pythonBatPath = "C:\\path\\to\\your\\start_fastapi.bat";

    private string pendingSelectedId = "";
    private static ClosetItemManager activeInstance;
    private Button runtimeDeleteButton;

    private static readonly Regex TopsIdRegex = new Regex(@"Tops_(\d+)\.png$", RegexOptions.IgnoreCase);
    private const string FrontMark = "\u524D\u670D";
    private const string BackMark = "\u5F8C\u670D";

    void Awake()
    {
        if (activeInstance != null && activeInstance != this)
        {
            Debug.LogWarning("[ClosetItemManager] Duplicate manager detected. Disabling this instance.");
            enabled = false;
            return;
        }

        activeInstance = this;
    }

    void OnDestroy()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
        }
    }

    void Start()
    {
        if (contentParent == null || itemPrefab == null)
        {
            Debug.LogError("[ClosetItemManager] contentParent or itemPrefab is not assigned.");
            return;
        }

        EnsureChoicePanelButtons();
        NormalizeScrollViewForRuntime();
        RefreshCloset();
    }

    public void RefreshCloset()
    {
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        string dir = Path.Combine(Application.persistentDataPath, "Closet");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        List<string> frontFiles = GetFrontImageFiles(dir);
        int count = 0;
        int texturedCount = 0;

        foreach (string filePath in frontFiles)
        {
            if (count >= maxItems)
            {
                break;
            }

            string id = ExtractTopsId(filePath);
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            if (CreateItemButton(id, filePath))
            {
                texturedCount++;
            }
            count++;
        }

        ApplyContentLayoutFallback(count);
        Debug.Log($"[ClosetItemManager] Loaded items: {count} (textured={texturedCount}) from {dir}");
        if (count > 0 && texturedCount == 0)
        {
            Debug.LogWarning("[ClosetItemManager] Items were created, but textures were not loaded.");
        }
    }

    private static List<string> GetFrontImageFiles(string dir)
    {
        var result = new List<string>();
        if (!Directory.Exists(dir))
        {
            return result;
        }

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

            result.Add(filePath);
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

    private bool CreateItemButton(string id, string filePath)
    {
        GameObject item = Instantiate(itemPrefab);
        item.transform.SetParent(contentParent, false);
        item.SetActive(true);

        RectTransform itemRect = item.GetComponent<RectTransform>();
        if (itemRect != null)
        {
            itemRect.localScale = Vector3.one;
        }

        Texture2D tex = LoadTexture(filePath);
        RawImage[] images = item.GetComponentsInChildren<RawImage>(true);
        if (images == null || images.Length == 0)
        {
            RawImage fallbackImage = item.GetComponent<RawImage>();
            if (fallbackImage == null)
            {
                fallbackImage = item.AddComponent<RawImage>();
            }

            images = new[] { fallbackImage };
        }

        Texture previewTexture = tex != null ? tex : Texture2D.whiteTexture;
        Color previewColor = tex != null ? Color.white : new Color(0.85f, 0.85f, 0.85f, 1f);

        foreach (RawImage rawImage in images)
        {
            if (rawImage == null) continue;
            rawImage.texture = previewTexture;
            rawImage.color = previewColor;
            rawImage.gameObject.SetActive(true);
            rawImage.enabled = true;
        }

        Text[] texts = item.GetComponentsInChildren<Text>(true);
        foreach (Text t in texts)
        {
            if (t != null && t.text != "Button")
            {
                t.text = "Outfit #" + id;
            }
        }

        Button btn = item.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnItemSelected(id));
        }

        if (tex == null)
        {
            Debug.LogWarning($"[ClosetItemManager] Texture load failed: {filePath}");
        }

        return tex != null;
    }

    private void ApplyContentLayoutFallback(int itemCount)
    {
        RectTransform contentRect = contentParent as RectTransform;
        if (contentRect == null)
        {
            return;
        }

        GridLayoutGroup grid = contentParent.GetComponent<GridLayoutGroup>();
        if (grid != null && itemCount > 0)
        {
            RectTransform viewportRect = contentRect.parent as RectTransform;
            float viewportWidth = viewportRect != null ? viewportRect.rect.width : 0f;
            float viewportHeight = viewportRect != null ? viewportRect.rect.height : 0f;

            // Force horizontal list: one row, items extend to the right.
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = 1;

            int cols;
            int rows;
            cols = itemCount;
            rows = 1;

            float requiredWidth = grid.padding.left + grid.padding.right +
                                  (cols * grid.cellSize.x) + (Mathf.Max(0, cols - 1) * grid.spacing.x);
            float requiredHeight = grid.padding.top + grid.padding.bottom +
                                   (rows * grid.cellSize.y) + (Mathf.Max(0, rows - 1) * grid.spacing.y);

            Vector2 size = contentRect.sizeDelta;
            size.x = Mathf.Max(viewportWidth, requiredWidth);
            size.y = Mathf.Max(viewportHeight, requiredHeight);
            contentRect.sizeDelta = size;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
    }

    private void NormalizeScrollViewForRuntime()
    {
        RectTransform contentRect = contentParent as RectTransform;
        if (contentRect == null)
        {
            return;
        }

        RectTransform viewportRect = contentRect.parent as RectTransform;
        RectTransform scrollRect = viewportRect != null ? viewportRect.parent as RectTransform : null;

        if (viewportRect != null)
        {
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
        }

        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);
        contentRect.anchoredPosition = Vector2.zero;

        if (scrollRect != null)
        {
            ScrollRect sr = scrollRect.GetComponent<ScrollRect>();
            if (sr != null)
            {
                sr.horizontal = true;
                sr.vertical = false;
                sr.movementType = ScrollRect.MovementType.Clamped;
                sr.horizontalNormalizedPosition = 0f;
            }
        }

        if (scrollRect != null && choicePanel != null && scrollRect.parent == choicePanel.transform.parent)
        {
            int targetIndex = Mathf.Max(0, choicePanel.transform.GetSiblingIndex() - 1);
            if (scrollRect.GetSiblingIndex() != targetIndex)
            {
                scrollRect.SetSiblingIndex(targetIndex);
            }
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

    private void OnItemSelected(string id)
    {
        pendingSelectedId = id;

        if (messageText != null)
        {
            messageText.text = choiceMessage;
        }

        if (choicePanel != null)
        {
            choicePanel.SetActive(true);
        }
    }

    public void SelectForEdit()
    {
        if (string.IsNullOrEmpty(pendingSelectedId))
        {
            Debug.LogWarning("[ClosetItemManager] No item selected for edit.");
            return;
        }

        PlayerPrefs.SetString("SelectedTopsId", pendingSelectedId);
        PlayerPrefs.Save();
        if (choicePanel != null)
        {
            choicePanel.SetActive(false);
        }

        if (!TryLoadEditScene())
        {
            Debug.LogError("[ClosetItemManager] No editable scene found in Build Settings. Add your edit scene or set nextSceneName.");
        }
    }

    public void SelectForUpload()
    {
        if (string.IsNullOrEmpty(pendingSelectedId))
        {
            Debug.LogWarning("[ClosetItemManager] No item selected for upload.");
            return;
        }

        StartCoroutine(ProcessAndUploadRoutine(pendingSelectedId));
    }

    // Alias for UI bindings that expect "save".
    public void SelectForSave()
    {
        SelectForUpload();
    }

    public void DeleteSelectedItem()
    {
        if (string.IsNullOrEmpty(pendingSelectedId))
        {
            Debug.LogWarning("[ClosetItemManager] No item selected for delete.");
            return;
        }

        string dir = Path.Combine(Application.persistentDataPath, "Closet");
        if (!Directory.Exists(dir))
        {
            Debug.LogWarning("[ClosetItemManager] Closet directory not found.");
            return;
        }

        string[] targets = Directory.GetFiles(dir, "*Tops_" + pendingSelectedId + ".png");
        int deleted = 0;
        foreach (string file in targets)
        {
            try
            {
                File.Delete(file);
                deleted++;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ClosetItemManager] Failed to delete file: " + file + " | " + e.Message);
            }
        }

        Debug.Log($"[ClosetItemManager] Deleted design id={pendingSelectedId}, files={deleted}");
        pendingSelectedId = "";
        if (choicePanel != null)
        {
            choicePanel.SetActive(false);
        }

        RefreshCloset();
    }

    private IEnumerator ProcessAndUploadRoutine(string id)
    {
        string dir = Path.Combine(Application.persistentDataPath, "Closet");
        string frontPath = FindDesignImagePath(dir, id, true);
        string backPath = FindDesignImagePath(dir, id, false);

        Texture2D frontTex = LoadTexture(frontPath);
        Texture2D backTex = LoadTexture(backPath);

        if (frontTex == null)
        {
            Debug.LogError($"[ClosetItemManager] Front texture not found for Tops_{id}.");
            yield break;
        }

        StartFastApiServer();
        yield return new WaitForSeconds(1f);

        yield return StartCoroutine(EnsureGlbFileLocal());

        string glbFullPath = Path.Combine(Application.persistentDataPath, muscleGlbPath);
#if UNITY_EDITOR
        if (!File.Exists(glbFullPath))
        {
            glbFullPath = Path.Combine(Application.streamingAssetsPath, muscleGlbPath);
        }
#endif

        GameObject muscleModel = GltfImportRuntimeHelper.LoadFromFileSafe(glbFullPath);
        if (muscleModel == null)
        {
            Debug.LogError("[ClosetItemManager] Failed to load base GLB.");
            yield break;
        }

        ApplyTexturesToMuscle(muscleModel, frontTex, backTex);

        GlbExporter glbExporter = gameObject.AddComponent<GlbExporter>();
        glbExporter.target = muscleModel;
        glbExporter.outputFileName = "closet_list_selected_model.glb";
        string path = glbExporter.ExportGLB();

        SendGlbToServer sender = gameObject.AddComponent<SendGlbToServer>();
        yield return StartCoroutine(sender.Send(path));

        Destroy(sender);
        Destroy(muscleModel);
        Destroy(glbExporter);
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

    private void ApplyTexturesToMuscle(GameObject muscleModel, Texture2D frontTex, Texture2D backTex)
    {
        if (muscleModel == null)
        {
            return;
        }

        Renderer[] renderers = muscleModel.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer rend in renderers)
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
                SetupTransparentMaterial(m);
                m.mainTexture = tex;
                mats[i] = m;
            }

            rend.materials = mats;
        }
    }

    private static void SetupTransparentMaterial(Material mat)
    {
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;
        mat.color = Color.white;
    }

    private void StartFastApiServer()
    {
        if (string.IsNullOrEmpty(pythonBatPath) || !File.Exists(pythonBatPath))
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = pythonBatPath,
                UseShellExecute = true
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ClosetItemManager] Failed to start FastAPI script: " + e.Message);
        }
    }

    public void StartNewDesign()
    {
        PlayerPrefs.DeleteKey("SelectedTopsId");
        PlayerPrefs.Save();
        if (!TryLoadEditScene())
        {
            Debug.LogError("[ClosetItemManager] No editable scene found in Build Settings. Add your edit scene or set nextSceneName.");
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
                Debug.LogError("[ClosetItemManager] Failed to copy GLB from StreamingAssets: " + www.error);
            }
        }
#endif
        yield break;
    }

    private bool TryLoadEditScene()
    {
        string candidate = nextSceneName;
        if (string.Equals(candidate, "DressUpScene", StringComparison.Ordinal))
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
                    Debug.LogWarning($"[ClosetItemManager] Scene '{candidate}' not available. Fallback to '{fallback}'.");
                    SceneManager.LoadScene(fallback);
                    return true;
                }
            }
        }

        return false;
    }

    private void EnsureChoicePanelButtons()
    {
        if (choicePanel == null)
        {
            return;
        }

        Transform existingDelete = choicePanel.transform.Find("DeleteButton");
        if (existingDelete != null)
        {
            runtimeDeleteButton = existingDelete.GetComponent<Button>();
            if (runtimeDeleteButton != null)
            {
                runtimeDeleteButton.onClick.RemoveAllListeners();
                runtimeDeleteButton.onClick.AddListener(DeleteSelectedItem);
                SetButtonLabel(runtimeDeleteButton, "\u524A\u9664");
            }
        }

        Button[] buttons = choicePanel.GetComponentsInChildren<Button>(true);
        foreach (Button b in buttons)
        {
            if (b == null)
            {
                continue;
            }

            string method = GetFirstTargetMethodName(b);
            if (b.gameObject.name == "DeleteButton" || method == "DeleteSelectedItem")
            {
                runtimeDeleteButton = b;
            }
            else if (method == "SelectForUpload")
            {
                SetButtonLabel(b, "\u4FDD\u5B58");
            }
            else if (method == "SelectForEdit")
            {
                SetButtonLabel(b, "\u7DE8\u96C6");
            }
        }

        if (runtimeDeleteButton != null)
        {
            return;
        }

        GameObject deleteButtonObj = new GameObject("DeleteButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        deleteButtonObj.transform.SetParent(choicePanel.transform, false);

        RectTransform rect = deleteButtonObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(272f, 75f);
        rect.anchoredPosition = new Vector2(0f, -285f);
        rect.localScale = Vector3.one;

        Image img = deleteButtonObj.GetComponent<Image>();
        img.color = new Color(1f, 0.84f, 0.84f, 1f);

        runtimeDeleteButton = deleteButtonObj.GetComponent<Button>();
        runtimeDeleteButton.onClick.RemoveAllListeners();
        runtimeDeleteButton.onClick.AddListener(DeleteSelectedItem);

        GameObject labelObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObj.transform.SetParent(deleteButtonObj.transform, false);

        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text label = labelObj.GetComponent<Text>();
        label.text = "\u524A\u9664";
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = 20;
        label.color = new Color(0.2f, 0.1f, 0.1f, 1f);
        Font builtInFont = null;
        try
        {
            builtInFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ClosetItemManager] Failed to load LegacyRuntime.ttf: " + e.Message);
        }

        if (builtInFont == null)
        {
            try
            {
                builtInFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch (Exception)
            {
                // Some Unity versions removed Arial as built-in.
            }
        }

        if (builtInFont != null)
        {
            label.font = builtInFont;
        }
    }

    private static string GetFirstTargetMethodName(Button button)
    {
        if (button == null)
        {
            return string.Empty;
        }

        if (button.onClick == null || button.onClick.GetPersistentEventCount() <= 0)
        {
            return string.Empty;
        }

        return button.onClick.GetPersistentMethodName(0);
    }

    private static void SetButtonLabel(Button button, string text)
    {
        if (button == null)
        {
            return;
        }

        Text t = button.GetComponentInChildren<Text>(true);
        if (t != null)
        {
            t.text = text;
        }
    }
}
