using ShanheWeijin.Map;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapGenerator))]
public sealed class MapGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("重新生成白模"))
            {
                ((MapGenerator)target).重新生成();
            }

            if (GUILayout.Button("清除生成结果"))
            {
                ((MapGenerator)target).清除生成结果();
            }
        }
    }
}
