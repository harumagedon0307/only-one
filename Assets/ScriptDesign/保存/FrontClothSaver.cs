using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class FrontClothSaver : MonoBehaviour
{
    public RectTransform frontCloth;
    public Text warningText;

    private const int SAVE_WIDTH = 593;
    private const int SAVE_HEIGHT = 486;

    // ★ GLB送信用（今は無効化）
    public string pythonBatPath = "C:\\path\\to\\your\\start_fastapi.bat";

    // ▽ SaveFront をコルーチン化して実行
    public void SaveFront()
    {
        StartCoroutine(SaveFrontRoutine());
    }

    IEnumerator SaveFrontRoutine()
    {
        // -------------------------
        // 1. 前服が非表示なら終了
        // -------------------------
        if (!frontCloth.gameObject.activeInHierarchy)
        {
            ShowWarning("前服が表示されていません");
            yield break;
        }

        HideWarning();

        // -------------------------
        // 2. 前服画像を隠しCanvasでキャプチャ
        // -------------------------
        Texture2D tex = CaptureHiddenUI(frontCloth, SAVE_WIDTH, SAVE_HEIGHT);

        string topsId = GetOrCreateTopsId();
        string fileName = "前服Tops_" + topsId + ".png";
        string path = Path.Combine(Application.persistentDataPath, fileName);

        File.WriteAllBytes(path, tex.EncodeToPNG());
        ShowWarning("前服を保存しました！");
        UnityEngine.Debug.Log("前服保存: " + path);

        // =====================================================
        // 3. FastAPI サーバー起動
        // =====================================================
        StartFastApiServer();
        yield return new WaitForSeconds(3f);

        // =====================================================
        // 4. PNG → Texture2D 読み込み
        // =====================================================
        Texture2D pngTex = new Texture2D(2, 2);
        pngTex.LoadImage(File.ReadAllBytes(path));

        // =====================================================
        // 5. Plane に貼り付けて3Dオブジェクト化
        // =====================================================
        GameObject plane = CreatePlane(pngTex);

        // =====================================================
        // 6. GLB へ変換
        // =====================================================
        var glbExporter = gameObject.AddComponent<GlbExporter>();
        glbExporter.target = plane;
        string glbPath = glbExporter.ExportGLB();
        UnityEngine.Debug.Log("GLB 変換完了: " + glbPath);

        // =====================================================
        // 7. FastAPI に送信
        // =====================================================
        var sender = gameObject.AddComponent<SendGlbToServer>();
        yield return StartCoroutine(sender.Send(glbPath));
        UnityEngine.Debug.Log("FastAPI への送信完了");

        // =====================================================
        // 8. 後片付け
        // =====================================================
        Destroy(plane);
    }

    void StartFastApiServer()
    {
        // =============================
        // FastAPI 起動処理（現在は無効化）
        // =============================

        // try
        // {
        //     ProcessStartInfo psi = new ProcessStartInfo();
        //     psi.FileName = pythonBatPath;
        //     psi.UseShellExecute = true;   // bat 実行
        //     Process.Start(psi);
        //
        //     UnityEngine.Debug.Log("FastAPI サーバー起動");
        // }
        // catch (System.Exception e)
        // {
        //     UnityEngine.Debug.LogError("FastAPI 起動失敗: " + e.Message);
        // }
    }


    // =============================
    // ID 管理
    // =============================
    string GetOrCreateTopsId()
    {
        if (PlayerPrefs.HasKey("SelectedTopsId"))
        {
            string existingId = PlayerPrefs.GetString("SelectedTopsId");
            if (!string.IsNullOrEmpty(existingId))
                return existingId;
        }

        int nextId = GetNextAvailableId();
        string newId = nextId.ToString();
        PlayerPrefs.SetString("SelectedTopsId", newId);

        return newId;
    }

    int GetNextAvailableId()
    {
        string dir = Application.persistentDataPath;
        string[] files = Directory.GetFiles(dir, "前服Tops_*.png");

        var ids = new HashSet<int>();
        var regex = new System.Text.RegularExpressions.Regex(@"前服Tops_(\d+)$");

        foreach (var file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            var m = regex.Match(name);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int n))
                ids.Add(n);
        }

        int candidate = 1;
        while (ids.Contains(candidate)) candidate++;

        return candidate;
    }

    void ShowWarning(string msg)
    {
        if (warningText)
        {
            warningText.text = msg;
            warningText.gameObject.SetActive(true);
        }
    }

    void HideWarning()
    {
        if (warningText)
        {
            warningText.text = "";
            warningText.gameObject.SetActive(false);
        }
    }

    // =============================
    // Plane 作成（今は未使用）
    // =============================
    GameObject CreatePlane(Texture2D tex)
    {
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        Material mat = new Material(Shader.Find("Standard"));
        mat.mainTexture = tex;

        plane.GetComponent<Renderer>().material = mat;
        plane.transform.localScale = new Vector3(0.3f, 1f, 0.3f);

        return plane;
    }

    // =============================
    // UI キャプチャ
    // =============================
    Texture2D CaptureHiddenUI(RectTransform original, int width, int height)
    {
        GameObject tempCanvasObj = new GameObject("HiddenCanvas_Front");
        Canvas tempCanvas = tempCanvasObj.AddComponent<Canvas>();
        tempCanvas.renderMode = RenderMode.ScreenSpaceCamera;

        GameObject camObj = new GameObject("HiddenCamera_Front");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.orthographic = true;
        tempCanvas.worldCamera = cam;

        RectTransform clone = Instantiate(original, tempCanvasObj.transform);
        clone.anchorMin = clone.anchorMax = new Vector2(0.5f, 0.5f);
        clone.pivot = new Vector2(0.5f, 0.5f);
        clone.anchoredPosition = Vector2.zero;
        clone.sizeDelta = new Vector2(width, height);
        clone.localScale = Vector3.one;

        RenderTexture rt = new RenderTexture(width, height, 32, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        cam.orthographicSize = height / 2f;
        cam.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        RenderTexture.active = null;
        cam.targetTexture = null;

        Destroy(rt);
        Destroy(tempCanvasObj);
        Destroy(camObj);

        return tex;
    }
}
