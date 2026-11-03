using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace HackerOrSoldier;

// Which neighbours of a shadow-receiving cell are solid. Light falls from
// the top left, so a ceiling shades a band downward, a wall on the left a
// band rightward, and a floor leaves a thin contact line. Each combination
// is one pre-baked tile in the atlas.
[Flags]
public enum ShadowMask { None = 0, Above = 1, Left = 2, Below = 4 }

// Builds the TileSet at runtime: pixel-art 32x32 tiles in the README
// aesthetic (VGA-style saturated colors, ordered dithering, light from the
// top left), with a collision square on solid tiles. No binary assets.
public static class TileSetBuilder
{
    public const int TileSize = 32;
    public const int SourceId = 0;

    // Night-sky star layouts. One level tile id and one palette entry; the
    // variant is picked per cell from its coordinates, so nothing extra is
    // serialized and a reload always reproduces the same sky. Variant 0 sits
    // in the defs row of the atlas, the rest in a second row.
    public const int SkyVariants = 10;

    // Water, lava and spikes animate via native TileSet animations; their
    // frames fill dedicated atlas rows below the defs row and the sky
    // variants. The shadow masks take the last row, one column per mask.
    private const int WaterFrames = 4;
    private const int LavaFrames = 3;
    private const int SpikeFrames = 8;
    private static readonly Vector2I WaterCoords = new(0, 2);
    private static readonly Vector2I LavaCoords = new(0, 3);
    private static readonly Vector2I SpikesCoords = new(0, 4);
    private const int ShadowRow = 5;
    private const int ShadowMasks = 8;
    private const int AtlasRows = 6;

    // Custom data layer: every atlas tile carries the level tile id it
    // draws, so a grid cell reads back without inverting GridCoordsFor.
    public const string TileIdData = "tile_id";

    private static readonly Dictionary<int, ImageTexture> IconCache = new();

    // Tone ramps, darkest first: shaded edge, dark partner, body, lit edge,
    // glint. Tone() dithers between neighbours, so a fractional index mixes
    // two tones in Bayer order. Backgrounds keep a narrow, dark ramp so the
    // playfield reads over them.
    private static readonly Color[] WallRamp = Ramp("141827", "262c3d", "3a4257", "4e5872", "6b7795");
    private static readonly Color[] ClayRamp = Ramp("3a2712", "6b4420", "8a5a2b", "a8713a", "c48a4a");
    private static readonly Color[] RedRamp = Ramp("4a0e0c", "6e1a14", "8f1f1a", "b03a28", "d0553a");
    private static readonly Color[] StoneRamp = Ramp("22222b", "3c3c47", "55555f", "71717e", "8e8e9c");
    private static readonly Color[] SteelRamp = Ramp("2e3549", "4a5570", "6f7e99", "8c9ab5", "b4c0d6");
    private static readonly Color[] IronRamp = Ramp("1a1d28", "2e3340", "444a5a", "5c6478", "7c8599");
    private static readonly Color[] ConcreteRamp = Ramp("3a3d47", "50535e", "6a6d78", "7d818e", "8f939f");
    private static readonly Color[] SandRamp = Ramp("5a4526", "8a6a3d", "b5915a", "cfa96b", "e3c080");
    private static readonly Color[] GreenRamp = Ramp("163016", "22471f", "2e5c2a", "3a7334", "4c9042");
    private static readonly Color[] TealRamp = Ramp("0b3838", "11504f", "1f8f8f", "2fb0b0", "45d6d6");
    private static readonly Color[] PlateRamp = Ramp("121318", "1a1c22", "22242c", "2e313b", "3c404c");
    private static readonly Color[] HazardRamp = Ramp("8a7320", "c4a428", "f2d43b", "f8e46b", "fff28a");
    private static readonly Color[] BladeRamp = Ramp("4a4a55", "7d7d8a", "a0a0ac", "c9c9d4", "f2f2f2");
    private static readonly Color[] CopperRamp = Ramp("5a2e10", "7a4418", "a05a22", "c47a30", "e0a050");
    private static readonly Color[] DimBrickRamp = Ramp("0e0f14", "1a1c23", "23252e", "2b2e38", "343845");
    private static readonly Color[] RockRamp = Ramp("1a1c23", "262930", "32353e", "3e424c", "4a4f5b");
    private static readonly Color[] PanelRamp = Ramp("141824", "1a1e2c", "1e2230", "262b3c", "303650");
    private static readonly Color[] HangarRamp = Ramp("15171d", "1c1f26", "23262e", "2a2e38", "343843");
    private static readonly Color[] HollowRamp = Ramp("07080b", "0e0f14", "14161c");
    private static readonly Color[] WaterRamp = Ramp("122660", "1a3380", "1f3a8f", "3b6fe0", "45d6d6", "c8f4f4");
    private static readonly Color[] LavaRamp = Ramp("3a0c0a", "6e1410", "b02a1a", "e85a20", "f2a030", "ffe066", "fff6c0");

    public static Vector2I AtlasCoordsFor(int tileId)
    {
        for (int i = 0; i < TileDefs.All.Length; i++)
            if (TileDefs.All[i].Id == tileId)
                return new Vector2I(i, 0);
        return Vector2I.Zero;
    }

    public static int SkyVariantFor(Vector2I cell) =>
        (int)((uint)(cell.X * 73856093 ^ cell.Y * 19349663) % SkyVariants);

    public static Vector2I NightSkyCoordsFor(Vector2I cell)
    {
        int variant = SkyVariantFor(cell);
        return variant == 0 ? AtlasCoordsFor(TileDefs.BackgroundSky) : new Vector2I(variant - 1, 1);
    }

    // Atlas coords for a painted cell: the night sky picks its per-cell
    // variant, water, lava and spikes point at their animated tiles.
    public static Vector2I GridCoordsFor(int tileId, Vector2I cell) => tileId switch
    {
        TileDefs.BackgroundSky => NightSkyCoordsFor(cell),
        TileDefs.Water => WaterCoords,
        TileDefs.Lava => LavaCoords,
        TileDefs.Spikes => SpikesCoords,
        _ => AtlasCoordsFor(tileId),
    };

    public static Vector2I ShadowCoordsFor(ShadowMask mask) => new((int)mask, ShadowRow);

    public static Image AtlasImage()
    {
        var defs = TileDefs.All;
        var tileRect = new Rect2I(0, 0, TileSize, TileSize);
        var image = Image.CreateEmpty(TileSize * defs.Length, TileSize * AtlasRows, false, Image.Format.Rgba8);
        void Put(Image tile, int column, int row) =>
            image.BlitRect(tile, tileRect, new Vector2I(column * TileSize, row * TileSize));
        for (int i = 0; i < defs.Length; i++)
            Put(BuildTileImage(defs[i].Id), i, 0);
        for (int v = 1; v < SkyVariants; v++)
            Put(BuildNightSkyImage(v), v - 1, 1);
        for (int f = 0; f < WaterFrames; f++)
            Put(BuildWaterImage(f), f, WaterCoords.Y);
        for (int f = 0; f < LavaFrames; f++)
            Put(BuildLavaImage(f), f, LavaCoords.Y);
        for (int f = 0; f < SpikeFrames; f++)
            Put(BuildSpikesImage(f), f, SpikesCoords.Y);
        for (int m = 1; m < ShadowMasks; m++)
            Put(BuildShadowImage((ShadowMask)m), m, ShadowRow);
        return image;
    }

    public static TileSet Build()
    {
        var defs = TileDefs.All;
        var tileSet = new TileSet { TileSize = new Vector2I(TileSize, TileSize) };
        tileSet.AddPhysicsLayer();
        tileSet.AddCustomDataLayer();
        tileSet.SetCustomDataLayerName(0, TileIdData);
        tileSet.SetCustomDataLayerType(0, Variant.Type.Int);

        var source = new TileSetAtlasSource
        {
            Texture = ImageTexture.CreateFromImage(AtlasImage()),
            TextureRegionSize = new Vector2I(TileSize, TileSize),
        };
        tileSet.AddSource(source, SourceId);

        TileData Create(Vector2I coords, int tileId)
        {
            source.CreateTile(coords);
            var data = source.GetTileData(coords, 0);
            data.SetCustomData(TileIdData, tileId);
            return data;
        }

        void Animated(Vector2I coords, int tileId, int frames, float frameDuration)
        {
            Create(coords, tileId);
            source.SetTileAnimationFramesCount(coords, frames);
            for (int f = 0; f < frames; f++)
                source.SetTileAnimationFrameDuration(coords, f, frameDuration);
        }

        float half = TileSize / 2f;
        var square = new Vector2[] { new(-half, -half), new(half, -half), new(half, half), new(-half, half) };
        for (int i = 0; i < defs.Length; i++)
        {
            var data = Create(new Vector2I(i, 0), defs[i].Id);
            if (defs[i].Solid)
            {
                data.AddCollisionPolygon(0);
                data.SetCollisionPolygonPoints(0, 0, square);
            }
        }
        for (int v = 1; v < SkyVariants; v++)
            Create(new Vector2I(v - 1, 1), TileDefs.BackgroundSky);
        Animated(WaterCoords, TileDefs.Water, WaterFrames, 0.25f);
        Animated(LavaCoords, TileDefs.Lava, LavaFrames, 0.3f);
        Animated(SpikesCoords, TileDefs.Spikes, SpikeFrames, 0.2f);
        for (int m = 1; m < ShadowMasks; m++)
            Create(ShadowCoordsFor((ShadowMask)m), TileDefs.Empty);
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
        switch (tileId)
        {
            case TileDefs.Wall:
                DrawBricks(img, WallRamp, new Color("0d0f18"), seed: 1);
                break;
            case TileDefs.DestructibleWall:
                DrawBricks(img, ClayRamp, new Color("2a1a0c"), seed: 2, wear: 0.1f);
                DrawCrack(img, ClayRamp);
                break;
            case TileDefs.AlarmFloor:
                DrawAlarmFloor(img);
                break;
            case TileDefs.ElectricFloor:
                DrawElectricFloor(img);
                break;
            case TileDefs.Water:
                return BuildWaterImage(0);
            case TileDefs.Lava:
                return BuildLavaImage(0);
            case TileDefs.Spikes:
                return BuildSpikesImage(4);
            case TileDefs.Spear:
                DrawSpears(img);
                break;
            case TileDefs.HiddenPlatform:
                DrawHiddenPlatform(img);
                break;
            case TileDefs.StoneBlocks:
                img.Fill(new Color("10101a"));
                for (int by = 0; by < 2; by++)
                    for (int bx = 0; bx < 2; bx++)
                        DrawBlock(img, new Rect2I(bx * 16 + 1, by * 16 + 1, 14, 14), StoneRamp, 10 + by * 2 + bx, rounded: true, wear: 0.07f);
                break;
            case TileDefs.RedBrick:
                DrawBricks(img, RedRamp, new Color("2a1010"), seed: 3);
                break;
            case TileDefs.SteelPlate:
                DrawSteelPlates(img);
                break;
            case TileDefs.Girder:
                DrawGirder(img);
                break;
            case TileDefs.Concrete:
                DrawConcrete(img);
                break;
            case TileDefs.Sandbags:
                DrawSandbags(img);
                break;
            case TileDefs.Container:
                DrawContainer(img);
                break;
            case TileDefs.PipeWall:
                DrawPipes(img);
                break;
            case TileDefs.BackgroundBrick:
                DrawBricks(img, DimBrickRamp, new Color("0a0b0f"), seed: 4, wear: 0.03f);
                break;
            case TileDefs.BackgroundCircuit:
                DrawCircuit(img);
                break;
            case TileDefs.BackgroundHangar:
                DrawHangar(img);
                break;
            case TileDefs.BackgroundSky:
                return BuildNightSkyImage(0);
            case TileDefs.BackgroundSkyDay:
                for (int y = 0; y < TileSize; y++)
                    for (int x = 0; x < TileSize; x++)
                        img.SetPixel(x, y, Noise(x, y, 25) < 0.004f ? new Color("5c8aec") : new Color("3b6fe0"));
                break;
            case TileDefs.BackgroundMesh:
                DrawMesh(img);
                break;
            case TileDefs.BackgroundRock:
                DrawRock(img);
                break;
            case TileDefs.BackgroundPanel:
                DrawPanels(img);
                break;
            default:
                img.Fill(TileDefs.Get(tileId)?.Color ?? new Color("141419"));
                break;
        }
        return img;
    }

    // Diagonal hazard stripes on a bevelled plate: each yellow band has a
    // lit upper edge and a dark lower edge so it reads as raised paint,
    // with scuffs and corner rivets.
    private static void DrawAlarmFloor(Image img)
    {
        var black = Ramp("0c0c10", "141419", "1c1c24", "2a2c36", "3a3c48");
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
            {
                int s = (x + y) % 8;
                Color c = s switch
                {
                    0 => HazardRamp[4],
                    1 or 2 => HazardRamp[2],
                    3 => HazardRamp[1],
                    4 => black[3],
                    5 or 6 => black[1],
                    _ => black[0],
                };
                if (x == 0 || y == 0)
                    c = s < 4 ? HazardRamp[4] : black[4];
                else if (x == 31 || y == 31)
                    c = s < 4 ? HazardRamp[0] : black[0];
                else if (Noise(x, y, 30) < 0.03f)
                    c = StoneRamp[1];
                img.SetPixel(x, y, c);
            }
        foreach (var (rx, ry) in new[] { (3, 3), (27, 3), (3, 27), (27, 27) })
            DrawRivet(img, rx, ry, SteelRamp);
    }

    // A dark steel plate with two copper electrodes and a lightning arc
    // between them: a pale core, a yellow glow and cyan sparks.
    private static void DrawElectricFloor(Image img)
    {
        DrawBlock(img, new Rect2I(0, 0, TileSize, TileSize), PlateRamp, 40, wear: 0.06f);
        DrawBlock(img, new Rect2I(2, 9, 3, 14), CopperRamp, 41, wear: 0f);
        DrawBlock(img, new Rect2I(27, 9, 3, 14), CopperRamp, 42, wear: 0f);
        foreach (var (rx, ry) in new[] { (12, 2), (18, 2), (12, 28), (18, 28) })
            DrawRivet(img, rx, ry, PlateRamp);
        for (int x = 5; x < 27; x++)
        {
            int yc = 15 + (x / 3 % 2 == 0 ? -2 : 2) + (x % 3 == 1 ? 1 : 0);
            var haze = Threshold(x, yc) < 0.5f ? HazardRamp[0] : PlateRamp[2];
            Dot(img, x, yc - 2, haze);
            Dot(img, x, yc + 2, haze);
            Dot(img, x, yc - 1, HazardRamp[2]);
            Dot(img, x, yc + 1, HazardRamp[2]);
            Dot(img, x, yc, new Color("fff7c0"));
        }
        for (int y = 6; y < 26; y++)
            for (int x = 5; x < 27; x++)
                if (Noise(x, y, 43) < 0.025f)
                    img.SetPixel(x, y, TealRamp[4]);
    }

    // Three spears on a dark pit floor: a two-tone shaft, a steel guard and
    // a blade lit along its left edge.
    private static void DrawSpears(Image img)
    {
        DrawPit(img, 44);
        foreach (int center in new[] { 6, 16, 26 })
        {
            for (int y = 14; y < 29; y++)
            {
                img.SetPixel(center - 1, y, ClayRamp[3]);
                img.SetPixel(center, y, ClayRamp[1]);
            }
            DrawBlock(img, new Rect2I(center - 4, 12, 8, 2), SteelRamp, 45, wear: 0f);
            for (int y = 2; y < 12; y++)
            {
                int halfWidth = (y - 1) / 3;
                for (int dx = -halfWidth; dx <= halfWidth; dx++)
                    img.SetPixel(center + dx, y, BladeTone(dx, halfWidth));
            }
            img.SetPixel(center, 1, Colors.White);
        }
    }

    // A hologram tile: cyan frame, diagonal stripe dither and a dot grid.
    private static void DrawHiddenPlatform(Image img)
    {
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
            {
                Color c = (x + y) % 8 == 0 ? new Color("163a44") : new Color("10202a");
                if (x % 4 == 1 && y % 4 == 1)
                    c = TealRamp[2];
                if (x == 0 || y == 0 || x == 31 || y == 31)
                    c = TealRamp[4];
                else if (x == 1 || y == 1 || x == 30 || y == 30)
                    c = TealRamp[1];
                img.SetPixel(x, y, c);
            }
    }

    // Four riveted plates with scratches.
    private static void DrawSteelPlates(Image img)
    {
        img.Fill(new Color("1e2333"));
        for (int py = 0; py < 2; py++)
            for (int px = 0; px < 2; px++)
            {
                int x0 = px * 16, y0 = py * 16, seed = 50 + py * 2 + px;
                DrawBlock(img, new Rect2I(x0, y0, 15, 15), SteelRamp, seed, wear: 0.02f, grade: 0.7f);
                for (int y = y0 + 2; y < y0 + 13; y++)
                    for (int x = x0 + 2; x < x0 + 13; x++)
                        for (int k = 0; k < 4; k++)
                            if (Noise(x - k, y - k, seed + 10) < 0.002f)
                                img.SetPixel(x, y, SteelRamp[4]);
                foreach (var (rx, ry) in new[] { (3, 3), (11, 3), (3, 11), (11, 11) })
                    DrawRivet(img, x0 + rx, y0 + ry, SteelRamp);
            }
    }

    // Top and bottom flanges with bolts, and a bevelled lattice web over a
    // dark hollow between them.
    private static void DrawGirder(Image img)
    {
        for (int y = 6; y < 26; y++)
            for (int x = 0; x < TileSize; x++)
            {
                int a = (x + y) % 16, b = (x - y + 96) % 16;
                Color c = Tone(HollowRamp, 1.2f - (y - 6) / 20f, x, y);
                if (a < 3)
                    c = IronRamp[a == 0 ? 3 : a == 1 ? 2 : 0];
                else if (b < 3)
                    c = IronRamp[b == 0 ? 3 : b == 1 ? 2 : 0];
                img.SetPixel(x, y, c);
            }
        DrawBlock(img, new Rect2I(-2, 0, 36, 6), IronRamp, 60, wear: 0.04f);
        DrawBlock(img, new Rect2I(-2, 26, 36, 6), IronRamp, 61, wear: 0.04f);
        foreach (int bx in new[] { 3, 11, 19, 27 })
        {
            DrawRivet(img, bx, 2, IronRamp);
            DrawRivet(img, bx, 28, IronRamp);
        }
    }

    // Two poured slabs per tile with form-tie holes, damp stains laid down
    // by low-frequency noise, and a hairline crack.
    private static void DrawConcrete(Image img)
    {
        for (int slab = 0; slab < 2; slab++)
        {
            int seed = 70 + slab;
            DrawBlock(img, new Rect2I(0, slab * 16, 32, 16), ConcreteRamp, seed, wear: 0.06f,
                tint: (x, y) => Smooth(x, y, 8, seed) > 0.74f ? -0.25f : 0f);
            foreach (int hx in new[] { 7, 23 })
            {
                int hy = slab * 16 + 7;
                img.FillRect(new Rect2I(hx, hy, 2, 2), ConcreteRamp[0]);
                img.SetPixel(hx + 2, hy + 1, ConcreteRamp[3]);
                img.SetPixel(hx + 1, hy + 2, ConcreteRamp[3]);
            }
        }
    }

    // Stacked bags in running bond: a soft cylinder shaded from a lit top
    // to a dark underside, rounded ends, and a coarse weave.
    private static void DrawSandbags(Image img)
    {
        var gap = new Color("3a2a14");
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
            {
                int offset = (y / 8) % 2 == 0 ? 0 : 8;
                int lx = (x + offset) % 16, ly = y % 8;
                bool end = lx == 0 || lx == 14;
                Color c;
                if (ly == 7 || lx == 15 || end && (ly == 0 || ly == 6))
                    c = gap;
                else
                {
                    float t = 3.6f - ly * 0.5f + (lx == 0 ? 0.4f : lx == 14 ? -0.8f : 0f);
                    if (Noise(x, y, 80) < 0.07f)
                        t -= 0.9f;
                    c = Tone(SandRamp, t, x, y);
                }
                img.SetPixel(x, y, c);
            }
    }

    // A shipping container: corrugated green panels between riveted steel
    // rails, grime gathering at the bottom and a few rust specks.
    private static void DrawContainer(Image img)
    {
        var rust = new Color("7a3a16");
        for (int y = 3; y < 29; y++)
            for (int x = 0; x < TileSize; x++)
            {
                float t = (x % 8) switch { 0 => 4f, 1 => 3f, 2 or 3 => 2f, 4 => 1.6f, 5 => 1f, 6 => 0f, _ => 1f };
                if (y > 22)
                    t -= (y - 22) / 6f;
                Color c = Noise(x, y, 90) < 0.004f ? rust : Tone(GreenRamp, t, x, y);
                img.SetPixel(x, y, c);
            }
        DrawBlock(img, new Rect2I(-2, 0, 36, 3), IronRamp, 91, wear: 0f);
        DrawBlock(img, new Rect2I(-2, 29, 36, 3), IronRamp, 92, wear: 0f);
        foreach (int rx in new[] { 3, 11, 19, 27 })
        {
            DrawRivet(img, rx, 0, IronRamp);
            DrawRivet(img, rx, 29, IronRamp);
        }
    }

    // Four horizontal pipes per tile, each a cylinder lit along its top,
    // with raised couplings staggered pipe to pipe.
    private static void DrawPipes(Image img)
    {
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
            {
                int ly = y % 8, pipe = y / 8;
                int lx = (x + pipe * 8) % 16;
                Color c;
                if (lx < 4)
                {
                    if (ly == 0)
                        c = TealRamp[3];
                    else if (ly == 7)
                        c = TealRamp[0];
                    else if (lx == 0)
                        c = TealRamp[4];
                    else if (lx == 3)
                        c = TealRamp[1];
                    else
                        c = Tone(TealRamp, 3.4f - ly * 0.35f, x, y);
                }
                else
                {
                    float t = ly switch { 0 => 0f, 1 => 4f, 2 => 3f, 3 => 2.3f, 4 => 2f, 5 => 1.5f, 6 => 1f, _ => 0f };
                    if (ly > 2 && Noise(x, y, 100 + pipe) < 0.04f)
                        t -= 1f;
                    c = Tone(TealRamp, t, x, y);
                }
                img.SetPixel(x, y, c);
            }
    }

    // A circuit board: traces with a shadow side, solder pads at the
    // junctions, two chips with pins, and a pair of LEDs.
    private static void DrawCircuit(Image img)
    {
        var board = new Color("0f1a14");
        var boardDark = new Color("0a120d");
        var trace = new Color("1f4a2c");
        var pad = new Color("2a5f38");
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
            {
                bool onTrace = IsTrace(x, y);
                Color c = onTrace ? trace
                    : IsTrace(x - 1, y) || IsTrace(x, y - 1) ? boardDark
                    : Noise(x, y, 110) < 0.05f ? new Color("16261c") : board;
                if (y % 8 == 2 && x % 8 == 5)
                    c = pad;
                img.SetPixel(x, y, c);
            }
        foreach (var (cx, cy) in new[] { (9, 9), (19, 19) })
        {
            img.FillRect(new Rect2I(cx, cy, 6, 4), new Color("0a0c0a"));
            img.FillRect(new Rect2I(cx, cy, 6, 1), new Color("2a2c33"));
            for (int px = cx; px < cx + 6; px += 2)
            {
                img.SetPixel(px, cy - 1, StoneRamp[1]);
                img.SetPixel(px, cy + 4, StoneRamp[1]);
            }
        }
        foreach (var (lx, ly) in new[] { (3, 27), (27, 3) })
        {
            img.FillRect(new Rect2I(lx, ly, 2, 2), new Color("2a8f3a"));
            img.SetPixel(lx, ly, new Color("5fd36a"));
        }

        static bool IsTrace(int x, int y)
        {
            x = Wrap(x);
            y = Wrap(y);
            return y % 8 == 2 && (x + 4) % 16 < 10 || x % 8 == 5 && (y + 6) % 16 < 12;
        }
    }

    // Corrugated sheet metal in two courses, bolted on, with rust streaks
    // bleeding down from the bolts.
    private static void DrawHangar(Image img)
    {
        var rust = new Color("3a2a1e");
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
            {
                Color c = HangarRamp[(x % 4) switch { 0 => 3, 1 => 2, 2 => 1, _ => 0 }];
                if (y % 16 == 15)
                    c = new Color("0f1115");
                else if (y % 16 == 0)
                    c = HangarRamp[4];
                else if (y % 16 is > 9 and < 15 && (x == 9 || x == 25) && Noise(x, y, 120) < 0.6f)
                    c = rust;
                img.SetPixel(x, y, c);
            }
        foreach (var (bx, by) in new[] { (8, 7), (24, 7), (8, 23), (24, 23) })
            DrawRivet(img, bx, by, IronRamp);
    }

    // A steel grate: bevelled bars over hollows shadowed under their
    // upper-left bars.
    private static void DrawMesh(Image img)
    {
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
            {
                int lx = x % 8, ly = y % 8;
                Color c;
                if (lx == 0 || ly == 0)
                    c = new Color("2a2d37");
                else if (lx == 1 || ly == 1)
                    c = new Color("1a1c23");
                else
                    c = Tone(HollowRamp, (lx + ly - 4) / 5f, x, y);
                img.SetPixel(x, y, c);
            }
    }

    // Cave rock: smooth noise quantised into facets, each a flat tone with
    // fine grain, a dark crack along every facet boundary with a lit lip on
    // its lower-right side, and pits.
    private static void DrawRock(Image img)
    {
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
            {
                // Background rock stays quiet: two close tones per facet and
                // only some facet edges cracked, so it never competes with
                // the playfield in front of it.
                float t = 1.6f + 0.5f * (Facet(x, y) % 2) + 0.4f * (Smooth(x, y, 4, 133) - 0.5f);
                Color c = Tone(RockRamp, t, x, y);
                if (IsCrack(x, y) && Smooth(x, y, 8, 134) > 0.55f || Noise(x, y, 130) < 0.01f)
                    c = RockRamp[1];
                else if (IsCrack(x - 1, y - 1) && Smooth(x - 1, y - 1, 8, 134) > 0.55f)
                    c = RockRamp[3];
                img.SetPixel(x, y, c);
            }

        static int Facet(int x, int y) =>
            (int)((0.65f * Smooth(x, y, 8, 131) + 0.35f * Smooth(x, y, 4, 132)) * 6f);
        static bool IsCrack(int x, int y) => Facet(x, y) != Facet(x - 1, y) || Facet(x, y) != Facet(x, y - 1);
    }

    // Four wall panels with corner screws and a diagonal sheen.
    private static void DrawPanels(Image img)
    {
        img.Fill(new Color("0f1219"));
        for (int py = 0; py < 2; py++)
            for (int px = 0; px < 2; px++)
            {
                int x0 = px * 16, y0 = py * 16;
                DrawBlock(img, new Rect2I(x0, y0, 15, 15), PanelRamp, 140 + py * 2 + px, wear: 0.02f,
                    tint: (x, y) => (x + y) % 32 < 10 ? 0.6f : 0f);
                foreach (var (sx, sy) in new[] { (2, 2), (11, 2), (2, 11), (11, 11) })
                    DrawRivet(img, x0 + sx, y0 + sy, PanelRamp);
            }
    }

    // A dark pit floor whose dither deepens toward the bottom.
    private static void DrawPit(Image img, int seed)
    {
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                img.SetPixel(x, y, Tone(HollowRamp, 1.6f - y / 20f + (Noise(x, y, seed) < 0.05f ? -1f : 0f), x, y));
    }

    // A jagged crack down the brick face with its right lip lit, and a
    // branch running off to the lower left.
    private static void DrawCrack(Image img, Color[] ramp)
    {
        for (int y = 0; y < TileSize; y++)
        {
            int cx = 14 + Mathf.RoundToInt(2f * Mathf.Sin(y * Mathf.Tau / TileSize));
            img.SetPixel(cx, y, ramp[0]);
            img.SetPixel(cx + 1, y, ramp[3]);
        }
        for (int i = 0; i < 9; i++)
            Dot(img, 14 - i, 17 + i, ramp[0]);
    }

    // Running-bond bricks, two per course and four courses per tile. Odd
    // courses shift by half a brick, so their end bricks straddle the tile
    // edge; both halves share a seed and tile-periodic noise, so the brick
    // reads whole across the seam. A share of bricks sits a tone darker.
    private static void DrawBricks(Image img, Color[] ramp, Color mortar, int seed, float wear = 0.05f)
    {
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                img.SetPixel(x, y, Noise(x, y, seed + 9) < 0.12f ? ramp[0] : mortar);
        for (int course = 0; course < 4; course++)
        {
            int offset = course % 2 == 0 ? 0 : 8;
            for (int col = -1; col <= 1; col++)
            {
                int brick = seed * 8 + course * 2 + (col & 1);
                float tone = Noise(col & 1, course, seed + 5) < 0.3f ? -0.4f : 0f;
                DrawBlock(img, new Rect2I(col * 16 + offset, course * 8, 15, 7), ramp, brick, wear: wear, tint: (_, _) => tone);
            }
        }
    }

    // A bevelled block lit from the top left: lit top and left edges, a
    // shaded bottom and right edge, a body that dithers darker toward the
    // bottom right, and pits and grime placed by the seed. Pixels outside
    // the tile are clipped, so a block may straddle the tile edge.
    private static void DrawBlock(Image img, Rect2I r, Color[] ramp, int seed,
        bool rounded = false, float wear = 0.05f, float grade = 1.3f, Func<int, int, float> tint = null)
    {
        int w = r.Size.X, h = r.Size.Y;
        for (int ly = 0; ly < h; ly++)
            for (int lx = 0; lx < w; lx++)
            {
                int x = r.Position.X + lx, y = r.Position.Y + ly;
                if (x < 0 || x >= TileSize || y < 0 || y >= TileSize)
                    continue;
                bool lit = lx == 0 || ly == 0, shaded = lx == w - 1 || ly == h - 1;
                if (rounded && (lx == 0 || lx == w - 1) && (ly == 0 || ly == h - 1))
                    continue;
                Color c;
                if (lit && shaded)
                    c = ramp[1];
                else if (lit)
                    c = lx == 0 && ly == 0 ? ramp[4] : ramp[3];
                else if (shaded)
                    c = ramp[0];
                else
                {
                    float t = 2.4f - grade * ly / (h - 1) - 0.3f * lx / (w - 1) + (tint?.Invoke(x, y) ?? 0f);
                    if (Noise(x, y, seed) < wear * 0.4f)
                        c = ramp[0];
                    else if (lx > 1 && ly > 1 && Noise(x - 1, y - 1, seed) < wear * 0.4f)
                        c = ramp[3];
                    else if (ly > h / 2 && Noise(x, y, seed + 1) < wear)
                        c = Tone(ramp, t - 0.9f, x, y);
                    else
                        c = Tone(ramp, t, x, y);
                }
                img.SetPixel(x, y, c);
            }
    }

    // A domed rivet head: glint at the top left, dark at the bottom right,
    // and a one-pixel cast shadow below and to the right.
    private static void DrawRivet(Image img, int x, int y, Color[] ramp)
    {
        Dot(img, x, y, ramp[4]);
        Dot(img, x + 1, y, ramp[2]);
        Dot(img, x, y + 1, ramp[2]);
        Dot(img, x + 1, y + 1, ramp[0]);
        Dot(img, x + 2, y + 1, ramp[1]);
        Dot(img, x + 1, y + 2, ramp[1]);
    }

    // A blade pixel by its offset from the spine: lit on the left edge,
    // dark on the right.
    private static Color BladeTone(int dx, int halfWidth) =>
        dx == -halfWidth ? BladeRamp[4]
        : dx < 0 ? BladeRamp[3]
        : dx == 0 ? BladeRamp[2]
        : dx < halfWidth ? BladeRamp[1]
        : BladeRamp[0];

    // Water frame: a foam line crawls along the surface, ripples drift one
    // pixel per frame, the body dithers darker with depth and glints wink.
    private static Image BuildWaterImage(int frame)
    {
        var img = Image.CreateEmpty(TileSize, TileSize, false, Image.Format.Rgba8);
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
            {
                Color c;
                if (y == 0)
                    c = (x + frame) % 4 < 2 ? WaterRamp[5] : WaterRamp[4];
                else if (y == 1)
                    c = (x + y + frame / 2) % 2 == 0 ? WaterRamp[4] : WaterRamp[3];
                else if (y == 2)
                    c = WaterRamp[3];
                else
                {
                    c = Tone(WaterRamp, 2.6f - (y - 3) / 14f, x, y);
                    if (y % 8 == 5 && (x + frame + y / 8 * 3) % 8 < 3)
                        c = WaterRamp[3];
                    else if (y % 8 == 6 && (x + frame + y / 8 * 3) % 8 < 3)
                        c = WaterRamp[0];
                    else if (y < 14 && Noise(x, y, 150 + frame) < 0.012f)
                        c = WaterRamp[5];
                }
                img.SetPixel(x, y, c);
            }
        return img;
    }

    // Lava frame: a churn of crust and glow from drifting smooth noise,
    // with sparks that twinkle frame to frame.
    private static Image BuildLavaImage(int frame)
    {
        var img = Image.CreateEmpty(TileSize, TileSize, false, Image.Format.Rgba8);
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
            {
                float n = 0.6f * Smooth(x, y, 8, 160) + 0.4f * Smooth(x + frame * 3, y + frame, 4, 161);
                Color c = Tone(LavaRamp, n * 7f - 0.8f, x, y);
                if (Noise(x, y, 162 + frame) < 0.012f)
                {
                    c = LavaRamp[6];
                    Dot(img, x, y - 1, LavaRamp[5]);
                }
                img.SetPixel(x, y, c);
            }
        return img;
    }

    // Spikes frame: the four spikes rise one by one from the left (frames
    // 0-4), then sink one by one from the left (frames 5-7), a sweep that
    // loops. Frame 4 is the fully raised tile used for the palette icon.
    private static Image BuildSpikesImage(int frame)
    {
        var img = Image.CreateEmpty(TileSize, TileSize, false, Image.Format.Rgba8);
        DrawPit(img, 170);
        DrawBlock(img, new Rect2I(-2, 28, 36, 4), SteelRamp, 171, wear: 0.03f);
        foreach (int rx in new[] { 1, 15, 29 })
            DrawRivet(img, rx, 29, SteelRamp);
        int first = frame <= 4 ? 0 : frame - 4;
        int last = frame <= 4 ? frame - 1 : 3;
        for (int spike = first; spike <= last; spike++)
        {
            int center = spike * 8 + 4;
            img.SetPixel(center, 5, Colors.White);
            for (int y = 6; y < 28; y++)
            {
                int halfWidth = (y - 4) / 6 + 1;
                for (int dx = -halfWidth; dx <= halfWidth; dx++)
                    Dot(img, center + dx, y, BladeTone(dx, halfWidth));
            }
        }
        return img;
    }

    // One night-sky tile: the base blue-black, a faint nebula from smooth
    // noise, and stars of three brightnesses scattered by the variant's seed.
    private static Image BuildNightSkyImage(int variant)
    {
        var img = Image.CreateEmpty(TileSize, TileSize, false, Image.Format.Rgba8);
        var sky = Ramp("0b0e1a", "10142a", "161b36");
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                img.SetPixel(x, y, Tone(sky, Smooth(x, y, 16, 180 + variant) * 2.4f - 0.8f, x, y));
        uint seed = (uint)variant * 747796405u + 2891336453u;
        uint Next() { seed = seed * 1664525u + 1013904223u; return seed >> 8; }
        int stars = 3 + (int)(Next() % 3);
        for (int i = 0; i < stars; i++)
        {
            int sx = (int)(Next() % TileSize), sy = (int)(Next() % TileSize);
            switch (Next() % 4)
            {
                case 0:
                    img.SetPixel(sx, sy, new Color("e8e8f4"));
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        Dot(img, sx + dx, sy + dy, new Color("4a4e66"));
                    break;
                case 1:
                    img.SetPixel(sx, sy, new Color("5a5a6a"));
                    break;
                default:
                    img.SetPixel(sx, sy, new Color("9a9aa5"));
                    break;
            }
        }
        return img;
    }

    // A shadow tile: translucent black laid down by ordered dithering,
    // densest at the edge that touches the solid neighbour. Bands add up
    // where they meet, so an inner corner comes out darker than either.
    private const float ShadowAlpha = 0.55f;
    private const int CeilingShade = 10;
    private const int WallShade = 6;
    private const int FloorContact = 2;

    private static Image BuildShadowImage(ShadowMask mask)
    {
        var img = Image.CreateEmpty(TileSize, TileSize, false, Image.Format.Rgba8);
        var ink = new Color(0, 0, 0, ShadowAlpha);
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
            {
                float cover = 0f;
                if (mask.HasFlag(ShadowMask.Above))
                    cover += Fade(y, CeilingShade);
                if (mask.HasFlag(ShadowMask.Left))
                    cover += Fade(x, WallShade);
                if (mask.HasFlag(ShadowMask.Below))
                    cover += Fade(TileSize - 1 - y, FloorContact);
                if (Threshold(x, y) < cover)
                    img.SetPixel(x, y, ink);
            }
        return img;

        static float Fade(int distance, int length) =>
            distance < length ? (length - distance) / (float)length : 0f;
    }

    private static Color[] Ramp(params string[] hex) => hex.Select(h => new Color(h)).ToArray();

    // The ramp colour for a fractional tone index, dithered in Bayer order
    // between the two tones it falls between.
    private static Color Tone(Color[] ramp, float t, int x, int y)
    {
        t = Mathf.Clamp(t, 0f, ramp.Length - 1);
        int i = Mathf.Min((int)t, ramp.Length - 2);
        return Threshold(x, y) < t - i ? ramp[i + 1] : ramp[i];
    }

    private static readonly int[,] Bayer =
    {
        { 0, 8, 2, 10 },
        { 12, 4, 14, 6 },
        { 3, 11, 1, 9 },
        { 15, 7, 13, 5 },
    };

    // Ordered-dither threshold in (0, 1): a pixel is "on" for a coverage
    // when its threshold falls below it, which fills in Bayer order.
    private static float Threshold(int x, int y) => (Bayer[y & 3, x & 3] + 0.5f) / 16f;

    // White noise in [0, 1), periodic in the tile so straddling blocks and
    // neighbouring tiles agree.
    private static float Noise(int x, int y, int seed)
    {
        uint h = unchecked((uint)(Wrap(x) * 374761393 + Wrap(y) * 668265263 + seed * 2246822519));
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xffff) / 65536f;
    }

    // Smooth value noise with lattice points every `cell` pixels, periodic
    // in the tile.
    private static float Smooth(int x, int y, int cell, int seed)
    {
        x = Wrap(x);
        y = Wrap(y);
        int n = TileSize / cell;
        int gx = x / cell, gy = y / cell;
        float fx = (x % cell) / (float)cell, fy = (y % cell) / (float)cell;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float Lattice(int i, int j) => Noise(i % n, j % n, seed);
        float top = Mathf.Lerp(Lattice(gx, gy), Lattice(gx + 1, gy), fx);
        float bottom = Mathf.Lerp(Lattice(gx, gy + 1), Lattice(gx + 1, gy + 1), fx);
        return Mathf.Lerp(top, bottom, fy);
    }

    private static void Dot(Image img, int x, int y, Color c)
    {
        if (x >= 0 && x < TileSize && y >= 0 && y < TileSize)
            img.SetPixel(x, y, c);
    }

    private static int Wrap(int value) => ((value % TileSize) + TileSize) % TileSize;
}
