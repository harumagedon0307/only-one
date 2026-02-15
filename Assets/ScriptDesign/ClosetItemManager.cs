using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;
using Siccity.GLTFUtility;
using UnityGLTF;

public class ClosetItemManager : MonoBehaviour
{
    [Header("UI References")]
    public Transform contentParent;
    public GameObject itemPrefab;
    
    [Header("Navigation")]
    public string nextSceneName = "DressUpScene";
    public int maxItems = 6;

    [Header("Choice UI")]
    public GameObject choicePanel;
    public Text messageText;
    public string choiceMessage = "どちらにしますか？";

    [Header("GLB Synthesis")]
    public string muscleGlbPath = "ムキムキ.glb";
    public string pythonBatPath = "C:\\path\\to\\your\\start_fastapi.bat";

    private string pendingSelectedId = "";

    void Start()
    {
        if (contentParent == null || itemPrefab == null)
        {
            Debug.LogError("ClosetItemManager: UIの参照が設定されていません。");
            return;
        }
        RefreshCloset();
    }

    public void RefreshCloset()
    {
        foreach (Transform child in contentParent) Destroy(child.gameObject);
        string dir = Path.Combine(Application.persistentDataPath, "Closet");
        Debug.Log($"【ClosetManager】クローゼット検索パス: {dir}");
        if (!Directory.Exists(dir))
        {
            Debug.Log($"【ClosetManager】ディレクトリが存在しないため作成します: {dir}");
            Directory.CreateDirectory(dir);
        }
        string[] files = Directory.GetFiles(dir, "前服Tops_*.png");
        Debug.Log($"【ClosetManager】見つかったファイルの数: {files.Length}");
        var regex = new Regex(@"前服Tops_(\d+)\.png$");
        int count = 0;
        foreach (string filePath in files)
        {
            if (count >= maxItems) break;
            var match = regex.Match(Path.GetFileName(filePath));
            if (match.Success)
            {
                count++;
                CreateItemButton(match.Groups[1].Value, filePath);
            }
        }
    }

    void CreateItemButton(string id, string filePath)
    {
        GameObject item = Instantiate(itemPrefab, contentParent);
        RawImage rawImage = item.GetComponentInChildren<RawImage>();
        if (rawImage != null)
        {
            Texture2D tex = LoadTexture(filePath);
            if (tex != null)
            {
                rawImage.texture = tex;
                rawImage.color = Color.white;
                rawImage.transform.SetAsFirstSibling();
            }
        }
        var texts = item.GetComponentsInChildren<Text>();
        foreach(var t in texts) if (t.text != "ボタン") t.text = "服 #" + id;
        Button btn = item.GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(() => OnItemSelected(id));
    }

    Texture2D LoadTexture(string path)
    {
        if (File.Exists(path))
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            if (tex.LoadImage(bytes)) return tex;
        }
        return null;
    }

    void OnItemSelected(string id)
    {
        pendingSelectedId = id;
        if (messageText != null) messageText.text = choiceMessage;
        if (choicePanel != null) choicePanel.SetActive(true);
    }

    public void SelectForEdit()
    {
        if (string.IsNullOrEmpty(pendingSelectedId)) return;
        PlayerPrefs.SetString("SelectedTopsId", pendingSelectedId);
        PlayerPrefs.Save();
        SceneManager.LoadScene(nextSceneName);
    }

    public void SelectForUpload()
    {
        if (string.IsNullOrEmpty(pendingSelectedId)) return;
        StartCoroutine(ProcessAndUploadRoutine(pendingSelectedId));
    }

    IEnumerator ProcessAndUploadRoutine(string id)
    {
        string dir = Path.Combine(Application.persistentDataPath, "Closet");
        Texture2D frontTex = LoadTexture(Path.Combine(dir, "前服Tops_" + id + ".png"));
        Texture2D backTex = LoadTexture(Path.Combine(dir, "後服Tops_" + id + ".png"));
        if (frontTex == null) yield break;

        StartFastApiServer();
        yield return new WaitForSeconds(1f);

        yield return StartCoroutine(EnsureGlbFileLocal());
        string glbFullPath = Path.Combine(Application.persistentDataPath, muscleGlbPath);
#if UNITY_EDITOR
        if (!File.Exists(glbFullPath)) glbFullPath = Path.Combine(Application.streamingAssetsPath, muscleGlbPath);
#endif
        GameObject muscleModel = GltfImportRuntimeHelper.LoadFromFileSafe(glbFullPath);
        if (muscleModel == null) yield break;

        ApplyTexturesToMuscle(muscleModel, frontTex, backTex);

        var glbExporter = gameObject.AddComponent<GlbExporter>();
        glbExporter.target = muscleModel;
        glbExporter.outputFileName = "closet_list_selected_model.glb";
        string path = glbExporter.ExportGLB();

        yield return StartCoroutine(gameObject.AddComponent<SendGlbToServer>().Send(path));
        Destroy(muscleModel);
        Destroy(glbExporter);
    }

    void ApplyTexturesToMuscle(GameObject muscleModel, Texture2D frontTex, Texture2D backTex)
    {
        Renderer[] renderers = muscleModel.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer rend in renderers)
        {
            string name = rend.name.ToLower();
            bool isFront = name.Contains(".002");
            bool isBack = name.Contains(".001");
            if (isFront || isBack)
            {
                Material[] mats = new Material[rend.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = new Material(Shader.Find("Standard"));
                    SetupTransparentMaterial(m);
                    m.mainTexture = isFront ? frontTex : backTex;
                    mats[i] = m;
                }
                rend.materials = mats;
            }
        }
    }

    void SetupTransparentMaterial(Material mat)
    {
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;
        mat.color = Color.white;
    }

    void StartFastApiServer()
    {
        if (string.IsNullOrEmpty(pythonBatPath) || !File.Exists(pythonBatPath)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = pythonBatPath, UseShellExecute = true }); } catch {}
    }

    public void StartNewDesign()
    {
        PlayerPrefs.DeleteKey("SelectedTopsId");
        PlayerPrefs.Save();
        SceneManager.LoadScene(nextSceneName);
    }

    IEnumerator EnsureGlbFileLocal()
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
        }
#endif
        yield break;
    }
}
