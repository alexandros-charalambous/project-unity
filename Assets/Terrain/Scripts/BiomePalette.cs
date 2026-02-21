using UnityEngine;

public readonly struct BiomePalette
{
    public readonly BiomeSettings biome0;
    public readonly BiomeSettings biome1;
    public readonly BiomeSettings biome2;
    public readonly BiomeSettings biome3;

    public readonly Vector2 site0;
    public readonly Vector2 site1;
    public readonly Vector2 site2;
    public readonly Vector2 site3;

    public readonly int count;
    public readonly float blendWidth;

    public BiomePalette(
        BiomeSettings biome0, Vector2 site0,
        BiomeSettings biome1, Vector2 site1,
        BiomeSettings biome2, Vector2 site2,
        BiomeSettings biome3, Vector2 site3,
        int count,
        float blendWidth)
    {
        this.biome0 = biome0;
        this.biome1 = biome1;
        this.biome2 = biome2;
        this.biome3 = biome3;

        this.site0 = site0;
        this.site1 = site1;
        this.site2 = site2;
        this.site3 = site3;

        this.count = Mathf.Clamp(count, 0, 4);
        this.blendWidth = blendWidth;
    }

    public BiomeSettings GetBiome(int index)
    {
        return index switch
        {
            0 => biome0,
            1 => biome1,
            2 => biome2,
            3 => biome3,
            _ => null
        };
    }

    public Vector2 GetSite(int index)
    {
        return index switch
        {
            0 => site0,
            1 => site1,
            2 => site2,
            3 => site3,
            _ => Vector2.zero
        };
    }
}
