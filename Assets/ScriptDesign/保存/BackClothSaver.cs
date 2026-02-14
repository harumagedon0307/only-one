using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections;
using System.Diagnostics;

public class BackClothSaver : MonoBehaviour
{
    public RectTransform backCloth;
    public Text errorText;

    // ★ FastAPI サーバー起動バッチ
    public string pythonBatPath = "C:\\path\\to\\your\\start_fastapi.bat";


    public void SaveBack()
    {
        StartCoroutine(SaveBackRoutine());
    }


    IEnumerator SaveBackRoutine()
    {
        // ---------------------------
        // 1. 非表示チェック
        // ---------------------------
        if (!backCloth.gameObject.activeInHierarchy)
        {
            ShowError("後服が表示されていません");
            yield break;
        }

        // サイズチェック
        Vector2 size = backCloth.rect.size;
        if (size.x <= 1f || size.y <= 1f)
        {
            ShowError("後ろ服のサイズが不正です");
            yield break;
        }

        ClearError();


        // ---------------------------
        // 2. 前服と同じ TopsId を取得
        // ---------------------------
        string topsId = GetOrCreateTopsId();
        if (string.IsNullOrEmpty(topsId))
        {
            ShowError("前服がまだ保存されていません");
            yield break;
        }


        // ---------------------------
        // 3. キャプチャ
        // ---------------------------
        int width = Mathf.RoundToInt(size.x);
        int height = Mathf.RoundToInt(size.y);

        Texture2D tex = CaptureHiddenUI(backCloth, width, height);

        string fileName = "後服" + topsId + ".png";
        string pngPath = Path.Combine(Application.persistentDataPath, fileName);

        File.WriteAllBytes(pngPath, tex.EncodeToPNG());
        ShowWarning("後服を保存しました！");
        UnityEngine.Debug.Log("後服保存: " + pngPath);



        // ---------------------------
        // 4. FastAPI サーバー起動
        // ---------------------------
        StartFastApiServer();
        yield return new WaitForSeconds(3f); // 少し待つ



        // ---------------------------
        // 5. PNG を Texture2D に読み込み
        // ---------------------------
        Texture2D pngTex = new Texture2D(2, 2);
        pngTex.LoadImage(File.ReadAllBytes(pngPath));


        // ---------------------------
        // 6. Plane 作成
        // ---------------------------
        GameObject plane = CreatePlane(pngTex);


        // ---------------------------
        // 7. GLB エクスポート
        // ---------------------------
        var exporter = gameObject.AddComponent<GlbExporter>();
        exporter.target = plane;

        string glbPath = exporter.ExportGLB();
        UnityEngine.Debug.Log("後服 GLB 完了: " + glbPath);



        // ---------------------------
        // 8. FastAPI に GLB 送信
        // ---------------------------
        var sender = gameObject.AddComponent<SendGlbToServer>();
        yield return StartCoroutine(sender.Send(glbPath));

        UnityEngine.Debug.Log("後服 GLB を FastAPI へ送信しました");



        // ---------------------------
        // 9. 後片付け
        // ---------------------------
        Destroy(plane);
    }




    // ======================
    //  前服と同じIDを取得
    // ======================
    string GetOrCreateTopsId()
    {
        if (PlayerPrefs.HasKey("SelectedTopsId"))
        {
            string existingId = PlayerPrefs.GetString("SelectedTopsId");
            if (!string.IsNullOrEmpty(existingId))
                return existingId;
        }

        return null; // 前服未保存
    }



    // ======================
    // FastAPI を起動（bat）
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
    // エラー表示
    // ======================
    void ShowWarning(string msg)
    {
        if (errorText != null) errorText.text = msg;
    }

    void ShowError(string msg)
    {
        if (errorText != null) errorText.text = msg;
    }

    void ClearError()
    {
        if (errorText != null) errorText.text = "";
    }



    // ======================
    //  Plane 作成
    // ======================
    GameObject CreatePlane(Texture2D tex)
    {
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);

        Material mat = new Material(Shader.Find("Standard"));
        mat.mainTexture = tex;

        plane.GetComponent<Renderer>().material = mat;
        plane.transform.localScale = new Vector3(0.3f, 1f, 0.3f);

        return plane;
    }



    // ======================
    // UI キャプチャ
    // ======================
    Texture2D CaptureHiddenUI(RectTransform original, int width, int height)
    {
        GameObject tempCanvasObj = new GameObject("HiddenCanvas_Back");
        Canvas tempCanvas = tempCanvasObj.AddComponent<Canvas>();
        tempCanvas.renderMode = RenderMode.ScreenSpaceCamera;

        GameObject camObj = new GameObject("HiddenCamera_Back");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.orthographic = true;
        tempCanvas.worldCamera = cam;

        RectTransform clone = Instantiate(original, tempCanvasObj.transform);

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
        cam.targetTexture = null;

        Destroy(rt);
        Destroy(tempCanvasObj);
        Destroy(camObj);

        return tex;
    }
}
