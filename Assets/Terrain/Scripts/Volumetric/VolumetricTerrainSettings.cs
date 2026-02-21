using UnityEngine;

[CreateAssetMenu(menuName = "Terrain/Volumetric Terrain Settings")]
public class VolumetricTerrainSettings : ScriptableObject
{
    public enum DensityMode
    {
        SurfaceOnly = 0,
        Volumetric3D = 1
    }

    [Header("Meshing")]
    [Tooltip("Iso-surface level used by the marching algorithm.\n\nThink of density as: 'solid' when density > isoLevel, and 'air' when density < isoLevel.\n\nIf you increase isoLevel, you generally need higher densities to remain solid (terrain tends to shrink / carve away). If you decrease isoLevel, terrain tends to grow / fill in.")]
    public float isoLevel = 0f;

    [Tooltip("Number of voxels per axis in a chunk (mesh resolution).\n\nHigher = smoother/more detailed surfaces but slower generation and more triangles.\nLower = blockier/faceted surfaces but faster.")]
    [Range(8, 64)]
    public int voxelsPerAxis = 24;

    [Header("Simple Tuning")]
    [Tooltip("If enabled, the generator derives most advanced noise/strength parameters from a small set of simple controls.\n\nThis is the recommended mode for quickly getting smooth, large-scale terrain. Disable to use the advanced parameters below as-is.")]
    public bool useSimpleTuning = false;

    [Tooltip("Overall world feature size in world units (bigger = smoother / larger-scale forms).\n\nThis drives the effective 3D noise frequencies when Simple Tuning is enabled.")]
    [Min(1f)]
    public float featureSize = 350f;

    [Tooltip("How much to domain-warp the 3D noise sampling when Simple Tuning is enabled.\n\n0 = no warp (more uniform). Higher = more natural, less grid-aligned features, but can become chaotic if too high.")]
    [Range(0f, 1f)]
    public float domainWarpAmount = 0.25f;

    [Tooltip("Overhang/arch amount when Simple Tuning is enabled.\n\nHigher values create more volumetric overhangs, but can also destabilize the surface if set too high.")]
    [Range(0f, 1f)]
    public float overhangAmount = 0.35f;

    [Tooltip("Cave amount when Simple Tuning is enabled.\n\nHigher values carve bigger/more connected caves.")]
    [Range(0f, 1f)]
    public float caveAmount = 0.30f;

    [Tooltip("If enabled, cave carving is biased to occur below sea level (underwater caves).\n\nSea level is taken from the active BiomeManager when available.")]
    public bool biasCavesUnderwater = true;

    [Tooltip("Floating island amount when Simple Tuning is enabled.\n\nHigher values create more/larger floating islands inside the island band.")]
    [Range(0f, 1f)]
    public float islandsAmount = 0.20f;

    [Tooltip("Island size multiplier when Simple Tuning is enabled.\n\nHigher values make islands larger by reducing the effective island-noise frequency.")]
    [Min(0.25f)]
    public float islandsSize = 1.75f;

    [Tooltip("Island rarity when Simple Tuning is enabled.\n\nHigher values produce fewer islands by raising the spawn threshold.")]
    [Range(0f, 1f)]
    public float islandsRarity = 0.70f;

    [Tooltip("How flat island tops should be when Simple Tuning is enabled.\n\n0 = purely blob-like tops. 1 = strong flat cap creating plateau-like tops.")]
    [Range(0f, 1f)]
    public float islandsTopFlatness = 0.65f;

    [Tooltip("How rough/rocky the island undersides should be when Simple Tuning is enabled.\n\nHigher values add more ridged detail on the underside only.")]
    [Range(0f, 1f)]
    public float islandsUndersideRoughness = 0.60f;

    [Tooltip("Optional terracing/plateau effect applied to the surface height (macro terrain).\n\n0 disables. Higher values create more distinct flats and steeper edges between them.")]
    [Min(0f)]
    public float terrainTerraceHeight = 0f;

    [Tooltip("Blend/smoothing for terracing transitions.\n\nLower = sharper cliff-like steps. Higher = smoother transitions.")]
    [Range(0.01f, 1f)]
    public float terrainTerraceBlend = 0.25f;

    [Header("Vertical Range (world Y)")]
    [Tooltip("Minimum world-space Y allowed for chunk streaming/generation.\n\nChunks below this band won't be created (helps performance and prevents endless vertical generation).")]
    public float minWorldY = -80f;

    [Tooltip("Maximum world-space Y allowed for chunk streaming/generation.\n\nChunks above this band won't be created. Make sure this overlaps any floating-island band if islands are enabled.")]
    public float maxWorldY = 80f;

    [Header("Surface Density")]
    [Tooltip("Scales the base terrain density computed from the height surface.\n\nBase density formula: density = (surfaceHeight(worldX,worldZ) - worldY) * surfaceDensityMultiplier\n\nHigher values make the ground 'more solid' (steeper transition across the surface). Lower values make it 'softer' (isoLevel affects it more).")]
    [Min(0.01f)] public float surfaceDensityMultiplier = 1f;

    [Header("Density Mode")]
    [Tooltip("Selects how density is computed:\n\nSurfaceOnly: classic heightfield-style terrain (no true overhangs).\nVolumetric3D: adds 3D noise terms for overhangs/caves/islands (true volumetric shapes).")]
    public DensityMode densityMode = DensityMode.SurfaceOnly;

    [System.Serializable]
    public struct Noise3DSettings
    {
        [Tooltip("Base frequency of the 3D noise in world units.\n\nLower = larger, smoother features (recommended for 'big scale').\nHigher = smaller, busier details (can look rough/noisy).")]
        [Min(0.000001f)] public float frequency;

        [Tooltip("Number of fractal layers (octaves).\n\nMore octaves = more detail across multiple scales, but can look 'crinkly' and costs more CPU.")]
        [Range(1, 12)] public int octaves;

        [Tooltip("Amplitude multiplier per octave (fractal persistence).\n\nLower values reduce small-detail contribution (smoother). Higher values add more fine detail.")]
        [Range(0f, 1f)] public float persistence;

        [Tooltip("Frequency multiplier per octave (fractal lacunarity).\n\n2.0 is common. Higher values pack more frequencies in and can increase apparent roughness.")]
        [Min(1f)] public float lacunarity;

        [Tooltip("World-space offset for this noise layer. Useful for shifting patterns without changing the seed.")]
        public Vector3 offset;
    }

    [Header("Volumetric 3D (Overhangs / Caves / Islands)")]


    [Header("Overhangs")]
    [Tooltip("Adds a 3D density term so overhangs and volumetric shapes are possible.")]
    public bool enableOverhangs;
    [Tooltip("How strongly the overhang 3D noise affects density.\n\nHigher = more dramatic arches/overhangs, but can also destabilize the surface if too strong.")]
    [Min(0f)] public float overhangStrength = 8f;

    [Tooltip("3D noise parameters for overhangs.\n\nTip: for large-scale, reduce frequency (e.g. 0.003–0.01) and keep octaves low (2–4).")]
    public Noise3DSettings overhangNoise = new Noise3DSettings
    {
        frequency = 0.006f,
        octaves = 4,
        persistence = 0.5f,
        lacunarity = 2f,
        offset = Vector3.zero
    };


    [Header("Domain Warp (Advanced)")]
    [Tooltip("Applies a low-frequency 3D warp to the sampling position before evaluating overhang/cave/island noises.\n\nThis helps create more natural large-scale forms and reduces the 'stacked layers' look.")]
    public bool enableDomainWarp = true;

    [Tooltip("Warp strength in world units (advanced).\n\nTypical values: 10–80 depending on feature size. Too high will smear features and can cause chaotic topology.")]
    [Min(0f)]
    public float domainWarpStrength = 35f;

    [Tooltip("3D noise parameters used for domain warping (advanced).")]
    public Noise3DSettings domainWarpNoise = new Noise3DSettings
    {
        frequency = 0.0035f,
        octaves = 2,
        persistence = 0.5f,
        lacunarity = 2f,
        offset = Vector3.zero
    };


    [Header("Caves")]
    [Tooltip("Carves caves out of existing solid volume using thresholded 3D noise.")]
    public bool enableCaves;
    [Tooltip("How strongly caves subtract from density.\n\nHigher = bigger/more open caves. Too high can swiss-cheese the terrain.")]
    [Min(0f)] public float caveStrength = 18f;

    [Tooltip("Threshold applied to the cave noise (noise is roughly in [-1, 1]).\n\nHigher threshold = fewer caves. Lower threshold = more caves.")]
    [Range(-1f, 1f)] public float caveThreshold = 0.2f;

    [Tooltip("Softness of the cave threshold transition, in noise-value units.\n\nHigher softness = smoother cave boundaries (less harsh/aliased). Lower = sharper cutouts.")]
    [Min(0.0001f)] public float caveSoftness = 0.25f;

    [Tooltip("3D noise parameters used to decide where caves appear.")]
    public Noise3DSettings caveNoise = new Noise3DSettings
    {
        frequency = 0.02f,
        octaves = 3,
        persistence = 0.5f,
        lacunarity = 2f,
        offset = Vector3.zero
    };

    [Header("Island")]
    [Tooltip("Creates detached volumetric blobs in a vertical band (floating islands).")]
    public bool enableFloatingIslands;
    [Tooltip("How strongly the island blob field contributes to density.\n\nHigher = islands become more solid/large. Lower = fewer/smaller islands.")]
    [Min(0f)] public float islandsStrength = 25f;

    [Tooltip("Threshold applied to island noise (noise is roughly in [-1, 1]).\n\nHigher = fewer islands. Lower = more islands.")]
    [Range(-1f, 1f)] public float islandsThreshold = 0.25f;

    [Tooltip("Softness of the island threshold.\n\nHigher = rounder, smoother blobs. Lower = sharper edges and more 'chunky' islands.")]
    [Min(0.0001f)] public float islandsSoftness = 0.35f;

    [Tooltip("Bottom of the world-space Y band where islands are allowed to appear.")]
    public float islandsMinY = 40f;

    [Tooltip("Top of the world-space Y band where islands are allowed to appear.")]
    public float islandsMaxY = 160f;

    [Tooltip("Blend distance at the edges of the islands band.\n\nHigher = islands fade in/out gradually over a thicker vertical range.")]
    [Min(0.0001f)] public float islandsBandBlend = 25f;

    [Tooltip("3D noise parameters used to place floating islands.")]
    public Noise3DSettings islandsNoise = new Noise3DSettings
    {
        frequency = 0.01f,
        octaves = 4,
        persistence = 0.5f,
        lacunarity = 2f,
        offset = Vector3.zero
    };

    [Header("Optimization")]
    [Tooltip("If enabled, chunks that have uniform density (no iso-surface crossing) will skip marching/normal generation and return an empty mesh.")]
    public bool earlyOutEmptyChunks = true;

    [Tooltip("Number of probe samples per axis used to detect an iso-surface crossing (early-out test).\n\nHigher = less likely to incorrectly skip small features (caves/islands), but slower generation.\n3 is usually enough for SurfaceOnly. Consider 4–6 for Volumetric3D.")]
    [Range(2, 6)]
    public int emptyChunkProbeResolution = 3;
}
