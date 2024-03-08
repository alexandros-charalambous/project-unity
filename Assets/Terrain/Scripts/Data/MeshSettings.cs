using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MeshSettings")]
public class MeshSettings : UpdatableData
{
    public const int numSupportedLODs = 5;
    public const int numSupportedChunkSizes = 9;
    public const int numSupportedFlatshadedChunkSizes = 3;
    public static readonly int[] supportedChunkSizes = { 48,72,96,120,144,168,192,216,240 };

    public float meshScale = 5f;
    public bool useFlatShading;    
    
    [Header("Map Size")]
    [Range(0, numSupportedChunkSizes - 1)] public int chunkSizeIndex;
    [Range(0, numSupportedFlatshadedChunkSizes - 1)] public int flatshadedChunkSizeIndex;

    // Number of vertices per line of mesh rendered at LOD = 0. 
    // Includes the 2 extra vertices that are excluded from final mesh, but used for calculating normals
    public int numberVerticesPerLine
    {
        get{
            return supportedChunkSizes[useFlatShading ? flatshadedChunkSizeIndex : chunkSizeIndex] + 5;
        }
    }

    public float meshWorldSize 
    {
        get {
            return (numberVerticesPerLine - 3) * meshScale;
        }
    }
}
