using UnityEngine;

[CreateAssetMenu(fileName = "New Biome", menuName = "Terrain/Biome Settings")]
public class BiomeSettings : ScriptableObject
{
    [Tooltip("Display name for this biome (used for debugging/UI). If empty, it defaults to the asset name.")]
    public string biomeName;

    [Header("Generation Settings")]
    [Tooltip("Distance from world center where this biome becomes eligible to appear.\n\n0 = can appear near spawn/center. Higher values push this biome outward.")]
    [Min(0f)] public float startDistance; // Distance from world center where this biome begins

    [Tooltip("Weight used during horizontal Voronoi selection.\n\nHigher weight = more likely to win territory vs other biomes at similar distances.")]
    [Min(0f)] public float voronoiWeight = 1f;

    [Tooltip("If enabled, this biome will never be chosen by horizontal Voronoi selection. Use this for height-only biomes like underwater/mountain.")]
    public bool excludeFromVoronoiSelection;

    [Header("Rendering")]
    [Tooltip("Material used to render terrain in this biome.\n\nIn the blended biome pipeline, the generator may build a palette of biome materials and blend them via vertex colors.")]
    public Material material;

    [Tooltip("Optional: Color used by the top-down world map for this biome.\n\nIf left transparent (A=0), the map uses an auto-generated distinct color.")]
    public Color mapColor = new Color(0f, 0f, 0f, 0f);

    [Header("Spawning")]
    [Tooltip("Prefabs that can spawn as trees in this biome.")]
    public GameObject[] treePrefabs;

    [Tooltip("Tree spawn density scalar for this biome. Higher = more trees per area.\n\nExact interpretation depends on the spawner, but this is intended as a designer-friendly knob.")]
    [Min(0f)] public float treeDensity = 0.1f;

    [Tooltip("Prefabs that can spawn as wildlife in this biome.")]
    public GameObject[] wildlifePrefabs;

    [Tooltip("Wildlife spawn density scalar for this biome. Higher = more wildlife per area.")]
    [Min(0f)] public float wildlifeDensity = 0.01f;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(biomeName))
        {
            biomeName = name;
        }

        startDistance = Mathf.Max(0f, startDistance);
        voronoiWeight = Mathf.Max(0f, voronoiWeight);
        treeDensity = Mathf.Max(0f, treeDensity);
        wildlifeDensity = Mathf.Max(0f, wildlifeDensity);
    }
}
