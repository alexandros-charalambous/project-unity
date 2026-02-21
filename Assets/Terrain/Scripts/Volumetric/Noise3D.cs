using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deterministic 3D Perlin + fBM helpers suitable for worker threads.
/// World-space sampling should be used by callers to keep chunk borders seamless.
/// </summary>
public static class Noise3D
{
    private static readonly object PermLock = new object();
    private static readonly Dictionary<int, int[]> PermCache = new Dictionary<int, int[]>();

    public static int[] GetPermutation(int seed)
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

    /// <summary>
    /// Improved Perlin in 3D, roughly in [-1,1].
    /// </summary>
    public static float Perlin(float x, float y, float z, int[] perm)
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

    public static float FBm(float x, float y, float z, int[] perm, int octaves, float baseFrequency, float lacunarity, float persistence)
    {
        octaves = Mathf.Clamp(octaves, 1, 12);
        lacunarity = Mathf.Max(1f, lacunarity);
        persistence = Mathf.Clamp01(persistence);

        float amp = 1f;
        float freq = Mathf.Max(1e-6f, baseFrequency);
        float sum = 0f;
        float ampSum = 0f;

        for (int i = 0; i < octaves; i++)
        {
            sum += Perlin(x * freq, y * freq, z * freq, perm) * amp;
            ampSum += amp;
            amp *= persistence;
            freq *= lacunarity;
        }

        if (ampSum > 1e-6f) sum /= ampSum;
        return sum;
    }

    /// <summary>
    /// Ridged fBM: returns roughly in [0,1] with sharper peaks.
    /// </summary>
    public static float Ridged(float x, float y, float z, int[] perm, int octaves, float baseFrequency, float lacunarity, float persistence)
    {
        float fbm = FBm(x, y, z, perm, octaves, baseFrequency, lacunarity, persistence); // [-1,1]
        float r = 1f - Mathf.Abs(fbm); // [0,1]
        return r * r;
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
