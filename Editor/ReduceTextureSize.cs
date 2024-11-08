using UnityEngine;
using UnityEditor;

public class ReduceTextureSize : MonoBehaviour
{
    [MenuItem("Tools/Italiandogs/Reduce Large Textures")]
    public static void ReduceLargeTextures()
    {
        // Get all materials in the active scene
        var renderers = FindObjectsOfType<Renderer>();

        foreach (var renderer in renderers)
        {
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null) continue;

                foreach (var texName in material.GetTexturePropertyNames())
                {
                    Texture texture = material.GetTexture(texName);
                    if (texture != null)
                    {
                        string path = AssetDatabase.GetAssetPath(texture);
                        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                        // Include textures with size 2048 or greater
                        if (importer != null && importer.maxTextureSize >= 2048)
                        {
                            importer.maxTextureSize = 1024;
                            importer.SaveAndReimport();
                            Debug.Log($"Reduced texture size: {texture.name}");
                        }
                    }
                }
            }
        }

        Debug.Log("Finished reducing textures of size 2048px or larger.");
    }
}
