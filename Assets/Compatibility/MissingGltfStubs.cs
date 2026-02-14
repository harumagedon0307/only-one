// Compatibility stubs for environments where GLTF packages are absent.
// Keep disabled by default. Enable only when absolutely necessary by defining:
// HNW_ENABLE_MISSING_GLTF_STUBS
#if HNW_ENABLE_MISSING_GLTF_STUBS
using System.IO;
using UnityEngine;

namespace Siccity.GLTFUtility
{
    public static class Importer
    {
        public static GameObject LoadFromFile(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[Stub Importer] File not found: {path}");
                return null;
            }

            var go = new GameObject("StubImportedModel");
            Debug.LogWarning("[Stub Importer] Siccity.GLTFUtility is missing. Returning placeholder GameObject.");
            return go;
        }
    }
}

namespace UnityGLTF
{
    public class ExportContext
    {
    }

    public class GLTFSceneExporter
    {
        public GLTFSceneExporter(Transform[] roots, ExportContext context)
        {
        }

        public void SaveGLBToStream(Stream stream, string sceneName)
        {
            // Write an empty payload so callers can continue without package dependency.
            stream.WriteByte(0);
            Debug.LogWarning("[Stub Exporter] UnityGLTF is missing. Wrote placeholder .glb content.");
        }
    }
}
#endif