using UnityEngine;

// Minimal fallback helper to avoid compile errors when URP conversion is not present.
using UnityEngine.Rendering;

public static class URPMaterialHelper
{
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProp = Shader.PropertyToID("_Color");
    private static readonly int SurfaceProp = Shader.PropertyToID("_Surface");
    private static readonly int CullProp = Shader.PropertyToID("_Cull");
    private static readonly int AlphaClipProp = Shader.PropertyToID("_AlphaClip");
    private static readonly int ZWriteProp = Shader.PropertyToID("_ZWrite");
    private static readonly int SrcBlendProp = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendProp = Shader.PropertyToID("_DstBlend");

    public static void EnsureVisible(GameObject target, bool forceDoubleSided = true)
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
                }
                if (mat.HasProperty(AlphaClipProp))
                {
                    mat.SetFloat(AlphaClipProp, 0f);
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

                if (mat.HasProperty(CullProp))
                {
                    mat.SetFloat(CullProp, (float)(forceDoubleSided ? CullMode.Off : CullMode.Back));
                }

                mat.renderQueue = -1;
            }
        }
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

    public static void ConvertToURPMaterials(GameObject target, bool forceDoubleSided = true)
    {
        if (target == null) return;
        EnsureVisible(target, forceDoubleSided);
        if (forceDoubleSided)
        {
            ForceDoubleSided(target, true);
        }
    }
}
