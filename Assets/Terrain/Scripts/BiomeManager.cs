using UnityEngine;
using System.Linq;

public struct BiomeBlendData
{
    public BiomeSettings primaryBiome;
    public BiomeSettings secondaryBiome;
    public float blendFactor;
}

public class BiomeManager : MonoBehaviour
{
    public int seed;
    public bool useSeedBasedBiomes;
    public BiomeSettings[] biomes;

    void Start()
    {
        // Sort biomes by start distance, ascending for blending logic
        biomes = biomes.OrderBy(b => b.startDistance).ToArray();
    }

    public BiomeSettings GetBiomeForChunk(Vector2 chunkCoord, float chunkSize)
    {
        return GetBiomeBlendData(chunkCoord, chunkSize).primaryBiome;
    }

    public BiomeBlendData GetBiomeBlendData(Vector2 chunkCoord, float chunkSize)
    {
        if (useSeedBasedBiomes)
        {
            // Seed-based blending is more complex and might require different logic.
            // For now, it will just return the primary biome.
            BiomeSettings primaryBiome = GetBiomeBySeed(chunkCoord);
            return new BiomeBlendData { primaryBiome = primaryBiome, secondaryBiome = null, blendFactor = 0 };
        }
        else
        {
            return GetBiomeBlendDataByDistance(chunkCoord, chunkSize);
        }
    }

    private BiomeSettings GetBiomeBySeed(Vector2 chunkCoord)
    {
        float hash = Mathf.PerlinNoise(chunkCoord.x * 0.1f + seed, chunkCoord.y * 0.1f + seed);
        int biomeIndex = Mathf.FloorToInt(hash * biomes.Length);
        biomeIndex = Mathf.Clamp(biomeIndex, 0, biomes.Length - 1);
        return biomes[biomeIndex];
    }

    private BiomeBlendData GetBiomeBlendDataByDistance(Vector2 chunkCoord, float chunkSize)
    {
        float distanceToCenter = chunkCoord.magnitude * chunkSize;
        BiomeSettings primaryBiome = null;
        BiomeSettings secondaryBiome = null;
        float blendFactor = 0;

        for (int i = 0; i < biomes.Length; i++)
        {
            if (distanceToCenter < biomes[i].startDistance)
            {
                primaryBiome = (i > 0) ? biomes[i - 1] : biomes[0];
                secondaryBiome = biomes[i];
                blendFactor = BiomeBlender.GetBiomeBlendFactor(primaryBiome, secondaryBiome, distanceToCenter);
                break;
            }
        }

        if (primaryBiome == null)
        {
            // If we are beyond all defined biomes
            primaryBiome = biomes.LastOrDefault();
            secondaryBiome = null;
            blendFactor = 0;
        }

        return new BiomeBlendData { primaryBiome = primaryBiome, secondaryBiome = secondaryBiome, blendFactor = blendFactor };
    }
}

