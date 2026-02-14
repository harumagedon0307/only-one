using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections;
using System.Collections.Generic; // ★ 追加
using System.Diagnostics;
using Siccity.GLTFUtility;
using UnityGLTF;

public class MuscleClothSaver : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform frontCloth;  // 前服のUI
    public RectTransform backCloth;   // 後服のUI
    public Text statusText;           // ステータス表示用

    [Header("Settings")]
    public string muscleGlbPath = "ムキムキ.glb";  // Assets内の相対パス
    public string pythonBatPath = "C:\\path\\to\\your\\start_fastapi.bat";

    private const int TEXTURE_WIDTH = 512;
    private const int TEXTURE_HEIGHT = 512;


    // ★ メインの保存処理
    public void SaveMuscleCloth()
    {
        StartCoroutine(SaveRoutine());
    }


    IEnumerator SaveRoutine()
    {
        // ---------------------------
        // 1. UI要素の確認
        // ---------------------------
        if (frontCloth == null)
        {
            ShowStatus("前服のUI参照が設定されていません");
            yield break;
        }
        // activeInHierarchyのチェックは削除（非表示でもキャプチャ可能にするため）

        ShowStatus("処理開始...");


        // ---------------------------
        // 2. 前後のPNGを作成
        // ---------------------------
        // ---------------------------
        // 2. 前後のPNGを作成
        // ---------------------------
        Texture2D frontTexture = CaptureHiddenUI(frontCloth, TEXTURE_WIDTH, TEXTURE_HEIGHT);
        Texture2D backTexture = CaptureHiddenUI(backCloth, TEXTURE_WIDTH, TEXTURE_HEIGHT);

        UnityEngine.Debug.Log("前後のテクスチャ作成完了");

        // ★ PNG保存処理を追加
        string topsId = GetOrCreateTopsId();

        string frontFileName = $"前服Tops_{topsId}.png";
        string frontPath = Path.Combine(Application.persistentDataPath, frontFileName);
        File.WriteAllBytes(frontPath, frontTexture.EncodeToPNG());

        if (backTexture != null)
        {
            string backFileName = $"後服Tops_{topsId}.png";
            string backPath = Path.Combine(Application.persistentDataPath, backFileName);
            File.WriteAllBytes(backPath, backTexture.EncodeToPNG());
        }

        UnityEngine.Debug.Log($"PNG保存完了: {frontPath}");
        ShowStatus("画像を保存しました");


        // ---------------------------
        // 3. ムキムキ.glbを読み込み
        // ---------------------------
        yield return StartCoroutine(EnsureGlbFileLocal());
        string glbFullPath = Path.Combine(Application.persistentDataPath, muscleGlbPath);
#if UNITY_EDITOR
        if (!File.Exists(glbFullPath)) glbFullPath = Path.Combine(Application.streamingAssetsPath, muscleGlbPath);
#endif
        GameObject muscleModel = Importer.LoadFromFile(glbFullPath);

        if (muscleModel == null)
        {
            ShowStatus("ムキムキ.glbの読み込みに失敗しました");
            yield break;
        }

        UnityEngine.Debug.Log("ムキムキ.glb読み込み完了");


        // ---------------------------
        // 4. マテリアルを適用
        // ---------------------------
        ApplyTexturesToMuscle(muscleModel, frontTexture, backTexture);
        UnityEngine.Debug.Log("マテリアル適用完了");


        // ---------------------------
        // 5. FastAPIサーバー起動
        // ---------------------------
        StartFastApiServer();
        yield return new WaitForSeconds(3f);


        // ---------------------------
        // 6. GLBエクスポート
        // ---------------------------
        var exporter = gameObject.AddComponent<GlbExporter>();
        exporter.target = muscleModel;
        exporter.outputFileName = "muscle_with_cloth.glb";

        string exportedGlbPath = exporter.ExportGLB();
        UnityEngine.Debug.Log("GLBエクスポート完了: " + exportedGlbPath);


        // ---------------------------
        // 7. サーバーに送信
        // ---------------------------
        var sender = gameObject.AddComponent<SendGlbToServer>();
        yield return StartCoroutine(sender.Send(exportedGlbPath));

        ShowStatus("サーバー送信完了！");
        UnityEngine.Debug.Log("全処理完了");


        // ---------------------------
        // 8. クリーンアップ
        // ---------------------------
        Destroy(muscleModel);
        Destroy(exporter);
        Destroy(sender);
    }


    // ======================
    // マテリアル適用
    // ======================
    void ApplyTexturesToMuscle(GameObject muscleModel, Texture2D frontTex, Texture2D backTex)
    {
        // 全てのRendererを取得（メッシュが分割されている場合に対応）
        Renderer[] renderers = muscleModel.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            UnityEngine.Debug.LogError("Rendererが見つかりません");
            return;
        }

        foreach (Renderer renderer in renderers)
        {
            // 既存のマテリアル配列を取得
            Material[] materials = renderer.materials;

            if (materials.Length < 1) continue;

            // マテリアルを適用
            // 前面用マテリアル（マテリアルスロット0）
            Material frontMat = new Material(Shader.Find("Standard"));
            frontMat.mainTexture = frontTex;
            materials[0] = frontMat;

            if (materials.Length >= 2 && backTex != null)
            {
                // 背面用マテリアル（マテリアルスロット1）
                Material backMat = new Material(Shader.Find("Standard"));
                backMat.mainTexture = backTex;
                materials[1] = backMat;
            }

            renderer.materials = materials;
        }

        UnityEngine.Debug.Log($"{renderers.Length}個のRendererにマテリアルを適用しました");
    }


    // ======================
    // UI キャプチャ
    // ======================
    Texture2D CaptureHiddenUI(RectTransform original, int width, int height)
    {
        // 隠しCanvasとカメラを作成
        GameObject tempCanvasObj = new GameObject("HiddenCanvas");
        Canvas tempCanvas = tempCanvasObj.AddComponent<Canvas>();
        tempCanvas.renderMode = RenderMode.ScreenSpaceCamera;

        GameObject camObj = new GameObject("HiddenCamera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.orthographic = true;
        tempCanvas.worldCamera = cam;

        // UI要素を複製
        RectTransform clone = Instantiate(original, tempCanvasObj.transform);
        clone.anchorMin = clone.anchorMax = new Vector2(0.5f, 0.5f);
        clone.pivot = new Vector2(0.5f, 0.5f);
        clone.anchoredPosition = Vector2.zero;
        clone.sizeDelta = new Vector2(width, height);
        clone.localScale = Vector3.one;

        // RenderTextureでキャプチャ
        RenderTexture rt = new RenderTexture(width, height, 32, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.orthographicSize = height / 2f;
        cam.Render();

        // Texture2Dに変換
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        // クリーンアップ
        RenderTexture.active = null;
        cam.targetTexture = null;
        Destroy(rt);
        Destroy(tempCanvasObj);
        Destroy(camObj);

        return tex;
    }


    // ======================
    // FastAPI サーバー起動
    // ======================
    void StartFastApiServer()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = pythonBatPath;
            psi.UseShellExecute = true;
            Process.Start(psi);

            UnityEngine.Debug.Log("FastAPI サーバー起動");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("FastAPI 起動失敗: " + e.Message);
        }
    }


    // ======================
    // ステータス表示
    // ======================
    void ShowStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        UnityEngine.Debug.Log(message);
    }

    // ======================
    // ID生成・取得
    // ======================
    string GetOrCreateTopsId()
    {
        if (PlayerPrefs.HasKey("SelectedTopsId"))
        {
            string existingId = PlayerPrefs.GetString("SelectedTopsId");
            if (!string.IsNullOrEmpty(existingId))
                return existingId; // "1", "2" などの数字
        }

        int nextId = GetNextAvailableId();
        string newId = nextId.ToString();
        PlayerPrefs.SetString("SelectedTopsId", newId);

        return newId;
    }

    int GetNextAvailableId()
    {
        string dir = Application.persistentDataPath;
        // マッチさせたいファイル名パターン: "前服Tops_1.png" など
        string[] files = Directory.GetFiles(dir, "前服Tops_*.png");

        var ids = new HashSet<int>();
        // 正規表現: "前服Tops_(\d+).png$"
        // Path.GetFileNameWithoutExtensionを使う場合、"前服Tops_(\d+)$"
        var regex = new System.Text.RegularExpressions.Regex(@"前服Tops_(\d+)$");

        foreach (var file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file); // e.g. "前服Tops_9"
            var m = regex.Match(name);
            if (m.Success)
            {
                if (int.TryParse(m.Groups[1].Value, out int n))
                    ids.Add(n);
            }
        }

        int candidate = 1;
        while (ids.Contains(candidate)) candidate++;

        return candidate;
    }

    IEnumerator EnsureGlbFileLocal()
    {
        string destPath = Path.Combine(Application.persistentDataPath, muscleGlbPath);
#if !UNITY_EDITOR && UNITY_ANDROID
        if (!File.Exists(destPath))
        {
            string streamingPath = Path.Combine(Application.streamingAssetsPath, muscleGlbPath);
            UnityEngine.Debug.Log($"【MuscleClothSaver】StreamingAssetsからコピー中: {streamingPath}");
            var www = UnityEngine.Networking.UnityWebRequest.Get(streamingPath);
            yield return www.SendWebRequest();
            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                File.WriteAllBytes(destPath, www.downloadHandler.data);
                UnityEngine.Debug.Log($"【MuscleClothSaver】コピー成功: {destPath}");
            }
            else
            {
                UnityEngine.Debug.LogError($"【MuscleClothSaver】コピー失敗: {www.error}");
                ShowStatus("モデルファイルの取得に失敗しました。 StreamingAssetsを確認してください。");
            }
        }
#endif
        yield break;
    }
}
