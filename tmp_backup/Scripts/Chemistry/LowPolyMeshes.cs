using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Provides low-poly meshes for molecule rendering on Quest 3.
/// Icosphere: 42 vertices / 80 tris (vs Unity sphere: 515 verts / 768 tris ≈ 10x reduction)
/// Cylinder:  34 vertices / 48 tris (vs Unity cylinder: ~120 verts / 80 tris)
/// </summary>
public static class LowPolyMeshes
{
    private static Mesh s_Sphere;
    private static Mesh s_Cylinder;

    /// <summary>
    /// Returns a cached icosphere mesh (42 vertices, 80 tris).
    /// Dimensions: unit diameter (radius 0.5), matching Unity sphere.
    /// </summary>
    public static Mesh GetSphere()
    {
        if (s_Sphere != null) return s_Sphere;
        s_Sphere = CreateIcosphere(1);
        s_Sphere.name = "LowPolySphere";
        return s_Sphere;
    }

    /// <summary>
    /// Returns a cached low-poly cylinder mesh (8 sides).
    /// Dimensions: height 2, radius 0.5, matching Unity cylinder.
    /// </summary>
    public static Mesh GetCylinder()
    {
        if (s_Cylinder != null) return s_Cylinder;
        s_Cylinder = CreateCylinder(8);
        s_Cylinder.name = "LowPolyCylinder";
        return s_Cylinder;
    }

    // ────────────────────────────────────────────────────────────
    // Icosphere generation
    // ────────────────────────────────────────────────────────────

    private static Mesh CreateIcosphere(int subdivisions)
    {
        // Golden ratio
        float t = (1f + Mathf.Sqrt(5f)) / 2f;

        var verts = new List<Vector3>
        {
            new Vector3(-1,  t,  0).normalized * 0.5f,
            new Vector3( 1,  t,  0).normalized * 0.5f,
            new Vector3(-1, -t,  0).normalized * 0.5f,
            new Vector3( 1, -t,  0).normalized * 0.5f,
            new Vector3( 0, -1,  t).normalized * 0.5f,
            new Vector3( 0,  1,  t).normalized * 0.5f,
            new Vector3( 0, -1, -t).normalized * 0.5f,
            new Vector3( 0,  1, -t).normalized * 0.5f,
            new Vector3( t,  0, -1).normalized * 0.5f,
            new Vector3( t,  0,  1).normalized * 0.5f,
            new Vector3(-t,  0, -1).normalized * 0.5f,
            new Vector3(-t,  0,  1).normalized * 0.5f,
        };

        var tris = new List<int>
        {
            0,11,5,  0,5,1,   0,1,7,   0,7,10,  0,10,11,
            1,5,9,   5,11,4,  11,10,2,  10,7,6,  7,1,8,
            3,9,4,   3,4,2,   3,2,6,   3,6,8,   3,8,9,
            4,9,5,   2,4,11,  6,2,10,  8,6,7,   9,8,1,
        };

        // Subdivide
        var midpointCache = new Dictionary<long, int>();
        for (int i = 0; i < subdivisions; i++)
        {
            var newTris = new List<int>();
            for (int j = 0; j < tris.Count; j += 3)
            {
                int a = tris[j];
                int b = tris[j + 1];
                int c = tris[j + 2];

                int ab = GetMidpoint(a, b, verts, midpointCache);
                int bc = GetMidpoint(b, c, verts, midpointCache);
                int ca = GetMidpoint(c, a, verts, midpointCache);

                newTris.AddRange(new[] { a, ab, ca });
                newTris.AddRange(new[] { b, bc, ab });
                newTris.AddRange(new[] { c, ca, bc });
                newTris.AddRange(new[] { ab, bc, ca });
            }
            tris = newTris;
        }

        // Normals = normalized vertex positions (for a sphere)
        var normals = new List<Vector3>();
        foreach (var v in verts)
            normals.Add(v.normalized);

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static int GetMidpoint(int a, int b, List<Vector3> verts, Dictionary<long, int> cache)
    {
        long key = ((long)Mathf.Min(a, b) << 32) + Mathf.Max(a, b);
        if (cache.TryGetValue(key, out int idx)) return idx;

        Vector3 mid = ((verts[a] + verts[b]) / 2f).normalized * 0.5f;
        idx = verts.Count;
        verts.Add(mid);
        cache[key] = idx;
        return idx;
    }

    // ────────────────────────────────────────────────────────────
    // Low-poly cylinder generation
    // ────────────────────────────────────────────────────────────

    private static Mesh CreateCylinder(int sides)
    {
        // Match Unity cylinder: height 2 (Y: -1 to +1), radius 0.5
        float radius = 0.5f;
        float halfH = 1f;

        int vertCount = (sides + 1) * 2 + (sides + 1) * 2; // side ring tops/bottoms + caps
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var tris = new List<int>();

        // Side vertices (top and bottom rings)
        for (int i = 0; i <= sides; i++)
        {
            float angle = (float)i / sides * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            Vector3 normal = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));

            // Bottom ring
            verts.Add(new Vector3(x, -halfH, z));
            normals.Add(normal);
            // Top ring
            verts.Add(new Vector3(x, halfH, z));
            normals.Add(normal);
        }

        // Side triangles
        for (int i = 0; i < sides; i++)
        {
            int bl = i * 2;
            int tl = bl + 1;
            int br = bl + 2;
            int tr = bl + 3;
            tris.AddRange(new[] { bl, tl, tr, bl, tr, br });
        }

        // Top cap
        int topCenter = verts.Count;
        verts.Add(new Vector3(0, halfH, 0));
        normals.Add(Vector3.up);
        for (int i = 0; i <= sides; i++)
        {
            float angle = (float)i / sides * Mathf.PI * 2f;
            verts.Add(new Vector3(Mathf.Cos(angle) * radius, halfH, Mathf.Sin(angle) * radius));
            normals.Add(Vector3.up);
        }
        for (int i = 0; i < sides; i++)
        {
            tris.AddRange(new[] { topCenter, topCenter + 1 + i, topCenter + 2 + i });
        }

        // Bottom cap
        int botCenter = verts.Count;
        verts.Add(new Vector3(0, -halfH, 0));
        normals.Add(Vector3.down);
        for (int i = 0; i <= sides; i++)
        {
            float angle = (float)i / sides * Mathf.PI * 2f;
            verts.Add(new Vector3(Mathf.Cos(angle) * radius, -halfH, Mathf.Sin(angle) * radius));
            normals.Add(Vector3.down);
        }
        for (int i = 0; i < sides; i++)
        {
            tris.AddRange(new[] { botCenter, botCenter + 2 + i, botCenter + 1 + i });
        }

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
