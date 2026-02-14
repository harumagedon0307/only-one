using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Siccity.GLTFUtility;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class ImageSender : MonoBehaviour
{
    private RawImage rawImage;

    [Header("FastAPI")]
    [SerializeField] private string classificationPath = "/face/classification_face";
    [SerializeField] private int timeoutSeconds = 20;
    [SerializeField] private int maxUploadAttempts = 2;

    [Header("Loop")]
    [SerializeField] public float interval = 5.0f;

    [Header("Imported Material")]
    [SerializeField] private bool convertImportedMaterialsToUrp = false;
    [SerializeField] private bool forceDoubleSided = true;
    [SerializeField] private bool saveReceivedGlbToDisk = false;

    private bool missingFaceDataWarningShown = false;
    private Coroutine sendLoopCoroutine;
    private UnityWebRequest activeRequest;
    private bool isShuttingDown = false;
    private static bool loggedFallbackShader = false;
    private bool isInitialized = false;

    private void Start()
    {
        rawImage = GetComponent<RawImage>();
        if (rawImage == null)
        {
            Debug.LogError("[ImageSender] RawImage is missing.");
            return;
        }

        Debug.Log("[ImageSender] Importer assembly: " + typeof(Importer).Assembly.FullName);
        LogShaderAvailability();

        string baseUrl = ServerEndpointSettings.GetBaseUrl();
#if UNITY_ANDROID && !UNITY_EDITOR
        if (baseUrl.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) >= 0 ||
            baseUrl.IndexOf("127.0.0.1", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            Debug.LogWarning("[ImageSender] Base URL points to localhost on Android. Set it to your PC LAN IP.");
        }
#endif

        isShuttingDown = false;
        if (sendLoopCoroutine == null)
        {
            sendLoopCoroutine = StartCoroutine(SendImageCoroutine());
        }
        isInitialized = true;
    }

    private void OnEnable()
    {
        if (!isInitialized) return;
        if (rawImage == null)
        {
            rawImage = GetComponent<RawImage>();
        }
        if (rawImage == null) return;

        isShuttingDown = false;
        if (sendLoopCoroutine == null)
        {
            sendLoopCoroutine = StartCoroutine(SendImageCoroutine());
        }
    }

    private void OnDisable()
    {
        Shutdown();
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    private IEnumerator SendImageCoroutine()
    {
        yield return new WaitForSeconds(1.0f);

        while (!isShuttingDown && isActiveAndEnabled)
        {
            Texture screenTexture = rawImage.texture;
            if (screenTexture == null)
            {
                Debug.LogWarning("[ImageSender] Texture is null.");
                yield return new WaitForSeconds(interval);
                continue;
            }

            byte[] imageData = ConvertTextureToJpg(screenTexture);
            if (imageData == null || imageData.Length == 0)
            {
                Debug.LogWarning("[ImageSender] Failed to encode texture to JPG.");
                yield return new WaitForSeconds(interval);
                continue;
            }

            yield return StartCoroutine(UploadImage(imageData));
            yield return new WaitForSeconds(interval);
        }

        sendLoopCoroutine = null;
    }

    private byte[] ConvertTextureToJpg(Texture texture)
    {
        const int targetWidth = 512;
        const int targetHeight = 512;

        var tex2d = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
        var renderTex = RenderTexture.GetTemporary(targetWidth, targetHeight, 0,
            RenderTextureFormat.Default, RenderTextureReadWrite.Linear);

        Graphics.Blit(texture, renderTex);
        RenderTexture.active = renderTex;
        tex2d.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
        tex2d.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(renderTex);

        byte[] imageData = tex2d.EncodeToJPG(75);
        Destroy(tex2d);

        return imageData;
    }

    private IEnumerator UploadImage(byte[] imageData)
    {
        if (isShuttingDown)
        {
            yield break;
        }

        if (imageData == null || imageData.Length == 0)
        {
            Debug.LogError("[ImageSender] Upload skipped: image data is empty.");
            yield break;
        }

        string endpoint = ServerEndpointSettings.BuildUrl(classificationPath);

        int attempts = Mathf.Max(1, maxUploadAttempts);
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            WWWForm form = new WWWForm();
            form.AddBinaryData("file", imageData, "image.jpg", "image/jpeg");

            using (UnityWebRequest www = UnityWebRequest.Post(endpoint, form))
            {
                www.useHttpContinue = false;
                www.timeout = timeoutSeconds;
                activeRequest = www;

                Debug.Log("[ImageSender] POST " + endpoint + $" (attempt {attempt}/{attempts})");
                yield return www.SendWebRequest();
                activeRequest = null;

                if (isShuttingDown)
                {
                    yield break;
                }

                if (www.result != UnityWebRequest.Result.Success)
                {
                    string err = www.error ?? string.Empty;
                    bool curl65 = err.IndexOf("Curl error 65", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (curl65 && attempt < attempts)
                    {
                        Debug.LogWarning("[ImageSender] Curl error 65 detected. Retrying request once.");
                        yield return null;
                        continue;
                    }

                    Debug.LogError("[ImageSender] Upload failed: " + err + " | HTTP " + www.responseCode);
                    yield break;
                }

                string contentType = www.GetResponseHeader("Content-Type") ?? string.Empty;
                string contentDisposition = www.GetResponseHeader("Content-Disposition") ?? string.Empty;
                byte[] responseData = www.downloadHandler?.data;

                bool isGlbResponse =
                    IsGlbContentType(contentType) ||
                    HasGlbFileName(contentDisposition) ||
                    LooksLikeGlb(responseData);

                if (!isGlbResponse)
                {
                    string body = www.downloadHandler?.text ?? string.Empty;
                    string bodyPreview = body.Length > 300 ? body.Substring(0, 300) : body;
                    Debug.LogWarning("[ImageSender] Response is not GLB. Body preview: " + bodyPreview);

                    if (TryExtractGlbUrl(body, endpoint, out string glbUrl))
                    {
                        Debug.Log("[ImageSender] GLB URL found in JSON response: " + glbUrl);

                        byte[] downloadedGlb = null;
                        yield return StartCoroutine(DownloadGlb(glbUrl, bytes => downloadedGlb = bytes));
                        if (downloadedGlb == null || downloadedGlb.Length == 0)
                        {
                            Debug.LogError("[ImageSender] Downloaded GLB is empty.");
                            yield break;
                        }

                        responseData = downloadedGlb;
                    }
                    else
                    {
                        if (!missingFaceDataWarningShown &&
                            body.IndexOf("No registered face data available", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            missingFaceDataWarningShown = true;
                            Debug.LogError("[ImageSender] Face data is not registered on server. Register face data first.");
                        }

                        yield break;
                    }
                }

                if (responseData == null || responseData.Length == 0)
                {
                    Debug.LogError("[ImageSender] GLB response body is empty.");
                    yield break;
                }

                if (saveReceivedGlbToDisk)
                {
                    try
                    {
                        string savePath = Path.Combine(
                            Application.persistentDataPath,
                            "model_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".glb");
                        File.WriteAllBytes(savePath, responseData);
                        Debug.Log("[ImageSender] Saved GLB: " + savePath);
                    }
                    catch (Exception saveEx)
                    {
                        Debug.LogWarning("[ImageSender] Failed to save GLB to disk: " + saveEx.Message);
                    }
                }

                GameObject importedModel = null;
                try
                {
                    ImportSettings importSettings = BuildImportSettings();
                    importedModel = Importer.LoadFromBytes(responseData, importSettings);
                }
                catch (Exception e)
                {
                    Debug.LogError("[ImageSender] Importer exception: " + e.Message);
                }

                if (importedModel == null)
                {
                    Debug.LogError("[ImageSender] Importer returned null.");
                    yield break;
                }

                PrepareImportedModel(importedModel);

                var replacer = FindObjectOfType<ModelReplacer>();
                if (replacer == null)
                {
                    Debug.LogError("[ImageSender] ModelReplacer not found.");
                    Destroy(importedModel);
                    yield break;
                }

                replacer.ReplaceModel(importedModel);
                Debug.Log("[ImageSender] Server model applied.");
                yield break;
            }
        }
    }

    private IEnumerator DownloadGlb(string glbUrl, Action<byte[]> onCompleted)
    {
        if (string.IsNullOrEmpty(glbUrl))
        {
            onCompleted?.Invoke(null);
            yield break;
        }

        using (UnityWebRequest req = UnityWebRequest.Get(glbUrl))
        {
            req.useHttpContinue = false;
            req.timeout = timeoutSeconds;
            activeRequest = req;

            yield return req.SendWebRequest();
            activeRequest = null;

            if (isShuttingDown)
            {
                onCompleted?.Invoke(null);
                yield break;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[ImageSender] GLB download failed: " + req.error + " | HTTP " + req.responseCode);
                onCompleted?.Invoke(null);
                yield break;
            }

            byte[] data = req.downloadHandler?.data;
            if (!LooksLikeGlb(data))
            {
                Debug.LogError("[ImageSender] Downloaded payload is not GLB.");
                onCompleted?.Invoke(null);
                yield break;
            }

            onCompleted?.Invoke(data);
        }
    }

    private void PrepareImportedModel(GameObject importedModel)
    {
        if (importedModel == null) return;

        if (convertImportedMaterialsToUrp)
        {
            URPMaterialHelper.ConvertToURPMaterials(importedModel, forceDoubleSided);
            return;
        }

        if (forceDoubleSided)
        {
            URPMaterialHelper.ForceDoubleSided(importedModel, true);
        }
    }

    private static bool IsGlbContentType(string contentType)
    {
        if (string.IsNullOrEmpty(contentType)) return false;

        return contentType.IndexOf("model/gltf-binary", StringComparison.OrdinalIgnoreCase) >= 0 ||
               contentType.IndexOf("application/gltf-binary", StringComparison.OrdinalIgnoreCase) >= 0 ||
               contentType.IndexOf("application/octet-stream", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool HasGlbFileName(string contentDisposition)
    {
        if (string.IsNullOrEmpty(contentDisposition)) return false;
        return contentDisposition.IndexOf(".glb", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool LooksLikeGlb(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 4) return false;
        return bytes[0] == 0x67 &&
               bytes[1] == 0x6C &&
               bytes[2] == 0x54 &&
               bytes[3] == 0x46;
    }

    private static bool TryExtractGlbUrl(string body, string endpoint, out string glbUrl)
    {
        glbUrl = null;
        if (string.IsNullOrWhiteSpace(body)) return false;

        Match match = Regex.Match(
            body,
            "\"(?:glb_url|glbUrl|model_url|modelUrl|download_url|downloadUrl|file_url|fileUrl|url|path)\"\\s*:\\s*\"(?<url>[^\"]+)\"",
            RegexOptions.IgnoreCase);

        if (!match.Success) return false;

        string rawUrl = match.Groups["url"].Value.Trim();
        if (string.IsNullOrEmpty(rawUrl)) return false;
        rawUrl = Regex.Unescape(rawUrl);
        if (rawUrl.IndexOf(".glb", StringComparison.OrdinalIgnoreCase) < 0) return false;

        if (Uri.TryCreate(rawUrl, UriKind.Absolute, out Uri absoluteUri))
        {
            glbUrl = absoluteUri.ToString();
            return true;
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri endpointUri))
        {
            return false;
        }

        if (!Uri.TryCreate(endpointUri, rawUrl, out Uri resolvedUri))
        {
            return false;
        }

        glbUrl = resolvedUri.ToString();
        return true;
    }

    private static ImportSettings BuildImportSettings()
    {
        var settings = new ImportSettings();
        var shaderOverrides = settings.shaderOverrides;

        AssignShaderOverride(shaderOverrides, "metallic",
            "GLTFUtility/URP/Standard (Metallic)",
            "GLTFUtility/Standard (Metallic)",
            "Universal Render Pipeline/Lit",
            "Standard");

        AssignShaderOverride(shaderOverrides, "metallicBlend",
            "GLTFUtility/URP/Standard Transparent (Metallic)",
            "GLTFUtility/Standard Transparent (Metallic)",
            "Universal Render Pipeline/Lit",
            "Standard");

        AssignShaderOverride(shaderOverrides, "specular",
            "GLTFUtility/URP/Standard (Specular)",
            "GLTFUtility/Standard (Specular)",
            "Universal Render Pipeline/Lit",
            "Standard");

        AssignShaderOverride(shaderOverrides, "specularBlend",
            "GLTFUtility/URP/Standard Transparent (Specular)",
            "GLTFUtility/Standard Transparent (Specular)",
            "Universal Render Pipeline/Lit",
            "Standard");

        return settings;
    }

    private static void AssignShaderOverride(ShaderSettings shaderSettings, string fieldName, params string[] candidates)
    {
        Shader shader = FindFirstShader(candidates);
        if (shader == null)
        {
            shader = FindGuaranteedShader();
            if (shader == null)
            {
                Debug.LogWarning("[ImageSender] No usable fallback shader for field: " + fieldName);
                return;
            }

            if (!loggedFallbackShader)
            {
                loggedFallbackShader = true;
                Debug.LogWarning("[ImageSender] GLTFUtility shader missing. Falling back to: " + shader.name);
            }
        }

        FieldInfo field = typeof(ShaderSettings).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(shaderSettings, shader);
        }
    }

    private static Shader FindFirstShader(params string[] names)
    {
        foreach (string name in names)
        {
            Shader s = Shader.Find(name);
            if (s != null) return s;
        }
        return null;
    }

    private static Shader FindGuaranteedShader()
    {
        return FindFirstShader(
            "Universal Render Pipeline/Lit",
            "Standard",
            "Unlit/Texture",
            "Sprites/Default",
            "UI/Default",
            "Hidden/InternalErrorShader");
    }

    private void Shutdown()
    {
        isShuttingDown = true;

        if (sendLoopCoroutine != null)
        {
            StopCoroutine(sendLoopCoroutine);
            sendLoopCoroutine = null;
        }

        if (activeRequest != null)
        {
            try
            {
                activeRequest.Abort();
            }
            catch
            {
                // Ignore abort errors during teardown.
            }
            activeRequest = null;
        }
    }

    private static void LogShaderAvailability()
    {
        string[] names =
        {
            "GLTFUtility/URP/Standard (Metallic)",
            "GLTFUtility/URP/Standard Transparent (Metallic)",
            "GLTFUtility/URP/Standard (Specular)",
            "GLTFUtility/URP/Standard Transparent (Specular)",
            "GLTFUtility/Standard (Metallic)",
            "GLTFUtility/Standard Transparent (Metallic)",
            "GLTFUtility/Standard (Specular)",
            "GLTFUtility/Standard Transparent (Specular)",
            "Universal Render Pipeline/Lit",
            "Standard"
        };

        foreach (string name in names)
        {
            Debug.Log("[ImageSender] Shader check: " + name + " => " + (Shader.Find(name) != null ? "FOUND" : "MISSING"));
        }
    }
}
