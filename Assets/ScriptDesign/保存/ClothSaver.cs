using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Siccity.GLTFUtility;
using UnityGLTF;

public class ClothSaver : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform frontCloth;
    public RectTransform backCloth;
    public Text warningText;
    
    private string loadedIdFromCloset = null;

    private const int SAVE_WIDTH = 593;
    private const int SAVE_HEIGHT = 486;

    [Header("File Paths")]
    public string muscleGlbPath = "ムキムキ.glb";
    public string pythonBatPath = "C:\\path\\to\\your\\start_fastapi.bat";

    void Start()
    {
        if (PlayerPrefs.HasKey("SelectedTopsId"))
        {
            loadedIdFromCloset = PlayerPrefs.GetString("SelectedTopsId");
            UnityEngine.Debug.Log($"【ClothSaver】クローゼットID={loadedIdFromCloset} を読み込みました");
        }
    }

    // 保存ボタン（前後一括）
    public void Save()
    {
        UnityEngine.Debug.Log("【ClothSaver】保存ボタンが押されました");
        StartCoroutine(SaveRoutine(forceNew: false));
    }

    // 互換性のために残す（インスペクターで設定済みの可能性があるため）
    public void SaveFront() { Save(); }
    public void SaveBack() { Save(); }
    public void SaveAsNew() { StartCoroutine(SaveRoutine(forceNew: true)); }

    IEnumerator SaveRoutine(bool forceNew)
    {
        if (frontCloth == null || backCloth == null)
        {
            ShowWarning("UIの参照が設定されていません");
            yield break;
        }

        HideWarning();

        UnityEngine.Debug.Log("【ClothSaver】前後の画像をキャプチャ中...");
        Texture2D frontTex = CaptureHiddenUI(frontCloth, SAVE_WIDTH, SAVE_HEIGHT);
        RemoveBlackBackground(frontTex);
        
        Texture2D backTex = CaptureHiddenUI(backCloth, SAVE_WIDTH, SAVE_HEIGHT);
        RemoveBlackBackground(backTex);

        string topsId = GetOrCreateTopsId(forceNew);
        string closetDir = Path.Combine(Application.persistentDataPath, "Closet");
        if (!Directory.Exists(closetDir)) Directory.CreateDirectory(closetDir);

        File.WriteAllBytes(Path.Combine(closetDir, "前服Tops_" + topsId + ".png"), frontTex.EncodeToPNG());
        File.WriteAllBytes(Path.Combine(closetDir, "後服Tops_" + topsId + ".png"), backTex.EncodeToPNG());

        UnityEngine.Debug.Log($"【ClothSaver】PNG保存完了: ID={topsId}");

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif

        StartFastApiServer();
        yield return new WaitForSeconds(1f);

        yield return StartCoroutine(EnsureGlbFileLocal());
        string glbFullPath = Path.Combine(Application.persistentDataPath, muscleGlbPath);
#if UNITY_EDITOR
        if (!File.Exists(glbFullPath)) glbFullPath = Path.Combine(Application.streamingAssetsPath, muscleGlbPath);
#endif
        GameObject muscleModel = Importer.LoadFromFile(glbFullPath);

        if (muscleModel == null)
        {
            ShowWarning("ムキムキ.glbの読み込みに失敗しました");
            yield break;
        }

        ApplyTexturesToMuscle(muscleModel, frontTex, backTex);

        var glbExporter = gameObject.AddComponent<GlbExporter>();
        glbExporter.target = muscleModel;
        glbExporter.outputFileName = "muscle_with_cloth.glb";

        string glbPath = glbExporter.ExportGLB();
        var sender = gameObject.AddComponent<SendGlbToServer>();
        yield return StartCoroutine(sender.Send(glbPath));

        Destroy(muscleModel);
        Destroy(glbExporter);
        Destroy(sender);
        
        ShowWarning("保存完了！ (ID: " + topsId + ")");
    }

    void ApplyTexturesToMuscle(GameObject muscleModel, Texture2D frontTex, Texture2D backTex)
    {
        Renderer[] renderers = muscleModel.GetComponentsInChildren<Renderer>(true);
        UnityEngine.Debug.Log($"【ClothSaver】全メッシュ数: {renderers.Length}");

        foreach (Renderer rend in renderers)
        {
            string name = rend.name.ToLower();
            UnityEngine.Debug.Log($"【ClothSaver】メッシュ解析: {name}");

            // ログ結果に基づいた修正：
            // .001 = 背中側 (Back)
            // .002 = お腹側 (Front)
            bool isFront = name.Contains(".002");
            bool isBack = name.Contains(".001");

            if (isFront || isBack)
            {
                int materialCount = rend.sharedMaterials.Length;
                Material[] newMaterials = new Material[materialCount];

                for (int i = 0; i < materialCount; i++)
                {
                    Material m = new Material(Shader.Find("Standard"));
                    SetupTransparentMaterial(m);

                    if (isFront)
                    {
                        m.mainTexture = frontTex;
                        UnityEngine.Debug.Log($"  -> [{name}] 前服として適用");
                    }
                    else
                    {
                        m.mainTexture = backTex;
                        UnityEngine.Debug.Log($"  -> [{name}] 後服として適用");
                    }
                    newMaterials[i] = m;
                }
                rend.materials = newMaterials;
            }
            else
            {
                UnityEngine.Debug.Log($"  -> [{name}] 判定対象外");
            }
        }
    }

    void SetupTransparentMaterial(Material mat)
    {
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        mat.color = Color.white;
    }

    void RemoveBlackBackground(Texture2D tex)
    {
        Color[] pixels = tex.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].r < 0.1f && pixels[i].g < 0.1f && pixels[i].b < 0.1f)
            {
                pixels[i] = new Color(0, 0, 0, 0);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
    }

    Texture2D CaptureHiddenUI(RectTransform original, int width, int height)
    {
        GameObject tempCanvasObj = new GameObject("HiddenCanvas");
        Canvas tempCanvas = tempCanvasObj.AddComponent<Canvas>();
        tempCanvas.renderMode = RenderMode.ScreenSpaceCamera;

        GameObject camObj = new GameObject("HiddenCamera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.orthographic = true;
        tempCanvas.worldCamera = cam;

        RectTransform clone = Instantiate(original, tempCanvasObj.transform);
        clone.gameObject.SetActive(true);
        clone.anchorMin = clone.anchorMax = new Vector2(0.5f, 0.5f);
        clone.pivot = new Vector2(0.5f, 0.5f);
        clone.anchoredPosition = Vector2.zero;
        clone.localScale = Vector3.one;
        clone.sizeDelta = new Vector2(width, height);

        RenderTexture rt = new RenderTexture(width, height, 32, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.orthographicSize = height / 2f;
        cam.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        RenderTexture.active = null;
        Destroy(rt);
        Destroy(tempCanvasObj);
        Destroy(camObj);
        return tex;
    }

    string GetOrCreateTopsId(bool forceNew)
    {
        if (forceNew) return GenerateNewId();
        if (!string.IsNullOrEmpty(loadedIdFromCloset)) return loadedIdFromCloset;
        return GenerateNewId();
    }

    string GenerateNewId()
    {
        string dir = Path.Combine(Application.persistentDataPath, "Closet");
        if (!Directory.Exists(dir)) return "1";
        string[] files = Directory.GetFiles(dir, "前服Tops_*.png");
        int maxId = 0;
        foreach (var file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            string idPart = name.Replace("前服Tops_", "");
            if (int.TryParse(idPart, out int id)) { if (id > maxId) maxId = id; }
        }
        return (maxId + 1).ToString();
    }

    void StartFastApiServer()
    {
        if (string.IsNullOrEmpty(pythonBatPath) || !File.Exists(pythonBatPath)) return;
        try {
            ProcessStartInfo psi = new ProcessStartInfo { FileName = pythonBatPath, UseShellExecute = true };
            Process.Start(psi);
        } catch {}
    }

    void ShowWarning(string msg) { if (warningText) { warningText.text = msg; warningText.gameObject.SetActive(true); } }
    void HideWarning() { if (warningText) warningText.gameObject.SetActive(false); }

    IEnumerator EnsureGlbFileLocal()
    {
        string destPath = Path.Combine(Application.persistentDataPath, muscleGlbPath);
#if !UNITY_EDITOR && UNITY_ANDROID
        if (!File.Exists(destPath))
        {
            string streamingPath = Path.Combine(Application.streamingAssetsPath, muscleGlbPath);
            UnityEngine.Debug.Log($"【ClothSaver】StreamingAssetsからコピー中: {streamingPath}");
            var www = UnityEngine.Networking.UnityWebRequest.Get(streamingPath);
            yield return www.SendWebRequest();
            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                File.WriteAllBytes(destPath, www.downloadHandler.data);
                UnityEngine.Debug.Log($"【ClothSaver】コピー成功: {destPath}");
            }
            else
            {
                UnityEngine.Debug.LogError($"【ClothSaver】コピー失敗: {www.error}");
                ShowWarning("モデルファイルの取得に失敗しました。StreamingAssetsを確認してください。");
            }
        }
#endif
        yield break;
    }
}
