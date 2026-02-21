using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class VolumetricTerrainGenerator : MonoBehaviour
{
    [Header("References")]
    public MeshSettings meshSettings;
    public BiomeManager biomeManager;
    public VolumetricTerrainSettings settings;
    public SurfaceHeightSettings surfaceHeightSettings;
    public Transform player;
    public Material material;

    [Header("Seed")]
    [Tooltip("The world seed used for ALL terrain noise sources (surface + overhangs + caves + islands). This makes the whole world deterministic from one number.")]
    public Seed globalSeed;

    [Tooltip("If enabled, a new random seed is chosen on Start (Play). Disable this to keep the world stable between runs.")]
    public bool randomizeSeedOnStart = false;

    [Header("Editor Preview")]
    [Tooltip("If enabled, terrain generates in Edit Mode (without pressing Play) so you can iterate on settings quickly.")]
    public bool previewInEditMode = false;

    [Tooltip("Optional override for the preview center in Edit Mode. If not set, uses Player if assigned, otherwise this object's transform.")]
    public Transform previewCenter;

    [Tooltip("If enabled, the preview automatically rebuilds when referenced settings/assets change.")]
    public bool livePreview = true;

    [Tooltip("If enabled, the Edit Mode preview continuously updates/streams chunks in the editor.\n\nTurn this OFF for best editor performance and use the Preview buttons instead.")]
    public bool autoUpdatePreview = false;

    [Tooltip("Minimum interval between preview updates in Edit Mode.")]
    [Min(0.05f)] public float previewUpdateIntervalSeconds = 0.35f;

    [Tooltip("How many chunks the preview is allowed to create per editor tick.\n\nHigher = faster preview fill-in, but can stall the editor while generating meshes.")]
    [Min(1)] public int previewMaxChunkCreatesPerTick = 32;

    [Tooltip("Optional preview override for how many chunks to generate around the preview center in XZ.\n\n0 = use Chunks Visible In View Distance XZ.")]
    [Min(0)] public int previewChunksVisibleInViewDistanceXZ = 0;

    [Tooltip("Optional preview override for how many chunks above/below the preview center's chunk-Y to generate.\n\n0 = use Chunks Visible In View Distance Y. Ignored if Preview Generate Full Vertical Range is enabled.")]
    [Min(0)] public int previewChunksVisibleInViewDistanceY = 0;

    [Tooltip("Optional preview override: if enabled, generates the full vertical range (minWorldY..maxWorldY) in Edit Mode preview.\n\nIf disabled, uses the preview Y distance band above/below the preview center.")]
    public bool previewGenerateFullVerticalRange = false;

    [Header("Rendering")]
    [Min(0.000001f)] public float worldUvScale = 0.02f;
    [Min(0.1f)] public float triplanarSharpness = 4f;

    [Header("Chunking")]
    [Min(1)] public int chunksVisibleInViewDistanceXZ = 2;

    [Tooltip("If enabled, chunks are loaded in a circle (XZ) instead of a square, reducing the total chunk count for the same view distance.")]
    public bool useCircularViewDistanceXZ = true;

    [Tooltip("If enabled, nearer chunks are created before far chunks (helps when view distance is large).")]
    public bool prioritizeNearChunkCreation = true;

    [Tooltip("How many chunks above/below the player's current chunk-Y to keep generated.")]
    [Min(0)] public int chunksVisibleInViewDistanceY = 1;

    [Tooltip("If enabled, generates the full vertical range between VolumetricTerrainSettings.minWorldY and maxWorldY (for every visible XZ chunk). Useful when you want tall mountains/islands above the player to be generated even if the player stays low.")]
    public bool generateFullVerticalRange = true;

    [Tooltip("If enabled, uses MeshSettings.chunkWorldSize.")]
    public bool useTerrainChunkWorldSize = true;
    [Min(1f)] public float chunkWorldSizeOverride = 32f;

    [Header("Colliders")]
    public bool generateColliders = true;
    [Min(0f)] public float colliderDistance = 20f;
    [Tooltip("If enabled, far-away chunks may have their MeshCollider cleared to save performance.")]
    public bool clearCollidersWhenFar = true;

    [Tooltip("If enabled, collider clearing will only happen after the initial world is ready (prevents 'fall through while loading' issues).")]
    public bool onlyClearCollidersAfterWorldReady = true;
    public bool logColliderAssignments;

    [Header("Loading")]
    public bool waitForInitialGeneration = true;
    public bool pauseTimeScaleUntilWorldReady = true;
    public MonoBehaviour[] disableUntilWorldReady;
    public GameObject loadingScreenRoot;

    [Header("Performance")]
    [Min(0.01f)] public float updateIntervalSeconds = 0.25f;
    [Min(1)] public int maxColliderChunkChecksPerTick = 16;
    [Min(1)] public int maxChunkCreatesPerTick = 4;

    public bool GenerateColliders => generateColliders && colliderDistance > 0f;

    private readonly Dictionary<Vector3Int, VolumetricTerrainChunk> chunks = new Dictionary<Vector3Int, VolumetricTerrainChunk>();
    private readonly List<VolumetricTerrainChunk> visibleChunks = new List<VolumetricTerrainChunk>();

    private readonly HashSet<Vector3Int> neededCoords = new HashSet<Vector3Int>();
    private readonly List<Vector3Int> removeCoords = new List<Vector3Int>();

    private readonly Queue<Vector3Int> createQueue = new Queue<Vector3Int>();
    private readonly HashSet<Vector3Int> queuedCoords = new HashSet<Vector3Int>();

    private readonly List<Vector3Int> missingCoordsBuffer = new List<Vector3Int>(1024);

    private float updateTimer;
    private int colliderUpdateIndex;

    private bool isLoading;
    private readonly HashSet<Vector3Int> initialChunksToWaitFor = new HashSet<Vector3Int>();
    private float timeScaleBeforeLoading = 1f;

    private bool loggedMissingBiomeWarning;
    private bool loggedVerticalClampWarning;

    private static readonly Vector2[] paletteSampleOffsets = new Vector2[13]
    {
        new Vector2(0f, 0f),
        new Vector2(-0.25f, 0f),
        new Vector2( 0.25f, 0f),
        new Vector2(0f, -0.25f),
        new Vector2(0f,  0.25f),
        new Vector2(-0.5f, 0f),
        new Vector2( 0.5f, 0f),
        new Vector2(0f, -0.5f),
        new Vector2(0f,  0.5f),
        new Vector2(-0.5f, -0.5f),
        new Vector2(-0.5f,  0.5f),
        new Vector2( 0.5f, -0.5f),
        new Vector2( 0.5f,  0.5f)
    };

    private struct PaletteVote
    {
        public BiomeSettings biome;
        public Vector2 site;
        public int vote;
        public float bestDistSqr;
    }

    private struct PaletteScore
    {
        public long key;
        public int vote;
        public float bestDistSqr;
    }

    private readonly Dictionary<long, PaletteVote> paletteVotesBuffer = new Dictionary<long, PaletteVote>();
    private readonly List<PaletteScore> paletteOrderedBuffer = new List<PaletteScore>(32);
    private readonly List<long> paletteOrderedKeysBuffer = new List<long>(4);

    private int EffectiveSeed => globalSeed.seed;

    // Main-thread caches for thread-safe sampling.
    private Noise2DSampler cachedSurfaceNoise;
    private NoiseSettings cachedSurfaceNoiseSettings;
    private int cachedSurfaceNoiseSeed;
    private float[] cachedSurfaceCurveLut;
    private AnimationCurve cachedSurfaceCurveSource;
    private int cachedSurfaceCurveHash;

    // Worker-thread buffers to reduce GC and large heap churn.
    [System.ThreadStatic] private static float[,,] threadDensityBuffer;
    [System.ThreadStatic] private static Vector3[,,] threadNormalBuffer;

    // Non-alloc biome query buffers.
    private readonly BiomeManager.BiomeCellInfo[] nearestBiomeCells4 = new BiomeManager.BiomeCellInfo[4];
    private readonly BiomeManager.BiomeCellInfo[] nearestBiomeCells2 = new BiomeManager.BiomeCellInfo[2];

#if UNITY_EDITOR
    private double lastPreviewUpdateTime;
    private Vector3 lastPreviewCenterPos;
    private bool editorPreviewDirty = true;
#endif

    private bool IsEditModePreviewActive => !Application.isPlaying && previewInEditMode;

    private Transform CenterTransform
    {
        get
        {
            if (Application.isPlaying)
            {
                return player != null ? player : transform;
            }

            if (previewCenter != null) return previewCenter;
            if (player != null) return player;
            return transform;
        }
    }

    private void Start()
    {
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            editorPreviewDirty = true;
            lastPreviewCenterPos = CenterTransform != null ? CenterTransform.position : transform.position;
            UpdateEditorPreviewSubscription();
#endif
            return;
        }

        if (randomizeSeedOnStart)
        {
            globalSeed = Seed.CreateRandom();
        }

        UpdateSurfaceSamplingCaches();

        if (biomeManager == null)
        {
            biomeManager = FindAnyObjectByType<BiomeManager>();
        }

        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }

        if (meshSettings == null)
        {
            Debug.LogError("VolumetricTerrainGenerator is missing MeshSettings.");
            enabled = false;
            return;
        }

        if (settings == null)
        {
            Debug.LogError("VolumetricTerrainGenerator is missing VolumetricTerrainSettings.");
            enabled = false;
            return;
        }

        if (surfaceHeightSettings == null)
        {
            Debug.LogError("VolumetricTerrainGenerator is missing SurfaceHeightSettings.");
            enabled = false;
            return;
        }

        if (biomeManager == null)
        {
            Debug.LogError("VolumetricTerrainGenerator is missing BiomeManager.");
            enabled = false;
            return;
        }

        if (player == null)
        {
            Debug.LogWarning("VolumetricTerrainGenerator is missing player Transform. Assign it or tag your player as 'Player'.");
        }

        if (settings != null && settings.densityMode == VolumetricTerrainSettings.DensityMode.Volumetric3D && settings.enableFloatingIslands)
        {
            float minY = Mathf.Min(settings.minWorldY, settings.maxWorldY);
            float maxY = Mathf.Max(settings.minWorldY, settings.maxWorldY);
            float islandsMin = Mathf.Min(settings.islandsMinY, settings.islandsMaxY);
            float islandsMax = Mathf.Max(settings.islandsMinY, settings.islandsMaxY);
            if (maxY < islandsMin || minY > islandsMax)
            {
                Debug.LogWarning("VolumetricTerrainGenerator: Floating islands are enabled, but VolumetricTerrainSettings minWorldY/maxWorldY do not overlap the islands band. Islands will never generate. Increase maxWorldY and/or adjust islandsMinY/islandsMaxY.", this);
            }
        }

        if (waitForInitialGeneration)
        {
            isLoading = true;
            if (loadingScreenRoot != null) loadingScreenRoot.SetActive(true);

            if (pauseTimeScaleUntilWorldReady)
            {
                timeScaleBeforeLoading = Time.timeScale;
                Time.timeScale = 0f;
            }

            if (disableUntilWorldReady != null)
            {
                for (int i = 0; i < disableUntilWorldReady.Length; i++)
                {
                    if (disableUntilWorldReady[i] != null) disableUntilWorldReady[i].enabled = false;
                }
            }
        }

        UpdateVisibleChunks(force: true);
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
        if (Application.isPlaying) return;
        UpdateEditorPreviewSubscription();
        editorPreviewDirty = true;
        lastPreviewCenterPos = CenterTransform != null ? CenterTransform.position : transform.position;
    }

    private void OnDisable()
    {
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.update -= EditorPreviewUpdate;
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        editorPreviewDirty = true;
        UpdateSurfaceSamplingCaches();
        UpdateEditorPreviewSubscription();
    }

    private void UpdateEditorPreviewSubscription()
    {
        UnityEditor.EditorApplication.update -= EditorPreviewUpdate;
        if (previewInEditMode && autoUpdatePreview)
        {
            UnityEditor.EditorApplication.update += EditorPreviewUpdate;
        }
    }

    private void EditorPreviewUpdate()
    {
        if (Application.isPlaying) return;
        if (!IsEditModePreviewActive) return;
        if (!autoUpdatePreview) return;

        double t = UnityEditor.EditorApplication.timeSinceStartup;
        if (t - lastPreviewUpdateTime < Mathf.Max(0.05f, previewUpdateIntervalSeconds)) return;
        lastPreviewUpdateTime = t;

        bool settingsDirty = false;
        if (livePreview)
        {
            if (meshSettings != null && UnityEditor.EditorUtility.IsDirty(meshSettings)) settingsDirty = true;
            if (settings != null && UnityEditor.EditorUtility.IsDirty(settings)) settingsDirty = true;
            if (surfaceHeightSettings != null && UnityEditor.EditorUtility.IsDirty(surfaceHeightSettings)) settingsDirty = true;
        }

        Vector3 cp = CenterTransform != null ? CenterTransform.position : transform.position;
        bool moved = (cp - lastPreviewCenterPos).sqrMagnitude > 0.01f;
        lastPreviewCenterPos = cp;

        if (editorPreviewDirty || settingsDirty)
        {
            editorPreviewDirty = false;
            UpdateSurfaceSamplingCaches();
            RebuildPreviewNow();
        }

        // Keep streaming in/out chunks while preview is active so large radii actually fill in.
        // (Chunk creation is budgeted; without this, only a few chunks may ever be created.)
        UpdateVisibleChunks(force: moved);
    }

    [ContextMenu("Preview/Rebuild Now")]
    private void RebuildPreviewNow()
    {
        if (!IsEditModePreviewActive) return;
        DestroyAllChunksImmediate();
        UpdateVisibleChunks(force: true);
    }

    [ContextMenu("Preview/Clear")]
    private void ClearPreviewNow()
    {
        if (Application.isPlaying) return;
        DestroyAllChunksImmediate();
    }

    [ContextMenu("Preview/Render Now")]
    private void RenderPreviewNow()
    {
        if (Application.isPlaying) return;
        if (!IsEditModePreviewActive) return;

        // Re-render currently-created chunks without destroying the chunk objects.
        // This is useful when tweaking noise/shading settings while keeping the same preview footprint.
        var mi = typeof(VolumetricTerrainChunk).GetMethod(
            "ForceRebuildMeshAndMaterial",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

        foreach (var kvp in chunks)
        {
            var chunk = kvp.Value;
            if (chunk == null) continue;
            mi?.Invoke(chunk, null);
        }
    }

    // Public wrappers for editor buttons (manual preview workflow).
    public void EditorPreviewRebuildNow()
    {
        if (Application.isPlaying) return;
        previewInEditMode = true;
        editorPreviewDirty = false;
        RebuildPreviewNow();
    }

    public void EditorPreviewUpdateOnce()
    {
        if (Application.isPlaying) return;
        if (!IsEditModePreviewActive) return;
        UpdateVisibleChunks(force: true);
    }

    public void EditorPreviewRenderNow()
    {
        if (Application.isPlaying) return;

        // Turn preview on so the button works even if the toggle was off.
        previewInEditMode = true;
        editorPreviewDirty = false;

        // Ensure we have something to render.
        if (chunks.Count == 0)
        {
            UpdateVisibleChunks(force: true);
        }

        RenderPreviewNow();
    }

    public void EditorPreviewClearNow()
    {
        if (Application.isPlaying) return;
        ClearPreviewNow();
    }

    private void DestroyAllChunksImmediate()
    {
        // First remove any existing preview chunk GameObjects under this generator.
        // This handles "orphan" preview objects that can remain after assembly reloads
        // (the dictionary is reconstructed, but the child GameObjects can persist).
        DestroyAllPreviewChunkChildrenImmediate();

        foreach (var kvp in chunks)
        {
            kvp.Value.Destroy();
        }

        chunks.Clear();
        visibleChunks.Clear();
        neededCoords.Clear();
        removeCoords.Clear();
        createQueue.Clear();
        queuedCoords.Clear();
        missingCoordsBuffer.Clear();
        initialChunksToWaitFor.Clear();
        isLoading = false;
    }

    private void DestroyAllPreviewChunkChildrenImmediate()
    {
        // Only delete children created by VolumetricTerrainChunk (name prefix).
        // We do NOT want to delete arbitrary child objects someone put under the generator.
        const string prefix = "Volumetric Chunk (";
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null) continue;
            if (!child.name.StartsWith(prefix)) continue;
            UnityEditor.Undo.DestroyObjectImmediate(child.gameObject);
        }
    }
#endif

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (player == null) return;

        updateTimer += Time.unscaledDeltaTime;
        if (updateTimer >= updateIntervalSeconds)
        {
            updateTimer = 0f;
            UpdateVisibleChunks(force: false);
            ProcessColliderUpdates();
        }
    }

    private float ChunkWorldSize
    {
        get
        {
            if (!useTerrainChunkWorldSize) return chunkWorldSizeOverride;
            return meshSettings != null ? meshSettings.chunkWorldSize : chunkWorldSizeOverride;
        }
    }

    public float EffectiveChunkWorldSize => ChunkWorldSize;

    private void UpdateVisibleChunks(bool force)
    {
        UpdateSurfaceSamplingCaches();

        Transform centerTransform = CenterTransform;
        if (centerTransform == null) return;

        float s = ChunkWorldSize;
        Vector3 p = centerTransform.position;

        int playerChunkX = Mathf.FloorToInt(p.x / s);
        int playerChunkY = Mathf.FloorToInt(p.y / s);
        int playerChunkZ = Mathf.FloorToInt(p.z / s);

        int viewXZ = chunksVisibleInViewDistanceXZ;
        int viewY = chunksVisibleInViewDistanceY;
        bool fullVertical = generateFullVerticalRange;
        if (IsEditModePreviewActive)
        {
            if (previewChunksVisibleInViewDistanceXZ > 0) viewXZ = previewChunksVisibleInViewDistanceXZ;
            if (previewChunksVisibleInViewDistanceY > 0) viewY = previewChunksVisibleInViewDistanceY;
            fullVertical = previewGenerateFullVerticalRange;
        }

        int allowedMinY = Mathf.FloorToInt(Mathf.Min(settings.minWorldY, settings.maxWorldY) / s);
        int allowedMaxY = Mathf.FloorToInt(Mathf.Max(settings.minWorldY, settings.maxWorldY) / s);

        // Used for prioritization and for clamping when using a limited vertical view distance.
        int bandCenterY = Mathf.Clamp(playerChunkY, allowedMinY, allowedMaxY);

        int minY;
        int maxY;
        if (fullVertical)
        {
            minY = allowedMinY;
            maxY = allowedMaxY;
        }
        else
        {
            // If the player is above/below the configured vertical range, clamp the band center.
            // Otherwise minY/maxY can invert, neededCoords becomes empty, and all chunks get deleted.
            int vy = Mathf.Max(0, viewY);
            minY = Mathf.Max(allowedMinY, bandCenterY - vy);
            maxY = Mathf.Min(allowedMaxY, bandCenterY + vy);
        }

        if (minY > maxY)
        {
            // Fallback to full allowed range (should be rare).
            minY = allowedMinY;
            maxY = allowedMaxY;

            if (!loggedVerticalClampWarning)
            {
                loggedVerticalClampWarning = true;
                Debug.LogWarning("VolumetricTerrainGenerator: vertical chunk range inverted after clamping; using full allowed Y range. Check minWorldY/maxWorldY and chunk size.");
            }
        }

        neededCoords.Clear();
        visibleChunks.Clear();

        // Rebuild creation queue each tick so we can prioritize near chunks and avoid stale queued coords.
        createQueue.Clear();
        queuedCoords.Clear();
        missingCoordsBuffer.Clear();

        int r = Mathf.Max(0, viewXZ);
        int rSqr = r * r;

        for (int z = -r; z <= r; z++)
        {
            for (int x = -r; x <= r; x++)
            {
                if (useCircularViewDistanceXZ && (x * x + z * z) > rSqr) continue;

                for (int y = minY; y <= maxY; y++)
                {
                    var coord = new Vector3Int(playerChunkX + x, y, playerChunkZ + z);
                    neededCoords.Add(coord);

                    if (chunks.TryGetValue(coord, out var existing))
                    {
                        visibleChunks.Add(existing);
                    }
                    else
                    {
                        missingCoordsBuffer.Add(coord);
                    }
                }
            }
        }

        if (missingCoordsBuffer.Count > 0)
        {
            if (prioritizeNearChunkCreation)
            {
                missingCoordsBuffer.Sort((a, b) =>
                {
                    int adx = a.x - playerChunkX;
                    int ady = a.y - bandCenterY;
                    int adz = a.z - playerChunkZ;
                    int bdx = b.x - playerChunkX;
                    int bdy = b.y - bandCenterY;
                    int bdz = b.z - playerChunkZ;

                    int aDist = adx * adx + adz * adz + (ady * ady * 4);
                    int bDist = bdx * bdx + bdz * bdz + (bdy * bdy * 4);
                    return aDist.CompareTo(bDist);
                });
            }

            for (int i = 0; i < missingCoordsBuffer.Count; i++)
            {
                var coord = missingCoordsBuffer[i];
                if (queuedCoords.Add(coord))
                {
                    createQueue.Enqueue(coord);
                }
            }
        }

        // On first loading tick, mark all currently-needed coords as required.
        if (isLoading && initialChunksToWaitFor.Count == 0)
        {
            foreach (var c in neededCoords)
            {
                initialChunksToWaitFor.Add(c);
            }
        }

        // Create a limited number of chunks this tick.
        int createBudget = Mathf.Max(1, maxChunkCreatesPerTick);
        if (IsEditModePreviewActive)
        {
            createBudget = Mathf.Max(1, previewMaxChunkCreatesPerTick);
        }
        while (createBudget-- > 0 && createQueue.Count > 0)
        {
            var c = createQueue.Dequeue();
            queuedCoords.Remove(c);

            if (!neededCoords.Contains(c))
            {
                continue;
            }

            if (chunks.ContainsKey(c))
            {
                continue;
            }

            Vector3 chunkOrigin = ChunkOriginWorld(c);
            float chunkSize = ChunkWorldSize;
            Vector2 chunkCenterXZ = new Vector2(chunkOrigin.x + chunkSize * 0.5f, chunkOrigin.z + chunkSize * 0.5f);
            BiomeBlendData blend = ResolveChunkBlendData(chunkCenterXZ, chunkSize);

            if (!loggedMissingBiomeWarning && blend.primaryBiome == null)
            {
                loggedMissingBiomeWarning = true;
                Debug.LogWarning("VolumetricTerrainGenerator: BiomeManager returned no primary biome for chunk generation. Terrain may appear flat (surface height defaults to 0). Check BiomeManager.biomes assignments and that the BiomeManager is enabled.");
            }

            BiomePalette palette = EvaluateChunkBiomePalette(chunkCenterXZ, chunkSize, blend, chunkOrigin.y);

            var chunk = new VolumetricTerrainChunk(c, blend, palette, this, transform);
            chunks.Add(c, chunk);
            visibleChunks.Add(chunk);

            if (isLoading && initialChunksToWaitFor.Contains(c))
            {
                chunk.onFirstMeshAssigned += OnInitialChunkMeshAssigned;
            }

            chunk.RequestMesh();
        }

        if (force || chunks.Count > neededCoords.Count)
        {
            removeCoords.Clear();
            foreach (var kvp in chunks)
            {
                if (!neededCoords.Contains(kvp.Key))
                {
                    kvp.Value.Destroy();
                    if (isLoading) initialChunksToWaitFor.Remove(kvp.Key);
                    removeCoords.Add(kvp.Key);
                }
            }
            for (int i = 0; i < removeCoords.Count; i++) chunks.Remove(removeCoords[i]);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying) return;
        if (!previewInEditMode) return;

        Transform centerTransform = CenterTransform;
        if (centerTransform == null) return;

        float s = ChunkWorldSize;
        int viewXZ = chunksVisibleInViewDistanceXZ;
        if (previewChunksVisibleInViewDistanceXZ > 0) viewXZ = previewChunksVisibleInViewDistanceXZ;
        viewXZ = Mathf.Max(0, viewXZ);

        // Approximate radius in world units.
        float radius = viewXZ * s;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(centerTransform.position, radius);
    }
#endif

    private void ProcessColliderUpdates()
    {
        if (!Application.isPlaying) return;
        if (!GenerateColliders) return;

        // Always prioritize the chunk the player is currently in.
        UpdateColliderForPlayerChunk();

        int count = visibleChunks.Count;
        if (count == 0) return;

        int checks = Mathf.Min(maxColliderChunkChecksPerTick, count);
        for (int i = 0; i < checks; i++)
        {
            if (colliderUpdateIndex >= count) colliderUpdateIndex = 0;
            visibleChunks[colliderUpdateIndex].UpdateCollider();
            colliderUpdateIndex++;
        }
    }

    private void UpdateColliderForPlayerChunk()
    {
        if (player == null) return;
        float s = ChunkWorldSize;
        if (s <= 0f) return;

        Vector3 p = player.position;
        int cx = Mathf.FloorToInt(p.x / s);
        int cy = Mathf.FloorToInt(p.y / s);
        int cz = Mathf.FloorToInt(p.z / s);

        // Update a small neighborhood around the player so collider handoff at chunk borders is reliable.
        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = cx + dx;
                int z = cz + dz;
                TryUpdateColliderAt(new Vector3Int(x, cy, z));
                TryUpdateColliderAt(new Vector3Int(x, cy - 1, z));
                TryUpdateColliderAt(new Vector3Int(x, cy + 1, z));
            }
        }
    }

    private void TryUpdateColliderAt(Vector3Int coord)
    {
        if (chunks.TryGetValue(coord, out var chunk))
        {
            chunk.UpdateCollider();
        }
    }

    private void OnInitialChunkMeshAssigned(VolumetricTerrainChunk chunk)
    {
        if (!isLoading) return;

        if (initialChunksToWaitFor.Remove(chunk.coord))
        {
            if (initialChunksToWaitFor.Count == 0)
            {
                isLoading = false;
                if (loadingScreenRoot != null) loadingScreenRoot.SetActive(false);

                if (pauseTimeScaleUntilWorldReady)
                {
                    Time.timeScale = timeScaleBeforeLoading;
                }

                if (disableUntilWorldReady != null)
                {
                    for (int i = 0; i < disableUntilWorldReady.Length; i++)
                    {
                        if (disableUntilWorldReady[i] != null) disableUntilWorldReady[i].enabled = true;
                    }
                }
            }
        }
    }

    internal Vector3 ChunkOriginWorld(Vector3Int coord)
    {
        float s = ChunkWorldSize;
        return new Vector3(coord.x * s, coord.y * s, coord.z * s);
    }

    internal float SqrDistanceToPlayer(Vector3 chunkOrigin)
    {
        Transform centerTransform = CenterTransform;
        if (centerTransform == null) return float.PositiveInfinity;
        Vector3 pp = centerTransform.position;
        float s = ChunkWorldSize;
        Vector3 center = chunkOrigin + new Vector3(s * 0.5f, s * 0.5f, s * 0.5f);
        return (pp - center).sqrMagnitude;
    }

    internal float SqrDistanceToPlayerXZ(Vector3 chunkOrigin)
    {
        Transform centerTransform = CenterTransform;
        if (centerTransform == null) return float.PositiveInfinity;
        Vector3 pp = centerTransform.position;
        float s = ChunkWorldSize;

        // Distance from player to the chunk's XZ bounds (0 when the player is inside the chunk).
        float minX = chunkOrigin.x;
        float maxX = chunkOrigin.x + s;
        float minZ = chunkOrigin.z;
        float maxZ = chunkOrigin.z + s;

        float dx = 0f;
        if (pp.x < minX) dx = minX - pp.x;
        else if (pp.x > maxX) dx = pp.x - maxX;

        float dz = 0f;
        if (pp.z < minZ) dz = minZ - pp.z;
        else if (pp.z > maxZ) dz = pp.z - maxZ;

        return dx * dx + dz * dz;
    }

    internal VolumetricMeshData GenerateChunkMeshData(Vector3Int coord, BiomeBlendData blend)
    {
        float chunkSize = ChunkWorldSize;
        Vector3 origin = ChunkOriginWorld(coord);

        int seed = EffectiveSeed;

        int voxels = Mathf.Max(1, settings.voxelsPerAxis);
        int points = voxels + 1;
        float cellSize = chunkSize / voxels;

        // Pad by 1 sample in every direction so normals at the chunk border can use central differences
        // without needing to consult neighbor chunks.
        int paddedPoints = points + 2;

        // Reuse large worker buffers when possible.
        float[,,] density = GetOrCreateThreadDensityBuffer(paddedPoints);

        // Copy surface settings into thread-safe locals.
        Noise2DSampler surfaceNoise = cachedSurfaceNoise;
        float[] surfaceCurveLut = cachedSurfaceCurveLut;

        float surfaceBase = surfaceHeightSettings != null ? surfaceHeightSettings.baseHeight : 0f;
        float surfaceMultiplier = surfaceHeightSettings != null ? surfaceHeightSettings.heightMultiplier : 0f;

        bool volumetric3D = settings != null && settings.densityMode == VolumetricTerrainSettings.DensityMode.Volumetric3D;
        bool useSimple = settings != null && settings.useSimpleTuning;

        // Derive effective advanced values from Simple Tuning when enabled.
        float effectiveOverhangStrength = settings != null ? settings.overhangStrength : 0f;
        float effectiveCaveStrength = settings != null ? settings.caveStrength : 0f;
        float effectiveCaveThreshold = settings != null ? settings.caveThreshold : 0f;
        float effectiveCaveSoftness = settings != null ? settings.caveSoftness : 0.25f;
        float effectiveIslandsStrength = settings != null ? settings.islandsStrength : 0f;
        float effectiveIslandsThreshold = settings != null ? settings.islandsThreshold : 0.25f;
        float effectiveIslandsSoftness = settings != null ? settings.islandsSoftness : 0.35f;

        VolumetricTerrainSettings.Noise3DSettings effectiveOverhangNoise = settings != null ? settings.overhangNoise : default;
        VolumetricTerrainSettings.Noise3DSettings effectiveCaveNoise = settings != null ? settings.caveNoise : default;
        VolumetricTerrainSettings.Noise3DSettings effectiveIslandsNoise = settings != null ? settings.islandsNoise : default;

        float effectiveTerraceHeight = 0f;
        float effectiveTerraceBlend = 0.25f;
        float effectiveIslandsTopFlatness = 0f;
        float effectiveIslandsUndersideRoughness = 0f;

        bool effectiveEnableWarp = settings != null && settings.enableDomainWarp;
        float effectiveWarpStrength = settings != null ? settings.domainWarpStrength : 0f;
        VolumetricTerrainSettings.Noise3DSettings effectiveWarpNoise = settings != null ? settings.domainWarpNoise : default;

        if (useSimple && settings != null)
        {
            float featureSize = Mathf.Max(1f, settings.featureSize);
            float baseFreq = 1f / featureSize;

            // Keep octaves low for smooth large-scale results.
            effectiveOverhangNoise = new VolumetricTerrainSettings.Noise3DSettings
            {
                frequency = baseFreq * 1.10f,
                octaves = 3,
                persistence = 0.45f,
                lacunarity = 2f,
                offset = settings.overhangNoise.offset
            };

            effectiveCaveNoise = new VolumetricTerrainSettings.Noise3DSettings
            {
                frequency = baseFreq * 2.15f,
                octaves = 3,
                persistence = 0.50f,
                lacunarity = 2f,
                offset = settings.caveNoise.offset
            };

            effectiveIslandsNoise = new VolumetricTerrainSettings.Noise3DSettings
            {
                frequency = baseFreq * 1.15f / Mathf.Max(0.25f, settings.islandsSize),
                octaves = 3,
                persistence = 0.50f,
                lacunarity = 2f,
                offset = settings.islandsNoise.offset
            };

            effectiveOverhangStrength = settings.overhangAmount * 12f;
            effectiveCaveStrength = settings.caveAmount * 22f;
            effectiveIslandsStrength = settings.islandsAmount * 32f;

            effectiveCaveThreshold = 0.12f;
            effectiveCaveSoftness = 0.32f;
            // Larger value => fewer islands.
            effectiveIslandsThreshold = Mathf.Lerp(0.05f, 0.50f, Mathf.Clamp01(settings.islandsRarity));
            effectiveIslandsSoftness = 0.38f;

            effectiveIslandsTopFlatness = Mathf.Clamp01(settings.islandsTopFlatness);
            effectiveIslandsUndersideRoughness = Mathf.Clamp01(settings.islandsUndersideRoughness);

            effectiveTerraceHeight = Mathf.Max(0f, settings.terrainTerraceHeight);
            effectiveTerraceBlend = Mathf.Clamp(settings.terrainTerraceBlend, 0.01f, 1f);

            effectiveEnableWarp = settings.enableDomainWarp;
            effectiveWarpStrength = settings.domainWarpAmount * (featureSize * 0.18f);
            effectiveWarpNoise = new VolumetricTerrainSettings.Noise3DSettings
            {
                frequency = baseFreq * 0.70f,
                octaves = 2,
                persistence = 0.50f,
                lacunarity = 2f,
                offset = settings.domainWarpNoise.offset
            };
        }

        bool useOverhangs = volumetric3D && settings.enableOverhangs && effectiveOverhangStrength > 0f;
        bool useCaves = volumetric3D && settings.enableCaves && effectiveCaveStrength > 0f;
        bool useIslands = volumetric3D && settings.enableFloatingIslands && effectiveIslandsStrength > 0f;
        bool useWarp = volumetric3D && effectiveEnableWarp && effectiveWarpStrength > 0f;

        Noise3DSampler warpNoise = useWarp ? new Noise3DSampler(effectiveWarpNoise, seed) : default;
        Noise3DSampler overhangNoise = useOverhangs ? new Noise3DSampler(effectiveOverhangNoise, seed) : default;
        Noise3DSampler caveNoise = useCaves ? new Noise3DSampler(effectiveCaveNoise, seed) : default;
        Noise3DSampler islandsNoise = useIslands ? new Noise3DSampler(effectiveIslandsNoise, seed) : default;

        // Islands underside detail: higher frequency ridged noise.
        bool useIslandDetail = useIslands && effectiveIslandsUndersideRoughness > 0f;
        Noise3DSampler islandsDetailNoise = default;
        if (useIslandDetail)
        {
            var detail = effectiveIslandsNoise;
            // Much higher frequency than the blob field: this is what makes it feel like "rock".
            detail.frequency *= 6.0f;
            detail.octaves = Mathf.Clamp(detail.octaves, 1, 4);
            islandsDetailNoise = new Noise3DSampler(detail, seed);
        }

        // Fast path: probe a coarse grid to see if the iso-surface can possibly exist in this chunk.
        // If density is uniform relative to isoLevel (all above or all below), marching produces 0 triangles.
        if (settings != null && settings.earlyOutEmptyChunks)
        {
            int res = Mathf.Clamp(settings.emptyChunkProbeResolution, 2, 6);
            float iso = settings.isoLevel;

            bool hasSign = false;
            bool firstAbove = false;
            bool crossing = false;

            // Probe within chunk bounds (no padding) for best signal.
            for (int pz = 0; pz < res && !crossing; pz++)
            {
                float tz = (res == 1) ? 0f : (float)pz / (res - 1);
                for (int py = 0; py < res && !crossing; py++)
                {
                    float ty = (res == 1) ? 0f : (float)py / (res - 1);
                    for (int px = 0; px < res; px++)
                    {
                        float tx = (res == 1) ? 0f : (float)px / (res - 1);
                        Vector3 wp = origin + new Vector3(tx * chunkSize, ty * chunkSize, tz * chunkSize);
                        float d = SampleDensity(
                            wp,
                            chunkSize,
                            surfaceCurveLut,
                            surfaceBase,
                            surfaceMultiplier,
                            effectiveTerraceHeight,
                            effectiveTerraceBlend,
                            surfaceNoise,
                            useWarp,
                            warpNoise,
                            effectiveWarpStrength,
                            useOverhangs,
                            overhangNoise,
                            effectiveOverhangStrength,
                            useCaves,
                            caveNoise,
                            effectiveCaveStrength,
                            effectiveCaveThreshold,
                            effectiveCaveSoftness,
                            settings.biasCavesUnderwater && biomeManager != null,
                            biomeManager != null ? biomeManager.seaLevel : 0f,
                            useIslands,
                            islandsNoise,
                            useIslandDetail,
                            islandsDetailNoise,
                            effectiveIslandsStrength,
                            effectiveIslandsThreshold,
                            effectiveIslandsSoftness,
                            effectiveIslandsTopFlatness,
                            effectiveIslandsUndersideRoughness);
                        bool above = d > iso;

                        if (!hasSign)
                        {
                            hasSign = true;
                            firstAbove = above;
                        }
                        else if (above != firstAbove)
                        {
                            crossing = true;
                            break;
                        }
                    }
                }
            }

            if (!crossing)
            {
                return new VolumetricMeshData(System.Array.Empty<Vector3>(), null, System.Array.Empty<int>());
            }
        }

        for (int z = 0; z < paddedPoints; z++)
        {
            for (int y = 0; y < paddedPoints; y++)
            {
                for (int x = 0; x < paddedPoints; x++)
                {
                    Vector3 wp = origin + new Vector3((x - 1) * cellSize, (y - 1) * cellSize, (z - 1) * cellSize);
                    density[x, y, z] = SampleDensity(
                        wp,
                        chunkSize,
                        surfaceCurveLut,
                        surfaceBase,
                        surfaceMultiplier,
                        effectiveTerraceHeight,
                        effectiveTerraceBlend,
                        surfaceNoise,
                        useWarp,
                        warpNoise,
                        effectiveWarpStrength,
                        useOverhangs,
                        overhangNoise,
                        effectiveOverhangStrength,
                        useCaves,
                        caveNoise,
                        effectiveCaveStrength,
                        effectiveCaveThreshold,
                        effectiveCaveSoftness,
                        settings.biasCavesUnderwater && biomeManager != null,
                        biomeManager != null ? biomeManager.seaLevel : 0f,
                        useIslands,
                        islandsNoise,
                        useIslandDetail,
                        islandsDetailNoise,
                        effectiveIslandsStrength,
                        effectiveIslandsThreshold,
                        effectiveIslandsSoftness,
                        effectiveIslandsTopFlatness,
                        effectiveIslandsUndersideRoughness);
                }
            }
        }

        Vector3[,,] normals = GetOrCreateThreadNormalBuffer(paddedPoints);
        BuildNormalGridInto(density, cellSize, normals);

        VolumetricMeshData meshData = MarchingCubes.Generate(density, normals, settings.isoLevel, cellSize, 1, 1, 1, voxels, voxels, voxels);

        // Compute biome indices + weights per vertex on the worker thread so the main thread
        // doesn't stall when assigning the mesh.
        BiomeManager bm = biomeManager;
        if (bm != null && meshData.vertices != null && meshData.vertices.Length > 0)
        {
            var verts = meshData.vertices;
            var vnormals = meshData.normals;
            bool hasVNormals = vnormals != null && vnormals.Length == verts.Length;

            var colors = new Color[verts.Length];
            var uv2 = new Vector2[verts.Length];

            bool hasUnderwater = bm.underwaterBiome != null;
            bool hasMountain = bm.mountainBiome != null;

            // Island underside rock override: use mountainBiome on downward-facing surfaces within the island band.
            bool islandRockOverride = settings != null && settings.enableFloatingIslands && settings.islandsUndersideRoughness > 0f;
            float islandMinY = 0f;
            float islandMaxY = 0f;
            float islandBlend = 0f;
            float undersideRockAmount = 0f;
            if (islandRockOverride && settings != null)
            {
                islandMinY = Mathf.Min(settings.islandsMinY, settings.islandsMaxY);
                islandMaxY = Mathf.Max(settings.islandsMinY, settings.islandsMaxY);
                islandBlend = Mathf.Max(0.0001f, settings.islandsBandBlend);
                undersideRockAmount = Mathf.Clamp01(settings.islandsUndersideRoughness);
            }

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 vertexWorldPos = verts[i] + origin;

                var b = bm.GetBiomeBlendDataFromWorldPos(new Vector2(vertexWorldPos.x, vertexWorldPos.z));
                int primaryIdx = bm.GetBiomeIndex(b.primaryBiome);
                int secondaryIdx = bm.GetBiomeIndex(b.secondaryBiome != null ? b.secondaryBiome : b.primaryBiome);
                uv2[i] = new Vector2(primaryIdx, secondaryIdx);

                float secondaryW = bm.TuneSecondaryBlend(b.blendFactor);
                float underwaterA = hasUnderwater ? bm.TuneUnderwaterAlpha(bm.GetUnderwaterAlpha(vertexWorldPos.y)) : 0f;
                float mountainA = hasMountain ? bm.TuneMountainAlpha(bm.GetMountainAlpha(vertexWorldPos.y)) : 0f;

                if (islandRockOverride && hasMountain && hasVNormals)
                {
                    // Band mask around the islands vertical range.
                    float up = Mathf.Clamp01((vertexWorldPos.y - (islandMinY - islandBlend)) / islandBlend);
                    float down = Mathf.Clamp01(((islandMaxY + islandBlend) - vertexWorldPos.y) / islandBlend);
                    float band = Mathf.SmoothStep(0f, 1f, Mathf.Min(up, down));

                    // Down-facing factor (0 on upward surfaces, 1 on strong undersides).
                    Vector3 wn = vnormals[i];
                    float downFacing = Mathf.Clamp01((-wn.y - 0.05f) / 0.70f);
                    float rock = band * downFacing * undersideRockAmount;
                    mountainA = Mathf.Max(mountainA, bm.TuneMountainAlpha(rock));
                }

                colors[i] = new Color(secondaryW, underwaterA, mountainA, 1f);
            }

            return new VolumetricMeshData(meshData.vertices, meshData.normals, meshData.triangles, colors, uv2);
        }

        return meshData;
    }

    private static readonly Vector2[] blendSampleOffsets = new Vector2[5]
    {
        new Vector2(0f, 0f),
        new Vector2(-0.5f, -0.5f),
        new Vector2(-0.5f,  0.5f),
        new Vector2( 0.5f, -0.5f),
        new Vector2( 0.5f,  0.5f)
    };

    private static Vector2 GetPaletteAnchor(Vector2 worldXZ, float chunkWorldSize)
    {
        float snap = Mathf.Max(1f, chunkWorldSize * 2f); // 2x2 chunk palette blocks
        float ax = Mathf.Floor(worldXZ.x / snap) * snap + snap * 0.5f;
        float az = Mathf.Floor(worldXZ.y / snap) * snap + snap * 0.5f;
        return new Vector2(ax, az);
    }

    private BiomeBlendData ResolveChunkBlendData(Vector2 chunkCenterXZ, float chunkWorldSize)
    {
        // Sample multiple points so neighbors agree on the same biome pair.
        BiomeBlendData best = biomeManager.GetBiomeBlendDataFromWorldPos(chunkCenterXZ);
        if (best.primaryBiome == null)
        {
            return best;
        }

        static bool SamePair(BiomeBlendData x, BiomeSettings p, BiomeSettings s)
        {
            return x.primaryBiome == p && x.secondaryBiome == s;
        }

        // Majority vote among up to 5 samples, without allocations.
        BiomeSettings aP = null; BiomeSettings aS = null; int ca = 0;
        BiomeSettings bP = null; BiomeSettings bS = null; int cb = 0;

        BiomeBlendData aSample = default;
        BiomeBlendData bSample = default;

        for (int i = 0; i < blendSampleOffsets.Length; i++)
        {
            var s = biomeManager.GetBiomeBlendDataFromWorldPos(chunkCenterXZ + blendSampleOffsets[i] * chunkWorldSize);
            if (s.primaryBiome == null) continue;

            if (aP == null || SamePair(s, aP, aS))
            {
                aP = s.primaryBiome;
                aS = s.secondaryBiome;
                ca++;
                aSample = s;
            }
            else if (bP == null || SamePair(s, bP, bS))
            {
                bP = s.primaryBiome;
                bS = s.secondaryBiome;
                cb++;
                bSample = s;
            }
            else
            {
                // More than 2 candidates: decay counts and keep the two strongest.
                ca--;
                cb--;
                if (ca <= 0)
                {
                    aP = s.primaryBiome;
                    aS = s.secondaryBiome;
                    ca = 1;
                    aSample = s;
                }
            }
        }

        // Pick the candidate with higher count (fallback to center sample).
        if (bP != null && cb > ca) return bSample.primaryBiome != null ? bSample : best;
        return aSample.primaryBiome != null ? aSample : best;
    }

    internal bool ShouldClearCollidersWhenFar()
    {
        if (!clearCollidersWhenFar) return false;
        if (onlyClearCollidersAfterWorldReady && isLoading) return false;
        return true;
    }

    private BiomePalette EvaluateChunkBiomePalette(Vector2 paletteAnchorWorldPos, float chunkWorldSize, BiomeBlendData biomeBlendData, float chunkOriginWorldY)
    {
        if (biomeManager == null || biomeBlendData.primaryBiome == null)
        {
            return default;
        }

        // Seam-safe fixed channel meaning:
        // R = primary biome (Voronoi)
        // G = secondary biome (Voronoi)
        // B = underwater biome (height mask only)
        // A = mountain biome (height mask only)
        //
        // Height biomes are injected with a far-away site so they never influence the
        // primary/secondary Voronoi selection.
        Vector2 farSite = new Vector2(1e9f, 1e9f);

        BiomeSettings primary = biomeBlendData.primaryBiome;
        Vector2 primarySite = biomeBlendData.primarySite;

        BiomeSettings secondary = biomeBlendData.secondaryBiome != null ? biomeBlendData.secondaryBiome : primary;
        Vector2 secondarySite = biomeBlendData.secondaryBiome != null ? biomeBlendData.secondarySite : primarySite;

        BiomeSettings underwater = biomeManager.underwaterBiome;
        BiomeSettings mountain = biomeManager.mountainBiome;

        // We keep count=4 when primary exists so the material binding path always sets all textures.
        return new BiomePalette(
            primary, primarySite,
            secondary, secondarySite,
            underwater, farSite,
            mountain, farSite,
            4,
            biomeManager.biomeBlendWidth);
    }

    private float SampleDensity(
        Vector3 worldPos,
        float chunkWorldSize,
        float[] surfaceCurveLut,
        float surfaceBaseHeight,
        float surfaceMultiplier,
        float terraceHeight,
        float terraceBlend,
        Noise2DSampler surfaceNoise,
        bool useWarp,
        Noise3DSampler warpNoise,
        float warpStrength,
        bool useOverhangs,
        Noise3DSampler overhangNoise,
        float overhangStrength,
        bool useCaves,
        Noise3DSampler caveNoise,
        float caveStrength,
        float caveThreshold,
        float caveSoftness,
        bool biasCavesUnderwater,
        float seaLevel,
        bool useIslands,
        Noise3DSampler islandsNoise,
        bool useIslandDetail,
        Noise3DSampler islandsDetailNoise,
        float islandsStrength,
        float islandsThreshold,
        float islandsSoftness,
        float islandsTopFlatness,
        float islandsUndersideRoughness)
    {
        float surfaceH = SampleSurfaceHeight(worldPos.x, worldPos.z, surfaceCurveLut, surfaceBaseHeight, surfaceMultiplier, surfaceNoise);
        if (terraceHeight > 0f)
        {
            surfaceH = Terrace(surfaceH, terraceHeight, terraceBlend);
        }

        // Signed distance-ish term in world units (positive = solid below surface).
        float signedToSurface = surfaceH - worldPos.y;
        float d = signedToSurface * settings.surfaceDensityMultiplier;

        // A wide mask around the surface so volumetric shaping mainly affects where it matters.
        // This keeps large-scale terrain smooth and reduces lighting noise from tiny density perturbations.
        float surfaceBand = Mathf.Max(12f, chunkWorldSize * 0.65f);
        float surfaceMask = 1f - Mathf.Clamp01(Mathf.Abs(signedToSurface) / surfaceBand);
        surfaceMask = Mathf.SmoothStep(0f, 1f, surfaceMask);

        Vector3 p = worldPos;
        if (useWarp && warpStrength > 0f)
        {
            // Vector domain warp via 3 decorrelated samples.
            float wx = warpNoise.SampleFBm(worldPos + new Vector3(37.2f, 11.7f, 5.3f));
            float wy = warpNoise.SampleFBm(worldPos + new Vector3(9.1f, 73.1f, 21.4f));
            float wz = warpNoise.SampleFBm(worldPos + new Vector3(19.7f, 2.9f, 61.6f));
            p = worldPos + new Vector3(wx, wy, wz) * warpStrength;
        }

        if (settings != null && settings.densityMode == VolumetricTerrainSettings.DensityMode.Volumetric3D)
        {
            if (useOverhangs && overhangStrength > 0f)
            {
                // Ridged noise creates structural arches, but keep it surface-biased to avoid "busy" interiors.
                float r = overhangNoise.SampleRidged(p); // ~[0,1]
                float centered = (r - 0.5f) * 2f;
                d += centered * overhangStrength * surfaceMask;
            }

            if (useIslands && islandsStrength > 0f)
            {
                float band = BandMask(worldPos.y, settings.islandsMinY, settings.islandsMaxY, settings.islandsBandBlend);
                // Force islands to not contribute outside the band.
                float islandsDensity = -1000f;
                if (band > 0f)
                {
                    float n = islandsNoise.SampleFBm(p); // [-1,1]
                    float blob = SmoothThreshold(n, islandsThreshold, islandsSoftness); // [0,1]
                    islandsDensity = (blob * 2f - 1f) * islandsStrength;
                    islandsDensity = Mathf.Lerp(-1000f, islandsDensity, band);

                    // Flatten island tops by intersecting with a cap plane near the top of the island band.
                    // This produces plateau-like tops while keeping the blob shape on the sides.
                    if (islandsTopFlatness > 0f)
                    {
                        float capY = Mathf.Max(settings.islandsMinY, settings.islandsMaxY) - Mathf.Max(5f, settings.islandsBandBlend * 0.5f);
                        float capStrength = Mathf.Lerp(0.15f, 2.25f, Mathf.Clamp01(islandsTopFlatness));
                        float cap = (capY - worldPos.y) * capStrength;
                        islandsDensity = Mathf.Min(islandsDensity, cap);
                    }

                    // Roughen island undersides with ridged detail, concentrated towards the bottom of the band.
                    if (useIslandDetail && islandsUndersideRoughness > 0f)
                    {
                        float minY = Mathf.Min(settings.islandsMinY, settings.islandsMaxY);
                        float bottomY = minY + Mathf.Max(6f, settings.islandsBandBlend * 0.25f);
                        // 0 above bottomY, ramps to 1 as we go below the underside.
                        float roughMask = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((bottomY - worldPos.y) / 18f));
                        float r = islandsDetailNoise.SampleRidged(p);
                        float centered = (r - 0.5f) * 2f;
                        // Sharpen peaks/valleys a bit to read more like fractured rock.
                        centered = Mathf.Sign(centered) * centered * centered;

                        float roughStrength = Mathf.Lerp(0f, islandsStrength * 0.85f, Mathf.Clamp01(islandsUndersideRoughness));
                        islandsDensity += centered * roughStrength * roughMask;
                    }
                }

                // Union with a soft blend so transitions are less abrupt than hard max().
                d = SmoothMax(d, islandsDensity, k: 6f);
            }

            if (useCaves && caveStrength > 0f)
            {
                float n = caveNoise.SampleFBm(p); // [-1,1]
                float mask = SmoothThreshold(n, caveThreshold, caveSoftness);

                // Only carve in solid-ish regions, with a gentle ramp near the surface.
                float solidBand = Mathf.Max(6f, chunkWorldSize * 0.18f);
                float solidMask = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(signedToSurface / solidBand));

                float underwaterMask = 1f;
                if (biasCavesUnderwater)
                {
                    float blend = 25f;
                    underwaterMask = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((seaLevel - worldPos.y) / blend));
                }

                d -= mask * caveStrength * solidMask * underwaterMask;
            }
        }

        return d;
    }

    private static float Terrace(float height, float step, float blend)
    {
        // Creates plateaus by snapping to step intervals, with controllable smoothing.
        float s = Mathf.Max(0.0001f, step);
        float b = Mathf.Clamp01(blend);
        float t = height / s;
        float baseLevel = Mathf.Floor(t);
        float frac = t - baseLevel;
        float smoothFrac = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(frac / Mathf.Max(0.0001f, b)));
        // If blend is small, this becomes a sharper step.
        float snapped = (baseLevel + smoothFrac) * s;
        return snapped;
    }

    private static float SmoothMax(float a, float b, float k)
    {
        // k: blend softness in density units.
        // Larger k -> softer transition.
        if (k <= 0f) return Mathf.Max(a, b);
        // Note: use (a - b) so that when a >> b we return a, and when b >> a we return b.
        float h = Mathf.Clamp01(0.5f + (a - b) / (2f * k));
        return Mathf.LerpUnclamped(b, a, h) + k * h * (1f - h);
    }

    private static float SmoothThreshold(float value, float threshold, float softness)
    {
        float s = Mathf.Max(0.0001f, softness);
        float x = Mathf.InverseLerp(threshold, threshold + s, value);
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));
    }

    private static float BandMask(float y, float minY, float maxY, float blend)
    {
        float b = Mathf.Max(0.0001f, blend);
        if (minY > maxY)
        {
            float tmp = minY;
            minY = maxY;
            maxY = tmp;
        }

        float up = Mathf.Clamp01((y - (minY - b)) / b);
        float down = Mathf.Clamp01(((maxY + b) - y) / b);
        float m = Mathf.Min(up, down);
        return Mathf.SmoothStep(0f, 1f, m);
    }

    private float SampleSurfaceHeight(float worldX, float worldZ, float[] curveLut, float baseHeight, float multiplier, Noise2DSampler sampler)
    {
        float v = sampler.Sample(worldX, worldZ);
        float shaped = EvaluateCurveLut(curveLut, v);
        return baseHeight + shaped * multiplier;
    }

    private static float EvaluateCurveLut(float[] lut, float t01)
    {
        if (lut == null || lut.Length < 2) return Mathf.Clamp01(t01);

        float t = Mathf.Clamp01(t01) * (lut.Length - 1);
        int i0 = Mathf.FloorToInt(t);
        int i1 = Mathf.Min(lut.Length - 1, i0 + 1);
        float a = t - i0;
        return Mathf.LerpUnclamped(lut[i0], lut[i1], a);
    }

    private readonly struct Noise2DSampler
    {
        private readonly int octaves;
        private readonly float scale;
        private readonly float persistance;
        private readonly float lacunarity;
        private readonly Vector2[] octaveOffsets;
        private readonly float maxPossibleHeight;

        public Noise2DSampler(NoiseSettings settings)
        {
            // Seed is provided by VolumetricTerrainGenerator (global seed). This constructor remains as a
            // fallback for any non-generator usage.
            this = new Noise2DSampler(settings, seedOverride: 0);
        }

        public Noise2DSampler(NoiseSettings settings, int seedOverride)
        {
            if (settings == null)
            {
                octaves = 0;
                scale = 1f;
                persistance = 0.5f;
                lacunarity = 2f;
                octaveOffsets = null;
                maxPossibleHeight = 1f;
                return;
            }

            // Copy + clamp into thread-safe locals (do not mutate the ScriptableObject on worker threads).
            scale = Mathf.Max(settings.scale, 0.01f);
            octaves = Mathf.Max(settings.octaves, 1);
            lacunarity = Mathf.Max(settings.lacunarity, 1f);
            persistance = Mathf.Clamp01(settings.persistance);

            System.Random prng = new System.Random(seedOverride);
            octaveOffsets = new Vector2[octaves];

            float amp = 1f;
            float mph = 0f;
            for (int i = 0; i < octaves; i++)
            {
                float offsetX = prng.Next(-100000, 100000) + settings.offset.x;
                float offsetY = prng.Next(-100000, 100000) - settings.offset.y;
                octaveOffsets[i] = new Vector2(offsetX, offsetY);
                mph += amp;
                amp *= persistance;
            }
            maxPossibleHeight = Mathf.Max(1e-6f, mph);
        }

        public float Sample(float worldX, float worldZ)
        {
            if (octaveOffsets == null || octaves <= 0) return 0f;

            float noiseHeight = 0f;
            float amplitude = 1f;
            float frequency = 1f;

            for (int i = 0; i < octaves; i++)
            {
                float sampleX = (worldX + octaveOffsets[i].x) / scale * frequency;
                float sampleY = (worldZ + octaveOffsets[i].y) / scale * frequency;
                float perlin = Mathf.PerlinNoise(sampleX, sampleY) * 2f - 1f;
                noiseHeight += perlin * amplitude;

                amplitude *= persistance;
                frequency *= lacunarity;
            }

            float denom = maxPossibleHeight * 2f;
            float normalized = (noiseHeight + maxPossibleHeight) / denom;
            return Mathf.Clamp01(normalized);
        }
    }

    private readonly struct Noise3DSampler
    {
        private readonly int[] perm;
        private readonly int octaves;
        private readonly float frequency;
        private readonly float persistence;
        private readonly float lacunarity;
        private readonly Vector3 offset;

        public Noise3DSampler(VolumetricTerrainSettings.Noise3DSettings settings, int seedOverride)
        {
            perm = Noise3D.GetPermutation(seedOverride);
            octaves = Mathf.Clamp(settings.octaves, 1, 12);
            frequency = Mathf.Max(0.000001f, settings.frequency);
            persistence = Mathf.Clamp01(settings.persistence);
            lacunarity = Mathf.Max(1f, settings.lacunarity);
            offset = settings.offset;
        }

        public float SampleFBm(Vector3 worldPos)
        {
            Vector3 p = worldPos + offset;
            return Noise3D.FBm(p.x, p.y, p.z, perm, octaves, frequency, lacunarity, persistence);
        }

        public float SampleRidged(Vector3 worldPos)
        {
            Vector3 p = worldPos + offset;
            return Noise3D.Ridged(p.x, p.y, p.z, perm, octaves, frequency, lacunarity, persistence);
        }
    }

    private static void BuildNormalGridInto(float[,,] density, float cellSize, Vector3[,,] normals)
    {
        int nx = density.GetLength(0);
        int ny = density.GetLength(1);
        int nz = density.GetLength(2);

        if (normals == null || normals.GetLength(0) != nx || normals.GetLength(1) != ny || normals.GetLength(2) != nz)
        {
            return;
        }
        float inv2 = 0.5f / Mathf.Max(1e-6f, cellSize);

        for (int z = 0; z < nz; z++)
        {
            int zm = Mathf.Max(0, z - 1);
            int zp = Mathf.Min(nz - 1, z + 1);
            for (int y = 0; y < ny; y++)
            {
                int ym = Mathf.Max(0, y - 1);
                int yp = Mathf.Min(ny - 1, y + 1);
                for (int x = 0; x < nx; x++)
                {
                    int xm = Mathf.Max(0, x - 1);
                    int xp = Mathf.Min(nx - 1, x + 1);

                    float dx = (density[xp, y, z] - density[xm, y, z]) * inv2;
                    float dy = (density[x, yp, z] - density[x, ym, z]) * inv2;
                    float dz = (density[x, y, zp] - density[x, y, zm]) * inv2;

                    // Density increases towards solid; surface normal should point outwards (towards air).
                    Vector3 g = new Vector3(dx, dy, dz);
                    Vector3 n = -g;
                    float len = n.magnitude;
                    if (len > 1e-6f) n /= len;
                    else n = Vector3.up;

                    normals[x, y, z] = n;
                }
            }
        }
    }

    private static float[,,] GetOrCreateThreadDensityBuffer(int paddedPoints)
    {
        if (threadDensityBuffer == null || threadDensityBuffer.GetLength(0) != paddedPoints || threadDensityBuffer.GetLength(1) != paddedPoints || threadDensityBuffer.GetLength(2) != paddedPoints)
        {
            threadDensityBuffer = new float[paddedPoints, paddedPoints, paddedPoints];
        }
        return threadDensityBuffer;
    }

    private static Vector3[,,] GetOrCreateThreadNormalBuffer(int paddedPoints)
    {
        if (threadNormalBuffer == null || threadNormalBuffer.GetLength(0) != paddedPoints || threadNormalBuffer.GetLength(1) != paddedPoints || threadNormalBuffer.GetLength(2) != paddedPoints)
        {
            threadNormalBuffer = new Vector3[paddedPoints, paddedPoints, paddedPoints];
        }
        return threadNormalBuffer;
    }

    private void UpdateSurfaceSamplingCaches()
    {
        // Must run on main thread (AnimationCurve.Evaluate is not guaranteed thread-safe).
        if (surfaceHeightSettings == null)
        {
            cachedSurfaceNoise = default;
            cachedSurfaceNoiseSettings = null;
            cachedSurfaceNoiseSeed = 0;
            cachedSurfaceCurveLut = null;
            cachedSurfaceCurveSource = null;
            cachedSurfaceCurveHash = 0;
            return;
        }

        int seed = EffectiveSeed;
        NoiseSettings ns = surfaceHeightSettings.noiseSettings;
        if (ns != cachedSurfaceNoiseSettings || seed != cachedSurfaceNoiseSeed)
        {
            cachedSurfaceNoiseSettings = ns;
            cachedSurfaceNoiseSeed = seed;
            cachedSurfaceNoise = ns != null ? new Noise2DSampler(ns, seed) : default;
        }

        AnimationCurve curve = surfaceHeightSettings.heightCurve;
        int h = CurveHash(curve);
        if (curve != cachedSurfaceCurveSource || h != cachedSurfaceCurveHash || cachedSurfaceCurveLut == null)
        {
            cachedSurfaceCurveSource = curve;
            cachedSurfaceCurveHash = h;
            cachedSurfaceCurveLut = BuildCurveLut(curve, 256);
        }
    }

    private static float[] BuildCurveLut(AnimationCurve curve, int size)
    {
        int n = Mathf.Clamp(size, 16, 1024);
        var lut = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (n <= 1) ? 0f : (float)i / (n - 1);
            lut[i] = curve != null ? curve.Evaluate(t) : t;
        }
        return lut;
    }

    private static int CurveHash(AnimationCurve curve)
    {
        if (curve == null) return 0;
        unchecked
        {
            int hash = 17;
            var keys = curve.keys;
            hash = hash * 31 + keys.Length;
            for (int i = 0; i < keys.Length; i++)
            {
                hash = hash * 31 + keys[i].time.GetHashCode();
                hash = hash * 31 + keys[i].value.GetHashCode();
                hash = hash * 31 + keys[i].inTangent.GetHashCode();
                hash = hash * 31 + keys[i].outTangent.GetHashCode();
            }
            return hash;
        }
    }
}
