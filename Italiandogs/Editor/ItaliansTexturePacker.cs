#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class ItaliansTexturePacker : EditorWindow
{
    private bool includeInactive = true;
    private int outputSize = 1024;
    private bool generateMipmaps = true;

    // Default behavior: don't touch materials already using Packed workflow.
    private bool onlyIfSeparateWorkflow = true;

    private const string MOCHIE_STANDARD_SHADER = "Mochie/Standard";
    private const string PACKER_SHADER = "Hidden/Mochie/TexturePacker";

    private const string WINDOW_TITLE = "Italian's Texture Packer for Mochie Shader";

    private const string PREFS_PREFIX = "Italiandogs.ItaliansTexturePacker.";
    private const string PREF_SKIP_PACKED = PREFS_PREFIX + "SkipPacked";

    [MenuItem("Tools/Italiandogs/Italian's Texture Packer for Mochie Shader")]
    public static void Open() => GetWindow<ItaliansTexturePacker>(WINDOW_TITLE);

    private void OnEnable()
    {
        // Default ON: skip materials already using Packed workflow.
        onlyIfSeparateWorkflow = EditorPrefs.GetBool(PREF_SKIP_PACKED, true);
    }

    private void OnDisable()
    {
        EditorPrefs.SetBool(PREF_SKIP_PACKED, onlyIfSeparateWorkflow);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Packs Mochie/Standard materials used in the active scene into _PackedMap (R=Occ, G=Rough, B=Metal, A=Height).",
            EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(8);

        includeInactive = EditorGUILayout.Toggle("Include inactive objects", includeInactive);
        outputSize = EditorGUILayout.IntPopup("Output size", outputSize,
            new[] { "512", "1024", "2048", "4096" }, new[] { 512, 1024, 2048, 4096 });
        generateMipmaps = EditorGUILayout.Toggle("Generate mipmaps", generateMipmaps);
        onlyIfSeparateWorkflow = EditorGUILayout.Toggle("Skip materials already Packed", onlyIfSeparateWorkflow);

        EditorGUILayout.Space(12);
        if (GUILayout.Button("Pack Mochie Standard Materials Used In Scene"))
        {
            PackAll();
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "This uses the GPU packer shader (Hidden/Mochie/TexturePacker). It samples the RED channel of each input texture.\n" +
            "Output is imported with sRGB OFF (correct for mask maps).",
            MessageType.Info);
    }

    private void PackAll()
    {
        var packerShader = Shader.Find(PACKER_SHADER);
        if (packerShader == null)
        {
            EditorUtility.DisplayDialog(WINDOW_TITLE,
                $"Could not find '{PACKER_SHADER}'. Ensure Mochie shaders are imported.",
                "OK");
            return;
        }

        var renderers = includeInactive
            ? Resources.FindObjectsOfTypeAll<Renderer>().Where(r => r.gameObject.scene.IsValid())
            : FindObjectsOfType<Renderer>(false);

        var mats = new HashSet<Material>();
        foreach (var r in renderers)
        foreach (var m in r.sharedMaterials)
        {
            if (m != null) mats.Add(m);
        }

        var targets = mats
            .Where(m => m.shader != null && string.Equals(m.shader.name, MOCHIE_STANDARD_SHADER, StringComparison.Ordinal))
            .ToList();

        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog(WINDOW_TITLE, "No Mochie/Standard materials found in the active scene.", "OK");
            return;
        }

        int packed = 0, skipped = 0;

        using var packerMatScope = new TempMaterialScope(new Material(packerShader));

        foreach (var mat in targets)
        {
            if (TryPackOne(mat, packerMatScope.Material, out var reason))
            {
                packed++;
                Debug.Log($"[ItaliansTexturePacker] Packed '{mat.name}'", mat);
            }
            else
            {
                skipped++;
                Debug.Log($"[ItaliansTexturePacker] Skipped '{mat.name}': {reason}", mat);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(WINDOW_TITLE,
            $"Done.\nPacked: {packed}\nSkipped: {skipped}\n\nSee Console for details.",
            "OK");
    }

    private bool TryPackOne(Material m, Material packerMat, out string reason)
    {
        reason = "";

        // Only asset materials (so we can save next to them)
        var matPath = AssetDatabase.GetAssetPath(m);
        if (string.IsNullOrEmpty(matPath))
        {
            reason = "Material is not an asset on disk (likely instantiated).";
            return false;
        }

        if (onlyIfSeparateWorkflow)
        {
            if (m.HasProperty("_PrimaryWorkflow"))
            {
                var workflow = Mathf.RoundToInt(m.GetFloat("_PrimaryWorkflow"));
                if (workflow != 0)
                {
                    reason = "Material is already using Packed workflow.";
                    return false;
                }
            }

            // Extra guard: some editors drive this keyword.
            if (m.IsKeywordEnabled("_WORKFLOW_PACKED_ON"))
            {
                reason = "Material has _WORKFLOW_PACKED_ON enabled (Packed workflow).";
                return false;
            }

            // Extra guard: if a packed map is already assigned, don't generate a new one.
            if (m.HasProperty("_PackedMap") && m.GetTexture("_PackedMap") != null)
            {
                reason = "Material already has a _PackedMap assigned.";
                return false;
            }
        }

        // Determine what is actually used
        bool useOcc = m.HasProperty("_SampleOcclusion") && Mathf.RoundToInt(m.GetFloat("_SampleOcclusion")) == 1;
        bool useRough = m.HasProperty("_SampleRoughness") && Mathf.RoundToInt(m.GetFloat("_SampleRoughness")) == 1;
        bool useMetal = m.HasProperty("_SampleMetallic") && Mathf.RoundToInt(m.GetFloat("_SampleMetallic")) == 1;

        var occTex = useOcc ? (m.GetTexture("_OcclusionMap") as Texture2D) : null;
        var roughTex = useRough ? (m.GetTexture("_RoughnessMap") as Texture2D) : null;
        var metalTex = useMetal ? (m.GetTexture("_MetallicMap") as Texture2D) : null;

        // Height: Mochie uses _HeightMap always present as property; actual use is gated by _PARALLAX_ON keyword.
        bool parallaxOn = m.IsKeywordEnabled("_PARALLAX_ON");
        var heightTex = parallaxOn ? (m.GetTexture("_HeightMap") as Texture2D) : null;

        if (occTex == null && roughTex == null && metalTex == null && heightTex == null)
        {
            reason = "No sampled maps found (Sample toggles off, or textures missing).";
            return false;
        }

        // Configure the packer. It uses .r from each.
        packerMat.SetTexture("_Red", occTex != null ? occTex : Texture2D.whiteTexture);
        packerMat.SetTexture("_Green", roughTex != null ? roughTex : Texture2D.whiteTexture);
        packerMat.SetTexture("_Blue", metalTex != null ? metalTex : Texture2D.whiteTexture);
        packerMat.SetTexture("_Alpha", heightTex != null ? heightTex : Texture2D.blackTexture);

        // Invert flags: usually OFF for Mochie Standard separate maps
        packerMat.SetFloat("_Invert_Red", 0f);
        packerMat.SetFloat("_Invert_Green", 0f);
        packerMat.SetFloat("_Invert_Blue", 0f);
        packerMat.SetFloat("_Invert_Alpha", 0f);

        // Bake output path beside material
        var folder = Path.GetDirectoryName(matPath)?.Replace("\\", "/") ?? "Assets";
        // Use the material *asset filename* for stable output names.
        // This prevents Unity from creating "..._Packed 1.png" duplicates on re-run.
        var matFileBase = Path.GetFileNameWithoutExtension(matPath);
        var outPath = $"{folder}/{matFileBase}_Packed.png";

        BakePackedTextureToPng(packerMat, outPath);

        if (!ApplyMaskImportSettings(outPath, out var importReason))
        {
            reason = importReason;
            return false;
        }

        // Assign packed texture + switch workflow
        var packedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        m.SetTexture("_PackedMap", packedTex);

        m.SetFloat("_PrimaryWorkflow", 1f); // Packed
        m.SetFloat("_PackedHeight", heightTex != null ? 1f : 0f);

        // Ensure channels are the expected defaults for Standard
        m.SetFloat("_OcclusionChannel", 0f); // Red
        m.SetFloat("_RoughnessChannel", 1f); // Green
        m.SetFloat("_MetallicChannel", 2f); // Blue
        m.SetFloat("_HeightChannel", 3f); // Alpha

        EditorUtility.SetDirty(m);
        return true;
    }

    private void BakePackedTextureToPng(Material packerMat, string outAssetPath)
    {
        var rt = RenderTexture.GetTemporary(outputSize, outputSize, 0, RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear);
        try
        {
            var prev = RenderTexture.active;
            Graphics.Blit(null, rt, packerMat);
            RenderTexture.active = rt;

            var tex = new Texture2D(outputSize, outputSize, TextureFormat.RGBA32, generateMipmaps, true);
            tex.ReadPixels(new Rect(0, 0, outputSize, outputSize), 0, 0);
            tex.Apply(generateMipmaps, false);

            // outAssetPath is like "Assets/...png"; write to the actual filesystem path under the Unity project.
            // Application.dataPath is "<project>/Assets".
            if (!outAssetPath.StartsWith("Assets/", StringComparison.Ordinal) && !string.Equals(outAssetPath, "Assets", StringComparison.Ordinal))
                throw new InvalidOperationException($"Output path must be under 'Assets/'. Got: {outAssetPath}");

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var fullPath = Path.Combine(projectRoot ?? string.Empty, outAssetPath.Replace('/', Path.DirectorySeparatorChar));
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            DestroyImmediate(tex);

            AssetDatabase.ImportAsset(outAssetPath, ImportAssetOptions.ForceUpdate);
            RenderTexture.active = prev;
        }
        finally
        {
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    private bool ApplyMaskImportSettings(string assetPath, out string reason)
    {
        reason = "";

        // Ensure Unity knows about this file.
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            // If the file was created this frame, sometimes a refresh is needed.
            AssetDatabase.Refresh();
            importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        }
        if (importer == null)
        {
            reason = "Could not get TextureImporter for generated packed texture. Is the output path inside the project's Assets folder?";
            Debug.LogWarning($"[ItaliansTexturePacker] {reason} Path: {assetPath}");
            return false;
        }

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.mipmapEnabled = generateMipmaps;
        importer.SaveAndReimport();

        return true;
    }

    private sealed class TempMaterialScope : IDisposable
    {
        public Material Material { get; }
        public TempMaterialScope(Material m) => Material = m;
        public void Dispose()
        {
            if (Material != null) DestroyImmediate(Material);
        }
    }
}
#endif
