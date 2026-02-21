using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VolumetricTerrainGenerator))]
public class VolumetricTerrainGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var gen = (VolumetricTerrainGenerator)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Edit Mode Preview", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            EditorGUILayout.HelpBox(
                "For best performance, keep Auto Update Preview OFF and use the buttons below to regenerate terrain on demand.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild Preview"))
                {
                    gen.EditorPreviewRebuildNow();
                    EditorUtility.SetDirty(gen);
                }

                if (GUILayout.Button("Render Changes"))
                {
                    gen.EditorPreviewRenderNow();
                    EditorUtility.SetDirty(gen);
                }

                if (GUILayout.Button("Update Once"))
                {
                    gen.EditorPreviewUpdateOnce();
                    EditorUtility.SetDirty(gen);
                }

                if (GUILayout.Button("Clear Preview"))
                {
                    gen.EditorPreviewClearNow();
                    EditorUtility.SetDirty(gen);
                }
            }
        }
    }
}
