#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WorldMapRenderer))]
public class WorldMapRendererEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var t = (WorldMapRenderer)target;

        GUILayout.Space(8);
        using (new EditorGUI.DisabledScope(t == null))
        {
            if (GUILayout.Button("Rebuild Map Now"))
            {
                t.RebuildNow();
                EditorUtility.SetDirty(t);
            }
        }

        if (t != null && t.MapTexture != null)
        {
            GUILayout.Space(8);
            Rect r = GUILayoutUtility.GetRect(128, 128, GUILayout.ExpandWidth(true));
            EditorGUI.DrawPreviewTexture(r, t.MapTexture, null, ScaleMode.ScaleToFit);
        }

        GUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "This map uses Physics raycasts to find the surface height. For best results, generate/stream the terrain so colliders exist in the area you are mapping.\n\nFor a full-world bake without colliders loaded, we'd need a procedural sampler (density/height) exposed from the terrain generator.",
            MessageType.Info);
    }
}
#endif
