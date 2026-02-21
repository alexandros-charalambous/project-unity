using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VolumetricTerrainSettings))]
public class VolumetricTerrainSettingsEditor : Editor
{
    private static bool showAdvanced;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawMeshing();
        EditorGUILayout.Space(6);

        DrawVerticalRange();
        EditorGUILayout.Space(6);

        DrawSimpleTuning();
        EditorGUILayout.Space(6);

        DrawDensityModeAndSurface();
        EditorGUILayout.Space(6);

        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced", true);
        if (showAdvanced)
        {
            EditorGUI.indentLevel++;
            DrawAdvanced();
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawMeshing()
    {
        EditorGUILayout.LabelField("Meshing", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isoLevel"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("voxelsPerAxis"));
    }

    private void DrawVerticalRange()
    {
        EditorGUILayout.LabelField("Vertical Range (world Y)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("minWorldY"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxWorldY"));
    }

    private void DrawDensityModeAndSurface()
    {
        EditorGUILayout.LabelField("Surface / Mode", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("surfaceDensityMultiplier"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("densityMode"));
    }

    private void DrawSimpleTuning()
    {
        EditorGUILayout.LabelField("Simple Tuning", EditorStyles.boldLabel);

        var useSimple = serializedObject.FindProperty("useSimpleTuning");
        EditorGUILayout.PropertyField(useSimple);

        using (new EditorGUI.DisabledScope(!useSimple.boolValue))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("featureSize"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("domainWarpAmount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("overhangAmount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("caveAmount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("biasCavesUnderwater"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("islandsAmount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("islandsSize"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("islandsRarity"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("islandsTopFlatness"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("islandsUndersideRoughness"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Surface Shape", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("terrainTerraceHeight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("terrainTerraceBlend"));

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Apply Smooth Large-Scale Defaults", GUILayout.MaxWidth(240)))
                {
                    ApplySmoothDefaults();
                }
            }
        }
    }

    private void ApplySmoothDefaults()
    {
        // Keep this intentionally conservative; the generator can still be tuned further.
        serializedObject.FindProperty("useSimpleTuning").boolValue = true;
        serializedObject.FindProperty("featureSize").floatValue = 450f;
        serializedObject.FindProperty("domainWarpAmount").floatValue = 0.22f;
        serializedObject.FindProperty("overhangAmount").floatValue = 0.35f;
        serializedObject.FindProperty("caveAmount").floatValue = 0.28f;
        serializedObject.FindProperty("biasCavesUnderwater").boolValue = true;
        serializedObject.FindProperty("islandsAmount").floatValue = 0.18f;
        serializedObject.FindProperty("islandsSize").floatValue = 2.25f;
        serializedObject.FindProperty("islandsRarity").floatValue = 0.78f;
        serializedObject.FindProperty("islandsTopFlatness").floatValue = 0.70f;
        serializedObject.FindProperty("islandsUndersideRoughness").floatValue = 0.65f;

        serializedObject.FindProperty("terrainTerraceHeight").floatValue = 0f;
        serializedObject.FindProperty("terrainTerraceBlend").floatValue = 0.25f;

        // Also enable volumetric mode + features (common expectation when using Simple mode).
        serializedObject.FindProperty("densityMode").enumValueIndex = (int)VolumetricTerrainSettings.DensityMode.Volumetric3D;
        serializedObject.FindProperty("enableOverhangs").boolValue = true;
        serializedObject.FindProperty("enableCaves").boolValue = true;
        serializedObject.FindProperty("enableFloatingIslands").boolValue = true;
    }

    private void DrawAdvanced()
    {
        // Volumetric feature toggles/fields
        EditorGUILayout.LabelField("Overhangs", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("enableOverhangs"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("overhangStrength"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("overhangNoise"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Domain Warp", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("enableDomainWarp"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("domainWarpStrength"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("domainWarpNoise"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Caves", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("enableCaves"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("caveStrength"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("caveThreshold"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("caveSoftness"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("caveNoise"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Floating Islands", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("enableFloatingIslands"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("islandsStrength"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("islandsThreshold"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("islandsSoftness"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("islandsMinY"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("islandsMaxY"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("islandsBandBlend"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("islandsNoise"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Optimization", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("earlyOutEmptyChunks"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("emptyChunkProbeResolution"));
    }
}
