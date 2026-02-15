using System;
using System.Reflection;
using Siccity.GLTFUtility;
using UnityEngine;

public static class GltfImportRuntimeHelper
{
    private static bool loggedFallbackShader;

    public static GameObject LoadFromFileSafe(string path)
    {
        try
        {
            return Importer.LoadFromFile(path, BuildImportSettings());
        }
        catch (Exception e)
        {
            Debug.LogError("[GltfImportRuntimeHelper] LoadFromFile failed: " + e.Message + " | path=" + path);
            return null;
        }
    }

    public static GameObject LoadFromBytesSafe(byte[] bytes)
    {
        try
        {
            return Importer.LoadFromBytes(bytes, BuildImportSettings());
        }
        catch (Exception e)
        {
            Debug.LogError("[GltfImportRuntimeHelper] LoadFromBytes failed: " + e.Message);
            return null;
        }
    }

    public static ImportSettings BuildImportSettings()
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

        EnsureNonNullShaderOverrides(shaderOverrides);
        shaderOverrides.CacheDefaultShaders();
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
                Debug.LogError("[GltfImportRuntimeHelper] No fallback shader for field: " + fieldName);
                return;
            }

            if (!loggedFallbackShader)
            {
                loggedFallbackShader = true;
                Debug.LogWarning("[GltfImportRuntimeHelper] Using fallback shader: " + shader.name);
            }
        }

        FieldInfo field = typeof(ShaderSettings).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            Debug.LogWarning("[GltfImportRuntimeHelper] ShaderSettings field not found: " + fieldName);
            return;
        }

        field.SetValue(shaderSettings, shader);
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

    private static void EnsureNonNullShaderOverrides(ShaderSettings shaderSettings)
    {
        if (shaderSettings == null) return;

        bool hasNull =
            shaderSettings.Metallic == null ||
            shaderSettings.MetallicBlend == null ||
            shaderSettings.Specular == null ||
            shaderSettings.SpecularBlend == null;
        if (!hasNull) return;

        Shader fallback = FindGuaranteedShader();
        if (fallback == null)
        {
            Debug.LogError("[GltfImportRuntimeHelper] Failed to resolve any fallback shader.");
            return;
        }

        AssignShaderOverride(shaderSettings, "metallic", fallback.name);
        AssignShaderOverride(shaderSettings, "metallicBlend", fallback.name);
        AssignShaderOverride(shaderSettings, "specular", fallback.name);
        AssignShaderOverride(shaderSettings, "specularBlend", fallback.name);
        Debug.LogWarning("[GltfImportRuntimeHelper] Some shader overrides were null. Forced fallback shader: " + fallback.name);
    }
}
