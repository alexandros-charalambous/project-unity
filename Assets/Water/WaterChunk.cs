using UnityEngine;

public class WaterChunk
{
    public readonly Vector2Int coord;

    private static bool warnedMissingWaterLayer;

    private const float kMinMeshSize = 1e-5f;

    private static Mesh cachedPlaneMesh;

    private readonly Transform player;
    private readonly float maxViewDistance;
    private readonly Bounds bounds;

    private readonly GameObject meshObject;
    private readonly MeshFilter meshFilter;
    private readonly MeshRenderer meshRenderer;
    private readonly BoxCollider boxCollider;

    public WaterChunk(Vector2Int coord, float size, float waterLevel, Transform parent, Transform player, Material material, Mesh renderMesh, float maxViewDistance)
    {
        this.coord = coord;
        this.player = player;
        this.maxViewDistance = Mathf.Max(0f, maxViewDistance);

        // Chunks are centered on a grid: center = coord * size.
        Vector3 chunkCenter = new Vector3(coord.x * size, waterLevel, coord.y * size);
        bounds = new Bounds(chunkCenter, new Vector3(size, 1f, size));

        meshObject = new GameObject($"Water Chunk ({coord.x},{coord.y})");
        meshFilter = meshObject.AddComponent<MeshFilter>();
        meshRenderer = meshObject.AddComponent<MeshRenderer>();

        meshRenderer.sharedMaterial = material;

        // Prefer a provided mesh (usually higher resolution for wave displacement).
        // If none is provided, fall back to Unity's built-in plane mesh.
        Mesh m = renderMesh != null ? renderMesh : GetOrCreatePlaneMesh();
        meshFilter.sharedMesh = m;

        // Add a lightweight trigger collider so map raycasts can detect water.
        // (The water surface does not need physics collisions, only ray hits.)
        boxCollider = meshObject.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true;

        // Scale the mesh so that it covers exactly (size x size) in world units.
        // This prevents gaps when chunk positions step by 'size'.
        Vector3 meshSize = (m != null) ? m.bounds.size : Vector3.one * 10f;
        float mx = Mathf.Max(kMinMeshSize, meshSize.x);
        float mz = Mathf.Max(kMinMeshSize, meshSize.z);
        meshObject.transform.localScale = new Vector3(size / mx, 1f, size / mz);

        // Match collider to the mesh bounds (in local space); scaling makes it cover (size x size) in world.
        boxCollider.center = (m != null) ? m.bounds.center : Vector3.zero;
        boxCollider.size = new Vector3(mx, 0.5f, mz);

        meshObject.transform.position = chunkCenter;
        meshObject.transform.SetParent(parent, false);
        int waterLayer = LayerMask.NameToLayer("Water");
        if (waterLayer >= 0)
        {
            meshObject.layer = waterLayer;
        }
        else if (!warnedMissingWaterLayer)
        {
            warnedMissingWaterLayer = true;
            Debug.LogWarning("WaterChunk: Unity layer 'Water' does not exist. Water chunks will remain on Default. Add it in Project Settings > Tags and Layers.");
        }

        SetVisible(false);
        UpdateWaterChunk();
    }

    private static Mesh GetOrCreatePlaneMesh()
    {
        if (cachedPlaneMesh != null) return cachedPlaneMesh;

        // Create once and cache; avoids creating/destroying primitives per chunk.
        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        var planeFilter = plane.GetComponent<MeshFilter>();
        cachedPlaneMesh = planeFilter != null ? planeFilter.sharedMesh : null;

        if (Application.isPlaying) Object.Destroy(plane);
        else Object.DestroyImmediate(plane);

        return cachedPlaneMesh;
    }

    private Vector3 PlayerPosition => player != null ? player.position : Vector3.zero;

    public void UpdateWaterChunk()
    {
        float playerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(PlayerPosition));
        bool visible = playerDstFromNearestEdge <= maxViewDistance;
        if (IsVisible() != visible)
        {
            SetVisible(visible);
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

    public void Destroy()
    {
        if (meshObject == null) return;
        if (Application.isPlaying) Object.Destroy(meshObject);
        else Object.DestroyImmediate(meshObject);
    }
}