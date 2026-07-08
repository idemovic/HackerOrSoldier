using System.Collections.Generic;
using Godot;

namespace HackerOrSoldier;

// Builds the TileSet at runtime: pixel-art 32x32 tiles in the README
// aesthetic (VGA-style saturated colors, checkerboard dithering), with a
// collision square on solid tiles. No binary assets.
public static class TileSetBuilder
{
    public const int TileSize = 32;
    public const int SourceId = 0;

    private static readonly Dictionary<int, ImageTexture> IconCache = new();

    public static Vector2I AtlasCoordsFor(int tileId)
    {
        for (int i = 0; i < TileDefs.All.Length; i++)
            if (TileDefs.All[i].Id == tileId)
                return new Vector2I(i, 0);
        return Vector2I.Zero;
    }

    public static TileSet Build()
    {
        var defs = TileDefs.All;
        var image = Image.CreateEmpty(TileSize * defs.Length, TileSize, false, Image.Format.Rgba8);
        for (int i = 0; i < defs.Length; i++)
            image.BlitRect(BuildTileImage(defs[i].Id), new Rect2I(0, 0, TileSize, TileSize), new Vector2I(i * TileSize, 0));

        var tileSet = new TileSet { TileSize = new Vector2I(TileSize, TileSize) };
        tileSet.AddPhysicsLayer();

        var source = new TileSetAtlasSource
        {
            Texture = ImageTexture.CreateFromImage(image),
            TextureRegionSize = new Vector2I(TileSize, TileSize),
        };
        tileSet.AddSource(source, SourceId);

        float half = TileSize / 2f;
        var square = new Vector2[] { new(-half, -half), new(half, -half), new(half, half), new(-half, half) };
        for (int i = 0; i < defs.Length; i++)
        {
            var coords = new Vector2I(i, 0);
            source.CreateTile(coords);
            if (defs[i].Solid)
            {
                var data = source.GetTileData(coords, 0);
                data.AddCollisionPolygon(0);
                data.SetCollisionPolygonPoints(0, 0, square);
            }
        }
        return tileSet;
    }

    // Small version for palette buttons.
    public static ImageTexture TileIcon(int tileId)
    {
        if (IconCache.TryGetValue(tileId, out var cached))
            return cached;
        var img = BuildTileImage(tileId);
        img.Resize(16, 16, Image.Interpolation.Nearest);
        var texture = ImageTexture.CreateFromImage(img);
        IconCache[tileId] = texture;
        return texture;
    }

    private static Image BuildTileImage(int tileId)
    {
        var img = Image.CreateEmpty(TileSize, TileSize, false, Image.Format.Rgba8);
        var bg = new Color("141419");
        switch (tileId)
        {
            case TileDefs.Wall:
                DrawBricks(img, new Color("353b4a"), new Color("434a5c"), new Color("10101a"));
                break;
            case TileDefs.DestructibleWall:
                DrawBricks(img, new Color("8a5a2b"), new Color("a06a35"), new Color("3a2712"));
                for (int i = 0; i < TileSize; i++)
                {
                    img.SetPixel(i, Wrap(i + 9), new Color("3a2712"));   // diagonal crack
                    if (i % 3 != 0)
                        img.SetPixel(Wrap(i + 4), i, new Color("3a2712"));
                }
                break;
            case TileDefs.AlarmFloor:
                for (int y = 0; y < TileSize; y++)
                    for (int x = 0; x < TileSize; x++)
                        img.SetPixel(x, y, ((x + y) / 4) % 2 == 0 ? new Color("f2d43b") : new Color("141419"));
                break;
            case TileDefs.ElectricFloor:
                img.Fill(new Color("22242c"));
                for (int y = 0; y < TileSize; y++)
                    for (int x = 0; x < TileSize; x++)
                    {
                        if (y is >= 12 and < 20 && (x + y * 2) % 8 < 2)
                            img.SetPixel(x, y, new Color("f2d43b"));   // zigzag arc
                        else if ((x * 7 + y * 13) % 37 == 0)
                            img.SetPixel(x, y, new Color("45d6d6"));   // sparks
                    }
                break;
            case TileDefs.Water:
                img.Fill(new Color("1f3a8f"));
                for (int y = 0; y < TileSize; y++)
                    for (int x = 0; x < TileSize; x++)
                    {
                        if (y < 4 && (x + y) % 2 == 0)
                            img.SetPixel(x, y, new Color("45d6d6"));   // dithered surface
                        else if (y % 8 == 5 && x % 4 < 2)
                            img.SetPixel(x, y, new Color("3b6fe0"));   // waves
                    }
                break;
            case TileDefs.Lava:
                img.Fill(new Color("8f1f1a"));
                for (int y = 0; y < TileSize; y++)
                    for (int x = 0; x < TileSize; x++)
                    {
                        if ((x / 2 + y / 2) % 2 == 0 && (x + y) % 3 != 0)
                            img.SetPixel(x, y, new Color("f28022"));   // dithered glow
                        else if ((x * 5 + y * 11) % 53 == 0)
                            img.SetPixel(x, y, new Color("f2d43b"));   // sparks
                    }
                break;
            case TileDefs.Spikes:
                img.Fill(bg);
                img.FillRect(new Rect2I(0, 28, TileSize, 4), new Color("2a2a33"));
                for (int spike = 0; spike < 4; spike++)
                    for (int y = 6; y < 28; y++)
                    {
                        int halfWidth = (y - 4) / 6 + 1;
                        int center = spike * 8 + 4;
                        for (int x = center - halfWidth; x <= center + halfWidth; x++)
                            img.SetPixel(Mathf.Clamp(x, 0, 31), y,
                                x < center ? new Color("c9c9d4") : new Color("7d7d8a"));
                    }
                break;
            case TileDefs.Spear:
                img.Fill(bg);
                img.FillRect(new Rect2I(14, 10, 4, 22), new Color("8a5a2b"));       // shaft
                img.FillRect(new Rect2I(10, 12, 12, 2), new Color("55555f"));       // guard
                for (int y = 2; y < 10; y++)
                {
                    int halfWidth = (y - 1) / 2;
                    for (int x = 16 - halfWidth; x < 16 + halfWidth; x++)
                        img.SetPixel(x, y, x < 16 ? new Color("f2f2f2") : new Color("9a9aa5"));
                }
                break;
            case TileDefs.HiddenPlatform:
                img.Fill(new Color("10202a"));
                for (int i = 0; i < TileSize; i++)
                {
                    img.SetPixel(i, 0, new Color("45d6d6"));
                    img.SetPixel(i, 31, new Color("45d6d6"));
                    img.SetPixel(0, i, new Color("45d6d6"));
                    img.SetPixel(31, i, new Color("45d6d6"));
                }
                for (int y = 1; y < 31; y++)
                    for (int x = 1; x < 31; x++)
                        if (x % 4 == 1 && y % 4 == 1)
                            img.SetPixel(x, y, new Color("1f8f8f"));
                break;
            case TileDefs.StoneBlocks:
                DrawStoneBlocks(img);
                break;
            case TileDefs.RedBrick:
                DrawBricks(img, new Color("8f1f1a"), new Color("b03a28"), new Color("2a1010"));
                break;
            case TileDefs.SteelPlate:
                for (int y = 0; y < TileSize; y++)
                    for (int x = 0; x < TileSize; x++)
                    {
                        int lx = x % 16, ly = y % 16;
                        Color c = new Color("7a8aa5");
                        if (lx == 15 || ly == 15)
                            c = new Color("4a5570");                     // plate seams
                        else if (lx == 0 || ly == 0)
                            c = new Color("9a9aa5");                     // lit edge
                        else if (lx is 3 or 4 && ly is 3 or 4 || lx is 11 or 12 && ly is 11 or 12
                            || lx is 3 or 4 && ly is 11 or 12 || lx is 11 or 12 && ly is 3 or 4)
                            c = new Color("2a2a33");                     // corner rivets
                        else if (ly > 8 && (x + y) % 2 == 0)
                            c = new Color("6f7e99");                     // dithered lower half
                        img.SetPixel(x, y, c);
                    }
                break;
            case TileDefs.Girder:
                img.Fill(bg);
                for (int y = 0; y < TileSize; y++)
                    for (int x = 0; x < TileSize; x++)
                    {
                        if (y < 6 || y > 25)
                        {
                            Color c = new Color("7a8aa5");               // flanges
                            if (y is 0 or 26)
                                c = new Color("9a9aa5");
                            else if (y is 5 or 31)
                                c = new Color("4a5570");
                            else if (x % 8 is 3 or 4 && y is 2 or 3 or 28 or 29)
                                c = new Color("2a2a33");                 // bolts
                            img.SetPixel(x, y, c);
                        }
                        else if ((x + y) % 12 < 2 || (x - y + 96) % 12 < 2)
                            img.SetPixel(x, y, new Color("55555f"));     // lattice web
                    }
                break;
            case TileDefs.BackgroundMesh:
                for (int y = 0; y < TileSize; y++)
                    for (int x = 0; x < TileSize; x++)
                    {
                        Color c = new Color("16181f");
                        if (x % 8 == 7 || y % 8 == 7)
                            c = new Color("0e0f14");                     // mesh seams
                        else if ((x + y) % 4 == 0)
                            c = new Color("1e2129");                     // faint dither
                        img.SetPixel(x, y, c);
                    }
                break;
            case TileDefs.BackgroundRock:
                for (int y = 0; y < TileSize; y++)
                    for (int x = 0; x < TileSize; x++)
                    {
                        Color c = new Color("3a3d47");
                        if ((x / 2 + y / 2) % 2 == 0)
                            c = new Color("424653");                     // coarse dither
                        if ((x * 3 + y * 7) % 31 == 0 || y == Wrap(x + 13))
                            c = new Color("2e313a");                     // pits and cracks
                        img.SetPixel(x, y, c);
                    }
                break;
            case TileDefs.BackgroundPanel:
                for (int y = 0; y < TileSize; y++)
                    for (int x = 0; x < TileSize; x++)
                    {
                        Color c = new Color("1e2230");
                        if (x % 16 == 15 || y % 16 == 15)
                            c = new Color("141824");                     // panel seams
                        else if ((x + y) % 32 < 8 && (x + y) % 2 == 0)
                            c = new Color("323a52");                     // diagonal light streak
                        img.SetPixel(x, y, c);
                    }
                break;
            default:
                img.Fill(TileDefs.Get(tileId)?.Color ?? bg);
                break;
        }
        return img;
    }

    // Four rounded stone blocks per tile, lit from the top left.
    private static void DrawStoneBlocks(Image img)
    {
        img.Fill(new Color("10101a"));
        for (int by = 0; by < 2; by++)
            for (int bx = 0; bx < 2; bx++)
                for (int y = 1; y < 15; y++)
                    for (int x = 1; x < 15; x++)
                    {
                        if (x is 1 or 14 && y is 1 or 14)
                            continue;   // rounded corners keep the seam color
                        Color c = new Color("55555f");
                        if (x == 1 || y == 1)
                            c = new Color("7d7d8a");
                        else if (x == 14 || y == 14)
                            c = new Color("2a2a33");
                        else if (y > 8 && (x + y) % 2 == 0)
                            c = new Color("4a4a55");
                        img.SetPixel(bx * 16 + x, by * 16 + y, c);
                    }
    }

    private static void DrawBricks(Image img, Color brick, Color highlight, Color mortar)
    {
        img.Fill(brick);
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
            {
                int rowOffset = (y / 8) % 2 == 0 ? 0 : 8;
                if (y % 8 == 7 || (x + rowOffset) % 16 == 15)
                    img.SetPixel(x, y, mortar);
                else if (y % 8 == 0 && x % 2 == 0)
                    img.SetPixel(x, y, highlight);   // dithered brick top
            }
    }

    private static int Wrap(int value) => ((value % TileSize) + TileSize) % TileSize;
}
