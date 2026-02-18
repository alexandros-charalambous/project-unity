using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class HeightMapGenerator
{
    public static HeightMap GenerateHeightMap(int width, int height, HeightMapSettings settings1, HeightMapSettings settings2, float blendFactor, Vector2 sampleCenter)   
    {
        float[,] values1 = Noise.GenerateNoiseMap(width, height, settings1.noiseSettings, sampleCenter);
        float[,] values2 = (settings2 != null) ? Noise.GenerateNoiseMap(width, height, settings2.noiseSettings, sampleCenter) : null;

        float[,] finalValues = new float[width, height];

        AnimationCurve heightCurve1 = new AnimationCurve(settings1.heightCurve.keys);
        AnimationCurve heightCurve2 = (settings2 != null) ? new AnimationCurve(settings2.heightCurve.keys) : null;

        float minValue = float.MaxValue;
        float maxValue = float.MinValue;

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                float height1 = values1[i, j] * heightCurve1.Evaluate(values1[i, j]) * settings1.heightMultyplier;
                float finalHeight;

                if (settings2 != null)
                {
                    float height2 = values2[i, j] * heightCurve2.Evaluate(values2[i, j]) * settings2.heightMultyplier;
                    finalHeight = Mathf.Lerp(height1, height2, blendFactor);
                }
                else
                {
                    finalHeight = height1;
                }

                finalValues[i, j] = finalHeight;

                if (finalHeight > maxValue)
                {
                    maxValue = finalHeight;
                }
                if (finalHeight < minValue)
                {
                    minValue = finalHeight;
                }
            }
        }
        return new HeightMap(finalValues, minValue, maxValue);
    }
}

public struct HeightMap
{
    public readonly float[,] values;
    public readonly float minValue;
    public readonly float maxValue;

    public HeightMap (float[,] values, float minValue, float maxValue)
    {
        this.values = values;
        this.minValue = minValue;
        this.maxValue = maxValue;
    }
}