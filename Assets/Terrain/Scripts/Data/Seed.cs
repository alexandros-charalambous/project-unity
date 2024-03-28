using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct Seed
{
    public int seed;

    public System.Random GenerateSeed()
    {
        return new System.Random(seed);
    }
}
