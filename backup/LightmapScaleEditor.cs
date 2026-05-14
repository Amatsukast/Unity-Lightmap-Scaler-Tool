using UnityEngine;
using UnityEditor;

public class LightmapScaleEditor : EditorWindow
{
    private GameObject rootObject;
    private float scaleInLightmap = 1.0f;

    [MenuItem("Tools/Lightmap Scale Editor")]
    public static void ShowWindow()
    {
        GetWindow<LightmapScaleEditor>("Lightmap Scale Editor");
    }

    private void OnGUI()
    {
        GUILayout.Label("Lightmap Scale Settings", EditorStyles.boldLabel);

        rootObject = (GameObject)EditorGUILayout.ObjectField("Root Object", rootObject, typeof(GameObject), true);
        scaleInLightmap = EditorGUILayout.FloatField("Scale in Lightmap", scaleInLightmap);

        if (GUILayout.Button("Apply to All Children"))
        {
            if (rootObject != null)
            {
                ApplyScaleToChildren(rootObject);
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Please assign a root GameObject.", "OK");
            }
        }
    }

    private void ApplyScaleToChildren(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        Undo.RecordObjects(renderers, "Change Scale in Lightmap");

        foreach (Renderer renderer in renderers)
        {
            SerializedObject so = new SerializedObject(renderer);
            SerializedProperty scaleProp = so.FindProperty("m_ScaleInLightmap");

            if (scaleProp != null)
            {
                scaleProp.floatValue = scaleInLightmap;
                so.ApplyModifiedProperties();
            }
        }

        Debug.Log($"Applied Scale in Lightmap = {scaleInLightmap} to {renderers.Length} renderers.");
    }
}
