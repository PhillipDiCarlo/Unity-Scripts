using UnityEngine;
using UnityEditor; // This namespace is necessary for EditorWindow and MenuItem

public class FixErrorShaders : EditorWindow
{
    [MenuItem("Tools/Italiandogs/Fix Error Shaders")] // Adds an item to the Unity Editor menu
    public static void ShowWindow()
    {
        GetWindow<FixErrorShaders>("Fix Error Shaders");
    }

    void OnGUI()
    {
        if (GUILayout.Button("Fix Error Shaders"))
        {
            FixShaders();
        }
    }

    private void FixShaders()
    {
        var materials = Resources.FindObjectsOfTypeAll<Material>();
        Shader standardShader = Shader.Find("Standard");

        foreach (var mat in materials)
        {
            if (mat.shader.name == "Hidden/InternalErrorShader")
            {
                mat.shader = standardShader;
                Debug.Log("Fixed shader for material: " + mat.name);
            }
        }
    }
}
