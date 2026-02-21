using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generates a top-down XZ map that encodes vertical information via surface height.
///
/// Runtime mode uses Physics raycasts against terrain colliders (works with volumetric/overhang terrain).
/// Color encodes biome blending + underwater/mountain overlays and can optionally be shaded by height.
///
/// This script does not create any UI. Optionally assign a RawImage to display the texture.
/// </summary>
public class WorldMapRenderer : MonoBehaviour
{
    public enum ExtentMode
    {
        AroundCenter,
        FixedBounds
    }

    [Header("References")]
    public BiomeManager biomeManager;
    public VolumetricTerrainGenerator terrain;
    public Transform center;

    [Header("Optional UI")]
    public RawImage target;

    [Header("Map")]
    public ExtentMode extentMode = ExtentMode.AroundCenter;

    [Min(16)] public int resolution = 256;

    [Tooltip("World size (meters) covered by the map when using AroundCenter.")]
    [Min(1f)] public float worldSize = 1024f;

    [Tooltip("Used when extentMode = FixedBounds.")]
    public Vector2 fixedMinXZ = new Vector2(-512f, -512f);

    [Tooltip("Used when extentMode = FixedBounds.")]
    public Vector2 fixedMaxXZ = new Vector2(512f, 512f);

    [Header("Sampling")]
    [Tooltip("Layer mask for colliders the map should raycast against (Terrain + Water).")]
    public LayerMask terrainLayerMask = ~0;

    [Tooltip("If a raycast hits a collider on this layer, the pixel is treated as water and tinted accordingly.")]
    public string waterLayerName = "Water";

    [Tooltip("If true, pixels with no ray hit are made transparent.")]
    public bool transparentWhenNoHit = true;

    [Tooltip("Extra height added above maxWorldY for raycast start.")]
    [Min(0f)] public float rayStartPadding = 50f;

    [Tooltip("Extra height added below minWorldY for raycast length.")]
    [Min(0f)] public float rayEndPadding = 50f;

    [Header("Look")]
    public bool shadeByHeight = true;

    [Tooltip("If enabled, uses a stronger height shading curve for clearer elevation reading.")]
    public bool strongHeightContrast = false;

    [Tooltip("Brightness at the low end of height shading.")]
    [Range(0f, 2f)] public float lowHeightBrightness = 0.7f;

    [Tooltip("Brightness at the high end of height shading.")]
    [Range(0f, 2f)] public float highHeightBrightness = 1.15f;

    [Tooltip("Tint used for underwater overlay in the map.")]
    public Color underwaterTint = new Color(0.1f, 0.35f, 0.85f, 1f);

    [Tooltip("Tint used for mountain overlay in the map.")]
    public Color mountainTint = new Color(0.75f, 0.75f, 0.78f, 1f);

    [Header("Update")]
    public bool updateOnStart = true;
    public bool updateContinuously = false;

    [Min(0.05f)] public float updateIntervalSeconds = 0.5f;

    private Texture2D mapTexture;
    private Color32[] pixels;
    private Coroutine rebuildRoutine;

    public Texture2D MapTexture => mapTexture;

    void Start()
    {
        if (!Application.isPlaying) return;
        if (updateOnStart) RequestRebuild();
        if (updateContinuously) InvokeRepeating(nameof(RequestRebuild), updateIntervalSeconds, updateIntervalSeconds);
    }

    void OnDisable()
    {
        if (Application.isPlaying)
        {
            CancelInvoke(nameof(RequestRebuild));
        }

        if (rebuildRoutine != null)
        {
            StopCoroutine(rebuildRoutine);
            rebuildRoutine = null;
        }
    }

    public void RequestRebuild()
    {
        if (!Application.isPlaying)
        {
            RebuildNow();
            return;
        }

        if (rebuildRoutine != null) return;
        rebuildRoutine = StartCoroutine(RebuildCoroutine());
    }

    public void RebuildNow()
    {
        EnsureTexture();
        BuildPixels(timeSliced: false);
        ApplyTexture();
    }

    private IEnumerator RebuildCoroutine()
    {
        EnsureTexture();
        BuildPixels(timeSliced: true);
        ApplyTexture();
        rebuildRoutine = null;
        yield break;
    }

    private void EnsureTexture()
    {
        int r = Mathf.Max(16, resolution);
        if (mapTexture == null || mapTexture.width != r || mapTexture.height != r)
        {
            mapTexture = new Texture2D(r, r, TextureFormat.RGBA32, mipChain: false, linear: false);
            mapTexture.wrapMode = TextureWrapMode.Clamp;
            mapTexture.filterMode = FilterMode.Point;
            pixels = new Color32[r * r];
        }
        else if (pixels == null || pixels.Length != r * r)
        {
            pixels = new Color32[r * r];
        }
    }

    private void ApplyTexture()
    {
        if (mapTexture == null || pixels == null) return;
        mapTexture.SetPixels32(pixels);
        mapTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

        if (target != null)
        {
            target.texture = mapTexture;
        }
    }

    private void BuildPixels(bool timeSliced)
    {
        BiomeManager bm = biomeManager != null ? biomeManager : FindAnyObjectByType<BiomeManager>();
        VolumetricTerrainGenerator gen = terrain != null ? terrain : FindAnyObjectByType<VolumetricTerrainGenerator>();

        Transform mapCenter = center != null ? center : (gen != null ? gen.player : null);
        if (mapCenter == null) mapCenter = transform;

        Vector2 minXZ;
        Vector2 maxXZ;

        if (extentMode == ExtentMode.FixedBounds)
        {
            minXZ = fixedMinXZ;
            maxXZ = fixedMaxXZ;
        }
        else
        {
            float half = Mathf.Max(1f, worldSize) * 0.5f;
            Vector3 c = mapCenter.position;
            minXZ = new Vector2(c.x - half, c.z - half);
            maxXZ = new Vector2(c.x + half, c.z + half);
        }

        float minY = gen != null && gen.settings != null ? Mathf.Min(gen.settings.minWorldY, gen.settings.maxWorldY) : -500f;
        float maxY = gen != null && gen.settings != null ? Mathf.Max(gen.settings.minWorldY, gen.settings.maxWorldY) : 500f;

        float rayStartY = maxY + Mathf.Max(0f, rayStartPadding);
        float rayLen = (rayStartY - (minY - Mathf.Max(0f, rayEndPadding)));
        rayLen = Mathf.Max(1f, rayLen);

        float heightDenom = Mathf.Max(0.0001f, maxY - minY);

        int r = mapTexture.width;
        float inv = 1f / (r - 1);

        // Scanline build.
        for (int py = 0; py < r; py++)
        {
            float tz = py * inv;
            float wz = Mathf.Lerp(minXZ.y, maxXZ.y, tz);

            for (int px = 0; px < r; px++)
            {
                float tx = px * inv;
                float wx = Mathf.Lerp(minXZ.x, maxXZ.x, tx);

                Color col = SampleAtWorldXZ(bm, wx, wz, rayStartY, rayLen, minY, heightDenom);
                pixels[py * r + px] = (Color32)col;
            }

            if (timeSliced && Application.isPlaying && (py % 8) == 0)
            {
                // Yield occasionally to keep the main thread responsive.
                // Note: This still does physics raycasts, so keep resolution moderate.
            }
        }
    }

    private Color SampleAtWorldXZ(BiomeManager bm, float worldX, float worldZ, float rayStartY, float rayLen, float minY, float heightDenom)
    {
        float surfaceY;
        int hitLayer;
        bool hasHit = TrySampleSurfaceHeight(worldX, worldZ, rayStartY, rayLen, out surfaceY, out hitLayer);

        if (!hasHit)
        {
            return transparentWhenNoHit ? new Color(0f, 0f, 0f, 0f) : new Color(0.05f, 0.05f, 0.05f, 1f);
        }

        int waterLayer = LayerMask.NameToLayer(waterLayerName);
        if (waterLayer >= 0 && hitLayer == waterLayer)
        {
            // Water surface overrides biome coloring.
            Color w = underwaterTint;
            if (shadeByHeight)
            {
                float t = Mathf.Clamp01((surfaceY - minY) / heightDenom);
                float brightness = Mathf.Lerp(lowHeightBrightness, highHeightBrightness, t);
                w.r = Mathf.Clamp01(w.r * brightness);
                w.g = Mathf.Clamp01(w.g * brightness);
                w.b = Mathf.Clamp01(w.b * brightness);
            }
            w.a = 1f;
            return w;
        }

        // Biome selection is driven by XZ only.
        BiomeBlendData blend = bm != null ? bm.GetBiomeBlendDataFromWorldPos(new Vector2(worldX, worldZ)) : default;

        Color cP = GetBiomeMapColor(blend.primaryBiome);
        Color cS = GetBiomeMapColor(blend.secondaryBiome != null ? blend.secondaryBiome : blend.primaryBiome);

        float secondaryW = bm != null ? bm.TuneSecondaryBlend(blend.blendFactor) : Mathf.Clamp01(blend.blendFactor);
        Color baseCol = Color.Lerp(cP, cS, secondaryW);

        float uwA = bm != null ? bm.TuneUnderwaterAlpha(bm.GetUnderwaterAlpha(surfaceY)) : 0f;
        float mA = bm != null ? bm.TuneMountainAlpha(bm.GetMountainAlpha(surfaceY)) : 0f;

        if (uwA > 0f) baseCol = Color.Lerp(baseCol, underwaterTint, uwA);
        if (mA > 0f) baseCol = Color.Lerp(baseCol, mountainTint, mA);

        if (shadeByHeight)
        {
            float t = Mathf.Clamp01((surfaceY - minY) / heightDenom);
            if (strongHeightContrast)
            {
                // Bias toward more visible midrange contrast.
                t = Mathf.SmoothStep(0f, 1f, t);
            }
            float brightness = Mathf.Lerp(lowHeightBrightness, highHeightBrightness, t);
            baseCol.r = Mathf.Clamp01(baseCol.r * brightness);
            baseCol.g = Mathf.Clamp01(baseCol.g * brightness);
            baseCol.b = Mathf.Clamp01(baseCol.b * brightness);
        }

        baseCol.a = 1f;
        return baseCol;
    }

    private bool TrySampleSurfaceHeight(float worldX, float worldZ, float rayStartY, float rayLen, out float surfaceY, out int hitLayer)
    {
        Vector3 origin = new Vector3(worldX, rayStartY, worldZ);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayLen, terrainLayerMask, QueryTriggerInteraction.Ignore))
        {
            surfaceY = hit.point.y;
            hitLayer = hit.collider != null ? hit.collider.gameObject.layer : 0;
            return true;
        }

        surfaceY = 0f;
        hitLayer = 0;
        return false;
    }

    private static Color GetBiomeMapColor(BiomeSettings biome)
    {
        if (biome == null) return new Color(0.25f, 0.25f, 0.25f, 1f);

        // Prefer explicit map color if provided.
        if (biome.mapColor.a > 0.001f)
        {
            Color c = biome.mapColor;
            c.a = 1f;
            return c;
        }

        // Otherwise, derive a distinct, deterministic color from the biome asset.
        // This avoids the common case where all materials have white base color.
        int id = biome.GetInstanceID();
        uint h = unchecked((uint)id);
        h ^= h >> 16;
        h *= 0x7FEB352Du;
        h ^= h >> 15;
        h *= 0x846CA68Bu;
        h ^= h >> 16;

        float hue = (h & 0xFFFFu) / 65535f;
        float sat = 0.75f + (((h >> 16) & 0xFFu) / 255f) * 0.2f;
        float val = 0.85f;
        Color auto = Color.HSVToRGB(hue, Mathf.Clamp01(sat), val);
        auto.a = 1f;
        return auto;
    }
}
