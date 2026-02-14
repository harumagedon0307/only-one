using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;
using Siccity.GLTFUtility;
using UnityGLTF;

public class ClosetFixedSlotLoader : MonoBehaviour
{
    public RawImage[] slotImages;
    public Button[] slotButtons;
    public string filePrefix = "前服Tops_"; 
    public string nextSceneName = "DressUpScene";
    public string muscleGlbPath = "ムキムキ.glb"; 
    public string pythonBatPath = "C:\\path\\to\\your\\start_fastapi.bat";
    public GameObject choicePanel;
    public Text messageText;
    private string pendingSelectedId = "";

    void Start() { LoadItemsToSlots(); }

    public void LoadItemsToSlots()
    {
        if (slotImages == null) return;
        foreach (var img in slotImages) if (img != null) img.enabled = false; 
        foreach (var btn in slotButtons) if (btn != null) btn.gameObject.SetActive(false);

        string dir = Path.Combine(Application.persistentDataPath, "Closet");
        Debug.Log($"【ClosetSlotLoader】検索パス: {dir}");
        if (!Directory.Exists(dir))
        {
            Debug.Log($"【ClosetSlotLoader】ディレクトリが存在しません: {dir}");
            return;
        }

        string[] files = Directory.GetFiles(dir, filePrefix + "*.png");
        Debug.Log($"【ClosetSlotLoader】見つかったファイルの数: {files.Length}");
        var regex = new Regex(Regex.Escape(filePrefix) + @"(\d+)\.png$");
        int index = 0;
        foreach (string filePath in files)
        {
            if (index >= slotImages.Length) break;
            var match = regex.Match(Path.GetFileName(filePath));
            if (match.Success) { FillSlot(index, match.Groups[1].Value, filePath); index++; }
        }
    }

    void FillSlot(int index, string id, string filePath)
    {
        Texture2D tex = LoadTexture(filePath);
        if (tex != null) { slotImages[index].texture = tex; slotImages[index].enabled = true; }
        slotButtons[index].gameObject.SetActive(true);
        slotButtons[index].onClick.RemoveAllListeners();
        slotButtons[index].onClick.AddListener(() => { pendingSelectedId = id; choicePanel.SetActive(true); });
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

    public void SelectForEdit()
    {
        PlayerPrefs.SetString("SelectedTopsId", pendingSelectedId);
        PlayerPrefs.Save();
        SceneManager.LoadScene(nextSceneName);
    }

    public void SelectForUpload() { StartCoroutine(ProcessAndUploadRoutine(pendingSelectedId)); }

    IEnumerator ProcessAndUploadRoutine(string id)
    {
        string dir = Path.Combine(Application.persistentDataPath, "Closet");
        Texture2D frontTex = LoadTexture(Path.Combine(dir, "前服Tops_" + id + ".png"));
        Texture2D backTex = LoadTexture(Path.Combine(dir, "後服Tops_" + id + ".png"));
        if (frontTex == null) yield break;

        yield return StartCoroutine(EnsureGlbFileLocal());
        string glbFullPath = Path.Combine(Application.persistentDataPath, muscleGlbPath);
#if UNITY_EDITOR
        if (!File.Exists(glbFullPath)) glbFullPath = Path.Combine(Application.streamingAssetsPath, muscleGlbPath);
#endif
        GameObject model = Importer.LoadFromFile(glbFullPath);
        ApplyTexturesToMuscle(model, frontTex, backTex);

        var exporter = gameObject.AddComponent<GlbExporter>();
        exporter.target = model;
        exporter.outputFileName = "closet_selected_model.glb";
        string path = exporter.ExportGLB();

        yield return StartCoroutine(gameObject.AddComponent<SendGlbToServer>().Send(path));
        Destroy(model);
        Destroy(exporter);
    }

    void ApplyTexturesToMuscle(GameObject model, Texture2D frontTex, Texture2D backTex)
    {
        foreach (Renderer rend in model.GetComponentsInChildren<Renderer>(true))
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
                    m.SetFloat("_Mode", 3);
                    m.EnableKeyword("_ALPHABLEND_ON");
                    m.renderQueue = 3000;
                    m.mainTexture = isFront ? frontTex : backTex;
                    mats[i] = m;
                }
                rend.materials = mats;
            }
        }
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
