using System;
using UnityEngine;

public static class ServerEndpointSettings
{
    private const string PlayerPrefsKey = "HNW_SERVER_BASE_URL";
    private const string DefaultBaseUrl = "http://192.168.0.4:8000";

    public static string GetBaseUrl()
    {
        string raw = PlayerPrefs.GetString(PlayerPrefsKey, DefaultBaseUrl);
        return NormalizeBaseUrl(raw);
    }

    public static void SetBaseUrl(string raw)
    {
        string normalized = NormalizeBaseUrl(raw);
        PlayerPrefs.SetString(PlayerPrefsKey, normalized);
        PlayerPrefs.Save();
        Debug.Log("[ServerEndpointSettings] Base URL updated: " + normalized);
    }

    public static string BuildUrl(string path)
    {
        string baseUrl = GetBaseUrl();
        string normalizedPath = NormalizePath(path);
        return baseUrl + normalizedPath;
    }

    public static void ResetToDefault()
    {
        PlayerPrefs.DeleteKey(PlayerPrefsKey);
        PlayerPrefs.Save();
        Debug.Log("[ServerEndpointSettings] Base URL reset to default: " + DefaultBaseUrl);
    }

    private static string NormalizeBaseUrl(string raw)
    {
        string value = (raw ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(value))
        {
            value = DefaultBaseUrl;
        }

        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = "http://" + value;
        }

        return value.TrimEnd('/');
    }

    private static string NormalizePath(string path)
    {
        string p = (path ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(p)) return string.Empty;
        if (!p.StartsWith("/")) p = "/" + p;
        return p;
    }
}
