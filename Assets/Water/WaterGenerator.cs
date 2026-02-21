using System.Collections.Generic;
using UnityEngine;

public class WaterGenerator : MonoBehaviour
{
	const float playerMoveThresholdToUpdate = 25f;
	[Header("View")]
	[Min(0f)] public float maxViewDistance = 1500f;
	public Transform player;

	[Tooltip("How often the water system updates streaming in Play Mode.")]
	[Min(0.01f)] public float updateIntervalSeconds = 0.25f;

	[Tooltip("How many new water chunks may be created per update tick. Higher fills faster, but can cause spikes.")]
	[Min(1)] public int maxChunkCreatesPerTick = 8;

	[Tooltip("If enabled, uses a circular view distance instead of a square, reducing chunk count.")]
	public bool useCircularViewDistanceXZ = true;

	[Tooltip("If enabled, creates nearer chunks first.")]
	public bool prioritizeNearChunkCreation = true;

	[Header("Water Level")]
	[Tooltip("World-space Y of the water surface.")]
	public float waterLevel = 180f;

	[Tooltip("If enabled and a BiomeManager exists, uses BiomeManager.seaLevel as the water level.")]
	public bool useBiomeManagerSeaLevel = true;

	[Header("Rendering")]
	public Material material;
	public Mesh mesh;
	public MeshSettings meshSettings;

	[Header("Shader Tuning")]
	[Tooltip("If enabled, pushes saner default values into the water material (only if those properties exist).")]
	public bool applyMaterialTuningOnStart = true;
	[Min(0f)] public float tunedFoamAmount = 0.25f;
	[Min(0f)] public float tunedFoamSpeed = 0.15f;
	[Min(0f)] public float tunedFoamScale = 0.75f;
	[Min(0f)] public float tunedWaterDepth = 20f;
	[Min(0f)] public float tunedWaveSpeed = 0.02f;

	private float chunkWorldSize;
	private int chunksVisibleInViewDst;
	private float updateTimer;
	private Vector2Int lastPlayerChunk;

	private readonly Dictionary<Vector2Int, WaterChunk> waterChunkDictionary = new Dictionary<Vector2Int, WaterChunk>();
	private readonly HashSet<Vector2Int> neededCoords = new HashSet<Vector2Int>();
	private readonly List<Vector2Int> removeCoords = new List<Vector2Int>();
	private readonly Queue<Vector2Int> createQueue = new Queue<Vector2Int>();
	private readonly HashSet<Vector2Int> queuedCoords = new HashSet<Vector2Int>();
	private readonly List<Vector2Int> missingCoordsBuffer = new List<Vector2Int>(1024);

	private Transform waterChunksParent;
	private BiomeManager biomeManager;

	private static readonly int foamAmountId = Shader.PropertyToID("_FoamAmount");
	private static readonly int foamSpeedId = Shader.PropertyToID("_FoamSpeed");
	private static readonly int foamScaleId = Shader.PropertyToID("_FoamScale");
	private static readonly int waterDepthId = Shader.PropertyToID("_WaterDepth");
	private static readonly int waveSpeedId = Shader.PropertyToID("_WaveSpeed");

	private void Start()
	{
		if (meshSettings == null)
		{
			var volumetricGen = FindAnyObjectByType<VolumetricTerrainGenerator>();
			if (volumetricGen != null) meshSettings = volumetricGen.meshSettings;
		}

		biomeManager = FindAnyObjectByType<BiomeManager>();
		if (useBiomeManagerSeaLevel && biomeManager != null)
		{
			waterLevel = biomeManager.seaLevel;
		}

		chunkWorldSize = (meshSettings != null) ? meshSettings.chunkWorldSize : 180f;
		chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDistance / chunkWorldSize);
		EnsureChunkParent();

		if (applyMaterialTuningOnStart)
		{
			ApplyMaterialTuning();
		}

		lastPlayerChunk = GetPlayerChunkCoord();
		UpdateVisibleChunks(force: true);
	}

	private void Update()
	{
		if (player == null) return;

		updateTimer += Time.unscaledDeltaTime;
		if (updateTimer < updateIntervalSeconds) return;
		updateTimer = 0f;

		Vector2Int playerChunk = GetPlayerChunkCoord();
		bool movedChunk = playerChunk != lastPlayerChunk;
		lastPlayerChunk = playerChunk;

		UpdateVisibleChunks(force: movedChunk);
	}

	private Vector2Int GetPlayerChunkCoord()
	{
		if (player == null || chunkWorldSize <= 0f) return Vector2Int.zero;
		Vector3 p = player.position;
		// Chunk positions are centered at (coord * chunkWorldSize). Rounding picks the nearest chunk center,
		// matching the previous water implementation and avoiding half-chunk offsets.
		int cx = Mathf.RoundToInt(p.x / chunkWorldSize);
		int cz = Mathf.RoundToInt(p.z / chunkWorldSize);
		return new Vector2Int(cx, cz);
	}

	private void UpdateVisibleChunks(bool force)
	{
		EnsureChunkParent();
		chunkWorldSize = (meshSettings != null) ? meshSettings.chunkWorldSize : chunkWorldSize;
		if (chunkWorldSize <= 0f) return;

		chunksVisibleInViewDst = Mathf.Max(0, Mathf.RoundToInt(maxViewDistance / chunkWorldSize));
		Vector2Int playerChunk = GetPlayerChunkCoord();

		neededCoords.Clear();
		missingCoordsBuffer.Clear();

		int r = chunksVisibleInViewDst;
		int rSqr = r * r;

		for (int zOffset = -r; zOffset <= r; zOffset++)
		{
			for (int xOffset = -r; xOffset <= r; xOffset++)
			{
				if (useCircularViewDistanceXZ)
				{
					int d = xOffset * xOffset + zOffset * zOffset;
					if (d > rSqr) continue;
				}

				Vector2Int coord = new Vector2Int(playerChunk.x + xOffset, playerChunk.y + zOffset);
				neededCoords.Add(coord);
				if (!waterChunkDictionary.ContainsKey(coord) && queuedCoords.Add(coord))
				{
					missingCoordsBuffer.Add(coord);
				}
			}
		}

		if (missingCoordsBuffer.Count > 0)
		{
			if (prioritizeNearChunkCreation)
			{
				missingCoordsBuffer.Sort((a, b) =>
				{
					int adx = a.x - playerChunk.x;
					int adz = a.y - playerChunk.y;
					int bdx = b.x - playerChunk.x;
					int bdz = b.y - playerChunk.y;
					int aDist = adx * adx + adz * adz;
					int bDist = bdx * bdx + bdz * bdz;
					return aDist.CompareTo(bDist);
				});
			}

			for (int i = 0; i < missingCoordsBuffer.Count; i++)
			{
				createQueue.Enqueue(missingCoordsBuffer[i]);
			}
		}

		// Update existing chunk visibility (cheap) so we can hide chunks at the boundary.
		foreach (var kvp in waterChunkDictionary)
		{
			kvp.Value.UpdateWaterChunk();
		}

		// Create a limited number of chunks this tick.
		int createBudget = Mathf.Max(1, maxChunkCreatesPerTick);
		while (createBudget-- > 0 && createQueue.Count > 0)
		{
			Vector2Int c = createQueue.Dequeue();
			queuedCoords.Remove(c);
			if (!neededCoords.Contains(c)) continue;
			if (waterChunkDictionary.ContainsKey(c)) continue;

			var newChunk = new WaterChunk(c, chunkWorldSize, waterLevel, waterChunksParent, player, material, mesh, maxViewDistance);
			waterChunkDictionary.Add(c, newChunk);
			newChunk.SetVisible(true);
		}

		// Remove chunks that are no longer needed to prevent unbounded growth.
		if (force || waterChunkDictionary.Count > neededCoords.Count)
		{
			removeCoords.Clear();
			foreach (var kvp in waterChunkDictionary)
			{
				if (!neededCoords.Contains(kvp.Key))
				{
					kvp.Value.Destroy();
					removeCoords.Add(kvp.Key);
				}
			}
			for (int i = 0; i < removeCoords.Count; i++)
			{
				waterChunkDictionary.Remove(removeCoords[i]);
				queuedCoords.Remove(removeCoords[i]);
			}
		}
	}

	private void EnsureChunkParent()
	{
		if (waterChunksParent != null) return;

		Transform existing = transform.Find("Water Chunks");
		waterChunksParent = existing != null ? existing : new GameObject("Water Chunks").transform;
		waterChunksParent.SetParent(transform, false);
	}

	private void ApplyMaterialTuning()
	{
		if (material == null) return;
		if (material.HasProperty(foamAmountId)) material.SetFloat(foamAmountId, tunedFoamAmount);
		if (material.HasProperty(foamSpeedId)) material.SetFloat(foamSpeedId, tunedFoamSpeed);
		if (material.HasProperty(foamScaleId)) material.SetFloat(foamScaleId, tunedFoamScale);
		if (material.HasProperty(waterDepthId)) material.SetFloat(waterDepthId, tunedWaterDepth);
		if (material.HasProperty(waveSpeedId)) material.SetFloat(waveSpeedId, tunedWaveSpeed);
	}
}