using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterGenerator : MonoBehaviour
{
    const float playerMoveThresholdToUpdate = 25f;
    const float sqrPlayerMoveThreshholdToUpdate = playerMoveThresholdToUpdate * playerMoveThresholdToUpdate;

    public LODInfo[] detailLevels;

    public MeshSettings meshSettings;
    public Transform player;
    public Material waterMaterial;

    Vector2 playerPosition;
    Vector2 playerPositionOld;

    float meshWorldSize;
    int chunksVisibleInViewDistance;

    Dictionary<Vector2, WaterChunk> waterChunkDictionary = new Dictionary<Vector2, WaterChunk>();
    List<WaterChunk> visibleWaterChunks = new List<WaterChunk>();

    void Start()
    {        
        float maxViewDistance = detailLevels[detailLevels.Length - 1].visibleDistanceThreshhold;
        meshWorldSize = meshSettings.meshWorldSize;
        chunksVisibleInViewDistance = Mathf.RoundToInt(maxViewDistance / meshWorldSize);
        
        UpdateVisibleChunks();
    }

    void Update()
    {
        playerPosition = new Vector2(player.position.x, player.position.z);

        if((playerPositionOld - playerPosition).sqrMagnitude > sqrPlayerMoveThreshholdToUpdate)
        {   
            playerPositionOld = playerPosition;
            UpdateVisibleChunks();
        }
    }

    void UpdateVisibleChunks()
    {
        HashSet<Vector2> alreadyUpdatedChunkCoords = new HashSet<Vector2>();
        for (int i = visibleWaterChunks.Count - 1; i >= 0; i--)
        {
            alreadyUpdatedChunkCoords.Add(visibleWaterChunks[i].coord);
            visibleWaterChunks[i].UpdateWaterChunk();
        }

        int currentChunkCoordX = Mathf.RoundToInt(playerPosition.x / meshWorldSize);
        int currentChunkCoordY = Mathf.RoundToInt(playerPosition.y / meshWorldSize);

        for (int yOffset = -chunksVisibleInViewDistance; yOffset <= chunksVisibleInViewDistance; yOffset++) { 
            for (int xOffset = -chunksVisibleInViewDistance; xOffset <= chunksVisibleInViewDistance; xOffset++) {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);
                if (!alreadyUpdatedChunkCoords.Contains(viewedChunkCoord))
                {
                    if (waterChunkDictionary.ContainsKey(viewedChunkCoord))
                    {
                        waterChunkDictionary[viewedChunkCoord].UpdateWaterChunk();
                    }
                    else
                    {
                        WaterChunk newChunk = new WaterChunk(viewedChunkCoord, meshSettings, detailLevels, transform, player, waterMaterial);
                        waterChunkDictionary.Add(viewedChunkCoord, newChunk);
                        newChunk.onVisibilityChange += OnWaterChunkVisibilityChanged;
                    }                    
                }
            }
        }
    }    

    void OnWaterChunkVisibilityChanged(WaterChunk chunk, bool isVisible)
    {
        if (isVisible)
        {
            visibleWaterChunks.Add(chunk);
        }
        else
        {
            visibleWaterChunks.Remove(chunk);
        }
    }
}