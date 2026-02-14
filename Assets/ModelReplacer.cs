using UnityEngine;
using Mediapipe.Unity;
using System.Reflection;

public class ModelReplacer : MonoBehaviour
{
  [SerializeField] private PointListAnnotation pointListAnnotation;
  [Header("Arm Mapping")]
  [SerializeField] private bool overrideMirrorMode = true;
  [SerializeField] private bool mirrorMode = true;

  private FieldInfo mirrorModeField;

  private void Awake()
  {
    if (pointListAnnotation == null)
    {
      pointListAnnotation = FindObjectOfType<PointListAnnotation>();
    }

    ApplyMirrorModeOverride();
  }

  public void ReplaceModel(GameObject newModel)
  {
    if (newModel == null)
    {
      Debug.LogError("ModelReplacer: 受け取ったモデルが null です。");
      return;
    }

    if (pointListAnnotation == null)
    {
      Debug.LogError("ModelReplacer: PointListAnnotationが設定されていません！");
      return;
    }

    ApplyMirrorModeOverride();

    // 実際の置き換え処理はPointListAnnotationに任せる
    pointListAnnotation.SetModel(newModel);
    
    // SetModel内でInstantiate(複製)されるため、元のnewModel（シーン内の静的なオブジェクト）は不要なので破棄する
    Destroy(newModel);

    Debug.Log("ModelReplacer: PointListAnnotationにモデルの置き換えを依頼し、元モデルを破棄しました。");
  }

  private void ApplyMirrorModeOverride()
  {
    if (!overrideMirrorMode || pointListAnnotation == null)
    {
      return;
    }

    if (mirrorModeField == null)
    {
      mirrorModeField = typeof(PointListAnnotation).GetField("_mirrorMode", BindingFlags.Instance | BindingFlags.NonPublic);
      if (mirrorModeField == null)
      {
        Debug.LogWarning("ModelReplacer: _mirrorMode field was not found. Mirror override skipped.");
        return;
      }
    }

    mirrorModeField.SetValue(pointListAnnotation, mirrorMode);
    Debug.Log("ModelReplacer: mirrorMode override applied = " + mirrorMode);
  }
}
