using UnityEngine;
using UnityEditor;

public class ApplyMeshCompression : MonoBehaviour
{
    [MenuItem("Tools/Italiandogs/Apply Mesh Compression")]
    public static void ApplyMeshCompressionToSceneObjects()
    {
        // Get all mesh filters in the active scene
        var meshFilters = FindObjectsOfType<MeshFilter>();

        foreach (var meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh != null)
            {
                string path = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;

                if (importer != null && importer.meshCompression == ModelImporterMeshCompression.Off)
                {
                    importer.meshCompression = ModelImporterMeshCompression.Medium;
                    importer.SaveAndReimport();
                    Debug.Log($"Applied medium mesh compression to: {meshFilter.sharedMesh.name}");
                }
            }
        }

        Debug.Log("Finished applying medium mesh compression to meshes without compression.");
    }
}
