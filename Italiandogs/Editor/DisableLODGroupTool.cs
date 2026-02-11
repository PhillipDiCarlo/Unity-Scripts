// DisableLODGroupTool.cs

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DisableLODGroupTool : EditorWindow
{
    private enum LodChoice
    {
        LOD0 = 0,
        LOD1 = 1,
        LOD2 = 2,
        LOD3 = 3
    }

    [MenuItem("Tools/Italiandogs/LOD Scene Override")]
    public static void ShowWindow()
    {
        var window = GetWindow<DisableLODGroupTool>("LOD Scene Override");
        window.minSize = new Vector2(420, 220);
        window.RefreshSceneCache();
    }

    private LodChoice selectedLod = LodChoice.LOD0;
    private bool enableLodGroupsToggle = true;

    // Snapshot of original states so we can revert.
    private bool hasSnapshot = false;
    private Dictionary<int, LODGroupSnapshot> snapshotByInstanceId = new Dictionary<int, LODGroupSnapshot>();

    private struct RendererState
    {
        public int rendererInstanceId;
        public bool enabled;
    }

    private class LODGroupSnapshot
    {
        public int lodGroupInstanceId;
        public bool lodGroupEnabled;
        public List<RendererState> rendererStates = new List<RendererState>();
    }

    private void OnEnable()
    {
        RefreshSceneCache();
    }

    private void OnHierarchyChange()
    {
        // Keep toggle label accurate if user changes things externally.
        // We do NOT auto-refresh snapshot to avoid stomping "original" state.
        RecomputeEnableToggleFromScene();
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("LOD Override (Scene-wide)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Apply Selected LOD will force only that LOD level's renderers to be enabled and will disable LODGroup components so Unity cannot auto-switch LODs.\n\n" +
            "Revert restores original LODGroup enabled state and renderer enabled states.",
            MessageType.Info
        );

        EditorGUILayout.Space(6);

        // Dropdown
        selectedLod = (LodChoice)EditorGUILayout.EnumPopup("Active LOD Level", selectedLod);

        EditorGUILayout.Space(6);

        // Toggle: enables/disables all LODGroup components in the scene.
        EditorGUI.BeginChangeCheck();
        enableLodGroupsToggle = EditorGUILayout.ToggleLeft("Enable LODGroups in Scene", enableLodGroupsToggle);
        if (EditorGUI.EndChangeCheck())
        {
            SetAllLodGroupsEnabled(enableLodGroupsToggle);
        }

        EditorGUILayout.Space(10);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Apply Selected LOD", GUILayout.Height(32)))
            {
                CaptureSnapshotIfNeeded();
                ApplyOnlySelectedLod((int)selectedLod);
            }

            if (GUILayout.Button("Revert (Restore Normal)", GUILayout.Height(32)))
            {
                RevertToSnapshot();
            }
        }

        EditorGUILayout.Space(6);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh / Re-scan Scene"))
            {
                // Does not wipe snapshot unless you explicitly choose to.
                RefreshSceneCache();
            }

            if (GUILayout.Button("Forget Snapshot (New Baseline)"))
            {
                // If you want "current" to become the new revert baseline.
                hasSnapshot = false;
                snapshotByInstanceId.Clear();
                CaptureSnapshotIfNeeded();
                EditorUtility.DisplayDialog("LOD Scene Override", "Snapshot reset. Revert baseline updated to current scene state.", "OK");
            }
        }

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField($"Snapshot captured: {(hasSnapshot ? "Yes" : "No")}");
        EditorGUILayout.LabelField($"LODGroups found in scene: {CountSceneLodGroups()}");
    }

    private void RefreshSceneCache()
    {
        RecomputeEnableToggleFromScene();
        Repaint();
    }

    private int CountSceneLodGroups()
    {
        var groups = FindSceneLodGroups(includeInactive: true);
        return groups.Count;
    }

    private void RecomputeEnableToggleFromScene()
    {
        var groups = FindSceneLodGroups(includeInactive: true);
        if (groups.Count == 0)
        {
            enableLodGroupsToggle = true;
            return;
        }

        // If all enabled -> toggle true, else false.
        bool allEnabled = true;
        foreach (var g in groups)
        {
            if (g == null) continue;
            if (!g.enabled) { allEnabled = false; break; }
        }
        enableLodGroupsToggle = allEnabled;
    }

    private void CaptureSnapshotIfNeeded()
    {
        if (hasSnapshot) return;

        snapshotByInstanceId.Clear();

        var groups = FindSceneLodGroups(includeInactive: true);
        foreach (var g in groups)
        {
            if (g == null) continue;

            var snap = new LODGroupSnapshot
            {
                lodGroupInstanceId = g.GetInstanceID(),
                lodGroupEnabled = g.enabled,
                rendererStates = new List<RendererState>()
            };

            var lods = g.GetLODs();
            for (int i = 0; i < lods.Length; i++)
            {
                var renderers = lods[i].renderers;
                if (renderers == null) continue;

                foreach (var r in renderers)
                {
                    if (r == null) continue;

                    snap.rendererStates.Add(new RendererState
                    {
                        rendererInstanceId = r.GetInstanceID(),
                        enabled = r.enabled
                    });
                }
            }

            snapshotByInstanceId[snap.lodGroupInstanceId] = snap;
        }

        hasSnapshot = true;
    }

    private void SetAllLodGroupsEnabled(bool enabled)
    {
        var groups = FindSceneLodGroups(includeInactive: true);
        if (groups.Count == 0) return;

        Undo.RecordObjects(groups.ToArray(), enabled ? "Enable LODGroups" : "Disable LODGroups");
        foreach (var g in groups)
        {
            if (g == null) continue;
            g.enabled = enabled;
            EditorUtility.SetDirty(g);
        }
    }

    private void ApplyOnlySelectedLod(int lodIndex)
    {
        var groups = FindSceneLodGroups(includeInactive: true);
        if (groups.Count == 0) return;

        int groupsMissingLod = 0;

        foreach (var g in groups)
        {
            if (g == null) continue;

            var lods = g.GetLODs();

            // Record for undo (both the LODGroup and all renderers in all LODs).
            var toRecord = new List<UnityEngine.Object> { g };

            for (int i = 0; i < lods.Length; i++)
            {
                var renderers = lods[i].renderers;
                if (renderers == null) continue;

                foreach (var r in renderers)
                {
                    if (r != null) toRecord.Add(r);
                }
            }

            Undo.RecordObjects(toRecord.ToArray(), $"Apply Only LOD{lodIndex}");

            // Disable LODGroup so it won't auto-switch and fight our overrides.
            g.enabled = false;
            EditorUtility.SetDirty(g);

            // If requested LOD doesn't exist for this group, disable all renderers in this group.
            if (lodIndex < 0 || lodIndex >= lods.Length)
            {
                groupsMissingLod++;
                for (int i = 0; i < lods.Length; i++)
                {
                    var renderers = lods[i].renderers;
                    if (renderers == null) continue;

                    foreach (var r in renderers)
                    {
                        if (r == null) continue;
                        r.enabled = false;
                        EditorUtility.SetDirty(r);
                    }
                }
                continue;
            }

            // Enable only selected LOD's renderers; disable all other LOD renderers.
            for (int i = 0; i < lods.Length; i++)
            {
                bool shouldEnable = (i == lodIndex);
                var renderers = lods[i].renderers;
                if (renderers == null) continue;

                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    r.enabled = shouldEnable;
                    EditorUtility.SetDirty(r);
                }
            }
        }

        // Keep the checkbox in sync with what we just did (we disabled all groups).
        enableLodGroupsToggle = false;

        if (groupsMissingLod > 0)
        {
            Debug.LogWarning($"LOD Scene Override: {groupsMissingLod} LODGroup(s) did not have LOD{lodIndex}. Their LOD renderers were disabled.");
        }
    }

    private void RevertToSnapshot()
    {
        if (!hasSnapshot)
        {
            EditorUtility.DisplayDialog("LOD Scene Override", "No snapshot exists yet. Click 'Apply Selected LOD' (or 'Forget Snapshot') to capture a baseline first.", "OK");
            return;
        }

        // Rebuild lookup of current objects by instance ID.
        var currentGroups = FindSceneLodGroups(includeInactive: true);
        var groupById = new Dictionary<int, LODGroup>();
        foreach (var g in currentGroups)
        {
            if (g == null) continue;
            groupById[g.GetInstanceID()] = g;
        }

        // Renderers can exist across many objects; find all renderers in scene (including inactive) for restore.
        var currentRenderers = FindSceneRenderers(includeInactive: true);
        var rendererById = new Dictionary<int, Renderer>();
        foreach (var r in currentRenderers)
        {
            if (r == null) continue;
            rendererById[r.GetInstanceID()] = r;
        }

        var undoObjects = new List<UnityEngine.Object>();

        foreach (var kvp in snapshotByInstanceId)
        {
            int groupId = kvp.Key;
            var snap = kvp.Value;

            if (!groupById.TryGetValue(groupId, out var g) || g == null)
                continue; // Group was deleted.

            undoObjects.Add(g);

            // Restore LODGroup enabled state.
            g.enabled = snap.lodGroupEnabled;
            EditorUtility.SetDirty(g);

            // Restore renderer enabled states (only those that existed in snapshot).
            foreach (var rs in snap.rendererStates)
            {
                if (!rendererById.TryGetValue(rs.rendererInstanceId, out var r) || r == null)
                    continue;

                undoObjects.Add(r);

                r.enabled = rs.enabled;
                EditorUtility.SetDirty(r);
            }
        }

        if (undoObjects.Count > 0)
        {
            Undo.RecordObjects(undoObjects.ToArray(), "Revert LOD Override");
        }

        RecomputeEnableToggleFromScene();
    }

    private static List<LODGroup> FindSceneLodGroups(bool includeInactive)
    {
        // Include inactive because user asked "all objects in a scene".
        // Filter out assets/prefabs that aren't in a valid scene.
        var found = UnityEngine.Object.FindObjectsOfType<LODGroup>(includeInactive);
        var list = new List<LODGroup>(found.Length);

        foreach (var g in found)
        {
            if (g == null) continue;
            if (EditorUtility.IsPersistent(g)) continue; // asset / prefab
            Scene s = g.gameObject.scene;
            if (!s.IsValid() || !s.isLoaded) continue;
            list.Add(g);
        }

        return list;
    }

    private static List<Renderer> FindSceneRenderers(bool includeInactive)
    {
        var found = UnityEngine.Object.FindObjectsOfType<Renderer>(includeInactive);
        var list = new List<Renderer>(found.Length);

        foreach (var r in found)
        {
            if (r == null) continue;
            if (EditorUtility.IsPersistent(r)) continue;
            Scene s = r.gameObject.scene;
            if (!s.IsValid() || !s.isLoaded) continue;
            list.Add(r);
        }

        return list;
    }
}
#endif
