using UnityEngine;

[CreateAssetMenu(fileName = "New Biome", menuName = "Terrain/Biome Settings")]
public class BiomeSettings : ScriptableObject
{
    public string biomeName;
    
    [Header("Generation Settings")]
    public float startDistance; // Distance from world center where this biome begins
    public HeightMapSettings heightMapSettings;
    // You should create a similar TextureData ScriptableObject for textures
    // public TextureData textureData; 

    [Header("Spawning")]
    public GameObject[] treePrefabs;
    public float treeDensity = 0.1f;
    
    public GameObject[] wildlifePrefabs;
    public float wildlifeDensity = 0.01f;
}
