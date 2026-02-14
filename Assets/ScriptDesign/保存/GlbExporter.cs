using UnityEngine;
using UnityGLTF;
using System.IO;

public class GlbExporter: MonoBehaviour
{
    public GameObject target;
    public string outputFileName = "output.glb";

    public string ExportGLB()
    {
        // �o�͐�: Assets/Export/output.glb
        string exportDir = Path.Combine(Application.persistentDataPath, "Export");
        if (!Directory.Exists(exportDir)) Directory.CreateDirectory(exportDir);
        string exportPath = Path.Combine(exportDir, outputFileName);

        // ��� ExportOptions ���g���i�v���p�e�B�������Ă�����Ȃ���S�j
        var options = new ExportContext();

        var exporter = new GLTFSceneExporter(
            new Transform[] { target.transform },
            options
        );

        using (var stream = new FileStream(exportPath, FileMode.Create))
        {
            exporter.SaveGLBToStream(stream, target.name);
        }

        Debug.Log("GLB exported: " + exportPath);
        return exportPath;
    }
}
