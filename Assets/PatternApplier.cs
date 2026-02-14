using UnityEngine;
using UnityEngine.UI;

public class PatternApplier : MonoBehaviour
{
    public Image targetImage;   // ”w’†‘¤‚Ì Image ‚ğ Inspector ‚Åİ’è

    public void ApplyPattern(Texture2D tex)
    {
        if (targetImage == null)
        {
            Debug.LogError("targetImage ‚ªİ’è‚³‚ê‚Ä‚¢‚Ü‚¹‚ñI");
            return;
        }

        Sprite newSprite = Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f)
        );

        targetImage.sprite = newSprite;
    }
}
