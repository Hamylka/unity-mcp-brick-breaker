using UnityEngine;

/// <summary>
/// Tiny built-in 5x7 pixel font, generated into a texture at runtime (no font asset needed).
/// Contains A-Z, 0-9 and a few punctuation marks. Lowercase is drawn as uppercase.
/// Add a glyph by adding one line to <see cref="Data"/>: the character, then 7 rows of 5 cells ('#' = pixel).
/// </summary>
public static class PixelFont
{
    public const int GlyphWidth = 5;
    public const int GlyphHeight = 7;

    private const int CellWidth = GlyphWidth + 2;   // 1 px of padding on each side
    private const int CellHeight = GlyphHeight + 2;

    private const string Data = @"
A .###. #...# #...# ##### #...# #...# #...#
B ####. #...# #...# ####. #...# #...# ####.
C .###. #...# #.... #.... #.... #...# .###.
D ####. #...# #...# #...# #...# #...# ####.
E ##### #.... #.... ####. #.... #.... #####
F ##### #.... #.... ####. #.... #.... #....
G .###. #...# #.... #.### #...# #...# .####
H #...# #...# #...# ##### #...# #...# #...#
I .###. ..#.. ..#.. ..#.. ..#.. ..#.. .###.
J ..### ...#. ...#. ...#. ...#. #..#. .##..
K #...# #..#. #.#.. ##... #.#.. #..#. #...#
L #.... #.... #.... #.... #.... #.... #####
M #...# ##.## #.#.# #.#.# #...# #...# #...#
N #...# ##..# #.#.# #..## #...# #...# #...#
O .###. #...# #...# #...# #...# #...# .###.
P ####. #...# #...# ####. #.... #.... #....
Q .###. #...# #...# #...# #.#.# #..#. .##.#
R ####. #...# #...# ####. #.#.. #..#. #...#
S .#### #.... #.... .###. ....# ....# ####.
T ##### ..#.. ..#.. ..#.. ..#.. ..#.. ..#..
U #...# #...# #...# #...# #...# #...# .###.
V #...# #...# #...# #...# #...# .#.#. ..#..
W #...# #...# #...# #.#.# #.#.# ##.## #...#
X #...# #...# .#.#. ..#.. .#.#. #...# #...#
Y #...# #...# .#.#. ..#.. ..#.. ..#.. ..#..
Z ##### ....# ...#. ..#.. .#... #.... #####
0 .###. #...# #..## #.#.# ##..# #...# .###.
1 ..#.. .##.. ..#.. ..#.. ..#.. ..#.. .###.
2 .###. #...# ....# ...#. ..#.. .#... #####
3 ####. ....# ....# .###. ....# ....# ####.
4 ...#. ..##. .#.#. #..#. ##### ...#. ...#.
5 ##### #.... ####. ....# ....# #...# .###.
6 ..##. .#... #.... ####. #...# #...# .###.
7 ##### ....# ...#. ..#.. .#... .#... .#...
8 .###. #...# #...# .###. #...# #...# .###.
9 .###. #...# #...# .#### ....# ...#. .##..
! ..#.. ..#.. ..#.. ..#.. ..#.. ..... ..#..
? .###. #...# ....# ...#. ..#.. ..... ..#..
/ ....# ....# ...#. ..#.. .#... #.... #....
% ##..# ##..# ...#. ..#.. .#... #..## #..##
+ ..... ..#.. ..#.. ##### ..#.. ..#.. .....
- ..... ..... ..... ##### ..... ..... .....
: ..... .##.. .##.. ..... .##.. .##.. .....
. ..... ..... ..... ..... ..... .##.. .##..
, ..... ..... ..... ..... .##.. ..#.. .#...
' ..#.. ..#.. .#... ..... ..... ..... .....
";

    private static Texture2D atlas;
    private static string characters;

    public static Texture2D Atlas
    {
        get
        {
            if (atlas == null) Build();
            return atlas;
        }
    }

    /// <summary>Index of a glyph in the atlas (0 = blank space), or -1 for unknown characters.</summary>
    public static int IndexOf(char c)
    {
        if (atlas == null) Build();
        return characters.IndexOf(char.ToUpperInvariant(c));
    }

    public static void GetUv(int index, out Vector2 min, out Vector2 max)
    {
        float w = atlas.width;
        float h = atlas.height;
        min = new Vector2((index * CellWidth + 1) / w, 1f / h);
        max = new Vector2((index * CellWidth + 1 + GlyphWidth) / w, (1 + GlyphHeight) / h);
    }

    private static void Build()
    {
        string[] lines = Data.Replace("\r", "").Split('\n');
        var chars = new System.Text.StringBuilder(" ");
        var rows = new System.Collections.Generic.List<string[]>();
        rows.Add(null); // space

        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            string[] parts = line.Split(' ');
            if (parts.Length != 1 + GlyphHeight) continue;
            chars.Append(parts[0][0]);
            rows.Add(parts[1..]);
        }

        characters = chars.ToString();
        var texture = new Texture2D(CellWidth * characters.Length, CellHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            name = "PixelFontAtlas"
        };

        var pixels = new Color32[texture.width * texture.height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 0);

        for (int g = 1; g < rows.Count; g++)
        {
            for (int row = 0; row < GlyphHeight; row++)
            {
                for (int col = 0; col < GlyphWidth; col++)
                {
                    if (rows[g][row][col] != '#') continue;
                    int x = g * CellWidth + 1 + col;
                    int y = 1 + (GlyphHeight - 1 - row); // row 0 is the top of the glyph
                    pixels[y * texture.width + x] = new Color32(255, 255, 255, 255);
                }
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        atlas = texture;
    }
}
