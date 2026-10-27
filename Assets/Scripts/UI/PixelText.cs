using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI text drawn with <see cref="PixelFont"/>. Crisp at any size, supports line breaks ('\n') and optional
/// word wrapping inside its rect, and works with the standard Outline and Shadow components. Not interactive.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
[AddComponentMenu("UI/Pixel Text")]
public class PixelText : MaskableGraphic
{
    public enum Align { Left, Center, Right }

    [SerializeField, TextArea] private string text = "PIXEL";
    [SerializeField, Min(1f)] private float pixelSize = 6f;
    [Tooltip("Gap between letters, in font pixels.")]
    [SerializeField, Min(0f)] private float spacing = 1f;
    [Tooltip("Gap between lines, in font pixels.")]
    [SerializeField, Min(0f)] private float lineGap = 3f;
    [Tooltip("Break lines at spaces so the text fits the width of the rect.")]
    [SerializeField] private bool wrap;
    [SerializeField] private Align alignment = Align.Center;

    private readonly List<string> lines = new();

    public override Texture mainTexture => PixelFont.Atlas;

    public string Text
    {
        get => text;
        set
        {
            if (text == value) return;
            text = value;
            SetVerticesDirty();
        }
    }

    public float PixelSize
    {
        get => pixelSize;
        set
        {
            pixelSize = Mathf.Max(1f, value);
            SetVerticesDirty();
        }
    }

    /// <summary>Gap between letters, in font pixels.</summary>
    public float Spacing
    {
        get => spacing;
        set
        {
            spacing = Mathf.Max(0f, value);
            SetVerticesDirty();
        }
    }

    /// <summary>Gap between lines, in font pixels.</summary>
    public float LineGap
    {
        get => lineGap;
        set
        {
            lineGap = Mathf.Max(0f, value);
            SetVerticesDirty();
        }
    }

    public Align Alignment
    {
        get => alignment;
        set
        {
            alignment = value;
            SetVerticesDirty();
        }
    }

    public bool Wrap
    {
        get => wrap;
        set
        {
            wrap = value;
            SetVerticesDirty();
        }
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    private float WidthOf(string s) => s.Length * (PixelFont.GlyphWidth + spacing) * pixelSize - spacing * pixelSize;

    private void BuildLines(float maxWidth)
    {
        lines.Clear();
        foreach (string paragraph in text.Split('\n'))
        {
            if (!wrap)
            {
                lines.Add(paragraph);
                continue;
            }

            string current = "";
            foreach (string word in paragraph.Split(' '))
            {
                string candidate = current.Length == 0 ? word : current + " " + word;
                if (current.Length > 0 && WidthOf(candidate) > maxWidth)
                {
                    lines.Add(current);
                    current = word;
                }
                else
                {
                    current = candidate;
                }
            }
            lines.Add(current);
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (string.IsNullOrEmpty(text)) return;

        Rect rect = GetPixelAdjustedRect();
        BuildLines(rect.width);

        float advance = (PixelFont.GlyphWidth + spacing) * pixelSize;
        float glyphW = PixelFont.GlyphWidth * pixelSize;
        float glyphH = PixelFont.GlyphHeight * pixelSize;
        float lineStep = glyphH + lineGap * pixelSize;
        float blockHeight = lines.Count * glyphH + (lines.Count - 1) * lineGap * pixelSize;
        float top = rect.center.y + blockHeight * 0.5f;

        Color32 tint = color;
        int vertex = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            float width = WidthOf(line);
            float x = alignment switch
            {
                Align.Left => rect.xMin,
                Align.Right => rect.xMax - width,
                _ => rect.center.x - width * 0.5f
            };
            float y = top - glyphH - i * lineStep;

            foreach (char c in line)
            {
                int index = PixelFont.IndexOf(c);
                if (index > 0) // 0 is the blank space glyph
                {
                    PixelFont.GetUv(index, out Vector2 uvMin, out Vector2 uvMax);
                    vh.AddVert(new Vector3(x, y), tint, new Vector2(uvMin.x, uvMin.y));
                    vh.AddVert(new Vector3(x, y + glyphH), tint, new Vector2(uvMin.x, uvMax.y));
                    vh.AddVert(new Vector3(x + glyphW, y + glyphH), tint, new Vector2(uvMax.x, uvMax.y));
                    vh.AddVert(new Vector3(x + glyphW, y), tint, new Vector2(uvMax.x, uvMin.y));
                    vh.AddTriangle(vertex, vertex + 1, vertex + 2);
                    vh.AddTriangle(vertex + 2, vertex + 3, vertex);
                    vertex += 4;
                }
                x += advance;
            }
        }
    }
}
