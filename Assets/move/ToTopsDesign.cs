using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ToTopsDesign : MonoBehaviour
{
    [Header("サムネイルを並べる親")]
    public Transform thumbnailParent;

    [Header("サムネイルImageのプレハブ")]
    public GameObject thumbnailPrefab;

    void Start()
    {
        LoadAllSavedTops();
    }

    // ---------------------------------------------------
    // 保存済みの前服画像を全部読み込んでサムネ生成
    // ---------------------------------------------------
    void LoadAllSavedTops()
    {
        string[] files = Directory.GetFiles(Application.persistentDataPath, "前服Tops_*.png");

        foreach (string path in files)
        {
            Texture2D tex = LoadTexture(path);
            if (tex == null) continue;

            // ① サムネイルUIを作成
            GameObject thumb = Instantiate(thumbnailPrefab, thumbnailParent);

            // Image を子から探す
            Image img = thumb.GetComponentInChildren<Image>();
            img.sprite = Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f));

            // Button を子から探す（Top1本体にある）
            Button btn = thumb.GetComponentInChildren<Button>();
            string id = ExtractId(path);
            btn.onClick.AddListener(() => OnClickThumbnail(id));
        }
    }

    // ファイル名からTopsIDを取り出す
    string ExtractId(string path)
    {
        string file = Path.GetFileNameWithoutExtension(path);     // 前服Tops_3
        string[] split = file.Split('_');                         // ["前服Tops","3"]
        return split[1];                                          // "3"
    }

    Texture2D LoadTexture(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(bytes);
        return tex;
    }

    // サムネが押されたら服編集画面へ
    public void OnClickThumbnail(string topsID)
    {
        PlayerPrefs.SetString("SelectedTopsId", topsID);
        PlayerPrefs.Save(); // ←これ必須！

        Debug.Log("【保存したID】" + topsID);

        ClothIDManager.CurrentID = 1;
        SceneManager.LoadScene("DesignCloth");
    }


    public void CreateNew()
    {
        ClothIDManager.CurrentID = -1;
        SceneManager.LoadScene("DesignCloth");
    }
}
