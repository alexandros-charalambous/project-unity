using UnityEngine;

[System.Serializable]
public struct Seed
{
    public int seed;

    public static Seed FromInt(int value)
    {
        return new Seed { seed = value };
    }

    public static Seed CreateRandom()
    {
        // Use UnityEngine.Random so the value can be generated on the main thread without allocations.
        // Mix in time to reduce the chance of repeats if Random has not been advanced.
        int v = unchecked((int)System.DateTime.UtcNow.Ticks);
        v ^= Random.Range(int.MinValue, int.MaxValue);
        return new Seed { seed = v };
    }

    public System.Random GenerateSeed()
    {
        return new System.Random(seed);
    }
}
