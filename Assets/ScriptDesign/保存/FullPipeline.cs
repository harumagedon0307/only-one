using UnityEngine;
using System.Collections;

public class FullPipeline : MonoBehaviour
{
    public Texture2D png;

    IEnumerator Start()
    {
        // PNG → Plane（モデル化）
        var maker = gameObject.AddComponent<PngToPlane>();
        maker.png = png;
        var plane = maker.CreateModel();

        // Plane → GLB
        var exporter = gameObject.AddComponent<GlbExporter>();
        exporter.target = plane;
        string glbPath = exporter.ExportGLB();

        // GLB → FastAPI
        var sender = gameObject.AddComponent<SendGlbToServer>();
        yield return StartCoroutine(sender.Send(glbPath));

        Debug.Log("全工程完了！");
    }
}

