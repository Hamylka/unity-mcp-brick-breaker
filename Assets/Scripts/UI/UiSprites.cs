using UnityEngine;

/// <summary>Generates simple flat-design sprites at runtime, so the HUD needs no image assets.</summary>
public static class UiSprites
{
    private const int Size = 64;
    private const int Radius = 28;

    private static Sprite roundedRect;

    /// <summary>
    /// A 9-sliced rounded rectangle. Use it with Image.Type.Sliced: when the rect is shorter than twice the radius
    /// the corners shrink, so a thin bar automatically becomes a pill.
    /// </summary>
    public static Sprite RoundedRect
    {
        get
        {
            if (roundedRect == null) roundedRect = BuildRoundedRect();
            return roundedRect;
        }
    }

    private static readonly System.Collections.Generic.Dictionary<string, Sprite> pixelArt = new();

    /// <summary>Builds a crisp sprite from rows of '#' (pixel) and '.' (empty). Tint it with Image.color.</summary>
    public static Sprite PixelArt(string rows)
    {
        if (pixelArt.TryGetValue(rows, out Sprite cached) && cached != null) return cached;

        string[] lines = rows.Replace("\r", "").Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
        int height = lines.Length;
        int width = 1;
        foreach (string line in lines) width = Mathf.Max(width, line.Trim().Length);

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            name = "PixelArt"
        };

        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            string line = lines[y].Trim();
            for (int x = 0; x < width; x++)
            {
                bool on = x < line.Length && line[x] == '#';
                pixels[(height - 1 - y) * width + x] = new Color32(255, 255, 255, (byte)(on ? 255 : 0));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        pixelArt[rows] = sprite;
        return sprite;
    }

    private static Sprite BuildRoundedRect()
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            name = "RoundedRect"
        };

        var pixels = new Color32[Size * Size];
        float half = Size * 0.5f;
        float inner = half - Radius;
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                // Signed distance to a rounded box, anti-aliased over one pixel.
                float qx = Mathf.Abs(x + 0.5f - half) - inner;
                float qy = Mathf.Abs(y + 0.5f - half) - inner;
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                float distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - Radius;
                byte alpha = (byte)(Mathf.Clamp01(0.5f - distance) * 255f);
                pixels[y * Size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        var sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(Radius, Radius, Radius, Radius));
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
