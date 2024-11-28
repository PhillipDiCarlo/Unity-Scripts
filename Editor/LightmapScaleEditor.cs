using UnityEngine;
using UnityEditor;

public class LightmapScaleEditor : EditorWindow
{
    float newScale = 1.0f;

    // Add menu named "Lightmap Scale Editor" to the Window menu
    [MenuItem("Tools/Italiandogs/Lightmap Scale Editor")]
    public static void ShowWindow()
    {
        //Show existing window instance. If one doesn't exist, make one.
        EditorWindow.GetWindow(typeof(LightmapScaleEditor));
    }

    void OnGUI()
    {
        GUILayout.Label("Set New Scale in Lightmap for All Objects", EditorStyles.boldLabel);
        newScale = EditorGUILayout.FloatField("New Scale", newScale);

        if (GUILayout.Button("Apply to All Objects"))
        {
            ApplyScaleToAllObjects(newScale);
        }
    }

    void ApplyScaleToAllObjects(float scale)
    {
        foreach (GameObject obj in FindObjectsOfType(typeof(GameObject)))
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                SerializedObject serializedObject = new SerializedObject(renderer);
                SerializedProperty lightmapScaleProperty = serializedObject.FindProperty("m_ScaleInLightmap");
                if (lightmapScaleProperty != null)
                {
                    lightmapScaleProperty.floatValue = scale;
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }
    }
}
