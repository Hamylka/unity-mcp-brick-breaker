using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a box with rounded vertical corners (a rounded rectangle extruded upward), for neon visuals.
/// The glowing outline is part of the geometry: a band of constant width around the top face and along the
/// top and bottom edges of the sides. Bands use UV (0.03, 0.5), which samples the bright border of the
/// NeonOutline texture; the rest uses (0.5, 0.5), its faint center. Visual only: keep box colliders for physics.
/// </summary>
public static class RoundedBoxMesh
{
    private static readonly Vector2 BrightUv = new(0.03f, 0.5f);
    private static readonly Vector2 FaintUv = new(0.5f, 0.5f);
    private static readonly Dictionary<(Vector3, float, float), Mesh> cache = new();

    /// <summary>A cached mesh of this world size (use it with a scale of 1).</summary>
    public static Mesh GetShared(Vector3 size, float radius, float outline)
    {
        var key = (size, radius, outline);
        if (cache.TryGetValue(key, out Mesh mesh) && mesh != null) return mesh;

        mesh = Build(size, radius, outline, Vector3.one);
        mesh.hideFlags = HideFlags.DontSave;
        cache[key] = mesh;
        return mesh;
    }

    /// <summary>
    /// Builds a mesh that has the given world size once the object's own scale (<paramref name="objectScale"/>) is applied,
    /// so the corners stay round on a non-uniformly scaled object.
    /// </summary>
    public static Mesh Build(Vector3 size, float radius, float outline, Vector3 objectScale, int segments = 4)
    {
        float hx = size.x * 0.5f;
        float hy = size.y * 0.5f;
        float hz = size.z * 0.5f;
        radius = Mathf.Clamp(radius, 0.001f, Mathf.Min(hx, hz));
        outline = Mathf.Clamp(outline, 0.001f, Mathf.Min(hx, hz) * 0.45f);

        var builder = new Builder();

        // Top: bright outline band, then a faint panel (separate vertices keep the edge between them crisp).
        List<Vector2> outer = Loop(hx, hz, radius, segments, out List<Vector2> outward);
        List<Vector2> inner = Loop(hx - outline, hz - outline, Mathf.Max(0.001f, radius - outline), segments, out _);
        int count = outer.Count;

        int outerTop = builder.Ring(outer, hy, Vector3.up, null, BrightUv);
        int innerBright = builder.Ring(inner, hy, Vector3.up, null, BrightUv);
        int innerFaint = builder.Ring(inner, hy, Vector3.up, null, FaintUv);
        int center = builder.Vertex(new Vector3(0f, hy, 0f), Vector3.up, FaintUv);
        for (int k = 0; k < count; k++)
        {
            int next = (k + 1) % count;
            builder.Quad(outerTop + k, outerTop + next, innerBright + next, innerBright + k, Vector3.up);
            builder.Triangle(center, innerFaint + k, innerFaint + next, Vector3.up);
        }

        // Sides: bright band at the bottom, faint middle, bright band at the top.
        float band = Mathf.Min(outline, hy * 0.5f);
        float[] heights = { -hy, -hy + band, hy - band, hy };
        Vector2[] bandUv = { BrightUv, FaintUv, BrightUv };
        for (int b = 0; b < 3; b++)
        {
            int lower = builder.Ring(outer, heights[b], Vector3.zero, outward, bandUv[b]);
            int upper = builder.Ring(outer, heights[b + 1], Vector3.zero, outward, bandUv[b]);
            for (int k = 0; k < count; k++)
            {
                int next = (k + 1) % count;
                Vector3 facing = new(outward[k].x + outward[next].x, 0f, outward[k].y + outward[next].y);
                builder.Quad(lower + k, lower + next, upper + next, upper + k, facing);
            }
        }

        return builder.ToMesh(objectScale);
    }

    // Rounded rectangle outline in XZ, counter-clockwise seen from above, with outward normals.
    private static List<Vector2> Loop(float hx, float hz, float r, int segments, out List<Vector2> normals)
    {
        var points = new List<Vector2>();
        normals = new List<Vector2>();
        float cx = Mathf.Max(0f, hx - r);
        float cz = Mathf.Max(0f, hz - r);
        Vector2[] centers = { new(cx, cz), new(-cx, cz), new(-cx, -cz), new(cx, -cz) };

        for (int corner = 0; corner < 4; corner++)
        {
            for (int s = 0; s <= segments; s++)
            {
                float angle = (corner * 90f + s * 90f / segments) * Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                points.Add(centers[corner] + direction * r);
                normals.Add(direction);
            }
        }
        return points;
    }

    private sealed class Builder
    {
        private readonly List<Vector3> vertices = new();
        private readonly List<Vector3> normals = new();
        private readonly List<Vector2> uvs = new();
        private readonly List<int> triangles = new();

        public int Vertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            vertices.Add(position);
            normals.Add(normal);
            uvs.Add(uv);
            return vertices.Count - 1;
        }

        // A horizontal ring of vertices; normals are either one fixed normal or per-point outward normals.
        public int Ring(List<Vector2> points, float y, Vector3 normal, List<Vector2> outward, Vector2 uv)
        {
            int start = vertices.Count;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 n = outward != null ? new Vector3(outward[i].x, 0f, outward[i].y) : normal;
                Vertex(new Vector3(points[i].x, y, points[i].y), n, uv);
            }
            return start;
        }

        // Adds a triangle facing the expected direction (the winding is fixed automatically).
        public void Triangle(int a, int b, int c, Vector3 expectedNormal)
        {
            Vector3 n = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(n, expectedNormal) < 0f) (b, c) = (c, b);
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        public void Quad(int a, int b, int c, int d, Vector3 expectedNormal)
        {
            Triangle(a, b, c, expectedNormal);
            Triangle(a, c, d, expectedNormal);
        }

        public Mesh ToMesh(Vector3 objectScale)
        {
            // Pre-divide by the object's scale so the final world shape is exact; normals use the inverse rule.
            var scaled = new List<Vector3>(vertices.Count);
            var scaledNormals = new List<Vector3>(normals.Count);
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 v = vertices[i];
                scaled.Add(new Vector3(v.x / objectScale.x, v.y / objectScale.y, v.z / objectScale.z));
                Vector3 n = normals[i];
                scaledNormals.Add(new Vector3(n.x * objectScale.x, n.y * objectScale.y, n.z * objectScale.z).normalized);
            }

            var mesh = new Mesh { name = "RoundedBox" };
            mesh.SetVertices(scaled);
            mesh.SetNormals(scaledNormals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
