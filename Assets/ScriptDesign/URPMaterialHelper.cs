using UnityEngine;

// Minimal fallback helper to avoid compile errors when URP conversion is not present.
using UnityEngine.Rendering;

public static class URPMaterialHelper
{
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
        if (forceDoubleSided)
        {
            ForceDoubleSided(target, true);
        }
    }
}
