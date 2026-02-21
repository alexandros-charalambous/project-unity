using System.Collections.Generic;
using UnityEngine;

public static class MarchingTetrahedra
{
    [System.ThreadStatic] private static List<Vector3> threadVertices;
    [System.ThreadStatic] private static List<Vector3> threadNormals;
    [System.ThreadStatic] private static List<int> threadTriangles;

    private static void GetThreadLists(int expectedCellCount, out List<Vector3> vertices, out List<Vector3> normals, out List<int> triangles)
    {
        // ExpectedCellCount is just a heuristic for initial capacity.
        int vCap = Mathf.Max(64, expectedCellCount);
        int tCap = Mathf.Max(128, expectedCellCount * 3);

        if (threadVertices == null) threadVertices = new List<Vector3>(vCap);
        if (threadNormals == null) threadNormals = new List<Vector3>(vCap);
        if (threadTriangles == null) threadTriangles = new List<int>(tCap);

        threadVertices.Clear();
        threadNormals.Clear();
        threadTriangles.Clear();

        if (threadVertices.Capacity < vCap) threadVertices.Capacity = vCap;
        if (threadNormals.Capacity < vCap) threadNormals.Capacity = vCap;
        if (threadTriangles.Capacity < tCap) threadTriangles.Capacity = tCap;

        vertices = threadVertices;
        normals = threadNormals;
        triangles = threadTriangles;
    }

    // Cube corner offsets in local voxel space.
    private static readonly Vector3Int[] cubeCornerOffsets = new Vector3Int[8]
    {
        new Vector3Int(0, 0, 0),
        new Vector3Int(1, 0, 0),
        new Vector3Int(1, 0, 1),
        new Vector3Int(0, 0, 1),
        new Vector3Int(0, 1, 0),
        new Vector3Int(1, 1, 0),
        new Vector3Int(1, 1, 1),
        new Vector3Int(0, 1, 1),
    };

    // 6 tetrahedra that partition a cube. Indices refer to the 8 cube corners above.
    private static readonly int[,] tets = new int[6, 4]
    {
        { 0, 5, 1, 6 },
        { 0, 1, 2, 6 },
        { 0, 2, 3, 6 },
        { 0, 3, 7, 6 },
        { 0, 7, 4, 6 },
        { 0, 4, 5, 6 },
    };

    // Marching-cubes style meshing (cube marched by tetra partition), with seam-safe normals.
    public static VolumetricMeshData Generate(float[,,] density, Vector3[,,] normals, float isoLevel, float cellSize)
    {
        int nx = density.GetLength(0);
        int ny = density.GetLength(1);
        int nz = density.GetLength(2);

        int cx = nx - 1;
        int cy = ny - 1;
        int cz = nz - 1;

        GetThreadLists(cx * cy * cz, out var vertices, out var vnormals, out var triangles);

        Vector3[] cornerPos = new Vector3[8];
        float[] cornerDen = new float[8];
        Vector3[] cornerNor = new Vector3[8];

        for (int z = 0; z < cz; z++)
        {
            for (int y = 0; y < cy; y++)
            {
                for (int x = 0; x < cx; x++)
                {
                    for (int c = 0; c < 8; c++)
                    {
                        Vector3Int o = cubeCornerOffsets[c];
                        int sx = x + o.x;
                        int sy = y + o.y;
                        int sz = z + o.z;

                        cornerDen[c] = density[sx, sy, sz];
                        cornerNor[c] = normals[sx, sy, sz];
                        cornerPos[c] = new Vector3(sx * cellSize, sy * cellSize, sz * cellSize);
                    }

                    for (int t = 0; t < 6; t++)
                    {
                        int i0 = tets[t, 0];
                        int i1 = tets[t, 1];
                        int i2 = tets[t, 2];
                        int i3 = tets[t, 3];

                        EmitTet(vertices, vnormals, triangles,
                            cornerPos[i0], cornerDen[i0], cornerNor[i0],
                            cornerPos[i1], cornerDen[i1], cornerNor[i1],
                            cornerPos[i2], cornerDen[i2], cornerNor[i2],
                            cornerPos[i3], cornerDen[i3], cornerNor[i3],
                            isoLevel);
                    }
                }
            }
        }

        return new VolumetricMeshData(vertices.ToArray(), vnormals.ToArray(), triangles.ToArray());
    }

    // Meshing a sub-region of a (possibly padded) density grid.
    // offset* refers to the starting sample index; cells* refers to cube count.
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
        GetThreadLists(cellsX * cellsY * cellsZ, out var vertices, out var vnormals, out var triangles);

        Vector3[] cornerPos = new Vector3[8];
        float[] cornerDen = new float[8];
        Vector3[] cornerNor = new Vector3[8];

        for (int z = 0; z < cellsZ; z++)
        {
            for (int y = 0; y < cellsY; y++)
            {
                for (int x = 0; x < cellsX; x++)
                {
                    for (int c = 0; c < 8; c++)
                    {
                        Vector3Int o = cubeCornerOffsets[c];
                        int sx = offsetX + x + o.x;
                        int sy = offsetY + y + o.y;
                        int sz = offsetZ + z + o.z;

                        cornerDen[c] = density[sx, sy, sz];
                        cornerNor[c] = normals[sx, sy, sz];

                        // Convert padded sample indices to local chunk-space positions.
                        cornerPos[c] = new Vector3((sx - offsetX) * cellSize, (sy - offsetY) * cellSize, (sz - offsetZ) * cellSize);
                    }

                    for (int t = 0; t < 6; t++)
                    {
                        int i0 = tets[t, 0];
                        int i1 = tets[t, 1];
                        int i2 = tets[t, 2];
                        int i3 = tets[t, 3];

                        EmitTet(vertices, vnormals, triangles,
                            cornerPos[i0], cornerDen[i0], cornerNor[i0],
                            cornerPos[i1], cornerDen[i1], cornerNor[i1],
                            cornerPos[i2], cornerDen[i2], cornerNor[i2],
                            cornerPos[i3], cornerDen[i3], cornerNor[i3],
                            isoLevel);
                    }
                }
            }
        }

        return new VolumetricMeshData(vertices.ToArray(), vnormals.ToArray(), triangles.ToArray());
    }

    private static void EmitTet(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<int> triangles,
        Vector3 p0, float d0, Vector3 n0,
        Vector3 p1, float d1, Vector3 n1,
        Vector3 p2, float d2, Vector3 n2,
        Vector3 p3, float d3, Vector3 n3,
        float iso)
    {
        bool in0 = d0 > iso;
        bool in1 = d1 > iso;
        bool in2 = d2 > iso;
        bool in3 = d3 > iso;

        int insideCount = (in0 ? 1 : 0) + (in1 ? 1 : 0) + (in2 ? 1 : 0) + (in3 ? 1 : 0);
        if (insideCount == 0 || insideCount == 4) return;

        int interCount = 0;
        Vector3 pI0 = default, pI1 = default, pI2 = default, pI3 = default;
        Vector3 nI0 = default, nI1 = default, nI2 = default, nI3 = default;

        AddEdge(p0, d0, n0, in0, p1, d1, n1, in1);
        AddEdge(p0, d0, n0, in0, p2, d2, n2, in2);
        AddEdge(p0, d0, n0, in0, p3, d3, n3, in3);
        AddEdge(p1, d1, n1, in1, p2, d2, n2, in2);
        AddEdge(p1, d1, n1, in1, p3, d3, n3, in3);
        AddEdge(p2, d2, n2, in2, p3, d3, n3, in3);

        if (interCount < 3) return;

        // Order the intersection polygon vertices around the centroid.
        // The iso-surface inside a tetrahedron is planar, so a simple angular sort is robust.
        Vector3 centroid;
        Vector3 avgNormal;
        Vector3 planeNormal;
        if (interCount == 3)
        {
            centroid = (pI0 + pI1 + pI2) / 3f;
            avgNormal = nI0 + nI1 + nI2;
            planeNormal = Vector3.Cross(pI1 - pI0, pI2 - pI0);
        }
        else
        {
            centroid = (pI0 + pI1 + pI2 + pI3) * 0.25f;
            avgNormal = nI0 + nI1 + nI2 + nI3;
            // Sum two triangle normals to stabilize plane direction.
            planeNormal = Vector3.Cross(pI1 - pI0, pI2 - pI0) + Vector3.Cross(pI2 - pI0, pI3 - pI0);
        }

        float avgLen = avgNormal.magnitude;
        if (avgLen > 1e-6f) avgNormal /= avgLen;
        else avgNormal = Vector3.up;

        float planeLen = planeNormal.magnitude;
        if (planeLen > 1e-6f) planeNormal /= planeLen;
        else planeNormal = avgNormal;

        // Build an orthonormal basis (u,v) on the polygon plane.
        Vector3 u = Vector3.Cross(planeNormal, Vector3.up);
        float uLen = u.magnitude;
        if (uLen <= 1e-6f)
        {
            u = Vector3.Cross(planeNormal, Vector3.right);
            uLen = u.magnitude;
        }
        if (uLen > 1e-6f) u /= uLen;
        else u = Vector3.right;

        Vector3 v = Vector3.Cross(planeNormal, u);
        float vLen = v.magnitude;
        if (vLen > 1e-6f) v /= vLen;

        float a0 = AngleOnPlane(pI0 - centroid);
        float a1 = AngleOnPlane(pI1 - centroid);
        float a2 = AngleOnPlane(pI2 - centroid);
        float a3 = 0f;
        if (interCount == 4) a3 = AngleOnPlane(pI3 - centroid);

        // Sort by angle (small n sorting network style; no allocations).
        Sort3(ref pI0, ref nI0, ref a0, ref pI1, ref nI1, ref a1, ref pI2, ref nI2, ref a2);
        if (interCount == 4)
        {
            // Insert 4th element into the sorted first three.
            Insert4(ref pI0, ref nI0, ref a0, ref pI1, ref nI1, ref a1, ref pI2, ref nI2, ref a2, ref pI3, ref nI3, ref a3);
        }

        int baseIndex = vertices.Count;
        vertices.Add(pI0); normals.Add(nI0);
        vertices.Add(pI1); normals.Add(nI1);
        vertices.Add(pI2); normals.Add(nI2);

        if (interCount == 3)
        {
            AddOrientedTriangle(baseIndex, baseIndex + 1, baseIndex + 2, pI0, pI1, pI2, nI0, nI1, nI2);
        }
        else
        {
            vertices.Add(pI3); normals.Add(nI3);

            // Triangulate quad as two triangles. Any diagonal is valid because the iso-surface is planar in a tet.
            AddOrientedTriangle(baseIndex, baseIndex + 1, baseIndex + 2, pI0, pI1, pI2, nI0, nI1, nI2);
            AddOrientedTriangle(baseIndex, baseIndex + 2, baseIndex + 3, pI0, pI2, pI3, nI0, nI2, nI3);
        }

        void AddOrientedTriangle(int i0, int i1, int i2, Vector3 tp0, Vector3 tp1, Vector3 tp2, Vector3 tn0, Vector3 tn1, Vector3 tn2)
        {
            Vector3 face = Vector3.Cross(tp1 - tp0, tp2 - tp0);
            Vector3 nAvg = tn0 + tn1 + tn2;
            if (Vector3.Dot(face, nAvg) < 0f)
            {
                // Flip winding.
                triangles.Add(i0);
                triangles.Add(i2);
                triangles.Add(i1);
            }
            else
            {
                triangles.Add(i0);
                triangles.Add(i1);
                triangles.Add(i2);
            }
        }

        float AngleOnPlane(Vector3 r)
        {
            float x = Vector3.Dot(r, u);
            float y = Vector3.Dot(r, v);
            return Mathf.Atan2(y, x);
        }

        void AddEdge(Vector3 aPos, float aDen, Vector3 aNor, bool aIn, Vector3 bPos, float bDen, Vector3 bNor, bool bIn)
        {
            if (aIn == bIn) return;

            float t = (iso - aDen) / (bDen - aDen);
            if (float.IsNaN(t) || float.IsInfinity(t)) t = 0.5f;
            else t = Mathf.Clamp01(t);
            Vector3 pos = Vector3.LerpUnclamped(aPos, bPos, t);
            Vector3 nor = Vector3.LerpUnclamped(aNor, bNor, t);
            float len = nor.magnitude;
            if (len > 1e-6f) nor /= len;
            else nor = Vector3.up;

            switch (interCount)
            {
                case 0: pI0 = pos; nI0 = nor; break;
                case 1: pI1 = pos; nI1 = nor; break;
                case 2: pI2 = pos; nI2 = nor; break;
                case 3: pI3 = pos; nI3 = nor; break;
            }

            interCount++;
        }
    }

    private static void Sort3(
        ref Vector3 p0, ref Vector3 n0, ref float a0,
        ref Vector3 p1, ref Vector3 n1, ref float a1,
        ref Vector3 p2, ref Vector3 n2, ref float a2)
    {
        if (a1 < a0) Swap(ref p0, ref n0, ref a0, ref p1, ref n1, ref a1);
        if (a2 < a1) Swap(ref p1, ref n1, ref a1, ref p2, ref n2, ref a2);
        if (a1 < a0) Swap(ref p0, ref n0, ref a0, ref p1, ref n1, ref a1);
    }

    private static void Insert4(
        ref Vector3 p0, ref Vector3 n0, ref float a0,
        ref Vector3 p1, ref Vector3 n1, ref float a1,
        ref Vector3 p2, ref Vector3 n2, ref float a2,
        ref Vector3 p3, ref Vector3 n3, ref float a3)
    {
        // p0..p2 are sorted. Insert p3.
        if (a3 < a0)
        {
            // rotate right
            Vector3 tp = p3; Vector3 tn = n3; float ta = a3;
            p3 = p2; n3 = n2; a3 = a2;
            p2 = p1; n2 = n1; a2 = a1;
            p1 = p0; n1 = n0; a1 = a0;
            p0 = tp; n0 = tn; a0 = ta;
            return;
        }
        if (a3 < a1)
        {
            Vector3 tp = p3; Vector3 tn = n3; float ta = a3;
            p3 = p2; n3 = n2; a3 = a2;
            p2 = p1; n2 = n1; a2 = a1;
            p1 = tp; n1 = tn; a1 = ta;
            return;
        }
        if (a3 < a2)
        {
            Swap(ref p2, ref n2, ref a2, ref p3, ref n3, ref a3);
        }
    }

    private static void Swap(ref Vector3 pa, ref Vector3 na, ref float aa, ref Vector3 pb, ref Vector3 nb, ref float ab)
    {
        Vector3 tp = pa; pa = pb; pb = tp;
        Vector3 tn = na; na = nb; nb = tn;
        float ta = aa; aa = ab; ab = ta;
    }
}
