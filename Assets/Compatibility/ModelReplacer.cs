using UnityEngine;

public class CompatibilityModelReplacerStub : MonoBehaviour
{
    public void ReplaceModel(GameObject newModel)
    {
        if (newModel == null) return;
        Debug.LogWarning("CompatibilityModelReplacerStub is active. Imported model is not attached.");
        Destroy(newModel);
    }
}
