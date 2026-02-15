using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

public class SendGlbToServer : MonoBehaviour
{
    [Header("Endpoint")]
    [SerializeField] private string uploadPath = "/face/upload_muscle_glb";
    [SerializeField] private string endpointOverride = "";
    [SerializeField] private int timeoutSeconds = 30;

    public IEnumerator Send(string glbPath)
    {
        if (string.IsNullOrWhiteSpace(glbPath))
        {
            Debug.LogError("[SendGlbToServer] GLB path is empty.");
            yield break;
        }
        if (!File.Exists(glbPath))
        {
            Debug.LogError("[SendGlbToServer] GLB file not found: " + glbPath);
            yield break;
        }

        byte[] data;
        try
        {
            data = File.ReadAllBytes(glbPath);
        }
        catch (Exception e)
        {
            Debug.LogError("[SendGlbToServer] Failed to read GLB: " + e.Message);
            yield break;
        }

        if (data == null || data.Length < 4)
        {
            Debug.LogError("[SendGlbToServer] GLB payload is empty or too small.");
            yield break;
        }

        string endpoint = BuildEndpoint();
        Debug.Log("[SendGlbToServer] POST " + endpoint + $" | bytes={data.Length}");

        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
        formData.Add(new MultipartFormFileSection("file", data, "model.glb", "application/octet-stream"));

        using (UnityWebRequest req = UnityWebRequest.Post(endpoint, formData))
        {
            req.useHttpContinue = false;
            req.timeout = Mathf.Max(5, timeoutSeconds);

            yield return req.SendWebRequest();

            string body = req.downloadHandler?.text ?? string.Empty;
            string bodyPreview = body.Length > 300 ? body.Substring(0, 300) : body;

            if (req.result == UnityWebRequest.Result.Success)
            {
                if (TryGetFailureMessage(body, out string failureMessage))
                {
                    Debug.LogError("[SendGlbToServer] Upload API returned failure: " + failureMessage);
                }
                else
                {
                    Debug.Log("[SendGlbToServer] Upload OK | HTTP " + req.responseCode + " | " + bodyPreview);
                }
            }
            else
            {
                Debug.LogError(
                    "[SendGlbToServer] Upload Error: " + req.error +
                    " | HTTP " + req.responseCode +
                    " | endpoint=" + endpoint +
                    " | body=" + bodyPreview);
            }
        }
    }

    private string BuildEndpoint()
    {
        string overrideValue = (endpointOverride ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(overrideValue))
        {
            if (!overrideValue.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !overrideValue.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                overrideValue = "http://" + overrideValue;
            }
            return overrideValue;
        }

        return ServerEndpointSettings.BuildUrl(uploadPath);
    }

    private static bool TryGetFailureMessage(string body, out string message)
    {
        message = null;
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        if (body.IndexOf("\"success\":false", StringComparison.OrdinalIgnoreCase) < 0 &&
            body.IndexOf("\"success\": false", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        Match m = Regex.Match(body, "\"message\"\\s*:\\s*\"(?<msg>[^\"]*)\"", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            message = Regex.Unescape(m.Groups["msg"].Value);
        }
        else
        {
            message = "Unknown server-side failure.";
        }
        return true;
    }
}
