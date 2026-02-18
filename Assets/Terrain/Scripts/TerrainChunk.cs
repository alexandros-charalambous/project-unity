using System.Collections;
using System.Collections.Generic;
using UnityEditor.TerrainTools;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TerrainChunk
{
    const float colliderGenerationDistanceThreshhold = 5f;
    public event System.Action<TerrainChunk, bool> onVisibilityChange;
    public Vector2 coord;

    GameObject meshObject;
    Vector2 sampleCenter;
    Bounds bounds;

    MeshRenderer meshRenderer;
    MeshFilter meshFilter;
    MeshCollider meshCollider;

    LODInfo[] detailLevels;
    LODMesh[] lodMeshes;
    int colliderLODIndex;

    public HeightMap heightMap;
    bool heightMapReceived;
    int previousLODIndex = -1;
    bool hasSetCollider;
    float maxViewDistance;

    HeightMapSettings heightMapSettings;
    MeshSettings meshSettings;
    BiomeSettings biome; // Added for biome-specific settings
    Transform player;

    public TerrainChunk(Vector2 coord, BiomeSettings biome, MeshSettings meshSettings, LODInfo[] detailLevels, int colliderLODIndex, Transform parent, Transform player, Material material)
    {
        this.coord = coord;
        this.biome = biome;
        this.heightMapSettings = biome.heightMapSettings;
        this.meshSettings = meshSettings;
        this.detailLevels = detailLevels;
        this.colliderLODIndex = colliderLODIndex;
        this.player = player;

        sampleCenter = coord * meshSettings.meshWorldSize / meshSettings.meshScale;
        Vector2 position = coord * meshSettings.meshWorldSize;
        bounds = new Bounds(position, Vector2.one * meshSettings.meshWorldSize);
        
        meshObject = new GameObject("Terrain Chunk");
        meshRenderer = meshObject.AddComponent<MeshRenderer>();
        meshFilter = meshObject.AddComponent<MeshFilter>();
        meshCollider = meshObject.AddComponent<MeshCollider>();
        meshRenderer.material = material;

        meshObject.transform.position = new Vector3(position.x, 0, position.y);
        meshObject.transform.parent = parent;
        meshObject.layer = LayerMask.NameToLayer("Terrain");
        SetVisible(false);

        lodMeshes = new LODMesh[detailLevels.Length];
        for (int i = 0; i < detailLevels.Length; i++)
        {
            lodMeshes[i] = new LODMesh(this, detailLevels[i].lod);
            lodMeshes[i].updateCallback += UpdateTerrainChunk;
            if (i == colliderLODIndex)
            {
                lodMeshes[i].updateCallback += UpdateCollisionMesh;
            }
        }

        maxViewDistance = detailLevels[detailLevels.Length - 1].visibleDistanceThreshhold;     
    }

    public void Load()
    {
        ThreadedDataRequester.RequestData(() => HeightMapGenerator.GenerateHeightMap(meshSettings.numberVerticesPerLine, meshSettings.numberVerticesPerLine, heightMapSettings, sampleCenter), OnHeightMapReceived);
    }

    void OnHeightMapReceived(object heightMapObject)
    {
        this.heightMap = (HeightMap)heightMapObject;
        heightMapReceived = true;

        UpdateTerrainChunk();
    }
    
    public void SpawnObjects()
    {
        if (biome.treePrefabs == null || biome.treePrefabs.Length == 0) return;

        // Use a seed based on chunk coordinates and world seed for deterministic spawning
        int seed = (coord.GetHashCode() + heightMapSettings.noiseSettings.seed.GetHashCode());
        System.Random rng = new System.Random(seed);

        int numVerticesPerLine = meshSettings.numberVerticesPerLine;
        float meshWorldSize = meshSettings.meshWorldSize;

        for (int i = 0; i < (biome.treeDensity * 100); i++) // Simplified density
        {
            // Get a random point within the chunk
            int x = rng.Next(0, numVerticesPerLine);
            int y = rng.Next(0, numVerticesPerLine);
            
            float height = heightMap.values[x, y];

            // Convert vertex coordinates to a world position
            Vector2 percent = new Vector2((x - 1f) / (numVerticesPerLine - 3f), (y - 1f) / (numVerticesPerLine - 3f));
            Vector2 positionOnChunk = (new Vector2(percent.x, -percent.y) - new Vector2(0.5f, -0.5f)) * meshWorldSize;
            Vector3 worldPosition = new Vector3(positionOnChunk.x + coord.x * meshWorldSize, height, positionOnChunk.y + coord.y * meshWorldSize);

            // Simple check to avoid spawning trees underwater (assuming water is at y=0)
            if (worldPosition.y > 0)
            {
                GameObject treePrefab = biome.treePrefabs[rng.Next(0, biome.treePrefabs.Length)];
                GameObject.Instantiate(treePrefab, worldPosition, Quaternion.identity, meshObject.transform);
            }
        }
    }

    Vector2 playerPosition
    {
        get {
            return new Vector2(player.position.x, player.position.z);
        }
    }

    public void UpdateTerrainChunk()
    {
        if (heightMapReceived)
        {
            float playerDistanceFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(playerPosition));
            bool wasVisible = IsVisible();
            bool visible = playerDistanceFromNearestEdge <= maxViewDistance;
            
            if (visible)
            {
                int lodIndex = 0;
                for (int i = 0; i < detailLevels.Length - 1; i++)
                {
                    if (playerDistanceFromNearestEdge > detailLevels[i].visibleDistanceThreshhold)
                    {
                        lodIndex = i + 1;
                    }
                    else 
                    {
                        break;
                    }
                }

                if (lodIndex != previousLODIndex)
                {
                    LODMesh lodMesh = lodMeshes[lodIndex];
                    if(lodMesh.hasMesh)
                    {
                        previousLODIndex = lodIndex;
                        meshFilter.mesh = lodMesh.mesh;
                    }
                    else if (!lodMesh.hasRequestedMesh)
                    {
                        lodMesh.RequestMesh(heightMap, meshSettings);    
                    }
                }                    
            }
            if (wasVisible != visible)
            {
                SetVisible(visible);    
                if (onVisibilityChange != null)
                {
                    onVisibilityChange(this, visible);                       
                }
            }
        }
    }

    public void UpdateCollisionMesh()
    {
        if (!hasSetCollider)
        {
            float sqrDistanceFromViewerToEdge = bounds.SqrDistance(playerPosition);
            
            if (sqrDistanceFromViewerToEdge < detailLevels[colliderLODIndex].sqrVisibleDistanceThreshold)
            {
                if (!lodMeshes[colliderLODIndex].hasRequestedMesh) {
                    lodMeshes[colliderLODIndex].RequestMesh(heightMap, meshSettings);
                }
            }

            if (sqrDistanceFromViewerToEdge < colliderGenerationDistanceThreshhold * colliderGenerationDistanceThreshhold)
            {
                if (lodMeshes[colliderLODIndex].hasMesh)
                {                    
                    meshCollider.sharedMesh = lodMeshes[colliderLODIndex].mesh;
                    hasSetCollider = true;
                }
            }
        }            
    }

    public void SetVisible(bool visible)
    {
        meshObject.SetActive(visible);
    }

    public bool IsVisible()
    {
        return meshObject.activeSelf;
    }
}

class LODMesh 
{
    public Mesh mesh;
    public bool hasRequestedMesh;
    public bool hasMesh;
    int lod;
    TerrainChunk parent; // Reference to parent chunk

    public event System.Action updateCallback;

    public LODMesh(TerrainChunk parent, int lod)
    {
        this.parent = parent;
        this.lod = lod;
    }

    void OnMeshDataReceived(object meshDataObject)
    {
        mesh = ((MeshData)meshDataObject).CreateMesh();
        hasMesh = true;

        if (lod == 0) // Only spawn objects on highest LOD
        {
            parent.SpawnObjects();
        }

        updateCallback();
    }

    public void RequestMesh(HeightMap heightMap, MeshSettings meshSettings)
    {
        hasRequestedMesh = true;
        ThreadedDataRequester.RequestData(() => MeshGenerator.GenerateTerrainMesh(heightMap.values, meshSettings, lod), OnMeshDataReceived);
    } 
}