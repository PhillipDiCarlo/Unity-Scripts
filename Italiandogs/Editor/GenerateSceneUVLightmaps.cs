#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Italiandogs.EditorTools
{
    public static class GenerateSceneUVLightmaps
    {
        private const string MenuPath = "Tools/Italiandogs/Generate Scene UV Lightmaps";

        [MenuItem(MenuPath)]
        private static void Run()
        {
            // Adjust if you want to exclude inactive objects.
            const bool includeInactive = true;

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                EditorUtility.DisplayDialog("Generate Scene UV Lightmaps", "No valid loaded scene found.", "OK");
                return;
            }

            // Collect all GameObjects in the active scene hierarchy.
            var roots = scene.GetRootGameObjects();
            var allGos = new List<GameObject>(capacity: 4096);
            foreach (var root in roots)
                CollectHierarchy(root, allGos, includeInactive);

            // Gather meshes referenced by scene components.
            var meshes = new HashSet<Mesh>();
            foreach (var go in allGos)
            {
                if (go == null) continue;

                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    meshes.Add(mf.sharedMesh);

                var smr = go.GetComponent<SkinnedMeshRenderer>();
                if (smr != null && smr.sharedMesh != null)
                    meshes.Add(smr.sharedMesh);
            }

            if (meshes.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Generate Scene UV Lightmaps",
                    "No MeshFilter or SkinnedMeshRenderer meshes found in the current scene hierarchy.",
                    "OK"
                );
                return;
            }

            // Decide which model asset paths actually need UV2 generation based on meshes in the scene.
            // If ANY mesh from a given model file lacks UV2, we enable generation for that model file.
            var pathsNeedingUv2 = new HashSet<string>();
            int meshesWithUv2 = 0;
            int meshesMissingUv2 = 0;
            int skippedNoPath = 0;
            int skippedNotModelImporter = 0;
            int uvReadErrors = 0;

            var meshList = meshes.ToList();

            try
            {
                for (int i = 0; i < meshList.Count; i++)
                {
                    var mesh = meshList[i];
                    EditorUtility.DisplayProgressBar(
                        "Generate Scene UV Lightmaps",
                        $"Scanning UV2 on meshes ({i + 1}/{meshList.Count})...",
                        (float)(i + 1) / meshList.Count
                    );

                    var path = AssetDatabase.GetAssetPath(mesh);
                    if (string.IsNullOrEmpty(path))
                    {
                        skippedNoPath++;
                        continue;
                    }

                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (importer == null)
                    {
                        skippedNotModelImporter++;
                        continue;
                    }

                    // If the importer is already generating secondary UVs, we don't need to inspect UV2.
                    // (Also avoids potentially touching unreadable meshes.)
                    if (importer.generateSecondaryUV)
                    {
                        meshesWithUv2++; // treat as "covered" for reporting
                        continue;
                    }

                    // Check if the mesh already has UV2. If yes, skip reimport.
                    // If no, flag the model path for enabling generateSecondaryUV.
                    bool hasUv2;
                    try
                    {
                        hasUv2 = MeshHasUv2(mesh);
                    }
                    catch
                    {
                        // Some meshes may throw when accessing UVs depending on import/readability.
                        // Conservative choice: treat as missing so the model gets the importer flag.
                        uvReadErrors++;
                        hasUv2 = false;
                    }

                    if (hasUv2)
                    {
                        meshesWithUv2++;
                    }
                    else
                    {
                        meshesMissingUv2++;
                        pathsNeedingUv2.Add(path);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (pathsNeedingUv2.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Generate Scene UV Lightmaps",
                    $"No changes needed.\n\n" +
                    $"Unique meshes scanned: {meshes.Count}\n" +
                    $"Meshes already with UV2 (or importer already enabled): {meshesWithUv2}\n" +
                    $"Meshes missing UV2: {meshesMissingUv2}\n" +
                    $"UV read errors (treated as missing): {uvReadErrors}\n" +
                    $"Skipped (no asset path): {skippedNoPath}\n" +
                    $"Skipped (not a model importer): {skippedNotModelImporter}",
                    "OK"
                );
                return;
            }

            // Enable Generate Secondary UVs only for the model assets that need it.
            int changedImporters = 0;
            int alreadyEnabledImporters = 0;

            try
            {
                var paths = pathsNeedingUv2.ToList();
                for (int i = 0; i < paths.Count; i++)
                {
                    var path = paths[i];
                    EditorUtility.DisplayProgressBar(
                        "Generate Scene UV Lightmaps",
                        $"Enabling Generate Secondary UVs ({i + 1}/{paths.Count})...",
                        (float)(i + 1) / paths.Count
                    );

                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (importer == null) continue;

                    if (!importer.generateSecondaryUV)
                    {
                        importer.generateSecondaryUV = true;

                        // Optional: tweak unwrap settings here if desired.
                        // importer.secondaryUVAngleDistortion = 8f;
                        // importer.secondaryUVAreaDistortion = 15f;
                        // importer.secondaryUVHardAngle = 88f;
                        // importer.secondaryUVPackMargin = 4f;

                        EditorUtility.SetDirty(importer);
                        changedImporters++;
                    }
                    else
                    {
                        alreadyEnabledImporters++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // Reimport only the assets we changed (or intended to change).
            AssetDatabase.StartAssetEditing();
            try
            {
                var paths = pathsNeedingUv2.ToList();
                for (int i = 0; i < paths.Count; i++)
                {
                    var path = paths[i];
                    EditorUtility.DisplayProgressBar(
                        "Generate Scene UV Lightmaps",
                        $"Reimporting {System.IO.Path.GetFileName(path)} ({i + 1}/{paths.Count})...",
                        (float)(i + 1) / paths.Count
                    );

                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            EditorSceneManager.MarkSceneDirty(scene);

            EditorUtility.DisplayDialog(
                "Generate Scene UV Lightmaps",
                $"Completed.\n\n" +
                $"Unique meshes scanned: {meshes.Count}\n" +
                $"Meshes already with UV2 (or importer already enabled): {meshesWithUv2}\n" +
                $"Meshes missing UV2: {meshesMissingUv2}\n" +
                $"UV read errors (treated as missing): {uvReadErrors}\n\n" +
                $"Model assets needing UV2 generation: {pathsNeedingUv2.Count}\n" +
                $"Model importers changed: {changedImporters}\n" +
                $"Model importers already enabled: {alreadyEnabledImporters}\n\n" +
                $"Skipped (no asset path): {skippedNoPath}\n" +
                $"Skipped (not a model importer): {skippedNotModelImporter}",
                "OK"
            );

            Debug.Log(
                $"[Italiandogs] Generate Scene UV Lightmaps finished. " +
                $"Meshes={meshes.Count}, WithUv2OrImporterOn={meshesWithUv2}, MissingUv2={meshesMissingUv2}, " +
                $"UvReadErrors={uvReadErrors}, AssetsToReimport={pathsNeedingUv2.Count}, " +
                $"ChangedImporters={changedImporters}"
            );
        }

        /// <summary>
        /// Returns true if UV2 exists and matches vertexCount (a practical definition of "has lightmap UVs").
        /// </summary>
        private static bool MeshHasUv2(Mesh mesh)
        {
            if (mesh == null) return false;

            // UV2 is channel index 1.
            // Using GetUVs avoids allocations if we reuse lists, but for a tooling pass this is fine.
            var uvs = new List<Vector2>();
            mesh.GetUVs(1, uvs);

            // Some meshes may have UV2 array but wrong length; treat that as missing.
            return uvs != null && uvs.Count == mesh.vertexCount && mesh.vertexCount > 0;
        }

        private static void CollectHierarchy(GameObject root, List<GameObject> results, bool includeInactive)
        {
            if (root == null) return;

            if (includeInactive || root.activeInHierarchy)
                results.Add(root);

            var t = root.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (child != null)
                    CollectHierarchy(child.gameObject, results, includeInactive);
            }
        }
    }
}
#endif
