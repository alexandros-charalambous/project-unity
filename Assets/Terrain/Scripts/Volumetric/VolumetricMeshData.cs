using UnityEngine;

public readonly struct VolumetricMeshData
{
    public readonly Vector3[] vertices;
    public readonly Vector3[] normals;
    public readonly int[] triangles;
    public readonly Color[] colors;
    public readonly Vector2[] uv2;

    public VolumetricMeshData(Vector3[] vertices, Vector3[] normals, int[] triangles)
    {
        this.vertices = vertices;
        this.normals = normals;
        this.triangles = triangles;
        colors = null;
        uv2 = null;
    }

    public VolumetricMeshData(Vector3[] vertices, Vector3[] normals, int[] triangles, Color[] colors, Vector2[] uv2)
    {
        this.vertices = vertices;
        this.normals = normals;
        this.triangles = triangles;
        this.colors = colors;
        this.uv2 = uv2;
    }

    public Mesh CreateMesh()
    {
        var mesh = new Mesh();
        mesh.indexFormat = vertices.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = vertices;
        if (normals != null && normals.Length == vertices.Length)
        {
            mesh.normals = normals;
        }
        if (uv2 != null && uv2.Length == vertices.Length)
        {
            mesh.uv2 = uv2;
        }
        if (colors != null && colors.Length == vertices.Length)
        {
            mesh.colors = colors;
        }
        mesh.triangles = triangles;
        if (mesh.normals == null || mesh.normals.Length != vertices.Length)
        {
            mesh.RecalculateNormals();
        }
        mesh.RecalculateBounds();
        return mesh;
    }
}
