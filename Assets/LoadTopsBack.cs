using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class LoadTopsWithPatternApplierBack : MonoBehaviour
{
    public PatternApplier applier; // Inspector で targetLayer をセット

    void Start()
    {
        // PlayerPrefs から選択された TopsId を取得
        string topsId = PlayerPrefs.GetString("SelectedTopsId", "");

        if (string.IsNullOrEmpty(topsId))
        {
            // ID がない場合はそのまま元の画像を使用
            Debug.Log("SelectedTopsId がないので既存の画像を使用します。");
            return;
        }

        LoadAndApplyTextureBack(topsId);
    }

    void LoadAndApplyTextureBack(string topsId)
    {
        string path = Path.Combine(Application.dataPath, "Closet", $"後服Tops_{topsId}.png");
        Debug.Log("Loading Back Texture: " + path);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"ファイルが存在しません: {path}");
            return;
        }

        byte[] bytes = File.ReadAllBytes(path);

        // RGBA32 指定の方が安全
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        if (!tex.LoadImage(bytes))
        {
            Debug.LogError("画像読み込みに失敗しました。");
            return;
        }

        if (applier == null)
        {
            Debug.LogError("PatternApplierBack が Inspector に設定されていません！");
            return;
        }

        applier.ApplyPattern(tex);
    }

}