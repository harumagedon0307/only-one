using System;
using UnityEngine;

public static class ServerEndpointSettings
{
    private const string PlayerPrefsKey = "HNW_SERVER_BASE_URL";
    private const string DefaultBaseUrl = "http://192.168.101.83:8000";

    public static string GetBaseUrl()
    {
        string raw = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        string normalized = NormalizeBaseUrl(raw);

        if (ShouldMigrateToDefault(normalized))
        {
            normalized = NormalizeBaseUrl(DefaultBaseUrl);
            PlayerPrefs.SetString(PlayerPrefsKey, normalized);
            PlayerPrefs.Save();
        }

        return normalized;
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

    private static bool ShouldMigrateToDefault(string normalized)
    {
        if (string.IsNullOrEmpty(normalized))
        {
            return true;
        }

        string currentDefault = NormalizeBaseUrl(DefaultBaseUrl);
        if (string.Equals(normalized, currentDefault, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalized.IndexOf("106.146.21.176", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("127.0.0.1", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("192.168.", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("10.", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("172.16.", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("172.17.", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("172.18.", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("172.19.", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("172.20.", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("172.21.", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("172.22.", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("172.23.", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("172.24.", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("172.25.", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("172.26.", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("172.27.", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("172.28.", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("172.29.", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("172.30.", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("172.31.", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return false;
    }

    private static string NormalizePath(string path)
    {
        string p = (path ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(p)) return string.Empty;
        if (!p.StartsWith("/")) p = "/" + p;
        return p;
    }
}
