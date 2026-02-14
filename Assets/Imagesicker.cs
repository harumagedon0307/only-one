using UnityEngine;
using UnityEngine.UI;

public class ImagePicker : MonoBehaviour
{
    [Header("服のUI Imageを配置する親レイヤー")]
    public Transform targetLayer;

    // ギャラリーから画像を選択
    public void PickImage()
    {
        NativeGallery.GetImageFromGallery((path) =>
        {
            if (string.IsNullOrEmpty(path)) return;

            Texture2D texture = NativeGallery.LoadImageAtPath(path, 2048);
            if (texture == null) return;

            AddPatternAsChild(texture);

        }, "画像を選択", "image/*");
    }

    // 選択した画像をUIに追加
    void AddPatternAsChild(Texture2D tex)
    {
        Texture2D resizedTex = ResizeTexture(tex, 512, 512);

        GameObject child = new GameObject("PatternImage");
        child.transform.SetParent(targetLayer, false);

        Image img = child.AddComponent<Image>();
        img.sprite = Sprite.Create(resizedTex,
            new Rect(0, 0, resizedTex.width, resizedTex.height),
            new Vector2(0.5f, 0.5f));
        img.raycastTarget = true;

        RectTransform rt = child.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(resizedTex.width * 0.4f, resizedTex.height * 0.4f);

        // 長押し移動スクリプトを追加
        child.AddComponent<EditablePattern>();

        // ★ Canvas は付けない！ ★
    }


    // テクスチャをリサイズ
    private Texture2D ResizeTexture(Texture2D source, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height);
        RenderTexture.active = rt;

        Graphics.Blit(source, rt);

        Texture2D result = new Texture2D(width, height);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        return result;
    }
}
