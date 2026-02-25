using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ReduceTextureSize
{
    [MenuItem("Tools/Italiandogs/Texture Resizer")]
    public static void Open()
    {
        ReduceTextureSizeWindow.ShowWindow();
    }
}

public sealed class ReduceTextureSizeWindow : EditorWindow
{
    private static readonly int[] AllowedSizes = { 512, 1024, 2048, 4096 };

    private int _targetSize = 1024;
    private Vector2 _scroll;
    private readonly List<TextureChangeCandidate> _candidates = new();
    private bool _includeEqual = false;

    public static void ShowWindow()
    {
        var window = GetWindow<ReduceTextureSizeWindow>();
        window.titleContent = new GUIContent("Texture Max Size");
        window.minSize = new Vector2(620, 360);
        window.Show();
    }

    private void OnEnable()
    {
        if (!AllowedSizes.Contains(_targetSize))
        {
            _targetSize = 1024;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Reduce texture import Max Size used by the active scene", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            _targetSize = DrawTargetSizePopup(_targetSize);
            _includeEqual = EditorGUILayout.ToggleLeft(new GUIContent("Include textures already at target size", "If enabled, preview list will also include textures whose max size equals the target."), _includeEqual);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Active Scene", GUILayout.Height(26)))
                {
                    ScanActiveScene();
                }

                using (new EditorGUI.DisabledScope(_candidates.Count == 0))
                {
                    if (GUILayout.Button("Apply", GUILayout.Height(26)))
                    {
                        ApplyChanges();
                    }
                }

                if (GUILayout.Button("Restore Original", GUILayout.Height(26)))
                {
                    RestoreOriginals();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var backupCount = TextureMaxSizeBackupAsset.GetOrCreate().Entries.Count;
                EditorGUILayout.LabelField($"Backup entries: {backupCount}");

                using (new EditorGUI.DisabledScope(backupCount == 0))
                {
                    if (GUILayout.Button("Clear Backup", GUILayout.Width(110)))
                    {
                        if (EditorUtility.DisplayDialog("Clear Backup", "This removes the saved original max sizes. This cannot be undone.\n\nContinue?", "Clear", "Cancel"))
                        {
                            TextureMaxSizeBackupAsset.Clear();
                        }
                    }
                }
            }
        }

        EditorGUILayout.Space(8);
        DrawCandidates();
    }

    private static int DrawTargetSizePopup(int current)
    {
        var index = Array.IndexOf(AllowedSizes, current);
        if (index < 0)
        {
            index = 1; // 1024
        }

        index = EditorGUILayout.Popup(new GUIContent("Reduce to", "Target importer Max Size"), index, AllowedSizes.Select(s => s.ToString()).ToArray());
        return AllowedSizes[index];
    }

    private void DrawCandidates()
    {
        using (new EditorGUILayout.VerticalScope())
        {
            EditorGUILayout.LabelField($"Textures in scene: {_candidates.Count}", EditorStyles.boldLabel);
            if (_candidates.Count == 0)
            {
                EditorGUILayout.HelpBox("Click 'Scan Active Scene' to preview which textures would change.", MessageType.Info);
                return;
            }

            var willChangeCount = _candidates.Count(c => c.WillChange);
            EditorGUILayout.LabelField($"Will change: {willChangeCount}");
            EditorGUILayout.Space(4);

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;

                foreach (var candidate in _candidates)
                {
                    DrawCandidateRow(candidate);
                }
            }
        }
    }

    private static void DrawCandidateRow(TextureChangeCandidate candidate)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(candidate.Texture == null))
            {
                if (GUILayout.Button("Ping", GUILayout.Width(44)))
                {
                    EditorGUIUtility.PingObject(candidate.Texture);
                    Selection.activeObject = candidate.Texture;
                }
            }

            var name = candidate.Texture != null ? candidate.Texture.name : "(missing)";
            var left = $"{name}  |  {candidate.Path}";
            EditorGUILayout.LabelField(left, GUILayout.MinWidth(380));

            var status = candidate.Importer == null
                ? "Not an importable texture"
                : candidate.WillChange
                    ? $"{candidate.CurrentMaxSize} → {candidate.TargetMaxSize}"
                    : $"{candidate.CurrentMaxSize} (no change)";

            var style = candidate.WillChange ? EditorStyles.boldLabel : EditorStyles.label;
            EditorGUILayout.LabelField(status, style, GUILayout.Width(170));
        }
    }

    private void ScanActiveScene()
    {
        _candidates.Clear();

        var uniqueTextures = new HashSet<Texture>();
        foreach (var renderer in FindSceneRenderers())
        {
            if (renderer == null) continue;

            var materials = renderer.sharedMaterials;
            if (materials == null) continue;

            foreach (var material in materials)
            {
                if (material == null) continue;

                foreach (var propertyName in material.GetTexturePropertyNames())
                {
                    var texture = material.GetTexture(propertyName);
                    if (texture == null) continue;
                    uniqueTextures.Add(texture);
                }
            }
        }

        foreach (var texture in uniqueTextures.OrderBy(t => t.name))
        {
            var path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrWhiteSpace(path))
            {
                _candidates.Add(TextureChangeCandidate.NotImportable(texture, ""));
                continue;
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                _candidates.Add(TextureChangeCandidate.NotImportable(texture, path));
                continue;
            }

            var current = importer.maxTextureSize;
            var willChange = _includeEqual
                ? current >= _targetSize
                : current > _targetSize;

            _candidates.Add(new TextureChangeCandidate(texture, path, importer, current, _targetSize, willChange));
        }

        Repaint();
        Debug.Log($"[Texture Max Size Tool] Scan complete. Found {_candidates.Count} unique textures in scene, {_candidates.Count(c => c.WillChange)} will change.");
    }

    private void ApplyChanges()
    {
        if (_candidates.Count == 0)
        {
            EditorUtility.DisplayDialog("Apply", "Nothing to apply. Scan the scene first.", "OK");
            return;
        }

        var toChange = _candidates.Where(c => c.WillChange && c.Importer != null).ToList();
        if (toChange.Count == 0)
        {
            EditorUtility.DisplayDialog("Apply", "No textures need changing for the selected target size.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Apply", $"This will set importer Max Size to {_targetSize} for {toChange.Count} texture(s).\n\nA backup of original Max Size values will be saved so you can restore later.\n\nContinue?", "Apply", "Cancel"))
        {
            return;
        }

        var backup = TextureMaxSizeBackupAsset.GetOrCreate();

        try
        {
            AssetDatabase.StartAssetEditing();
            for (var i = 0; i < toChange.Count; i++)
            {
                var candidate = toChange[i];
                EditorUtility.DisplayProgressBar("Applying", $"{candidate.Texture.name} ({i + 1}/{toChange.Count})", (float)(i + 1) / toChange.Count);

                TextureMaxSizeBackupAsset.EnsureBackedUp(backup, candidate.Path, candidate.CurrentMaxSize);

                candidate.Importer.maxTextureSize = candidate.TargetMaxSize;
                candidate.Importer.SaveAndReimport();
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
            TextureMaxSizeBackupAsset.Save(backup);
        }

        Debug.Log($"[Texture Max Size Tool] Applied Max Size {_targetSize} to {toChange.Count} texture(s).");
        ScanActiveScene();
    }

    private void RestoreOriginals()
    {
        var backup = TextureMaxSizeBackupAsset.GetOrCreate();
        if (backup.Entries.Count == 0)
        {
            EditorUtility.DisplayDialog("Restore Original", "No backup entries found. Apply changes first (or you already cleared the backup).", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Restore Original", $"This will restore importer Max Size for {backup.Entries.Count} texture(s) from the saved backup.\n\nContinue?", "Restore", "Cancel"))
        {
            return;
        }

        var restored = 0;
        var missing = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (var i = 0; i < backup.Entries.Count; i++)
            {
                var entry = backup.Entries[i];
                var path = AssetDatabase.GUIDToAssetPath(entry.Guid);
                EditorUtility.DisplayProgressBar("Restoring", $"{path} ({i + 1}/{backup.Entries.Count})", (float)(i + 1) / backup.Entries.Count);

                if (string.IsNullOrWhiteSpace(path))
                {
                    missing++;
                    continue;
                }

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    missing++;
                    continue;
                }

                importer.maxTextureSize = entry.OriginalMaxTextureSize;
                importer.SaveAndReimport();
                restored++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"[Texture Max Size Tool] Restored {restored} texture(s). Missing/unrestorable: {missing}.");
        ScanActiveScene();
    }

    private static IEnumerable<Renderer> FindSceneRenderers()
    {
        // Includes inactive scene objects, excludes assets/prefabs.
        // Resources.FindObjectsOfTypeAll is editor-safe and works across Unity 2022.
        var all = Resources.FindObjectsOfTypeAll<Renderer>();
        foreach (var renderer in all)
        {
            if (renderer == null) continue;
            if (EditorUtility.IsPersistent(renderer)) continue;

            var scene = renderer.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded) continue;

            yield return renderer;
        }
    }

    private readonly struct TextureChangeCandidate
    {
        public Texture Texture { get; }
        public string Path { get; }
        public TextureImporter Importer { get; }
        public int CurrentMaxSize { get; }
        public int TargetMaxSize { get; }
        public bool WillChange { get; }

        public TextureChangeCandidate(Texture texture, string path, TextureImporter importer, int currentMaxSize, int targetMaxSize, bool willChange)
        {
            Texture = texture;
            Path = path;
            Importer = importer;
            CurrentMaxSize = currentMaxSize;
            TargetMaxSize = targetMaxSize;
            WillChange = willChange;
        }

        public static TextureChangeCandidate NotImportable(Texture texture, string path)
        {
            return new TextureChangeCandidate(texture, path, null, 0, 0, false);
        }
    }
}

[Serializable]
public sealed class TextureMaxSizeBackupAsset : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public string Guid;
        public int OriginalMaxTextureSize;
    }

    public List<Entry> Entries = new();

    private const string AssetPath = "Assets/Italiandogs/Editor/TextureMaxSizeBackup.asset";

    public static TextureMaxSizeBackupAsset GetOrCreate()
    {
        var asset = AssetDatabase.LoadAssetAtPath<TextureMaxSizeBackupAsset>(AssetPath);
        if (asset != null)
        {
            return asset;
        }

        EnsureFolders();
        asset = CreateInstance<TextureMaxSizeBackupAsset>();
        AssetDatabase.CreateAsset(asset, AssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return asset;
    }

    public static void EnsureBackedUp(TextureMaxSizeBackupAsset backup, string assetPath, int originalMaxTextureSize)
    {
        if (backup == null) return;
        if (string.IsNullOrWhiteSpace(assetPath)) return;

        var guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrWhiteSpace(guid)) return;

        var existing = backup.Entries.FirstOrDefault(e => e.Guid == guid);
        if (existing != null)
        {
            return;
        }

        backup.Entries.Add(new Entry
        {
            Guid = guid,
            OriginalMaxTextureSize = originalMaxTextureSize,
        });

        EditorUtility.SetDirty(backup);
    }

    public static void Save(TextureMaxSizeBackupAsset backup)
    {
        if (backup == null) return;
        EditorUtility.SetDirty(backup);
        AssetDatabase.SaveAssets();
    }

    public static void Clear()
    {
        var backup = GetOrCreate();
        backup.Entries.Clear();
        Save(backup);
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Italiandogs"))
        {
            AssetDatabase.CreateFolder("Assets", "Italiandogs");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Italiandogs/Editor"))
        {
            AssetDatabase.CreateFolder("Assets/Italiandogs", "Editor");
        }
    }
}
