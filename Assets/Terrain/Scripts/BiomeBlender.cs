using UnityEngine;

public class BiomeBlender
{
    public static float GetBiomeBlendFactor(BiomeSettings biomeA, BiomeSettings biomeB, float distanceToCenter)
    {
        float blendRange = 50f; // Defines the transition zone size between biomes

        if (biomeA == null || biomeB == null || biomeA == biomeB)
        {
            return 0f; // No blend needed if biomes are the same or one is null
        }

        // Assuming biomes are sorted by startDistance
        float transitionStart = biomeB.startDistance;
        float transitionEnd = transitionStart + blendRange;

        if (distanceToCenter < transitionStart)
        {
            return 0f; // Fully in biomeA
        }
        else if (distanceToCenter > transitionEnd)
        {
            return 1f; // Fully in biomeB
        }
        else
        {
            // Calculate the blend factor within the transition zone
            return (distanceToCenter - transitionStart) / blendRange;
        }
    }
}
