using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class TextureCompressionTool : EditorWindow
{
    private List<string> texturesToChange = new List<string>();
    private List<string> texturesFailed = new List<string>();
    private int totalTexturesChanged = 0;

    [MenuItem("Tools/Italiandogs/Texture Compression Tool")]
    public static void ShowWindow()
    {
        TextureCompressionTool window = GetWindow<TextureCompressionTool>("Texture Compression Tool");
        window.FindTexturesToProcess();
        window.Show();
    }

    private void FindTexturesToProcess()
    {
        // Find all root game objects in the scene
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

        // Collect all dependencies
        Object[] dependencies = EditorUtility.CollectDependencies(rootObjects);

        // Filter out Texture2D assets
        HashSet<string> texturePaths = new HashSet<string>();
        foreach (Object obj in dependencies)
        {
            if (obj is Texture2D)
            {
                string assetPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    texturePaths.Add(assetPath);
                }
            }
        }

        texturesToChange.Clear();
        foreach (string assetPath in texturePaths)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType == TextureImporterType.Default)
            {
                bool needsUpdate = false;

                // Check if compression is set to 'None'
                if (importer.textureCompression == TextureImporterCompression.Uncompressed)
                {
                    needsUpdate = true;
                }

                // Check if Crunch Compression is not enabled
                if (!importer.crunchedCompression)
                {
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    texturesToChange.Add(assetPath);
                }
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Texture Compression Tool", EditorStyles.boldLabel);
        GUILayout.Space(10);
        GUILayout.Label($"Found {texturesToChange.Count} textures to process.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(10);

        if (texturesToChange.Count > 0)
        {
            if (GUILayout.Button("Process Textures", GUILayout.Height(40)))
            {
                bool confirm = EditorUtility.DisplayDialog(
                    "Confirm Texture Processing",
                    $"This will process {texturesToChange.Count} textures.\nDo you want to continue?",
                    "Yes",
                    "No"
                );

                if (confirm)
                {
                    ProcessTextures();
                }
            }
        }
        else
        {
            GUILayout.Label("No textures need processing.", EditorStyles.wordWrappedLabel);
        }
    }

    private void ProcessTextures()
    {
        totalTexturesChanged = 0;
        texturesFailed.Clear();

        int count = texturesToChange.Count;
        for (int i = 0; i < count; i++)
        {
            string assetPath = texturesToChange[i];

            if (EditorUtility.DisplayCancelableProgressBar(
                "Processing Textures",
                $"Processing {System.IO.Path.GetFileName(assetPath)} ({i + 1}/{count})",
                (float)i / count))
            {
                break;
            }

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                bool changed = false;

                // Apply 'Normal Quality' compression if compression is 'None'
                if (importer.textureCompression == TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    importer.compressionQuality = 50;
                    changed = true;
                }

                // Enable Crunch Compression if not already enabled
                if (!importer.crunchedCompression)
                {
                    importer.crunchedCompression = true;
                    importer.compressionQuality = 50; // Set compressor quality to 50
                    changed = true;
                }

                if (changed)
                {
                    try
                    {
                        importer.SaveAndReimport();
                        totalTexturesChanged++;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Failed to import asset at path {assetPath}: {e.Message}");
                        texturesFailed.Add(assetPath);
                    }
                }
            }
            else
            {
                Debug.LogWarning($"Could not get TextureImporter for asset at path {assetPath}");
                texturesFailed.Add(assetPath);
            }
        }

        EditorUtility.ClearProgressBar();

        // Show summary window
        TextureCompressionSummaryWindow.ShowWindow(totalTexturesChanged, texturesFailed);
    }
}

public class TextureCompressionSummaryWindow : EditorWindow
{
    private int totalTexturesChanged;
    private List<string> texturesFailed;

    public static void ShowWindow(int totalTexturesChanged, List<string> texturesFailed)
    {
        TextureCompressionSummaryWindow window = GetWindow<TextureCompressionSummaryWindow>("Processing Complete");
        window.totalTexturesChanged = totalTexturesChanged;
        window.texturesFailed = texturesFailed;
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Label("Processing Complete", EditorStyles.boldLabel);
        GUILayout.Space(10);
        GUILayout.Label($"Textures processed: {totalTexturesChanged}", EditorStyles.wordWrappedLabel);
        GUILayout.Label($"Textures failed: {texturesFailed.Count}", EditorStyles.wordWrappedLabel);
        GUILayout.Space(10);

        if (texturesFailed.Count > 0)
        {
            GUILayout.Label("Textures that failed to process:", EditorStyles.boldLabel);
            foreach (string path in texturesFailed)
            {
                GUILayout.Label(path, EditorStyles.wordWrappedLabel);
            }
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Close", GUILayout.Height(30)))
        {
            Close();
        }
    }
}
