using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterChunk
{
    const float colliderGenerationDistanceThreshhold = 5f;
    public event System.Action<WaterChunk, bool> onVisibilityChange;
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

    HeightMap heightMap;
    bool heightMapReceived;
    int previousLODIndex = -1;
    bool hasSetCollider;
    float maxViewDistance;

    MeshSettings meshSettings;
    Transform player;

    public WaterChunk(Vector2 coord, MeshSettings meshSettings, LODInfo[] detailLevels, Transform parent, Transform player, Material material)
    {
        this.coord = coord;
        this.detailLevels = detailLevels;
        this.meshSettings = meshSettings;
        this.player = player;

        sampleCenter = coord * meshSettings.meshWorldSize / meshSettings.meshScale;
        Vector2 position = coord * meshSettings.meshWorldSize;
        bounds = new Bounds(position, Vector2.one * meshSettings.meshWorldSize);

        meshObject = new GameObject("Water Chunk");
        meshRenderer = meshObject.AddComponent<MeshRenderer>();
        meshFilter = meshObject.AddComponent<MeshFilter>();
        meshRenderer.material = material;

        meshObject.transform.position = new Vector3(position.x, 0, position.y);
        meshObject.transform.parent = parent;
        meshObject.layer = LayerMask.NameToLayer("Water");
        SetVisible(true);

        lodMeshes = new LODMesh[detailLevels.Length];
        for (int i = 0; i < detailLevels.Length; i++)
        {
            lodMeshes[i] = new LODMesh(detailLevels[i].lod);
            lodMeshes[i].updateCallback += UpdateWaterChunk;
        }

        maxViewDistance = detailLevels[detailLevels.Length - 1].visibleDistanceThreshhold;
    }

    Vector2 playerPosition
    {
        get {
            return new Vector2(player.position.x, player.position.z);
        }
    }

    public void UpdateWaterChunk()
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
                        lodMesh.RequestMesh(meshSettings);
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

    public void SetVisible(bool visible)
    {
        meshObject.SetActive(visible);
    }

    public bool IsVisible()
    {
        return meshObject.activeSelf;
    }
}