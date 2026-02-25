#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ReduceNormalMapSize : EditorWindow
{
    private const string WINDOW_TITLE = "Reduce Scene Normal Map Size";

    private static readonly int[] SizeOptions = { 512, 1024, 2048, 4096 };
    private static readonly string[] SizeLabels = { "512", "1024", "2048", "4096" };

    private int targetMaxSize = 1024;

    private readonly List<string> normalsNeedingChange = new();
    private readonly List<string> normalsAllFound = new();

    private Vector2 scroll;

    [MenuItem("Tools/Italiandogs/Reduce Scene Normal Map Size")]
    public static void ShowWindow()
    {
        var window = GetWindow<ReduceNormalMapSize>(WINDOW_TITLE);
        window.RefreshList();
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Finds normal maps referenced by the currently loaded scene object(s) and reduces their import Max Size.",
            EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(8);

        targetMaxSize = EditorGUILayout.IntPopup("Target Max Size", targetMaxSize, SizeLabels, SizeOptions);

        EditorGUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh", GUILayout.Height(28)))
            {
                RefreshList();
            }

            using (new EditorGUI.DisabledScope(normalsNeedingChange.Count == 0))
            {
                if (GUILayout.Button("Apply", GUILayout.Height(28)))
                {
                    var confirm = EditorUtility.DisplayDialog(
                        WINDOW_TITLE,
                        $"This will reduce up to {normalsNeedingChange.Count} normal map(s) to {targetMaxSize} Max Size (never upscales).\n\nContinue?",
                        "Yes",
                        "No");

                    if (confirm)
                    {
                        Process();
                        RefreshList();
                    }
                }
            }
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Only textures that are referenced by objects in the currently loaded scene(s) are considered.\n" +
            "A texture qualifies as a normal map if its importer Texture Type is set to 'Normal map'.",
            MessageType.Info);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField($"Normal maps found in scene dependencies: {normalsAllFound.Count}");
        EditorGUILayout.LabelField($"Normal maps needing change: {normalsNeedingChange.Count}");

        if (normalsNeedingChange.Count > 0)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Will be changed:", EditorStyles.boldLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var path in normalsNeedingChange)
            {
                EditorGUILayout.LabelField(path);
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void RefreshList()
    {
        normalsNeedingChange.Clear();
        normalsAllFound.Clear();

        var rootObjects = GetAllLoadedSceneRootObjects();
        if (rootObjects.Count == 0)
        {
            return;
        }

        var dependencies = EditorUtility.CollectDependencies(rootObjects.ToArray());

        var texturePaths = new HashSet<string>();
        foreach (var obj in dependencies)
        {
            if (obj is not Texture2D)
            {
                continue;
            }

            var assetPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                continue;
            }

            texturePaths.Add(assetPath);
        }

        foreach (var assetPath in texturePaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            if (importer.textureType != TextureImporterType.NormalMap)
            {
                continue;
            }

            normalsAllFound.Add(assetPath);

            if (NeedsReduction(importer, targetMaxSize))
            {
                normalsNeedingChange.Add(assetPath);
            }
        }
    }

    private static List<GameObject> GetAllLoadedSceneRootObjects()
    {
        var roots = new List<GameObject>(256);

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
            {
                continue;
            }

            roots.AddRange(scene.GetRootGameObjects());
        }

        return roots;
    }

    private static bool NeedsReduction(TextureImporter importer, int target)
    {
        if (importer.maxTextureSize > target)
        {
            return true;
        }

        // Reduce any existing platform override max sizes too (only if overridden).
        foreach (var platform in GetKnownPlatforms())
        {
            var ps = importer.GetPlatformTextureSettings(platform);
            if (ps != null && ps.overridden && ps.maxTextureSize > target)
            {
                return true;
            }
        }

        return false;
    }

    private void Process()
    {
        int changedCount = 0;
        var failed = new List<string>();

        int count = normalsNeedingChange.Count;
        for (int i = 0; i < count; i++)
        {
            var assetPath = normalsNeedingChange[i];

            if (EditorUtility.DisplayCancelableProgressBar(
                WINDOW_TITLE,
                $"Processing {Path.GetFileName(assetPath)} ({i + 1}/{count})",
                (float)i / count))
            {
                break;
            }

            try
            {
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    failed.Add(assetPath);
                    continue;
                }

                bool changed = false;

                if (importer.maxTextureSize > targetMaxSize)
                {
                    importer.maxTextureSize = targetMaxSize;
                    changed = true;
                }

                foreach (var platform in GetKnownPlatforms())
                {
                    var ps = importer.GetPlatformTextureSettings(platform);
                    if (ps == null || !ps.overridden)
                    {
                        continue;
                    }

                    if (ps.maxTextureSize > targetMaxSize)
                    {
                        ps.maxTextureSize = targetMaxSize;
                        importer.SetPlatformTextureSettings(ps);
                        changed = true;
                    }
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                    changedCount++;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{nameof(ReduceNormalMapSize)}] Failed processing '{assetPath}': {e.Message}");
                failed.Add(assetPath);
            }
        }

        EditorUtility.ClearProgressBar();

        if (failed.Count > 0)
        {
            Debug.LogWarning($"[{nameof(ReduceNormalMapSize)}] Completed with failures. Failed: {failed.Count}");
            foreach (var path in failed)
            {
                Debug.LogWarning($"[{nameof(ReduceNormalMapSize)}] Failed: {path}");
            }
        }

        EditorUtility.DisplayDialog(
            WINDOW_TITLE,
            $"Done.\nChanged: {changedCount}\nFailed: {failed.Count}",
            "OK");
    }

    private static IEnumerable<string> GetKnownPlatforms()
    {
        // Common Unity platform names for TextureImporterPlatformSettings.
        yield return "Standalone";
        yield return "Android";
        yield return "iPhone";
    }
}
#endif
