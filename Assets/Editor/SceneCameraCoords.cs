#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SceneCameraCoords
{
    static SceneCameraCoords()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        Vector3 pos = sceneView.camera.transform.position;
        string text = $"Camera Pos: X:{pos.x:F1} Y:{pos.y:F1} Z:{pos.z:F1}";

        Handles.BeginGUI();
        GUI.color = Color.black;
        GUI.Label(new Rect(12, 12, 300, 20), text); // Тень
        GUI.color = Color.green;
        GUI.Label(new Rect(10, 10, 300, 20), text); // Текст
        Handles.EndGUI();
    }
}
#endif