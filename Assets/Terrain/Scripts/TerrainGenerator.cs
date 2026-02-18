using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    const float playerMoveThresholdToUpdate = 25f;
    const float sqrPlayerMoveThreshholdToUpdate = playerMoveThresholdToUpdate * playerMoveThresholdToUpdate;

    public int colliderLODIndex;
    public LODInfo[] detailLevels;

    public MeshSettings meshSettings;
    public HeightMapSettings heightMapSettings;
    public Transform player;
    public Material mapMaterial;

    public BiomeManager biomeManager; // Added for biome integration

    Vector2 playerPosition;
    Vector2 playerPositionOld;

    float meshWorldSize;
    int chunksVisibleInViewDistance;

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    List<TerrainChunk> visibleTerrainChunks = new List<TerrainChunk>();

    void Start()
    {
        if (biomeManager == null) {
            biomeManager = FindObjectOfType<BiomeManager>();
        }

        float maxViewDistance = detailLevels[detailLevels.Length - 1].visibleDistanceThreshhold;
        meshWorldSize = meshSettings.meshWorldSize;
        chunksVisibleInViewDistance = Mathf.RoundToInt(maxViewDistance / meshWorldSize);
        
        UpdateVisibleChunks();
    }

    void Update()
    {
        playerPosition = new Vector2(player.position.x, player.position.z);

        if (playerPosition != playerPositionOld)
        {
            foreach(TerrainChunk chunk in visibleTerrainChunks)
            {
                chunk.UpdateCollisionMesh();
            }
        }

        if((playerPositionOld - playerPosition).sqrMagnitude > sqrPlayerMoveThreshholdToUpdate)
        {   
            playerPositionOld = playerPosition;
            UpdateVisibleChunks();
        }
    }

    void UpdateVisibleChunks()
    {
        HashSet<Vector2> alreadyUpdatedChunkCoords = new HashSet<Vector2>();
        for (int i = visibleTerrainChunks.Count - 1; i >= 0; i--)
        {
            alreadyUpdatedChunkCoords.Add(visibleTerrainChunks[i].coord);
            visibleTerrainChunks[i].UpdateTerrainChunk();
        }

        int currentChunkCoordX = Mathf.RoundToInt(playerPosition.x / meshWorldSize);
        int currentChunkCoordY = Mathf.RoundToInt(playerPosition.y / meshWorldSize);

        for (int yOffset = -chunksVisibleInViewDistance; yOffset <= chunksVisibleInViewDistance; yOffset++) { 
            for (int xOffset = -chunksVisibleInViewDistance; xOffset <= chunksVisibleInViewDistance; xOffset++) {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);
                if (!alreadyUpdatedChunkCoords.Contains(viewedChunkCoord))
                {
                    if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
                    {
                        terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
                    }
                    else
                    {
                        BiomeSettings biome = biomeManager.GetBiomeForChunk(viewedChunkCoord, meshWorldSize);
                        if (biome != null)
                        {
                            TerrainChunk newChunk = new TerrainChunk(viewedChunkCoord, biome, meshSettings, detailLevels, colliderLODIndex, transform, player, mapMaterial);
                            terrainChunkDictionary.Add(viewedChunkCoord, newChunk);
                            newChunk.onVisibilityChange += OnTerrainChunkVisibilityChanged;
                            newChunk.Load();
                        }
                    }                    
                }
            }
        }
    }    

    void OnTerrainChunkVisibilityChanged(TerrainChunk chunk, bool isVisible)
    {
        if (isVisible)
        {
            visibleTerrainChunks.Add(chunk);
        }
        else
        {
            visibleTerrainChunks.Remove(chunk);
        }
    }
}

[System.Serializable]
public struct LODInfo 
{
    [Range(0, MeshSettings.numSupportedLODs - 1)] public int lod;
    public float visibleDistanceThreshhold;

    public float sqrVisibleDistanceThreshold {
        get {
            return visibleDistanceThreshhold * visibleDistanceThreshhold;
        }
    }
}