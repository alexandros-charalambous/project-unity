using UnityEngine;
using System.Linq;

public class BiomeManager : MonoBehaviour
{
    public BiomeSettings[] biomes;

    void Start()
    {
        // Sort biomes by start distance, descending
        biomes = biomes.OrderByDescending(b => b.startDistance).ToArray();
    }

    public BiomeSettings GetBiomeForChunk(Vector2 chunkCoord, float chunkSize)
    {
        float distanceToCenter = chunkCoord.magnitude * chunkSize;
        
        // Find the first biome whose start distance is less than the chunk's distance
        foreach (var biome in biomes)
        {
            if (distanceToCenter >= biome.startDistance)
            {
                return biome;
            }
        }
        
        // Return the last biome as a default (the one with the smallest start distance)
        return biomes.LastOrDefault();
    }
}
