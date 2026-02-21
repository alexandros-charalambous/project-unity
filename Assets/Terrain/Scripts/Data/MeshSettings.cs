using UnityEngine;

[CreateAssetMenu(fileName = "MeshSettings", menuName = "Terrain/Mesh Settings")]
public class MeshSettings : UpdatableData
{
    public const int numSupportedLODs = 5;

    public enum ChunkSizeOption
    {
        Size48 = 48,
        Size72 = 72,
        Size96 = 96,
        Size120 = 120,
        Size144 = 144,
        Size168 = 168,
        Size192 = 192,
        Size216 = 216,
        Size240 = 240
    }

    [Tooltip("World-space scale of each mesh grid step.\n\nLarger meshScale = each chunk covers more meters (bigger terrain features per chunk), but also reduces geometric detail per meter unless you increase resolution elsewhere.")]
    [Min(0.01f)]
    public float meshScale = 5f;

    [Header("Map Size")]
    [SerializeField]
    [Tooltip("Base chunk size option.\n\nThis controls how many samples/steps exist across the chunk in the original (heightmap) mesh system.\nIn the volumetric system, the generator uses MeshSettings.chunkWorldSize as the streamed chunk world size.")]
    private ChunkSizeOption chunkSize = ChunkSizeOption.Size120;

    public int ChunkSize => (int)chunkSize;

    // Number of vertices per line of mesh rendered at LOD = 0. 
    // Includes the 2 extra vertices that are excluded from final mesh, but used for calculating normals
    public int numberVerticesPerLine
    {
        get
        {
            return ChunkSize + 5;
        }
    }

    public float meshWorldSize
    {
        get
        {
            return (numberVerticesPerLine - 3) * meshScale;
        }
    }

    // Semantic alias: this value is used as the world-space size of a single streamed chunk.
    public float chunkWorldSize
    {
        get
        {
            return meshWorldSize;
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        meshScale = Mathf.Max(0.01f, meshScale);
        base.OnValidate();
    }
#endif
}
