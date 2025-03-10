using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class ShaderReplacer : EditorWindow
{
    private Shader sourceShader;
    private Shader targetShader;
    private List<Shader> allShaders;

    [MenuItem("Tools/Italiandogs/Replace Shaders")]
    public static void ShowWindow()
    {
        GetWindow<ShaderReplacer>("Replace Shaders");
    }

    void OnEnable()
    {
        allShaders = Resources.FindObjectsOfTypeAll<Shader>().ToList();
    }

    void OnGUI()
    {
        GUILayout.Label("Select shaders to replace", EditorStyles.boldLabel);

        // Dropdown for source shader
        int sourceIndex = allShaders.IndexOf(sourceShader);
        sourceIndex = EditorGUILayout.Popup("Source Shader", sourceIndex, allShaders.Select(shader => shader.name).ToArray());
        sourceShader = allShaders.ElementAtOrDefault(sourceIndex);

        // Dropdown for target shader
        int targetIndex = allShaders.IndexOf(targetShader);
        targetIndex = EditorGUILayout.Popup("Target Shader", targetIndex, allShaders.Select(shader => shader.name).ToArray());
        targetShader = allShaders.ElementAtOrDefault(targetIndex);

        if (GUILayout.Button("Replace Shaders"))
        {
            ReplaceShaders();
        }
    }

    private void ReplaceShaders()
    {
        if (sourceShader == null || targetShader == null)
        {
            Debug.LogError("Please select both source and target shaders.");
            return;
        }

        var materials = Resources.FindObjectsOfTypeAll<Material>();

        foreach (var mat in materials)
        {
            if (mat.shader == sourceShader)
            {
                mat.shader = targetShader;
                Debug.Log($"Shader for material '{mat.name}' replaced with '{targetShader.name}'");
            }
        }
    }
}
