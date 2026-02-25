using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ApplyMeshCompressionWindow : EditorWindow
{
    private ModelImporterMeshCompression _targetCompression = ModelImporterMeshCompression.Medium;
    private Vector2 _scroll;
    private ScanResults _scanResults;
    private readonly Dictionary<string, bool> _foldoutsByAssetPath = new(StringComparer.OrdinalIgnoreCase);

    [MenuItem("Tools/Italiandogs/Apply Mesh Compression")]
    public static void Open()
    {
        var window = GetWindow<ApplyMeshCompressionWindow>(utility: false, title: "Apply Mesh Compression", focus: true);
        window.minSize = new Vector2(560f, 360f);
    }

    private void OnEnable()
    {
        _scanResults ??= new ScanResults();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (var changed = new EditorGUI.ChangeCheckScope())
            {
                _targetCompression = (ModelImporterMeshCompression)EditorGUILayout.EnumPopup("Mesh Compression", _targetCompression);
                if (changed.changed)
                {
                    // Keep the UI responsive: we don't auto-scan to avoid expensive rescans on every click.
                }
            }

            if (GUILayout.Button("Scan", GUILayout.Width(90)))
            {
                _scanResults = ScanActiveScene(_targetCompression);
                Repaint();
            }

            using (new EditorGUI.DisabledScope(_scanResults == null || _scanResults.AssetsToChange.Count == 0))
            {
                if (GUILayout.Button("Apply", GUILayout.Width(90)))
                {
                    if (EditorUtility.DisplayDialog(
                            "Apply Mesh Compression",
                            $"Apply '{_targetCompression}' mesh compression to {_scanResults.AssetsToChange.Count} model asset(s)?",
                            "Apply",
                            "Cancel"))
                    {
                        ApplyChanges(_scanResults, _targetCompression);
                        _scanResults = ScanActiveScene(_targetCompression);
                    }
                }
            }
        }

        EditorGUILayout.Space(8);
        DrawSummary(_scanResults);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Preview (Scene Objects Affected)", EditorStyles.boldLabel);

        if (_scanResults == null || _scanResults.AssetsToChange.Count == 0)
        {
            EditorGUILayout.HelpBox("Click Scan to see what would change.", MessageType.Info);
            return;
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (var entry in _scanResults.AssetsToChange.OrderBy(e => e.AssetPath, StringComparer.OrdinalIgnoreCase))
        {
            if (!_foldoutsByAssetPath.TryGetValue(entry.AssetPath, out var isOpen))
                isOpen = false;

            var header = $"{entry.AssetName}  ({entry.CurrentCompression} → {_targetCompression})";
            isOpen = EditorGUILayout.Foldout(isOpen, header, toggleOnLabelClick: true);
            _foldoutsByAssetPath[entry.AssetPath] = isOpen;

            if (!isOpen)
                continue;

            using (new EditorGUI.IndentLevelScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.SelectableLabel(entry.AssetPath, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    if (GUILayout.Button("Ping", GUILayout.Width(60)))
                    {
                        var asset = AssetDatabase.LoadMainAssetAtPath(entry.AssetPath);
                        if (asset != null)
                            EditorGUIUtility.PingObject(asset);
                    }
                }

                foreach (var obj in entry.SceneObjects.OrderBy(o => o.HierarchyPath, StringComparer.OrdinalIgnoreCase))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(obj.GameObject, typeof(GameObject), allowSceneObjects: true, GUILayout.Width(200));
                        EditorGUILayout.LabelField($"{obj.ComponentLabel}", GUILayout.Width(150));
                        EditorGUILayout.LabelField(obj.HierarchyPath);
                    }
                }
            }

            EditorGUILayout.Space(4);
        }
        EditorGUILayout.EndScrollView();
    }

    private static void DrawSummary(ScanResults results)
    {
        if (results == null)
        {
            EditorGUILayout.HelpBox("No scan results yet.", MessageType.None);
            return;
        }

        EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Model assets that would change: {results.AssetsToChange.Count}");
        EditorGUILayout.LabelField($"Scene object references affected: {results.SceneObjectReferenceCount}");
        if (results.NonModelImporters > 0)
            EditorGUILayout.LabelField($"Non-model mesh assets encountered: {results.NonModelImporters}");
        if (results.MeshesWithoutAssetPath > 0)
            EditorGUILayout.LabelField($"Meshes with no asset path: {results.MeshesWithoutAssetPath}");
    }

    private static ScanResults ScanActiveScene(ModelImporterMeshCompression targetCompression)
    {
        var results = new ScanResults();

        var activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            Debug.LogWarning("No active loaded scene found.");
            return results;
        }

        // Resources.FindObjectsOfTypeAll includes inactive objects (and also includes assets/prefabs in the project),
        // so we filter to objects that actually belong to the active scene.
        void AddMeshReference(GameObject go, string componentLabel, Mesh mesh)
        {
            if (go == null || mesh == null)
                return;
            if (go.scene != activeScene)
                return;
            if (EditorUtility.IsPersistent(go))
                return;

            var path = AssetDatabase.GetAssetPath(mesh);
            if (string.IsNullOrEmpty(path))
            {
                results.MeshesWithoutAssetPath++;
                return;
            }

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                results.NonModelImporters++;
                return;
            }

            if (importer.meshCompression == targetCompression)
                return;

            if (!results.AssetsByPath.TryGetValue(path, out var assetEntry))
            {
                assetEntry = new AssetEntry
                {
                    AssetPath = path,
                    AssetName = mesh.name,
                    CurrentCompression = importer.meshCompression,
                };
                results.AssetsByPath.Add(path, assetEntry);
            }

            assetEntry.SceneObjects.Add(new SceneObjectRef
            {
                GameObject = go,
                ComponentLabel = componentLabel,
                HierarchyPath = GetHierarchyPath(go.transform),
            });
            results.SceneObjectReferenceCount++;
        }

        foreach (var meshFilter in Resources.FindObjectsOfTypeAll<MeshFilter>())
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
                continue;
            AddMeshReference(meshFilter.gameObject, "MeshFilter", meshFilter.sharedMesh);
        }

        foreach (var skinnedRenderer in Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>())
        {
            if (skinnedRenderer == null || skinnedRenderer.sharedMesh == null)
                continue;
            AddMeshReference(skinnedRenderer.gameObject, "SkinnedMeshRenderer", skinnedRenderer.sharedMesh);
        }

        results.AssetsToChange = results.AssetsByPath.Values.ToList();
        return results;
    }

    private static void ApplyChanges(ScanResults results, ModelImporterMeshCompression targetCompression)
    {
        var activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            Debug.LogWarning("No active loaded scene found.");
            return;
        }

        if (results == null || results.AssetsToChange.Count == 0)
        {
            Debug.Log("Nothing to apply.");
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar(
                "Apply Mesh Compression",
                "Writing import settings...",
                0f);

            AssetDatabase.StartAssetEditing();

            var i = 0;
            foreach (var entry in results.AssetsToChange)
            {
                i++;
                EditorUtility.DisplayProgressBar(
                    "Apply Mesh Compression",
                    $"Updating: {entry.AssetPath}",
                    Mathf.Clamp01(i / (float)results.AssetsToChange.Count));

                var importer = AssetImporter.GetAtPath(entry.AssetPath) as ModelImporter;
                if (importer == null)
                    continue;

                if (importer.meshCompression == targetCompression)
                    continue;

                importer.meshCompression = targetCompression;
                AssetDatabase.WriteImportSettingsIfDirty(entry.AssetPath);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        foreach (var entry in results.AssetsToChange)
            AssetDatabase.ImportAsset(entry.AssetPath, ImportAssetOptions.ForceUpdate);

        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(activeScene);
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null)
            return string.Empty;

        // Avoid allocations from string concatenation in a loop.
        var stack = new Stack<string>();
        while (t != null)
        {
            stack.Push(t.name);
            t = t.parent;
        }
        return string.Join("/", stack);
    }

    private sealed class ScanResults
    {
        public readonly Dictionary<string, AssetEntry> AssetsByPath = new(StringComparer.OrdinalIgnoreCase);
        public List<AssetEntry> AssetsToChange = new();

        public int MeshesWithoutAssetPath;
        public int NonModelImporters;
        public int SceneObjectReferenceCount;
    }

    private sealed class AssetEntry
    {
        public string AssetPath;
        public string AssetName;
        public ModelImporterMeshCompression CurrentCompression;
        public readonly List<SceneObjectRef> SceneObjects = new();
    }

    private sealed class SceneObjectRef
    {
        public GameObject GameObject;
        public string ComponentLabel;
        public string HierarchyPath;
    }
}
