using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates a flat-shaded gem: a hexagonal bipyramid with a short crown on top and a longer point below,
/// centered on its widest ring. Every face has its own vertices, so with the Neon Fresnel shader each facet gets
/// its own brightness and the edges read clearly. Meshes are cached per size.
/// </summary>
public static class CrystalMesh
{
    private const int Sides = 6;
    private const float CrownShare = 0.4f; // share of the height above the widest ring

    private static readonly Dictionary<(float, float), Mesh> cache = new();

    public static Mesh Get(float radius, float height)
    {
        var key = (radius, height);
        if (cache.TryGetValue(key, out Mesh mesh) && mesh != null) return mesh;

        var top = new Vector3(0f, height * CrownShare, 0f);
        var bottom = new Vector3(0f, -height * (1f - CrownShare), 0f);
        var ring = new Vector3[Sides];
        for (int i = 0; i < Sides; i++)
        {
            float a = i * Mathf.PI * 2f / Sides;
            ring[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
        }

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();
        for (int i = 0; i < Sides; i++)
        {
            Vector3 a = ring[i];
            Vector3 b = ring[(i + 1) % Sides];
            AddFace(top, b, a);    // crown facet
            AddFace(bottom, a, b); // pavilion facet
        }

        void AddFace(Vector3 p0, Vector3 p1, Vector3 p2)
        {
            Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0).normalized;
            int start = vertices.Count;
            vertices.Add(p0); vertices.Add(p1); vertices.Add(p2);
            normals.Add(normal); normals.Add(normal); normals.Add(normal);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }

        mesh = new Mesh { name = $"Crystal {radius:0.##}x{height:0.##}" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        cache[key] = mesh;
        return mesh;
    }
}
