using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterGenerator : MonoBehaviour
{
    const float playerMoveThresholdToUpdate = 25f;
    const float sqrPlayerMoveThreshholdToUpdate = playerMoveThresholdToUpdate * playerMoveThresholdToUpdate;
	
	public const float maxViewDistance = 7200f;
	public Transform player;

	public static Vector2 playerPosition;
    Vector2 playerPositionOld;

	public Material material;
	public Mesh mesh;
	int chunkSize;
	int chunksVisibleInViewDst;

	Dictionary<Vector2, WaterChunk> waterChunkDictionary = new Dictionary<Vector2, WaterChunk>();
	List<WaterChunk> visibleWaterChunks = new List<WaterChunk>();

	void Start()
	{
		chunkSize = 180;
		chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDistance / chunkSize);
        UpdateVisibleChunks();
	}

	void Update()
	{
        playerPosition = new Vector2(player.position.x, player.position.z);
		
        // if (playerPosition != playerPositionOld)
        // {
        //     foreach(WaterChunk chunk in visibleWaterChunks)
        //     {
        //         chunk.UpdateCollisionMesh();
        //     }
        // }

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

		int currentChunkCoordX = Mathf.RoundToInt(playerPosition.x / chunkSize);
		int currentChunkCoordY = Mathf.RoundToInt(playerPosition.y / chunkSize);

		for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
		{
			for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
			{
				Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);
                if (!alreadyUpdatedChunkCoords.Contains(viewedChunkCoord))
                {
					if (waterChunkDictionary.ContainsKey(viewedChunkCoord))
					{
						waterChunkDictionary[viewedChunkCoord].UpdateWaterChunk();
					}
					else
					{
						WaterChunk newChunk = new WaterChunk(viewedChunkCoord, chunkSize, transform, player, material, mesh);
						waterChunkDictionary.Add(viewedChunkCoord, newChunk);
						newChunk.onVisibilityChange += OnWaterChunkVisibilityChanged;
						newChunk.Load();
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