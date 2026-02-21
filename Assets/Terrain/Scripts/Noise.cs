using System.Collections.Generic;
using UnityEngine;

public static class Noise
{
    public enum NormalizeMode { Local, Global };

    private static readonly object PermLock = new object();
    private static readonly Dictionary<int, int[]> PermCache = new Dictionary<int, int[]>();

    public static float[,] GenerateNoiseMap(int mapWidth, int mapHeight, NoiseSettings settings, Vector2 sampleCenter)
    {
        // Compatibility overload for older callers. Seed is now provided by the generator.
        return GenerateNoiseMap(mapWidth, mapHeight, settings, sampleCenter, seed: 0);
    }

    public static float[,] GenerateNoiseMap(int mapWidth, int mapHeight, NoiseSettings settings, Vector2 sampleCenter, int seed)
    {
        float[,] noiseMap = new float[mapWidth, mapHeight];

        System.Random prng = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[settings.octaves];
        float[] octaveOffsetsZ = settings.use3DNoise ? new float[settings.octaves] : null;

        int[] perm = null;
        if (settings.use3DNoise)
        {
            perm = GetPermutation(seed);
        }

        float maxPossibleHeight = 0f;
        float amplitude = 1f;

        for (int i = 0; i < settings.octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) + settings.offset.x + sampleCenter.x;
            float offsetY = prng.Next(-100000, 100000) - settings.offset.y - sampleCenter.y;
            octaveOffsets[i] = new Vector2(offsetX, offsetY);

            if (settings.use3DNoise)
            {
                float offsetZ = prng.Next(-100000, 100000) + settings.offsetZ + settings.zSlice;
                octaveOffsetsZ[i] = offsetZ;
            }

            maxPossibleHeight += amplitude;
            amplitude *= settings.persistance;
        }

        float maxLocalNoiseHeight = float.MinValue;
        float minLocalNoiseHeight = float.MaxValue;

        float halfWidth = mapWidth / 2f;
        float halfHeight = mapHeight / 2f;

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                amplitude = 1f;
                float frequency = 1f;
                float noiseHeight = 0f;
                for (int i = 0; i < settings.octaves; i++)
                {
                    float sampleX = (x - halfWidth + octaveOffsets[i].x) / settings.scale * frequency;
                    float sampleY = (y - halfHeight + octaveOffsets[i].y) / settings.scale * frequency;

                    float perlinValue;
                    if (settings.use3DNoise)
                    {
                        float sampleZ = (octaveOffsetsZ[i] / settings.zScale) * frequency;
                        perlinValue = Perlin3D(sampleX, sampleY, sampleZ, perm);
                    }
                    else
                    {
                        perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    }
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= settings.persistance;
                    frequency *= settings.lacunarity;
                }

                if (noiseHeight > maxLocalNoiseHeight)
                {
                    maxLocalNoiseHeight = noiseHeight;
                }

                if (noiseHeight < minLocalNoiseHeight)
                {
                    minLocalNoiseHeight = noiseHeight;
                }

                noiseMap[x, y] = noiseHeight;
            }
        }

        // Normalize the noise map
        if (settings.normalizeMode == NormalizeMode.Local)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    noiseMap[x, y] = Mathf.InverseLerp(minLocalNoiseHeight, maxLocalNoiseHeight, noiseMap[x, y]);
                }
            }
        }
        else // NormalizeMode.Global
        {
            // Normalize to a stable [0,1] range using the theoretical max amplitude.
            // This keeps results consistent across chunks and prevents extreme values
            // from feeding into height curves/multipliers.
            float denom = Mathf.Max(1e-6f, maxPossibleHeight * 2f);
            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    float normalizedHeight = (noiseMap[x, y] + maxPossibleHeight) / denom;
                    noiseMap[x, y] = Mathf.Clamp01(normalizedHeight);
                }
            }
        }

        return noiseMap;
    }

    private static int[] GetPermutation(int seed)
    {
        lock (PermLock)
        {
            if (PermCache.TryGetValue(seed, out var existing))
            {
                return existing;
            }

            var p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;

            var rng = new System.Random(seed);
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                int tmp = p[i];
                p[i] = p[j];
                p[j] = tmp;
            }

            var perm = new int[512];
            for (int i = 0; i < 512; i++) perm[i] = p[i & 255];

            PermCache[seed] = perm;
            return perm;
        }
    }

    // Improved Perlin noise in 3D. Returns roughly [-1,1].
    private static float Perlin3D(float x, float y, float z, int[] perm)
    {
        int X = Mathf.FloorToInt(x) & 255;
        int Y = Mathf.FloorToInt(y) & 255;
        int Z = Mathf.FloorToInt(z) & 255;

        x -= Mathf.Floor(x);
        y -= Mathf.Floor(y);
        z -= Mathf.Floor(z);

        float u = Fade(x);
        float v = Fade(y);
        float w = Fade(z);

        int A = perm[X] + Y;
        int AA = perm[A] + Z;
        int AB = perm[A + 1] + Z;
        int B = perm[X + 1] + Y;
        int BA = perm[B] + Z;
        int BB = perm[B + 1] + Z;

        float res = Lerp(w,
            Lerp(v,
                Lerp(u, Grad(perm[AA], x, y, z), Grad(perm[BA], x - 1, y, z)),
                Lerp(u, Grad(perm[AB], x, y - 1, z), Grad(perm[BB], x - 1, y - 1, z))
            ),
            Lerp(v,
                Lerp(u, Grad(perm[AA + 1], x, y, z - 1), Grad(perm[BA + 1], x - 1, y, z - 1)),
                Lerp(u, Grad(perm[AB + 1], x, y - 1, z - 1), Grad(perm[BB + 1], x - 1, y - 1, z - 1))
            )
        );

        return res;
    }

    private static float Fade(float t)
    {
        return t * t * t * (t * (t * 6 - 15) + 10);
    }

    private static float Lerp(float t, float a, float b)
    {
        return a + t * (b - a);
    }

    private static float Grad(int hash, float x, float y, float z)
    {
        int h = hash & 15;
        float u = h < 8 ? x : y;
        float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
        return (((h & 1) == 0) ? u : -u) + (((h & 2) == 0) ? v : -v);
    }
}

[System.Serializable]
public class NoiseSettings
{
    public Noise.NormalizeMode normalizeMode;

    [Header("Map Properties")]
    public float scale = 50f;
    public int octaves = 6;
    [Range(0, 1)] public float persistance = .5f;
    public float lacunarity = 2f;
    public Vector2 offset;

    [Header("3D Perlin (optional)")]
    public bool use3DNoise = false;
    public float zSlice = 0f;
    public float zScale = 1f;
    public float offsetZ = 0f;

    public void ValidateValues()
    {
        scale = Mathf.Max(scale, .01f);
        octaves = Mathf.Max(octaves, 1);
        lacunarity = Mathf.Max(lacunarity, 1f);
        persistance = Mathf.Clamp01(persistance);
        zScale = Mathf.Max(zScale, .0001f);
    }
}
