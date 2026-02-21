using UnityEngine;

/// <summary>
/// "Marching cubes" mesher for this project.
/// Implementation uses a cube marched via tetrahedra partition (marching tetrahedra),
/// which avoids the ambiguous cases of classic marching cubes lookup tables while
/// still operating on cubes.
/// </summary>
public static class MarchingCubes
{
    public static VolumetricMeshData Generate(float[,,] density, Vector3[,,] normals, float isoLevel, float cellSize)
    {
        return MarchingTetrahedra.Generate(density, normals, isoLevel, cellSize);
    }

    public static VolumetricMeshData Generate(
        float[,,] density,
        Vector3[,,] normals,
        float isoLevel,
        float cellSize,
        int offsetX,
        int offsetY,
        int offsetZ,
        int cellsX,
        int cellsY,
        int cellsZ)
    {
        return MarchingTetrahedra.Generate(density, normals, isoLevel, cellSize, offsetX, offsetY, offsetZ, cellsX, cellsY, cellsZ);
    }
}
