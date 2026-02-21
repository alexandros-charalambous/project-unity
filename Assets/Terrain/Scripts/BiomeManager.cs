using UnityEngine;
using System.Collections.Generic;
using System.Threading;

public struct BiomeBlendData
{
    public BiomeSettings primaryBiome;
    public BiomeSettings secondaryBiome;
    // Weight for secondary biome. With the current Voronoi ordering, points fully inside the
    // primary region tend toward 0; points across the boundary trend toward 1.
    public float blendFactor;

    // Voronoi sites used for the primary/secondary selection (world XZ plane stored as Vector2).
    public Vector2 primarySite;
    public Vector2 secondarySite;

    // World-space width (in meters) for the blend transition around the bisector.
    public float blendWidth;
}

public class BiomeManager : MonoBehaviour
{
    public int seed;

    [Header("References")]
    [Tooltip("Optional. If assigned, BiomeManager uses this to derive Voronoi cell size.")]
    public MeshSettings meshSettings;

    [Header("Voronoi")]
    [Range(1, 4)] public int voronoiNeighborSearchRadius = 2;
    [Min(0f)] public float biomeBlendWidth = 40f;

    [Header("Voronoi Warp")]
    [Tooltip("If enabled, domain-warps the Voronoi sampling space to make biome borders curvy/organic instead of straight bisectors.")]
    public bool enableVoronoiWarp = false;

    [Tooltip("Warp amplitude in world meters. Keep this below ~0.5× the Voronoi cell size to avoid needing a larger neighbor search radius.")]
    [Min(0f)] public float voronoiWarpStrength = 60f;

    [Tooltip("Warp noise frequency (cycles per meter). Example: 0.002 ≈ one large bend every 500m.")]
    [Min(0.000001f)] public float voronoiWarpFrequency = 0.002f;

    [Tooltip("Number of noise octaves used for warping. 1-3 is usually enough.")]
    [Range(1, 5)] public int voronoiWarpOctaves = 2;

    [Tooltip("Warp noise persistence per octave.")]
    [Range(0.1f, 0.95f)] public float voronoiWarpPersistence = 0.5f;

    [Tooltip("Warp noise lacunarity (frequency multiplier per octave).")]
    [Range(1.2f, 4f)] public float voronoiWarpLacunarity = 2f;

    [Header("Blend Tuning")]
    [Tooltip("Adjusts how soft/sharp the primary↔secondary blend is.\n\n1 = unchanged.\n>1 = sharper boundaries (more primary/secondary regions).\n<1 = softer/blurrier blending.")]
    [Min(0.05f)] public float secondaryBlendExponent = 1f;

    [Tooltip("Strength multiplier for the underwater overlay alpha.")]
    [Min(0f)] public float underwaterStrength = 1f;

    [Tooltip("Strength multiplier for the mountain overlay alpha (also affects underside rock override).")]
    [Min(0f)] public float mountainStrength = 1f;

    public BiomeSettings[] biomes;

    [Header("Height-Based Biomes (Optional)")]
    [Tooltip("Biome used for surfaces below sea level.")]
    public BiomeSettings underwaterBiome;
    public float seaLevel = 0f;
    [Min(0f)] public float seaBlend = 20f;

    [Tooltip("Biome used for surfaces above the mountain start height.")]
    public BiomeSettings mountainBiome;
    public float mountainStart = 450f;
    [Min(0f)] public float mountainBlend = 80f;

    // Global biome texture indexing.
    // This enables seam-safe biome shading without per-chunk texture palettes.
    private readonly Dictionary<BiomeSettings, int> biomeIndex = new Dictionary<BiomeSettings, int>();
    private Texture2DArray biomeTextureArray;
    private int biomeTextureArrayVersion;
    private int cachedBiomeConfigHash;

    internal Texture2DArray BiomeTextureArray
    {
        get
        {
            EnsureBiomeTextureArrayBuilt();
            return biomeTextureArray;
        }
    }

    internal int BiomeTextureArrayVersion
    {
        get
        {
            EnsureBiomeTextureArrayBuilt();
            return biomeTextureArrayVersion;
        }
    }

    internal int GetBiomeIndex(BiomeSettings biome)
    {
        if (biome == null) return 0;
        // Worker-thread safe: relies on WarmupBiomeTextureArray() having run on the main thread.
        return biomeIndex.TryGetValue(biome, out int idx) ? idx : 0;
    }

    public void WarmupBiomeTextureArray()
    {
        // This must run on the main thread (Graphics.CopyTexture + texture creation).
        EnsureBiomeTextureArrayBuilt();
    }

    internal float GetUnderwaterAlpha(float worldY)
    {
        if (underwaterBiome == null) return 0f;
        // Underwater should never affect terrain above sea level.
        if (seaBlend <= 0f) return worldY < seaLevel ? 1f : 0f;
        if (worldY >= seaLevel) return 0f;

        float t = Mathf.InverseLerp(seaLevel, seaLevel - seaBlend, worldY);
        return Mathf.SmoothStep(0f, 1f, t);
    }

    internal float GetMountainAlpha(float worldY)
    {
        if (mountainBiome == null) return 0f;
        // Mountain should never affect terrain below the mountain start height.
        if (mountainBlend <= 0f) return worldY > mountainStart ? 1f : 0f;
        if (worldY <= mountainStart) return 0f;

        float t = Mathf.InverseLerp(mountainStart, mountainStart + mountainBlend, worldY);
        return Mathf.SmoothStep(0f, 1f, t);
    }

    internal float TuneSecondaryBlend(float secondaryWeight)
    {
        float w = Mathf.Clamp01(secondaryWeight);
        float exp = Mathf.Max(0.05f, secondaryBlendExponent);
        if (Mathf.Abs(exp - 1f) < 0.0001f) return w;
        return Mathf.Clamp01(Mathf.Pow(w, exp));
    }

    internal float TuneUnderwaterAlpha(float underwaterAlpha)
    {
        return Mathf.Clamp01(underwaterAlpha * Mathf.Max(0f, underwaterStrength));
    }

    internal float TuneMountainAlpha(float mountainAlpha)
    {
        return Mathf.Clamp01(mountainAlpha * Mathf.Max(0f, mountainStrength));
    }

    private void EnsureBiomeTextureArrayBuilt()
    {
        // Never build or touch GPU resources from a worker thread.
        if (mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId != mainThreadId)
        {
            return;
        }

        int hash = 17;
        unchecked
        {
            hash = hash * 31 + seed;
            if (biomes != null)
            {
                hash = hash * 31 + biomes.Length;
                for (int i = 0; i < biomes.Length; i++)
                {
                    hash = hash * 31 + (biomes[i] != null ? biomes[i].GetInstanceID() : 0);
                }
            }
            hash = hash * 31 + (underwaterBiome != null ? underwaterBiome.GetInstanceID() : 0);
            hash = hash * 31 + (mountainBiome != null ? mountainBiome.GetInstanceID() : 0);
        }

        if (biomeTextureArray != null && cachedBiomeConfigHash == hash)
        {
            return;
        }

        cachedBiomeConfigHash = hash;
        biomeIndex.Clear();

        // Build a stable list of unique biomes.
        var list = new List<BiomeSettings>(32);
        void AddUnique(BiomeSettings b)
        {
            if (b == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == b) return;
            }
            list.Add(b);
        }

        if (biomes != null)
        {
            for (int i = 0; i < biomes.Length; i++) AddUnique(biomes[i]);
        }
        AddUnique(underwaterBiome);
        AddUnique(mountainBiome);

        if (list.Count == 0)
        {
            // Ensure at least one layer exists.
            list.Add(null);
        }

        // Choose a reference texture (first non-null biome texture, else white).
        Texture reference = null;
        for (int i = 0; i < list.Count; i++)
        {
            reference = TryGetBiomeAlbedoTexture(list[i]);
            if (reference != null) break;
        }
        if (reference == null) reference = Texture2D.whiteTexture;

        int w = Mathf.Max(1, reference.width);
        int h = Mathf.Max(1, reference.height);

        // Rebuild texture array in a universally copyable format.
        // This avoids Graphics.CopyTexture failures due to mixed formats/compression across biome textures.
        if (biomeTextureArray != null)
        {
            if (Application.isPlaying) Destroy(biomeTextureArray);
            else DestroyImmediate(biomeTextureArray);
        }

        biomeTextureArray = new Texture2DArray(w, h, list.Count, TextureFormat.RGBA32, mipChain: false, linear: false);
        biomeTextureArray.wrapMode = reference.wrapMode;
        biomeTextureArray.filterMode = reference.filterMode;
        biomeTextureArray.anisoLevel = reference.anisoLevel;

        // Temporary RT used to resample & convert each texture into RGBA32 at the array's size.
        RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.wrapMode = reference.wrapMode;
        rt.filterMode = reference.filterMode;

        try
        {
            for (int i = 0; i < list.Count; i++)
            {
                var biome = list[i];
                biomeIndex[biome] = i;

                Texture src = TryGetBiomeAlbedoTexture(biome);
                if (src == null) src = reference;

                // Convert/resample to the array resolution.
                Graphics.Blit(src, rt);

                try
                {
                    Graphics.CopyTexture(rt, 0, 0, biomeTextureArray, i, 0);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"BiomeManager: Failed to copy biome texture '{src.name}' into Texture2DArray (layer {i}). {ex.Message}", this);
                }
            }
        }
        finally
        {
            RenderTexture.ReleaseTemporary(rt);
        }

        biomeTextureArrayVersion++;
    }

    private static Texture TryGetBiomeAlbedoTexture(BiomeSettings biome)
    {
        if (biome == null || biome.material == null) return null;

        // Prefer URP Lit base map, then common main texture names.
        Material m = biome.material;
        Texture tex = null;
        if (m.HasProperty("_BaseMap")) tex = m.GetTexture("_BaseMap");
        if (tex == null && m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
        if (tex == null) tex = m.mainTexture;
        return tex;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        seaBlend = Mathf.Max(0f, seaBlend);
        mountainBlend = Mathf.Max(0f, mountainBlend);

        if (mountainBiome != null && biomes != null)
        {
            for (int i = 0; i < biomes.Length; i++)
            {
                if (biomes[i] == mountainBiome)
                {
                    Debug.LogWarning("BiomeManager: mountainBiome is also present in biomes[]. If you want mountains ONLY by height, remove the mountain biome from biomes[] and assign it only to mountainBiome.", this);
                    break;
                }
            }
        }

        if (underwaterBiome != null && biomes != null)
        {
            for (int i = 0; i < biomes.Length; i++)
            {
                if (biomes[i] == underwaterBiome)
                {
                    Debug.LogWarning("BiomeManager: underwaterBiome is also present in biomes[]. If you want underwater ONLY by height, remove the underwater biome from biomes[] and assign it only to underwaterBiome.", this);
                    break;
                }
            }
        }
    }
#endif

    public readonly struct BiomeSiteInfo
    {
        public readonly BiomeSettings biome;
        public readonly Vector2 site;
        public readonly float distSqr;

        public BiomeSiteInfo(BiomeSettings biome, Vector2 site, float distSqr)
        {
            this.biome = biome;
            this.site = site;
            this.distSqr = distSqr;
        }
    }

    public readonly struct BiomeCellInfo
    {
        public readonly BiomeSettings biome;
        public readonly Vector2 site;
        public readonly float distSqr;
        public readonly int cellX;
        public readonly int cellY;

        public BiomeCellInfo(BiomeSettings biome, Vector2 site, float distSqr, int cellX, int cellY)
        {
            this.biome = biome;
            this.site = site;
            this.distSqr = distSqr;
            this.cellX = cellX;
            this.cellY = cellY;
        }

        public long Key
        {
            get
            {
                return ((long)cellX << 32) ^ (uint)cellY;
            }
        }
    }

    // Scratch buffer to avoid per-call allocations in nearest-cell queries.
    // Main-thread use only.
    private readonly List<BiomeCellInfo> nearestCellsScratch = new List<BiomeCellInfo>(256);

    private const float defaultVoronoiCellSize = 250f;

    private MeshSettings cachedMeshSettings;
    private VolumetricTerrainGenerator cachedVolumetric;
    private int mainThreadId;

    void Awake()
    {
        mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
        EnsureBiomeTextureArrayBuilt();
    }

    private MeshSettings ResolveMeshSettings()
    {
        if (meshSettings != null)
        {
            cachedMeshSettings = meshSettings;
            return cachedMeshSettings;
        }

        if (cachedMeshSettings != null)
        {
            return cachedMeshSettings;
        }

        // Unity object lookups must only happen on the main thread.
        if (System.Threading.Thread.CurrentThread.ManagedThreadId != mainThreadId)
        {
            return null;
        }

        var volumetric = FindAnyObjectByType<VolumetricTerrainGenerator>();
        if (volumetric != null)
        {
            cachedMeshSettings = volumetric.meshSettings;
            return cachedMeshSettings;
        }

        return cachedMeshSettings;
    }

    private float GetVoronoiCellSize()
    {
        // Prefer the active volumetric generator's effective chunk size so biomes line up with
        // whatever chunk sizing the terrain system is actually using (including overrides).
        if (cachedVolumetric == null)
        {
            // Unity object lookups must only happen on the main thread.
            if (System.Threading.Thread.CurrentThread.ManagedThreadId == mainThreadId)
            {
                cachedVolumetric = FindAnyObjectByType<VolumetricTerrainGenerator>();
            }
        }

        if (cachedVolumetric != null)
        {
            return Mathf.Max(1f, cachedVolumetric.EffectiveChunkWorldSize);
        }

        MeshSettings meshSettings = ResolveMeshSettings();
        if (meshSettings != null)
        {
            return Mathf.Max(1f, meshSettings.chunkWorldSize);
        }

        return defaultVoronoiCellSize;
    }

    public BiomeSettings GetBiomeForChunk(Vector2Int chunkCoord, float chunkSize)
    {
        return GetBiomeBlendData(chunkCoord, chunkSize).primaryBiome;
    }

    public BiomeBlendData GetBiomeBlendData(Vector2Int chunkCoord, float chunkSize)
    {
        Vector2 worldPos = new Vector2(chunkCoord.x * chunkSize, chunkCoord.y * chunkSize);
        return GetBiomeBlendDataFromWorldPos(worldPos);
    }

    public BiomeBlendData GetBiomeBlendDataFromWorldPos(Vector2 worldPos)
    {
        if (biomes == null || biomes.Length == 0)
        {
            return new BiomeBlendData { primaryBiome = null, secondaryBiome = null, blendFactor = 0f, primarySite = Vector2.zero, secondarySite = Vector2.zero, blendWidth = biomeBlendWidth };
        }

        return CalculateBiomeDataInfinite(worldPos);
    }

    public BiomeSiteInfo[] GetNearestBiomeSites(Vector2 worldPos, int maxCount)
    {
        maxCount = Mathf.Clamp(maxCount, 1, 4);
        if (biomes == null || biomes.Length == 0)
        {
            return System.Array.Empty<BiomeSiteInfo>();
        }

        float cellSize = GetVoronoiCellSize();
        Vector2 warpedPos = ApplyVoronoiWarp(worldPos);
        int cellX = Mathf.FloorToInt(warpedPos.x / cellSize);
        int cellY = Mathf.FloorToInt(warpedPos.y / cellSize);

        // Search a bit wider than the primary/secondary search to collect enough unique biomes.
        int r = Mathf.Clamp(voronoiNeighborSearchRadius + 1, 1, 6);
        var bestByBiome = new Dictionary<BiomeSettings, BiomeSiteInfo>();
        for (int dy = -r; dy <= r; dy++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                int cx = cellX + dx;
                int cy = cellY + dy;
                Vector2 site = GetFeaturePoint(cx, cy, cellSize);
                BiomeSettings biome = GetBiomeForCell(cx, cy, cellSize);
                if (biome == null) continue;

                float distSqr = (ApplyVoronoiWarp(site) - warpedPos).sqrMagnitude;
                if (bestByBiome.TryGetValue(biome, out var existing))
                {
                    if (distSqr < existing.distSqr)
                    {
                        bestByBiome[biome] = new BiomeSiteInfo(biome, site, distSqr);
                    }
                }
                else
                {
                    bestByBiome.Add(biome, new BiomeSiteInfo(biome, site, distSqr));
                }
            }
        }

        var list = new List<BiomeSiteInfo>(bestByBiome.Values);
        list.Sort((a, b) => a.distSqr.CompareTo(b.distSqr));
        if (list.Count > maxCount) list.RemoveRange(maxCount, list.Count - maxCount);
        return list.ToArray();
    }

    /// <summary>
    /// Returns up to <paramref name="maxCount"/> nearest Voronoi cell sites, allowing duplicate biome types.
    /// This is useful for stable blending/palette selection across chunk borders.
    /// </summary>
    public BiomeSiteInfo[] GetNearestBiomeSitesNonUnique(Vector2 worldPos, int maxCount)
    {
        maxCount = Mathf.Clamp(maxCount, 1, 8);
        if (biomes == null || biomes.Length == 0)
        {
            return System.Array.Empty<BiomeSiteInfo>();
        }

        float cellSize = GetVoronoiCellSize();
        Vector2 warpedPos = ApplyVoronoiWarp(worldPos);
        int cellX = Mathf.FloorToInt(warpedPos.x / cellSize);
        int cellY = Mathf.FloorToInt(warpedPos.y / cellSize);

        int r = Mathf.Clamp(voronoiNeighborSearchRadius + 1, 1, 8);
        var list = new List<BiomeSiteInfo>((2 * r + 1) * (2 * r + 1));
        for (int dy = -r; dy <= r; dy++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                int cx = cellX + dx;
                int cy = cellY + dy;
                Vector2 site = GetFeaturePoint(cx, cy, cellSize);
                BiomeSettings biome = GetBiomeForCell(cx, cy, cellSize);
                if (biome == null) continue;

                float distSqr = (ApplyVoronoiWarp(site) - warpedPos).sqrMagnitude;
                list.Add(new BiomeSiteInfo(biome, site, distSqr));
            }
        }

        list.Sort((a, b) => a.distSqr.CompareTo(b.distSqr));
        if (list.Count > maxCount) list.RemoveRange(maxCount, list.Count - maxCount);
        return list.ToArray();
    }

    /// <summary>
    /// Returns up to <paramref name="maxCount"/> nearest Voronoi cells/points, allowing duplicate biome types.
    /// Keyed stably by cell coordinates (infinite) or point index (finite).
    /// </summary>
    public BiomeCellInfo[] GetNearestBiomeCellsNonUnique(Vector2 worldPos, int maxCount)
    {
        maxCount = Mathf.Clamp(maxCount, 1, 16);
        if (biomes == null || biomes.Length == 0)
        {
            return System.Array.Empty<BiomeCellInfo>();
        }

        // Preserve old API behavior, but route through the non-alloc core.
        var tmp = new BiomeCellInfo[maxCount];
        int n = GetNearestBiomeCellsNonUniqueNonAlloc(worldPos, maxCount, tmp);
        if (n <= 0) return System.Array.Empty<BiomeCellInfo>();
        if (n == tmp.Length) return tmp;
        var trimmed = new BiomeCellInfo[n];
        System.Array.Copy(tmp, trimmed, n);
        return trimmed;
    }

    /// <summary>
    /// Non-alloc variant of GetNearestBiomeCellsNonUnique. Fills <paramref name="results"/> with up to <paramref name="maxCount"/> nearest cells.
    /// Returns the number of elements written.
    /// </summary>
    public int GetNearestBiomeCellsNonUniqueNonAlloc(Vector2 worldPos, int maxCount, BiomeCellInfo[] results)
    {
        maxCount = Mathf.Clamp(maxCount, 1, 16);
        if (results == null || results.Length == 0) return 0;
        if (biomes == null || biomes.Length == 0) return 0;

        float cellSize = GetVoronoiCellSize();
        Vector2 warpedPos = ApplyVoronoiWarp(worldPos);
        int cellX = Mathf.FloorToInt(warpedPos.x / cellSize);
        int cellY = Mathf.FloorToInt(warpedPos.y / cellSize);

        // Use a slightly larger radius so palette selection remains robust if generation settings change.
        int r = Mathf.Clamp(voronoiNeighborSearchRadius + 2, 2, 10);

        nearestCellsScratch.Clear();
        int capacityGuess = (2 * r + 1) * (2 * r + 1);
        if (nearestCellsScratch.Capacity < capacityGuess) nearestCellsScratch.Capacity = capacityGuess;

        for (int dy = -r; dy <= r; dy++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                int cx = cellX + dx;
                int cy = cellY + dy;
                Vector2 site = GetFeaturePoint(cx, cy, cellSize);
                BiomeSettings biome = GetBiomeForCell(cx, cy, cellSize);
                if (biome == null) continue;

                float distSqr = (ApplyVoronoiWarp(site) - warpedPos).sqrMagnitude;
                nearestCellsScratch.Add(new BiomeCellInfo(biome, site, distSqr, cx, cy));
            }
        }

        nearestCellsScratch.Sort((a, b) => a.distSqr.CompareTo(b.distSqr));

        int n = Mathf.Min(maxCount, Mathf.Min(results.Length, nearestCellsScratch.Count));
        for (int i = 0; i < n; i++)
        {
            results[i] = nearestCellsScratch[i];
        }
        return n;
    }

    private BiomeBlendData CalculateBiomeDataInfinite(Vector2 worldPos)
    {
        float cellSize = GetVoronoiCellSize();
        Vector2 warpedPos = ApplyVoronoiWarp(worldPos);
        int cellX = Mathf.FloorToInt(warpedPos.x / cellSize);
        int cellY = Mathf.FloorToInt(warpedPos.y / cellSize);

        float minDistSqr = float.MaxValue;
        float secondMinDistSqr = float.MaxValue;
        int closestCellX = 0, closestCellY = 0;
        int secondCellX = 0, secondCellY = 0;

        int r = Mathf.Clamp(voronoiNeighborSearchRadius, 1, 4);
        for (int dy = -r; dy <= r; dy++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                int cx = cellX + dx;
                int cy = cellY + dy;
                Vector2 feature = GetFeaturePoint(cx, cy, cellSize);
                Vector2 warpedFeature = ApplyVoronoiWarp(feature);
                float distSqr = (warpedFeature - warpedPos).sqrMagnitude;

                if (distSqr < minDistSqr)
                {
                    secondMinDistSqr = minDistSqr;
                    secondCellX = closestCellX;
                    secondCellY = closestCellY;

                    minDistSqr = distSqr;
                    closestCellX = cx;
                    closestCellY = cy;
                }
                else if (distSqr < secondMinDistSqr)
                {
                    secondMinDistSqr = distSqr;
                    secondCellX = cx;
                    secondCellY = cy;
                }
            }
        }

        Vector2 primarySite = GetFeaturePoint(closestCellX, closestCellY, cellSize);
        Vector2 secondarySite = GetFeaturePoint(secondCellX, secondCellY, cellSize);

        Vector2 warpedPrimarySite = ApplyVoronoiWarp(primarySite);
        Vector2 warpedSecondarySite = ApplyVoronoiWarp(secondarySite);

        BiomeSettings primaryBiome = GetBiomeForCell(closestCellX, closestCellY, cellSize);
        BiomeSettings secondaryBiome = GetBiomeForCell(secondCellX, secondCellY, cellSize);

        float blendWidth = biomeBlendWidth;
        float blendFactor = 0f;
        if (secondaryBiome != null && secondaryBiome != primaryBiome && blendWidth > 0f)
        {
            Vector2 n = warpedSecondarySite - warpedPrimarySite;
            float len = n.magnitude;
            if (len > 1e-6f)
            {
                float c = (warpedSecondarySite.sqrMagnitude - warpedPrimarySite.sqrMagnitude) * 0.5f;
                float signedDist = (Vector2.Dot(warpedPos, n) - c) / len;
                float t = 0.5f + signedDist / (2f * blendWidth);
                blendFactor = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            }
        }

        if (secondaryBiome == primaryBiome)
        {
            return new BiomeBlendData { primaryBiome = primaryBiome, secondaryBiome = null, blendFactor = 0f, primarySite = primarySite, secondarySite = primarySite, blendWidth = blendWidth };
        }

        return new BiomeBlendData { primaryBiome = primaryBiome, secondaryBiome = secondaryBiome, blendFactor = blendFactor, primarySite = primarySite, secondarySite = secondarySite, blendWidth = blendWidth };
    }

    private Vector2 ApplyVoronoiWarp(Vector2 p)
    {
        if (!enableVoronoiWarp) return p;
        float strength = Mathf.Max(0f, voronoiWarpStrength);
        if (strength <= 0f) return p;

        int octaves = Mathf.Clamp(voronoiWarpOctaves, 1, 5);
        float frequency = Mathf.Max(0.000001f, voronoiWarpFrequency);
        float persistence = Mathf.Clamp(voronoiWarpPersistence, 0.1f, 0.95f);
        float lacunarity = Mathf.Clamp(voronoiWarpLacunarity, 1.2f, 4f);

        // fBm in [-1,1] range per component.
        float amp = 1f;
        float sumAmp = 0f;
        float fx = 0f;
        float fy = 0f;

        // Seed offsets so different worlds have different warps.
        // Keep them large to avoid visible repetition around the origin.
        float ox1 = (seed * 0.00137f) + 113.17f;
        float oy1 = (seed * 0.00211f) + 419.53f;
        float ox2 = (seed * 0.00391f) + 911.41f;
        float oy2 = (seed * 0.00119f) + 37.87f;

        float f = frequency;
        for (int i = 0; i < octaves; i++)
        {
            float x = p.x * f;
            float y = p.y * f;

            float nx = Mathf.PerlinNoise(x + ox1, y + oy1) * 2f - 1f;
            float ny = Mathf.PerlinNoise(x + ox2, y + oy2) * 2f - 1f;

            fx += nx * amp;
            fy += ny * amp;
            sumAmp += amp;

            amp *= persistence;
            f *= lacunarity;
        }

        if (sumAmp > 1e-6f)
        {
            fx /= sumAmp;
            fy /= sumAmp;
        }

        return p + new Vector2(fx, fy) * strength;
    }

    private Vector2 GetFeaturePoint(int cellX, int cellY, float cellSize)
    {
        // Deterministic pseudo-random feature point per cell.
        uint seedU = unchecked((uint)seed);
        uint hx = Hash2D(cellX, cellY, seedU);
        uint hy = Hash2D(cellX, cellY, seedU ^ 0xA2C2A9u);
        // Map hash bits to [0,1)
        float rx = (hx & 0xFFFFFFu) / 16777216f;
        float ry = (hy & 0xFFFFFFu) / 16777216f;
        float px = (cellX + rx) * cellSize;
        float py = (cellY + ry) * cellSize;
        return new Vector2(px, py);
    }

    private BiomeSettings GetBiomeForCell(int cellX, int cellY, float cellSize)
    {
        // Choose biome based on feature point distance from origin and per-biome weights.
        Vector2 feature = GetFeaturePoint(cellX, cellY, cellSize);
        float radius = feature.magnitude;

        int nonNullCount = 0;
        int eligibleCount = 0;
        int lastNonNullIndex = -1;
        int lastEligibleIndex = -1;
        float totalWeight = 0f;

        for (int i = 0; i < biomes.Length; i++)
        {
            BiomeSettings biome = biomes[i];
            if (biome == null) continue;

            // Height-based biomes must not participate in horizontal Voronoi selection.
            if (biome.excludeFromVoronoiSelection) continue;
            if (biome == mountainBiome || biome == underwaterBiome) continue;

            nonNullCount++;
            lastNonNullIndex = i;

            if (radius < biome.startDistance) continue;

            eligibleCount++;
            lastEligibleIndex = i;
            totalWeight += Mathf.Max(0f, biome.voronoiWeight);
        }

        if (nonNullCount == 0)
        {
            return null;
        }

        // If no eligible biomes at this radius, fall back to non-null.
        bool canUseEligible = eligibleCount > 0;

        if (!canUseEligible || totalWeight <= 0f)
        {
            // Deterministic uniform pick among eligible (if any) or among all non-null.
            uint h0 = Hash2D(cellX, cellY, unchecked((uint)seed));
            int pick = (int)(h0 % (uint)(canUseEligible ? eligibleCount : nonNullCount));

            int seen = 0;
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeSettings biome = biomes[i];
                if (biome == null) continue;
                if (biome.excludeFromVoronoiSelection) continue;
                if (biome == mountainBiome || biome == underwaterBiome) continue;
                if (canUseEligible && radius < biome.startDistance) continue;

                if (seen == pick) return biome;
                seen++;
            }

            // Fallback: last eligible (or last non-null).
            return (canUseEligible && lastEligibleIndex >= 0) ? biomes[lastEligibleIndex] : biomes[lastNonNullIndex];
        }

        // Deterministic weighted pick from hash (iterate in biome array order).
        uint h = Hash2D(cellX, cellY, unchecked((uint)seed) ^ 0x6D2B79F5u);
        float pickWeight = (h / (float)uint.MaxValue) * totalWeight;
        float cumulative = 0f;
        for (int i = 0; i < biomes.Length; i++)
        {
            BiomeSettings biome = biomes[i];
            if (biome == null) continue;
            if (biome.excludeFromVoronoiSelection) continue;
            if (biome == mountainBiome || biome == underwaterBiome) continue;
            if (radius < biome.startDistance) continue;

            cumulative += Mathf.Max(0f, biome.voronoiWeight);
            if (pickWeight <= cumulative) return biome;
        }

        return biomes[lastEligibleIndex >= 0 ? lastEligibleIndex : lastNonNullIndex];
    }

    private static uint Hash2D(int x, int y, uint s)
    {
        unchecked
        {
            // Strong-ish 2D hash (uint mix). Deterministic across platforms.
            uint h = s;
            h ^= 0x9E3779B9u;
            h += (uint)x * 0x85EBCA6Bu;
            h = (h << 13) | (h >> 19);
            h ^= (uint)y * 0xC2B2AE35u;

            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }
    }
}

