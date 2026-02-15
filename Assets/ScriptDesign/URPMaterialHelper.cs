using UnityEngine;

// Minimal fallback helper to avoid compile errors when URP conversion is not present.
using UnityEngine.Rendering;

public static class URPMaterialHelper
{
    private static readonly Color FallbackVisibleColor = new Color(1.0f, 0.45f, 0.2f, 1.0f);
    private static readonly int BaseMapProp = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexProp = Shader.PropertyToID("_MainTex");
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProp = Shader.PropertyToID("_Color");
    private static readonly int ModeProp = Shader.PropertyToID("_Mode");
    private static readonly int SurfaceProp = Shader.PropertyToID("_Surface");
    private static readonly int CullProp = Shader.PropertyToID("_Cull");
    private static readonly int AlphaClipProp = Shader.PropertyToID("_AlphaClip");
    private static readonly int ZWriteProp = Shader.PropertyToID("_ZWrite");
    private static readonly int SrcBlendProp = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendProp = Shader.PropertyToID("_DstBlend");
    private static readonly int TintColorProp = Shader.PropertyToID("_TintColor");
    private static bool loggedShaderUpgrade = false;
    private static bool loggedSolidColorOverride = false;

    public static void EnsureVisible(
        GameObject target,
        bool forceDoubleSided = true,
        bool preferUnlit = false,
        bool forceSolidColor = false)
    {
        if (target == null) return;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            renderer.enabled = true;

            Material[] materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material mat = materials[i];
                if (mat == null) continue;
                TryUpgradeShaderForRuntime(mat, preferUnlit);
                if (forceSolidColor)
                {
                    ApplySolidColorOverride(mat);
                    ApplyOpaqueRenderState(mat);
                }
                else
                {
                    EnsureColorAlphaVisible(mat);
                }

                if (mat.HasProperty(CullProp))
                {
                    mat.SetFloat(CullProp, (float)(forceDoubleSided ? CullMode.Off : CullMode.Back));
                }
            }
        }
    }

    private static void ApplyOpaqueRenderState(Material mat)
    {
        if (mat == null) return;

        if (mat.HasProperty(BaseColorProp))
        {
            Color c = mat.GetColor(BaseColorProp);
            c.a = 1f;
            mat.SetColor(BaseColorProp, c);
        }
        if (mat.HasProperty(ColorProp))
        {
            Color c = mat.GetColor(ColorProp);
            c.a = 1f;
            mat.SetColor(ColorProp, c);
        }

        if (mat.HasProperty(SurfaceProp))
        {
            mat.SetFloat(SurfaceProp, 0f);
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        if (mat.HasProperty(AlphaClipProp))
        {
            mat.SetFloat(AlphaClipProp, 0f);
            mat.DisableKeyword("_ALPHATEST_ON");
        }
        if (mat.HasProperty(ZWriteProp))
        {
            mat.SetFloat(ZWriteProp, 1f);
        }
        if (mat.HasProperty(SrcBlendProp))
        {
            mat.SetFloat(SrcBlendProp, (float)BlendMode.One);
        }
        if (mat.HasProperty(DstBlendProp))
        {
            mat.SetFloat(DstBlendProp, (float)BlendMode.Zero);
        }

        mat.renderQueue = (int)RenderQueue.Geometry;
    }

    private static void ApplySolidColorOverride(Material mat)
    {
        if (mat == null) return;

        if (mat.HasProperty(BaseMapProp))
        {
            mat.SetTexture(BaseMapProp, null);
        }
        if (mat.HasProperty(MainTexProp))
        {
            mat.SetTexture(MainTexProp, null);
        }

        if (mat.HasProperty(BaseColorProp))
        {
            mat.SetColor(BaseColorProp, FallbackVisibleColor);
        }
        if (mat.HasProperty(ColorProp))
        {
            mat.SetColor(ColorProp, FallbackVisibleColor);
        }
        if (mat.HasProperty(TintColorProp))
        {
            mat.SetColor(TintColorProp, FallbackVisibleColor);
        }

        if (!loggedSolidColorOverride)
        {
            loggedSolidColorOverride = true;
            Debug.Log("[URPMaterialHelper] Solid color visibility override is active.");
        }
    }

    private static void EnsureColorAlphaVisible(Material mat)
    {
        if (mat == null) return;

        if (mat.HasProperty(BaseColorProp))
        {
            Color c = mat.GetColor(BaseColorProp);
            if (c.a < 0.99f)
            {
                c.a = 1f;
                mat.SetColor(BaseColorProp, c);
            }
        }
        if (mat.HasProperty(ColorProp))
        {
            Color c = mat.GetColor(ColorProp);
            if (c.a < 0.99f)
            {
                c.a = 1f;
                mat.SetColor(ColorProp, c);
            }
        }
    }

    private static void TryUpgradeShaderForRuntime(Material mat, bool preferUnlit)
    {
        if (mat == null || mat.shader == null) return;

        string shaderName = mat.shader.name ?? string.Empty;
        bool isGltfUtilityStandard =
            shaderName.IndexOf("GLTFUtility/Standard", System.StringComparison.OrdinalIgnoreCase) >= 0;
        bool isBuiltinStandard =
            shaderName.Equals("Standard", System.StringComparison.OrdinalIgnoreCase) ||
            shaderName.IndexOf("Standard (Specular setup)", System.StringComparison.OrdinalIgnoreCase) >= 0;
        if (!isGltfUtilityStandard && !isBuiltinStandard) return;
        bool wasTransparent = IsLikelyTransparent(mat, shaderName);

        Shader targetShader = null;
        if (preferUnlit)
        {
            targetShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (targetShader == null)
            {
                targetShader = Shader.Find("Unlit/Texture");
            }
            if (targetShader == null)
            {
                targetShader = Shader.Find("Sprites/Default");
            }
        }

        if (targetShader == null)
        {
            bool usingRenderPipeline = GraphicsSettings.renderPipelineAsset != null;
            targetShader = Shader.Find("Universal Render Pipeline/Lit");
            if (targetShader == null && usingRenderPipeline)
            {
                targetShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            }
            if (targetShader == null && usingRenderPipeline)
            {
                targetShader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            if (targetShader == null)
            {
                targetShader = Shader.Find("Unlit/Texture");
            }
            if (targetShader == null)
            {
                targetShader = Shader.Find("Sprites/Default");
            }
            if (targetShader == null)
            {
                targetShader = Shader.Find("Standard");
            }
        }

        if (targetShader == mat.shader)
        {
            // In URP runtime, keeping built-in Standard often fails to render properly on device.
            bool usingRenderPipeline = GraphicsSettings.renderPipelineAsset != null;
            if (usingRenderPipeline && isBuiltinStandard)
            {
                Shader fallback = Shader.Find("Unlit/Texture");
                if (fallback == null)
                {
                    fallback = Shader.Find("Sprites/Default");
                }
                if (fallback != null && fallback != mat.shader)
                {
                    targetShader = fallback;
                }
            }
        }

        if (targetShader == null || targetShader == mat.shader) return;

        Texture mainTex = null;
        if (mat.HasProperty(BaseMapProp)) mainTex = mat.GetTexture(BaseMapProp);
        if (mainTex == null && mat.HasProperty(MainTexProp)) mainTex = mat.GetTexture(MainTexProp);

        Color baseColor = Color.white;
        if (mat.HasProperty(BaseColorProp))
        {
            baseColor = mat.GetColor(BaseColorProp);
        }
        else if (mat.HasProperty(ColorProp))
        {
            baseColor = mat.GetColor(ColorProp);
        }

        mat.shader = targetShader;

        if (mat.HasProperty(BaseMapProp) && mainTex != null)
        {
            mat.SetTexture(BaseMapProp, mainTex);
        }
        if (mat.HasProperty(MainTexProp) && mainTex != null)
        {
            mat.SetTexture(MainTexProp, mainTex);
        }
        if (mat.HasProperty(BaseColorProp))
        {
            mat.SetColor(BaseColorProp, baseColor);
        }
        if (mat.HasProperty(ColorProp))
        {
            mat.SetColor(ColorProp, baseColor);
        }

        if (wasTransparent)
        {
            if (mat.HasProperty(SurfaceProp))
            {
                mat.SetFloat(SurfaceProp, 1f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            if (mat.HasProperty(AlphaClipProp))
            {
                mat.SetFloat(AlphaClipProp, 0f);
                mat.DisableKeyword("_ALPHATEST_ON");
            }
            if (mat.HasProperty(ZWriteProp))
            {
                mat.SetFloat(ZWriteProp, 0f);
            }
            if (mat.HasProperty(SrcBlendProp))
            {
                mat.SetFloat(SrcBlendProp, (float)BlendMode.SrcAlpha);
            }
            if (mat.HasProperty(DstBlendProp))
            {
                mat.SetFloat(DstBlendProp, (float)BlendMode.OneMinusSrcAlpha);
            }

            mat.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            mat.renderQueue = -1;
        }

        if (!loggedShaderUpgrade)
        {
            loggedShaderUpgrade = true;
            Debug.Log("[URPMaterialHelper] Upgraded material shader to: " + targetShader.name + " (from=" + shaderName + ", preferUnlit=" + preferUnlit + ")");
        }
    }

    private static bool IsLikelyTransparent(Material mat, string shaderName)
    {
        if (shaderName.IndexOf("Transparent", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (mat != null && mat.HasProperty(ModeProp))
        {
            // Standard shader: 0=Opaque,1=Cutout,2=Fade,3=Transparent
            float mode = mat.GetFloat(ModeProp);
            if (mode >= 2f)
            {
                return true;
            }
        }

        if (mat != null && mat.renderQueue >= (int)RenderQueue.Transparent - 10)
        {
            return true;
        }

        return false;
    }

    public static void ForceDoubleSided(GameObject target, bool doubleSided = true)
    {
        if (target == null) return;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material mat = materials[i];
                if (mat == null) continue;
                if (mat.HasProperty("_Cull"))
                {
                    mat.SetFloat("_Cull", (float)(doubleSided ? CullMode.Off : CullMode.Back));
                }
            }
        }
    }

    public static void ConvertToURPMaterials(
        GameObject target,
        bool forceDoubleSided = true,
        bool preferUnlit = false,
        bool forceSolidColor = false)
    {
        if (target == null) return;
        EnsureVisible(target, forceDoubleSided, preferUnlit, forceSolidColor);
        if (forceDoubleSided)
        {
            ForceDoubleSided(target, true);
        }
    }
}
