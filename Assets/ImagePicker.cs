using UnityEngine;
using UnityEngine.UI;

public class Imagepicker : MonoBehaviour
{
    // Image が付いている GameObject（ButtonでもOK）
    public RectTransform targetRectTransform;

    public void PickImage()
    {
        NativeGallery.GetImageFromGallery((path) =>
        {
            if (string.IsNullOrEmpty(path)) return;

            Texture2D texture = NativeGallery.LoadImageAtPath(path, 2048);
            if (texture == null) return;

            ApplyImage(texture);

        }, "画像を選択", "image/*");
    }

    void ApplyImage(Texture2D tex)
    {
        Image img = targetRectTransform.GetComponent<Image>();
        if (img == null)
        {
            Debug.LogError("Image コンポーネントが見つかりません");
            return;
        }

        // Sprite 作成（PPU = 1）
        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            1f // ★ 1px = 1
        );

        img.sprite = sprite;
        img.preserveAspect = true;

        // -------- RectTransform が「出力サイズ」を決める --------
        targetRectTransform.localScale = Vector3.one;

        // ★ Canvas Scaler を無視して実寸サイズ指定
        targetRectTransform.sizeDelta = new Vector2(
            tex.width,
            tex.height
        );
    }
}
