using UnityEngine;

/// <summary>
/// Building blocks for soft neon glows that do not depend on bloom alone: additive quads with a radial falloff
/// (camera-facing halos, light pools on the floor) and additive neon lines. Shares one generated texture and
/// one material built from Resources/FX/M_NeonFx; each renderer gets its own color through a property block.
/// </summary>
public static class NeonGlow
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    private static Texture2D radialTexture;
    private static Material glowMaterial;
    private static Material lineMaterial;
    private static Mesh quadMesh;
    private static MaterialPropertyBlock block;

    /// <summary>Additive material with the radial falloff texture (halos, floor pools).</summary>
    public static Material GlowMaterial
    {
        get
        {
            if (glowMaterial != null) return glowMaterial;
            Material source = Resources.Load<Material>("FX/M_NeonFx");
            if (source == null) return null;
            glowMaterial = new Material(source) { name = "NeonGlow (runtime)" };
            glowMaterial.SetTexture(BaseMapId, RadialTexture);
            return glowMaterial;
        }
    }

    /// <summary>Plain additive material, no texture (lines, trails).</summary>
    public static Material LineMaterial
    {
        get
        {
            if (lineMaterial != null) return lineMaterial;
            lineMaterial = Resources.Load<Material>("FX/M_NeonFx");
            return lineMaterial;
        }
    }

    /// <summary>
    /// A white disc fading to transparent: a bright center, then a long soft tail, so the glow
    /// reads both as a hot spot and as light spilling around it.
    /// </summary>
    public static Texture2D RadialTexture
    {
        get
        {
            if (radialTexture != null) return radialTexture;
            const int size = 64;
            radialTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "NeonRadial (runtime)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float falloff = 1f - r;
                float alpha = 0.55f * falloff * falloff + 0.45f * Mathf.Pow(falloff, 6f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
            }
            radialTexture.SetPixels32(pixels);
            radialTexture.Apply(false, true);
            return radialTexture;
        }
    }

    /// <summary>A unit quad facing -Z (like Unity's built-in one) with white vertex colors, which the particle shader multiplies in.</summary>
    public static Mesh QuadMesh
    {
        get
        {
            if (quadMesh != null) return quadMesh;
            quadMesh = new Mesh { name = "NeonQuad (runtime)" };
            quadMesh.SetVertices(new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
            });
            quadMesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            quadMesh.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
            quadMesh.SetNormals(new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            quadMesh.SetTriangles(new[] { 0, 3, 1, 3, 0, 2 }, 0);
            quadMesh.RecalculateBounds();
            return quadMesh;
        }
    }

    /// <summary>Creates a glow quad as a child (no collider, no shadows). It faces -Z of its own transform.</summary>
    public static MeshRenderer CreateQuad(Transform parent, string name, float size)
    {
        var quad = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        quad.GetComponent<MeshFilter>().sharedMesh = QuadMesh;
        quad.transform.SetParent(parent, false);
        quad.transform.localScale = Vector3.one * size;

        var renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = GlowMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    /// <summary>Sets an HDR color on a renderer that uses one of the materials above.</summary>
    public static void SetColor(Renderer renderer, Color color)
    {
        if (renderer == null) return;
        block ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        renderer.SetPropertyBlock(block);
    }

    /// <summary>A color multiplied for HDR (keeps alpha).</summary>
    public static Color Hdr(Color color, float intensity) =>
        new(color.r * intensity, color.g * intensity, color.b * intensity, color.a);
}
