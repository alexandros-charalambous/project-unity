using UnityEngine;

public class VolumetricTerrainChunk
{
    private static bool warnedMissingBiomeChannels;
    private static bool warnedMissingTerrainLayer;
    private static Shader biomeBlendArrayShader;
    private static Material sharedBiomeBlendArrayMaterial;
    private static readonly int biomeTexArrayId = Shader.PropertyToID("_BiomeTexArray");
    private static readonly int biomeCountId = Shader.PropertyToID("_BiomeCount");
    private static readonly int underwaterIndexId = Shader.PropertyToID("_UnderwaterIndex");
    private static readonly int mountainIndexId = Shader.PropertyToID("_MountainIndex");
    private static readonly int worldUvScaleId = Shader.PropertyToID("_WorldUVScale");
    private static readonly int triplanarSharpnessId = Shader.PropertyToID("_TriplanarSharpness");

    public readonly Vector3Int coord;
    public GameObject gameObject { get; private set; }
    public readonly BiomeBlendData biomeBlendData;
    public readonly BiomePalette biomePalette;

    public event System.Action<VolumetricTerrainChunk> onFirstMeshAssigned;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;

    private readonly VolumetricTerrainGenerator owner;
    private readonly Transform parent;
    private readonly Vector3 chunkOriginWorld;

    private MaterialPropertyBlock materialPropertyBlock;

    private bool hasMesh;

    public VolumetricTerrainChunk(Vector3Int coord, BiomeBlendData biomeBlendData, BiomePalette biomePalette, VolumetricTerrainGenerator owner, Transform parent)
    {
        this.coord = coord;
        this.biomeBlendData = biomeBlendData;
        this.biomePalette = biomePalette;
        this.owner = owner;
        this.parent = parent;
        chunkOriginWorld = owner != null ? owner.ChunkOriginWorld(coord) : Vector3.zero;
    }

    private void EnsureGameObjectCreated()
    {
        if (gameObject != null) return;
        if (owner == null) return;

        gameObject = new GameObject($"Volumetric Chunk ({coord.x},{coord.y},{coord.z})");
        if (!Application.isPlaying)
        {
            // Preview objects should not be saved into the scene.
            gameObject.hideFlags = HideFlags.DontSaveInEditor;
        }
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshCollider = gameObject.AddComponent<MeshCollider>();
        meshCollider.convex = false;

        // Only enable rendering once we have a non-empty mesh.
        meshRenderer.enabled = false;

        // Put all terrain chunks on the Terrain layer (if present) so minimap raycasts and
        // other layer-masked systems can include/exclude terrain cleanly.
        int terrainLayer = LayerMask.NameToLayer("Terrain");
        if (terrainLayer >= 0)
        {
            gameObject.layer = terrainLayer;
        }
        else if (!warnedMissingTerrainLayer)
        {
            warnedMissingTerrainLayer = true;
            Debug.LogWarning("VolumetricTerrainChunk: Unity layer 'Terrain' does not exist. Chunks will remain on Default. Add it in Project Settings > Tags and Layers.");
        }

        AssignMaterial();

        gameObject.transform.SetParent(parent, false);
        gameObject.transform.position = chunkOriginWorld;
        gameObject.SetActive(true);
    }

    private void AssignMaterial()
    {
        if (meshRenderer == null) return;

        if (biomeBlendArrayShader == null)
        {
            biomeBlendArrayShader = Shader.Find("Custom/BiomeBlendArrayShader");
        }

        if (biomeBlendArrayShader == null)
        {
            // Fallback if shader isn't present.
            if (owner.material != null) meshRenderer.sharedMaterial = owner.material;
            else meshRenderer.sharedMaterial = biomeBlendData.primaryBiome != null ? biomeBlendData.primaryBiome.material : null;
            return;
        }

        if (sharedBiomeBlendArrayMaterial == null)
        {
            sharedBiomeBlendArrayMaterial = new Material(biomeBlendArrayShader);
        }

        meshRenderer.sharedMaterial = sharedBiomeBlendArrayMaterial;

        if (materialPropertyBlock == null)
        {
            materialPropertyBlock = new MaterialPropertyBlock();
        }

        BiomeManager bm = owner != null ? owner.biomeManager : null;
        var texArray = bm != null ? bm.BiomeTextureArray : null;
        int biomeCount = texArray != null ? texArray.depth : 1;
        int underwaterIndex = bm != null && bm.underwaterBiome != null ? bm.GetBiomeIndex(bm.underwaterBiome) : -1;
        int mountainIndex = bm != null && bm.mountainBiome != null ? bm.GetBiomeIndex(bm.mountainBiome) : -1;

        meshRenderer.GetPropertyBlock(materialPropertyBlock);
        if (texArray != null)
        {
            materialPropertyBlock.SetTexture(biomeTexArrayId, texArray);
        }
        materialPropertyBlock.SetInt(biomeCountId, biomeCount);
        materialPropertyBlock.SetInt(underwaterIndexId, underwaterIndex);
        materialPropertyBlock.SetInt(mountainIndexId, mountainIndex);

        if (owner != null)
        {
            materialPropertyBlock.SetFloat(worldUvScaleId, Mathf.Max(0.000001f, owner.worldUvScale));
            materialPropertyBlock.SetFloat(triplanarSharpnessId, Mathf.Max(0.1f, owner.triplanarSharpness));
        }
        meshRenderer.SetPropertyBlock(materialPropertyBlock);
    }

    private static Texture GetOrFallbackTexture(BiomeSettings biome)
    {
        var tex = TryGetBiomeTexture(biome);
        return tex != null ? tex : Texture2D.whiteTexture;
    }

    private static Texture GetOrFallbackTexture(BiomeSettings biome, Texture fallback)
    {
        var tex = TryGetBiomeTexture(biome);
        if (tex != null) return tex;
        return fallback != null ? fallback : Texture2D.whiteTexture;
    }

    private static Texture TryGetBiomeTexture(BiomeSettings biome)
    {
        if (biome == null || biome.material == null) return null;
        return biome.material.mainTexture ?? biome.material.GetTexture("_BaseMap");
    }

    public void RequestMesh()
    {
        if (hasMesh) return;
        BiomeBlendData blendCopy = biomeBlendData;

        // In edit mode we generate synchronously so the preview updates immediately
        // (ThreadedDataRequester runs only during play in most setups).
        if (!Application.isPlaying)
        {
            object data = null;
            try
            {
                data = owner.GenerateChunkMeshData(coord, blendCopy);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
            OnMeshDataReceived(data);
            return;
        }

        ThreadedDataRequester.RequestData(() => owner.GenerateChunkMeshData(coord, blendCopy), OnMeshDataReceived);
    }

    private void OnMeshDataReceived(object obj)
    {
        if (!(obj is VolumetricMeshData data)) return;

        // If the surface does not intersect this chunk (fully solid or fully empty),
        // marching-cubes-style meshing yields 0 triangles. In that case we skip creating
        // a Mesh and disable rendering/colliders to save CPU/GPU and physics overhead.
        if (data.triangles == null || data.triangles.Length < 3 || data.vertices == null || data.vertices.Length == 0)
        {
            // If we previously had a mesh but now meshing returns empty, we must clear visuals/collider.
            if (meshFilter != null)
            {
                if (!Application.isPlaying && meshFilter.sharedMesh != null)
                {
                    Object.DestroyImmediate(meshFilter.sharedMesh);
                }
                meshFilter.sharedMesh = null;
            }

            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null;
            }

            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }

            bool firstEmpty = !hasMesh;
            hasMesh = true;
            if (firstEmpty && onFirstMeshAssigned != null)
            {
                onFirstMeshAssigned(this);
            }
            return;
        }

        EnsureGameObjectCreated();
        if (meshFilter == null) return;

        // Avoid leaking meshes in Edit Mode when we regenerate the preview.
        if (!Application.isPlaying && meshFilter.sharedMesh != null)
        {
            Object.DestroyImmediate(meshFilter.sharedMesh);
            meshFilter.sharedMesh = null;
        }

        Mesh mesh = data.CreateMesh();

        // Defensive: if the worker didn't provide biome channels for some reason, the shader
        // will default to biome index 0 everywhere. Compute them on the main thread as a fallback.
        bool missingUv2 = mesh.uv2 == null || mesh.uv2.Length != mesh.vertexCount;
        bool missingColors = mesh.colors == null || mesh.colors.Length != mesh.vertexCount;
        if ((missingUv2 || missingColors) && owner != null && owner.biomeManager != null)
        {
            if (!warnedMissingBiomeChannels)
            {
                warnedMissingBiomeChannels = true;
                Debug.LogWarning("VolumetricTerrainChunk: Mesh arrived without uv2/colors for biome blending. Falling back to main-thread biome evaluation (may stall).", gameObject);
            }
            ApplyBiomeVertexColors(mesh);
        }
        meshFilter.sharedMesh = mesh;

        if (meshRenderer != null && !meshRenderer.enabled) meshRenderer.enabled = true;

        bool first = !hasMesh;
        hasMesh = true;

        if (first && onFirstMeshAssigned != null)
        {
            onFirstMeshAssigned(this);
        }

        if (meshCollider != null)
        {
            // Force collider refresh when a new mesh is assigned.
            meshCollider.sharedMesh = null;
        }
        UpdateCollider();
    }

    public void ForceRebuildMeshAndMaterial()
    {
        hasMesh = false;

        if (meshRenderer != null)
        {
            // Hide until the new mesh arrives.
            meshRenderer.enabled = false;
        }

        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null;
        }

        AssignMaterial();
        RequestMesh();
    }

    private void ApplyBiomeVertexColors(Mesh mesh)
    {
        if (mesh == null) return;
        var vertices = mesh.vertices;
        if (vertices == null || vertices.Length == 0) return;

        var normals = mesh.normals;
        bool hasNormals = normals != null && normals.Length == vertices.Length;

        var colors = new Color[vertices.Length];

        BiomeManager bm = owner != null ? owner.biomeManager : null;
        if (bm == null)
        {
            Color c = new Color(1f, 0f, 0f, 0f);
            for (int i = 0; i < colors.Length; i++) colors[i] = c;
            mesh.colors = colors;
            return;
        }

        // Biome indices are stored in uv2 (TEXCOORD1). We round in the shader.
        var uv2 = new Vector2[vertices.Length];
        Vector3 chunkWorld = chunkOriginWorld;

        bool hasUnderwater = bm.underwaterBiome != null;
        bool hasMountain = bm.mountainBiome != null;

        // Island underside rock override: use mountainBiome on downward-facing surfaces within the island band.
        VolumetricTerrainSettings s = owner != null ? owner.settings : null;
        bool islandRockOverride = s != null && s.enableFloatingIslands && s.islandsUndersideRoughness > 0f;
        float islandMinY = 0f;
        float islandMaxY = 0f;
        float islandBlend = 0f;
        float undersideRockAmount = 0f;
        if (islandRockOverride)
        {
            islandMinY = Mathf.Min(s.islandsMinY, s.islandsMaxY);
            islandMaxY = Mathf.Max(s.islandsMinY, s.islandsMaxY);
            islandBlend = Mathf.Max(0.0001f, s.islandsBandBlend);
            undersideRockAmount = Mathf.Clamp01(s.islandsUndersideRoughness);
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertexWorldPos = vertices[i] + chunkWorld;
            // Per-vertex global Voronoi evaluation (seam-safe).
            var blend = bm.GetBiomeBlendDataFromWorldPos(new Vector2(vertexWorldPos.x, vertexWorldPos.z));
            int primaryIdx = bm.GetBiomeIndex(blend.primaryBiome);
            int secondaryIdx = bm.GetBiomeIndex(blend.secondaryBiome != null ? blend.secondaryBiome : blend.primaryBiome);
            uv2[i] = new Vector2(primaryIdx, secondaryIdx);

            float secondaryW = bm.TuneSecondaryBlend(blend.blendFactor);
            float underwaterA = hasUnderwater ? bm.TuneUnderwaterAlpha(bm.GetUnderwaterAlpha(vertexWorldPos.y)) : 0f;
            float mountainA = hasMountain ? bm.TuneMountainAlpha(bm.GetMountainAlpha(vertexWorldPos.y)) : 0f;

            // Slope-based rock override (undersides only). This is what makes island bottoms read as rock.
            if (islandRockOverride && hasMountain && hasNormals)
            {
                // Band mask around the islands vertical range.
                float up = Mathf.Clamp01((vertexWorldPos.y - (islandMinY - islandBlend)) / islandBlend);
                float down = Mathf.Clamp01(((islandMaxY + islandBlend) - vertexWorldPos.y) / islandBlend);
                float band = Mathf.SmoothStep(0f, 1f, Mathf.Min(up, down));

                // Down-facing factor (0 on upward surfaces, 1 on strong undersides).
                Vector3 wn = normals[i];
                if (gameObject != null) wn = gameObject.transform.TransformDirection(wn);
                float downFacing = Mathf.Clamp01((-wn.y - 0.05f) / 0.70f);

                float rock = band * downFacing * undersideRockAmount;
                if (rock > 0f)
                {
                    mountainA = Mathf.Max(mountainA, bm.TuneMountainAlpha(rock));
                }
            }

            // Color channels now mean:
            // r = secondary biome blend factor
            // g = underwater alpha
            // b = mountain alpha
            colors[i] = new Color(secondaryW, underwaterA, mountainA, 1f);
        }

        mesh.colors = colors;
        mesh.uv2 = uv2;
    }

    private int FindPaletteIndex(BiomeSettings target)
    {
        if (target == null) return -1;
        int count = Mathf.Clamp(biomePalette.count, 0, 4);
        for (int i = 0; i < count; i++)
        {
            if (biomePalette.GetBiome(i) == target) return i;
        }
        return -1;
    }

    private static void SetChannelWeight(int channel, float w, ref float wR, ref float wG, ref float wB, ref float wA)
    {
        if (w <= 0f) return;
        switch (channel)
        {
            case 0: wR = w; break;
            case 1: wG = w; break;
            case 2: wB = w; break;
            case 3: wA = w; break;
        }
    }

    private static void ApplyChannelOverride(ref float wR, ref float wG, ref float wB, ref float wA, int channel, float alpha)
    {
        if (channel < 0 || channel > 3) return;
        alpha = Mathf.Clamp01(alpha);
        if (alpha <= 0f) return;

        float otherSum;
        switch (channel)
        {
            case 0: otherSum = wG + wB + wA; break;
            case 1: otherSum = wR + wB + wA; break;
            case 2: otherSum = wR + wG + wA; break;
            case 3: otherSum = wR + wG + wB; break;
            default: return;
        }

        float keep = 1f - alpha;
        if (otherSum <= 1e-6f)
        {
            wR = channel == 0 ? 1f : 0f;
            wG = channel == 1 ? 1f : 0f;
            wB = channel == 2 ? 1f : 0f;
            wA = channel == 3 ? 1f : 0f;
            return;
        }

        if (channel != 0) wR = wR / otherSum * keep;
        if (channel != 1) wG = wG / otherSum * keep;
        if (channel != 2) wB = wB / otherSum * keep;
        if (channel != 3) wA = wA / otherSum * keep;

        if (channel == 0) wR = alpha;
        else if (channel == 1) wG = alpha;
        else if (channel == 2) wB = alpha;
        else wA = alpha;
    }

    public void UpdateCollider()
    {
        if (!hasMesh) return;
        if (!owner.GenerateColliders) return;
        if (meshCollider == null || meshFilter == null) return;
        if (meshFilter.sharedMesh == null) return;

        // Use horizontal distance for collider gating so vertical chunk stacks don't prevent colliders near the player.
        float sqrDist = owner.SqrDistanceToPlayerXZ(chunkOriginWorld);
        float max = owner.colliderDistance;
        if (sqrDist <= max * max)
        {
            if (meshCollider.sharedMesh == null)
            {
                // Unity sometimes requires a null-reset to reliably update MeshCollider.
                meshCollider.sharedMesh = null;
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                if (owner.logColliderAssignments)
                {
                    Debug.Log($"Assigned volumetric collider for chunk {coord}.");
                }
            }
        }
        else
        {
            if (meshCollider.sharedMesh != null)
            {
                if (owner.ShouldClearCollidersWhenFar())
                {
                    meshCollider.sharedMesh = null;
                }
            }
        }
    }

    public void Destroy()
    {
        if (gameObject != null)
        {
            if (!Application.isPlaying)
            {
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    Object.DestroyImmediate(meshFilter.sharedMesh);
                    meshFilter.sharedMesh = null;
                }
                Object.DestroyImmediate(gameObject);
            }
            else
            {
                Object.Destroy(gameObject);
            }
            gameObject = null;
        }
    }
}
