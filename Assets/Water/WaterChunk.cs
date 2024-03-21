using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WaterChunk
{    
    const float colliderGenerationDistanceThreshhold = 5f;
    public event System.Action<WaterChunk, bool> onVisibilityChange;
    public Vector2 coord;
    Mesh mesh;

    GameObject meshObject;
    MeshCollider meshCollider;
    MeshRenderer meshRenderer;
    Vector2 position;
    Bounds bounds;

    bool planeReceived;
    float maxViewDistance;
    bool hasSetCollider;
    
    Transform player;

    public WaterChunk(Vector2 coord, int size, Transform parent, Transform player, Material material, Mesh mesh)
    {
        this.player = player;
        this.coord = coord;
        this.mesh = mesh;

        position = coord * size;
        bounds = new Bounds(position, Vector2.one * size);
        Vector3 positionV3 = new Vector3(position.x, 180, position.y);

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

        // UpdateCollisionMesh();

        maxViewDistance = 7200f;
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

    Vector2 playerPosition
    {
        get {
            return new Vector2(player.position.x, player.position.z);
        }
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
    
    // public void UpdateCollisionMesh()
    // {
    //     if (!hasSetCollider)
    //     {
    //         meshCollider.sharedMesh = null;
    //         float sqrDistanceFromViewerToEdge = bounds.SqrDistance(playerPosition);

    //         if (sqrDistanceFromViewerToEdge < colliderGenerationDistanceThreshhold * colliderGenerationDistanceThreshhold)
    //         {             
    //             meshCollider.sharedMesh = mesh;
    //             hasSetCollider = true;
    //         }
    //     }            
    // }

    public void SetVisible(bool visible)
    {
        meshObject.SetActive(visible);
    }

    public bool IsVisible()
    {
        return meshObject.activeSelf;
    }
}