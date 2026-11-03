using System.Collections.Generic;
using Godot;

namespace HackerOrSoldier;

// Hand-drawn bitmap font so UI text has the same chunky pixels as the
// sprites instead of Godot's smooth default. Autoloaded, so every scene
// (game, editor, smoke test) gets it as the default font.
//
// Each sheet lists its characters, then the rows of their glyphs: 5-wide
// cells separated by a space, '#' set. Rows 0-6 sit above the baseline and
// rows 7-8 hold descenders; a sheet may stop after row 6. Glyphs are trimmed
// to their used columns, so the font is proportional.
public partial class PixelFont : Node
{
    // Font sizes are integer multiples of this: 8-15 draw at 1x, 16+ at 2x.
    private const int BaseSize = 8;
    private const int Rows = 9;
    private const int Baseline = 7;
    private const int SpaceAdvance = 3;

    private static readonly (string Chars, string[] Rows)[] Sheets =
    {
        ("ABCDEFGH", new[]
        {
            ".###. ####. .###. ####. ##### ##### .###. #...#",
            "#...# #...# #...# #...# #.... #.... #...# #...#",
            "#...# #...# #.... #...# #.... #.... #.... #...#",
            "##### ####. #.... #...# ####. ####. #.### #####",
            "#...# #...# #.... #...# #.... #.... #...# #...#",
            "#...# #...# #...# #...# #.... #.... #...# #...#",
            "#...# ####. .###. ####. ##### #.... .#### #...#",
        }),
        ("IJKLMNOP", new[]
        {
            ".###. ..### #...# #.... #...# #...# .###. ####.",
            "..#.. ...#. #..#. #.... ##.## #...# #...# #...#",
            "..#.. ...#. #.#.. #.... #.#.# ##..# #...# #...#",
            "..#.. ...#. ##... #.... #.#.# #.#.# #...# ####.",
            "..#.. ...#. #.#.. #.... #...# #..## #...# #....",
            "..#.. #..#. #..#. #.... #...# #...# #...# #....",
            ".###. .##.. #...# ##### #...# #...# .###. #....",
        }),
        ("QRSTUVWX", new[]
        {
            ".###. ####. .#### ##### #...# #...# #...# #...#",
            "#...# #...# #.... ..#.. #...# #...# #...# #...#",
            "#...# #...# #.... ..#.. #...# #...# #...# .#.#.",
            "#...# ####. .###. ..#.. #...# #...# #.#.# ..#..",
            "#.#.# #.#.. ....# ..#.. #...# #...# #.#.# .#.#.",
            "#..#. #..#. ....# ..#.. #...# .#.#. #.#.# #...#",
            ".##.# #...# ####. ..#.. .###. ..#.. .#.#. #...#",
        }),
        ("YZ012345", new[]
        {
            "#...# ##### .###. ..#.. .###. ####. ...#. #####",
            "#...# ....# #...# .##.. #...# ....# ..##. #....",
            ".#.#. ...#. #..## ..#.. ....# ....# .#.#. ####.",
            "..#.. ..#.. #.#.# ..#.. ...#. .###. #..#. ....#",
            "..#.. .#... ##..# ..#.. ..#.. ....# ##### ....#",
            "..#.. #.... #...# ..#.. .#... ....# ...#. #...#",
            "..#.. ##### .###. .###. ##### ####. ...#. .###.",
        }),
        ("6789!?.,", new[]
        {
            "..##. ##### .###. .###. ..#.. .###. ..... .....",
            ".#... ....# #...# #...# ..#.. #...# ..... .....",
            "#.... ...#. #...# #...# ..#.. ....# ..... .....",
            "####. ..#.. .###. .#### ..#.. ...#. ..... .....",
            "#...# .#... #...# ....# ..#.. ..#.. ..... .....",
            "#...# .#... #...# ...#. ..... ..... ..... ..#..",
            ".###. .#... .###. .##.. ..#.. ..#.. ..#.. ..#..",
            "..... ..... ..... ..... ..... ..... ..... .#...",
        }),
        (":;'\"-+=/", new[]
        {
            "..... ..... ..#.. .#.#. ..... ..... ..... ....#",
            "..... ..... ..#.. .#.#. ..... ..#.. ..... ...#.",
            "..#.. ..#.. ..... ..... ..... ..#.. ##### ...#.",
            "..... ..... ..... ..... .###. ##### ..... ..#..",
            "..... ..... ..... ..... ..... ..#.. ##### .#...",
            "..#.. ..#.. ..... ..... ..... ..#.. ..... .#...",
            "..... ..#.. ..... ..... ..... ..... ..... #....",
            "..... .#... ..... ..... ..... ..... ..... .....",
        }),
        ("()[]<>_%", new[]
        {
            "...#. .#... .###. .###. ...#. .#... ..... ##..#",
            "..#.. ..#.. .#... ...#. ..#.. ..#.. ..... ##..#",
            ".#... ...#. .#... ...#. .#... ...#. ..... ...#.",
            ".#... ...#. .#... ...#. #.... ....# ..... ..#..",
            ".#... ...#. .#... ...#. .#... ...#. ..... .#...",
            "..#.. ..#.. .#... ...#. ..#.. ..#.. ..... #..##",
            "...#. .#... .###. .###. ...#. .#... ..... #..##",
            "..... ..... ..... ..... ..... ..... ##### .....",
        }),
        ("#$&*@\\^`", new[]
        {
            ".#.#. ..#.. .##.. ..... .###. #.... ..#.. .#...",
            ".#.#. .#### #..#. ..#.. #...# .#... .#.#. ..#..",
            "##### #.#.. #.#.. #.#.# #.### .#... #...# .....",
            ".#.#. .###. .#... .###. #.#.# ..#.. ..... .....",
            "##### ..#.# #.#.# #.#.# #.### ...#. ..... .....",
            ".#.#. ####. #..#. ..#.. #.... ...#. ..... .....",
            ".#.#. ..#.. .##.# ..... .#### ....# ..... .....",
        }),
        ("{|}~", new[]
        {
            "...#. ..#.. .#... .....",
            "..#.. ..#.. ..#.. .....",
            "..#.. ..#.. ..#.. .#...",
            ".#... ..#.. ...#. #.#.#",
            "..#.. ..#.. ..#.. ...#.",
            "..#.. ..#.. ..#.. .....",
            "...#. ..#.. .#... .....",
        }),
        ("abcdefgh", new[]
        {
            "..... #.... ..... ....# ..... ..##. ..... #....",
            "..... #.... ..... ....# ..... .#... ..... #....",
            ".###. ####. .###. .#### .###. ####. .#### ####.",
            "....# #...# #.... #...# #...# .#... #...# #...#",
            ".#### #...# #.... #...# ##### .#... #...# #...#",
            "#...# #...# #.... #...# #.... .#... #...# #...#",
            ".#### ####. .###. .#### .###. .#... .#### #...#",
            "..... ..... ..... ..... ..... ..... ....# .....",
            "..... ..... ..... ..... ..... ..... .###. .....",
        }),
        ("ijklmnop", new[]
        {
            "..#.. ...#. #.... .#... ..... ..... ..... .....",
            "..... ..... #.... .#... ..... ..... ..... .....",
            "..#.. ...#. #..#. .#... ##.#. ####. .###. ####.",
            "..#.. ...#. #.#.. .#... #.#.# #...# #...# #...#",
            "..#.. ...#. ##... .#... #.#.# #...# #...# #...#",
            "..#.. ...#. #.#.. .#... #.#.# #...# #...# #...#",
            "..#.. ...#. #..#. ..#.. #.#.# #...# .###. ####.",
            "..... ...#. ..... ..... ..... ..... ..... #....",
            "..... .##.. ..... ..... ..... ..... ..... #....",
        }),
        ("qrstuvwx", new[]
        {
            "..... ..... ..... .#... ..... ..... ..... .....",
            "..... ..... ..... .#... ..... ..... ..... .....",
            ".#### #.##. .#### ####. #...# #...# #...# #...#",
            "#...# ##... #.... .#... #...# #...# #...# .#.#.",
            "#...# #.... .###. .#... #...# #...# #.#.# ..#..",
            "#...# #.... ....# .#... #...# .#.#. #.#.# .#.#.",
            ".#### #.... ####. ..##. .#### ..#.. .#.#. #...#",
            "....# ..... ..... ..... ..... ..... ..... .....",
            "....# ..... ..... ..... ..... ..... ..... .....",
        }),
        ("yz", new[]
        {
            "..... .....",
            "..... .....",
            "#...# #####",
            "#...# ...#.",
            "#...# ..#..",
            "#...# .#...",
            ".#### #####",
            "....# .....",
            ".###. .....",
        }),
    };

    public override void _EnterTree()
    {
        // The engine's default theme carries its own font, which controls
        // find before the fallback, so both need replacing.
        var font = Build(ThemeDB.FallbackFont);
        ThemeDB.FallbackFont = font;
        ThemeDB.GetDefaultTheme().DefaultFont = font;
    }

    private static FontFile Build(Font fallback)
    {
        var glyphs = new List<(char Char, bool[,] Pixels)>();
        foreach (var (chars, rows) in Sheets)
            for (int i = 0; i < chars.Length; i++)
                glyphs.Add((chars[i], Cut(chars, rows, i)));

        int atlasWidth = 0;
        foreach (var (_, pixels) in glyphs)
            atlasWidth += pixels.GetLength(0) + 1;
        var atlas = Image.CreateEmpty(atlasWidth, Rows, false, Image.Format.La8);

        var font = new FontFile
        {
            FixedSize = BaseSize,
            FixedSizeScaleMode = TextServer.FixedSizeScaleMode.IntegerOnly,
            Antialiasing = TextServer.FontAntialiasing.None,
            Hinting = TextServer.Hinting.None,
            SubpixelPositioning = TextServer.SubpixelPositioning.Disabled,
            GenerateMipmaps = false,
            Fallbacks = new Godot.Collections.Array<Font> { fallback },
        };
        var size = new Vector2I(BaseSize, 0);
        font.SetCacheAscent(0, BaseSize, Baseline + 1);
        font.SetCacheDescent(0, BaseSize, Rows - Baseline);

        int x = 0;
        foreach (var (ch, pixels) in glyphs)
        {
            int width = pixels.GetLength(0);
            for (int px = 0; px < width; px++)
                for (int py = 0; py < Rows; py++)
                    if (pixels[px, py])
                        atlas.SetPixel(x + px, py, Colors.White);
            font.SetGlyphAdvance(0, BaseSize, ch, new Vector2(width + 1, 0));
            font.SetGlyphOffset(0, size, ch, new Vector2(0, -Baseline));
            font.SetGlyphSize(0, size, ch, new Vector2(width, Rows));
            font.SetGlyphUVRect(0, size, ch, new Rect2(x, 0, width, Rows));
            font.SetGlyphTextureIdx(0, size, ch, 0);
            x += width + 1;
        }
        font.SetGlyphAdvance(0, BaseSize, ' ', new Vector2(SpaceAdvance, 0));
        font.SetTextureImage(0, size, 0, atlas);
        return font;
    }

    // Glyph i of a sheet, trimmed to its used columns: [column, row].
    private static bool[,] Cut(string chars, string[] rows, int i)
    {
        int first = 5, last = -1;
        for (int y = 0; y < rows.Length; y++)
        {
            if (rows[y].Length != chars.Length * 6 - 1)
                throw new System.Exception($"font sheet '{chars}' row {y} has length {rows[y].Length}");
            for (int c = 0; c < 5; c++)
                if (rows[y][i * 6 + c] == '#')
                {
                    first = Mathf.Min(first, c);
                    last = Mathf.Max(last, c);
                }
        }
        if (last < 0)
            throw new System.Exception($"font glyph '{chars[i]}' is empty");
        var pixels = new bool[last - first + 1, Rows];
        for (int y = 0; y < rows.Length; y++)
            for (int c = first; c <= last; c++)
                pixels[c - first, y] = rows[y][i * 6 + c] == '#';
        return pixels;
    }
}
