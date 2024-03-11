using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WaterChunk
{
    public event System.Action<WaterChunk, bool> onVisibilityChange;
    public Vector2 coord;

    GameObject meshObject;
    Bounds bounds;

    MeshRenderer meshRenderer;
    MeshFilter meshFilter;
    MeshCollider meshCollider;

    float maxViewDistance;

    Transform player;

    public WaterChunk(Vector2 coord, MeshSettings meshSettings, Transform parent, Transform player, Material material)
    {
        this.coord = coord;
        this.player = player;

        Vector2 position = coord * meshSettings.meshWorldSize;
        bounds = new Bounds(position, Vector2.one * meshSettings.meshWorldSize);

        meshObject = new GameObject("Water Chunk");
        meshRenderer = meshObject.AddComponent<MeshRenderer>();
        meshFilter = meshObject.AddComponent<MeshFilter>();
        meshRenderer.material = material;

        meshObject.transform.position = new Vector3(position.x, 200, position.y);
        meshObject.transform.parent = parent;
        meshObject.layer = LayerMask.NameToLayer("Water");
        SetVisible(false);

        maxViewDistance = 7200f;
    }
    Vector2 playerPosition
    {
        get {
            return new Vector2(player.position.x, player.position.z);
        }
    }

    public void UpdateWaterChunk()
    {
        float playerDistanceFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(playerPosition));
        bool wasVisible = IsVisible();
        bool visible = playerDistanceFromNearestEdge <= maxViewDistance;
        
        if (wasVisible != visible)
        {
            SetVisible(visible);    
            if (onVisibilityChange != null)
            {
                onVisibilityChange(this, visible);                       
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