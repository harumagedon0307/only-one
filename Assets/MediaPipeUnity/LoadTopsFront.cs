using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class LoadTopsWithPatternApplier : MonoBehaviour
{
    public PatternApplier applier; // Inspector で targetLayer をセット

    void Start()
    {
        // PlayerPrefs から選択された TopsId を取得
        string topsId = PlayerPrefs.GetString("SelectedTopsId", "");
        Debug.Log("【読んだID】" + topsId);

        if (string.IsNullOrEmpty(topsId))
        {
            // ID がない場合はそのまま元の画像を使用
            Debug.Log("SelectedTopsId がないので既存の画像を使用します。");
            return;
        }

        LoadAndApplyTexture(topsId);
    }

    void LoadAndApplyTexture(string topsId)
    {
        // ファイルパスを作成
        string path = Path.Combine(Application.dataPath, "Closet", $"前服Tops_{topsId}.png");

        if (!File.Exists(path))
        {
            Debug.LogWarning($"ファイルが存在しません: {path}。既存の画像を使用します。");
            return; // 上書きせず終了
        }

        // PNG を読み込んで Texture2D に変換
        byte[] bytes = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(bytes);

        // PatternApplier に渡して Image/SpriteRenderer に反映
        applier.ApplyPattern(tex);
    }
}
