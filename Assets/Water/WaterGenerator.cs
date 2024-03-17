using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterGenerator : MonoBehaviour
{
	public const float maxViewDistance = 7200f;
	public Transform player;

	public static Vector2 playerPosition;
	public Material material;
	int chunkSize;
	int chunksVisibleInViewDst;

	Dictionary<Vector2, WaterChunk> waterChunkDictionary = new Dictionary<Vector2, WaterChunk>();
	List<WaterChunk> waterChunksVisibleLastUpdate = new List<WaterChunk>();

	void Start()
	{
		chunkSize = 720;
		chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDistance / chunkSize);
	}

	void Update()
	{
		playerPosition = new Vector2(player.position.x, player.position.z);
		UpdateVisibleChunks();
	}

	void UpdateVisibleChunks()
	{

		for (int i = 0; i < waterChunksVisibleLastUpdate.Count; i++)
		{
			waterChunksVisibleLastUpdate[i].SetVisible(false);
		}
		waterChunksVisibleLastUpdate.Clear();

		int currentChunkCoordX = Mathf.RoundToInt(playerPosition.x / chunkSize);
		int currentChunkCoordY = Mathf.RoundToInt(playerPosition.y / chunkSize);

		for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
		{
			for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
			{
				Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

				if (waterChunkDictionary.ContainsKey(viewedChunkCoord))
				{
					waterChunkDictionary[viewedChunkCoord].UpdateWaterChunk();
					if (waterChunkDictionary[viewedChunkCoord].IsVisible())
					{
						waterChunksVisibleLastUpdate.Add(waterChunkDictionary[viewedChunkCoord]);
					}
				}
				else
				{
					WaterChunk newChunk = new WaterChunk(viewedChunkCoord, chunkSize, transform, material);
					waterChunkDictionary.Add(viewedChunkCoord, newChunk);
					newChunk.Load();
				}

			}
		}
	}

	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	public class WaterChunk
	{
		GameObject meshObject;
		MeshCollider meshCollider;
		MeshRenderer meshRenderer;
		Vector2 position;
		Bounds bounds;

		bool planeReceived;
		
    	public event System.Action<WaterChunk, bool> onVisibilityChange;

		public WaterChunk(Vector2 coord, int size, Transform parent, Material material)
		{
			position = coord * size;
			bounds = new Bounds(position, Vector2.one * size);
			Vector3 positionV3 = new Vector3(position.x, 200, position.y);

			meshObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
			meshObject.name = "Water Chunk";
			
			meshCollider = meshObject.GetComponent<MeshCollider>();
			meshRenderer = meshObject.GetComponent<MeshRenderer>();

			meshRenderer.material = material;

			meshCollider.convex = true;
			meshCollider.isTrigger = true;
			meshCollider.sharedMesh = null;

			meshObject.transform.position = positionV3;
			meshObject.transform.localScale = Vector3.one * size / 10f;
			meshObject.transform.parent = parent;
			meshObject.layer = LayerMask.NameToLayer("Water");

			SetVisible(false);
		}

		public void Load()
		{
			ThreadedDataRequester.RequestData(() => meshObject, OnPlaneReceived);
		}

		void OnPlaneReceived(object planeObject)
		{
			this.meshObject = (GameObject)planeObject;
			planeReceived = true;

			UpdateWaterChunk();
		}

		public void UpdateWaterChunk()
		{
			if (planeReceived)
			{
				float playerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(playerPosition));
				bool wasVisible = IsVisible();
				bool visible = playerDstFromNearestEdge <= maxViewDistance;			
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

		public void SetVisible(bool visible)
		{
			meshObject.SetActive(visible);
		}

		public bool IsVisible()
		{
			return meshObject.activeSelf;
		}
	}
}