using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class SendGlbToServer : MonoBehaviour
{
    //URLïœçXÇ∑ÇÈ
    private string fastApiEndpoint = "http://localhost:8000/face/upload_muscle_glb";

    public IEnumerator Send(string glbPath)
    {
        byte[] data = File.ReadAllBytes(glbPath);

        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
        formData.Add(new MultipartFormFileSection("file", data, "model.glb", "application/octet-stream"));

        using (UnityWebRequest req = UnityWebRequest.Post(fastApiEndpoint, formData))
        {
            req.useHttpContinue = false;
            req.timeout = 30;

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                Debug.Log("Upload OK: " + req.downloadHandler.text);
            else
                Debug.LogError("Upload Error: " + req.error);
        }
    }
}
