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

// Which sides of a pipe cell join a neighbouring pipe. Each combination is
// one pre-baked tile in the atlas.
[Flags]
public enum PipeLinks { None = 0, Up = 1, Right = 2, Down = 4, Left = 8 }

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

    // Water and lava animate via native TileSet animations; their frames
    // fill dedicated atlas rows below the defs row and the sky variants.
    // The shadow masks take the last row, one column per mask.
    private const int WaterFrames = 4;
    private const int LavaFrames = 6;
    private static readonly Vector2I WaterCoords = new(0, 2);
    // Water under water: same row, after the surface frames.
    private static readonly Vector2I DeepWaterCoords = new(WaterFrames, 2);
    private static readonly Vector2I LavaCoords = new(0, 3);
    // Lava under lava: same row, after the surface frames.
    private static readonly Vector2I DeepLavaCoords = new(LavaFrames, 3);
    // Spike frames: frame 0 is both spikes down, then the left spike rising
    // through SpikeLevels heights, then the right one. Not a native
    // animation: the play session picks the frame from its own clock.
    public const int SpikeLevels = 3;
    private const int SpikeFrames = 1 + 2 * SpikeLevels;
    private static readonly Vector2I SpikesCoords = new(0, 4);
    // Spear frames: same row, after the spike frames. One frame per state:
    // bare holes, tips showing in the holes, spears out. Not a native
    // animation: the play session picks the frame from its own clock.
    public const int SpearFrames = 3;
    private static readonly int[] SpearRise = { 0, 3, 12 };   // pixels above the plate, per frame
    private const int ShadowRow = 5;
    private const int ShadowMasks = 8;
    // Electric floor joins: join = (left neighbour is electric ? 1 : 0) |
    // (right ? 2 : 0), each an animated run of frames in its own row.
    private const int ElectricJoinRow = 6;
    private const int ElectricJoins = 4;
    private const int ElectricFrames = 4;
    private static Vector2I ElectricJoinCoords(int join) => new(join * ElectricFrames, ElectricJoinRow);
    // Pipe walls: column = the PipeLinks mask of the cell.
    private const int PipeRow = 7;
    private const int PipeVariants = 16;
    // The circuit board: a Span x Span block of tiles, picked per cell.
    private const int CircuitRow = 8;
    private const int RockRow = CircuitRow + CircuitBoard.Span;
    private const int AtlasRows = RockRow + RockFace.Span;

    // Custom data layer: every atlas tile carries the level tile id it
    // draws, so a grid cell reads back without inverting GridCoordsFor.
    public const string TileIdData = "tile_id";

    private static readonly Dictionary<int, ImageTexture> IconCache = new();
    private static readonly Dictionary<int, ImageTexture> TextureCache = new();

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
    // variant, water, lava and electric floor point at their animated
    // tiles.
    public static Vector2I GridCoordsFor(int tileId, Vector2I cell) => tileId switch
    {
        TileDefs.BackgroundSky => NightSkyCoordsFor(cell),
        TileDefs.BackgroundCircuit => CircuitCoordsFor(cell),
        TileDefs.BackgroundRock => RockCoordsFor(cell),
        TileDefs.ElectricFloor => ElectricJoinCoords(0),
        TileDefs.Water => WaterCoords,
        TileDefs.Lava => LavaCoords,
        _ => AtlasCoordsFor(tileId),
    };

    // Only the top cell of a water column draws the surface; the cells
    // under it continue the body without foam.
    public static Vector2I WaterCoordsFor(bool waterAbove) => waterAbove ? DeepWaterCoords : WaterCoords;

    // Lava follows the same rule: bubbles only on the top cell.
    public static Vector2I LavaCoordsFor(bool lavaAbove) => lavaAbove ? DeepLavaCoords : LavaCoords;

    // spike 0 is the left one, 1 the right one; level 0 is fully down.
    public static Vector2I SpikeCoordsFor(int spike, int level) =>
        new(level == 0 ? 0 : 1 + spike * SpikeLevels + level - 1, SpikesCoords.Y);

    public static Vector2I SpearCoordsFor(int frame) => new(SpikeFrames + frame, SpikesCoords.Y);

    public static Vector2I ShadowCoordsFor(ShadowMask mask) => new((int)mask, ShadowRow);

    public static Vector2I PipeCoordsFor(PipeLinks links) => new((int)links, PipeRow);

    public static Vector2I CircuitCoordsFor(Vector2I cell) => new(
        Mathf.PosMod(cell.X, CircuitBoard.Span), CircuitRow + Mathf.PosMod(cell.Y, CircuitBoard.Span));

    public static Vector2I RockCoordsFor(Vector2I cell) => new(
        Mathf.PosMod(cell.X, RockFace.Span), RockRow + Mathf.PosMod(cell.Y, RockFace.Span));

    // An electric floor cell joined to its electric neighbours: one arc runs
    // through a whole run, with electrodes only at its two ends.
    public static Vector2I ElectricFloorCoordsFor(bool joinLeft, bool joinRight) =>
        ElectricJoinCoords((joinLeft ? 1 : 0) | (joinRight ? 2 : 0));

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
        {
            Put(BuildWaterImage(f, surface: true), WaterCoords.X + f, WaterCoords.Y);
            Put(BuildWaterImage(f, surface: false), DeepWaterCoords.X + f, DeepWaterCoords.Y);
        }
        for (int f = 0; f < LavaFrames; f++)
        {
            Put(BuildLavaImage(f, surface: true), LavaCoords.X + f, LavaCoords.Y);
            Put(BuildLavaImage(f, surface: false), DeepLavaCoords.X + f, DeepLavaCoords.Y);
        }
        Put(BuildSpikesImage(0, 0), 0, SpikesCoords.Y);
        for (int level = 1; level <= SpikeLevels; level++)
        {
            Put(BuildSpikesImage(level, 0), SpikeCoordsFor(0, level).X, SpikesCoords.Y);
            Put(BuildSpikesImage(0, level), SpikeCoordsFor(1, level).X, SpikesCoords.Y);
        }
        for (int f = 0; f < SpearFrames; f++)
        {
            var at = SpearCoordsFor(f);
            Put(BuildSpearImage(f), at.X, at.Y);
        }
        for (int m = 1; m < ShadowMasks; m++)
            Put(BuildShadowImage((ShadowMask)m), m, ShadowRow);
        for (int join = 0; join < ElectricJoins; join++)
            for (int f = 0; f < ElectricFrames; f++)
            {
                var tile = Image.CreateEmpty(TileSize, TileSize, false, Image.Format.Rgba8);
                DrawElectricFloor(tile, joinLeft: (join & 1) != 0, joinRight: (join & 2) != 0, f);
                var at = ElectricJoinCoords(join);
                Put(tile, at.X + f, at.Y);
            }
        for (int l = 0; l < PipeVariants; l++)
            Put(BuildPipeImage((PipeLinks)l), l, PipeRow);
        for (int y = 0; y < CircuitBoard.Span; y++)
            for (int x = 0; x < CircuitBoard.Span; x++)
                Put(CircuitBoard.Tile(x, y), x, CircuitRow + y);
        for (int y = 0; y < RockFace.Span; y++)
            for (int x = 0; x < RockFace.Span; x++)
                Put(RockFace.Tile(x, y), x, RockRow + y);
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

        TileData Animated(Vector2I coords, int tileId, int frames, float frameDuration)
        {
            var data = Create(coords, tileId);
            source.SetTileAnimationFramesCount(coords, frames);
            for (int f = 0; f < frames; f++)
                source.SetTileAnimationFrameDuration(coords, f, frameDuration);
            return data;
        }

        float half = TileSize / 2f;
        var square = new Vector2[] { new(-half, -half), new(half, -half), new(half, half), new(-half, half) };
        void Solid(TileData data)
        {
            data.AddCollisionPolygon(0);
            data.SetCollisionPolygonPoints(0, 0, square);
        }
        for (int i = 0; i < defs.Length; i++)
        {
            var data = Create(new Vector2I(i, 0), defs[i].Id);
            if (defs[i].Solid)
                Solid(data);
        }
        for (int v = 1; v < SkyVariants; v++)
            Create(new Vector2I(v - 1, 1), TileDefs.BackgroundSky);
        Animated(WaterCoords, TileDefs.Water, WaterFrames, 0.25f);
        Animated(DeepWaterCoords, TileDefs.Water, WaterFrames, 0.25f);
        Animated(LavaCoords, TileDefs.Lava, LavaFrames, 0.3f);
        Animated(DeepLavaCoords, TileDefs.Lava, LavaFrames, 0.3f);
        for (int f = 0; f < SpikeFrames; f++)
            Create(new Vector2I(f, SpikesCoords.Y), TileDefs.Spikes);
        for (int f = 0; f < SpearFrames; f++)
            Create(SpearCoordsFor(f), TileDefs.Spear);
        for (int m = 1; m < ShadowMasks; m++)
            Create(ShadowCoordsFor((ShadowMask)m), TileDefs.Empty);
        for (int join = 0; join < ElectricJoins; join++)
            Solid(Animated(ElectricJoinCoords(join), TileDefs.ElectricFloor, ElectricFrames, 0.09f));
        for (int l = 0; l < PipeVariants; l++)
            Solid(Create(PipeCoordsFor((PipeLinks)l), TileDefs.PipeWall));
        for (int y = 0; y < CircuitBoard.Span; y++)
            for (int x = 0; x < CircuitBoard.Span; x++)
                Create(new Vector2I(x, CircuitRow + y), TileDefs.BackgroundCircuit);
        for (int y = 0; y < RockFace.Span; y++)
            for (int x = 0; x < RockFace.Span; x++)
                Create(new Vector2I(x, RockRow + y), TileDefs.BackgroundRock);
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

    // Full-size tile art, for the editor's brush preview.
    public static ImageTexture TileTexture(int tileId)
    {
        if (TextureCache.TryGetValue(tileId, out var cached))
            return cached;
        var texture = ImageTexture.CreateFromImage(BuildTileImage(tileId));
        TextureCache[tileId] = texture;
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
                DrawElectricFloor(img, joinLeft: false, joinRight: false, frame: 0);
                break;
            case TileDefs.Water:
                return BuildWaterImage(0, surface: true);
            case TileDefs.Lava:
                return BuildLavaImage(3, surface: true);
            case TileDefs.Spikes:
                return BuildSpikesImage(SpikeLevels, SpikeLevels);
            case TileDefs.Spear:
                return BuildSpearImage(SpearFrames - 1);
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
                return BuildPipeImage(PipeLinks.Left | PipeLinks.Right);
            case TileDefs.BackgroundBrick:
                DrawBricks(img, DimBrickRamp, new Color("0a0b0f"), seed: 4, wear: 0.03f);
                break;
            case TileDefs.BackgroundCircuit:
                return CircuitBoard.Tile(0, 0);
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
                return RockFace.Tile(0, 0);
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

    // Arc height per column, periodic in the tile: a jagged bolt whose last
    // column steps into the first, so joined tiles draw one unbroken arc.
    private static readonly int[] BoltOffset =
    {
        0, -1, -2, -3, -2, -1, 0, 1, 2, 1, 0, -1, -1, -2, -3, -2,
        -1, 0, 1, 2, 3, 2, 1, 0, -1, -2, -1, 0, 1, 2, 1, 1,
    };

    // Per animation frame, how far the bolt shape slides along the tile and
    // whether it flips. A shift keeps the offsets periodic in the tile, so
    // every frame still joins seamlessly into its neighbour.
    private static readonly (int Shift, int Sign)[] BoltFrames = { (0, 1), (11, -1), (5, 1), (21, -1) };

    // A dark steel plate carrying a lightning arc: a pale core, a yellow glow
    // and cyan sparks. A side joined to another electric floor has no bevel
    // and no electrode, so the plate and arc run straight on into it; an
    // open side ends in a copper electrode. Each frame reshapes the arc and
    // rescatters the sparks, so the floor crackles.
    private static void DrawElectricFloor(Image img, bool joinLeft, bool joinRight, int frame)
    {
        var (shift, sign) = BoltFrames[frame];
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
            {
                bool lit = y == 0 || x == 0 && !joinLeft;
                bool shaded = y == TileSize - 1 || x == TileSize - 1 && !joinRight;
                Color c = lit && shaded ? PlateRamp[1]
                    : lit ? PlateRamp[3]
                    : shaded ? PlateRamp[0]
                    : Noise(x, y, 40) < 0.024f ? PlateRamp[0]
                    : Tone(PlateRamp, 2.4f - 1.3f * y / (TileSize - 1), x, y);
                img.SetPixel(x, y, c);
            }
        foreach (var (rx, ry) in new[] { (12, 2), (18, 2), (12, 28), (18, 28) })
            DrawRivet(img, rx, ry, PlateRamp);
        if (!joinLeft)
            DrawBlock(img, new Rect2I(2, 9, 3, 14), CopperRamp, 41, wear: 0f);
        if (!joinRight)
            DrawBlock(img, new Rect2I(27, 9, 3, 14), CopperRamp, 42, wear: 0f);
        int x0 = joinLeft ? 0 : 5, x1 = joinRight ? TileSize : 27;
        for (int x = x0; x < x1; x++)
        {
            int yc = 15 + sign * BoltOffset[(x + shift) % TileSize];
            var haze = Threshold(x, yc) < 0.5f ? HazardRamp[0] : PlateRamp[2];
            Dot(img, x, yc - 2, haze);
            Dot(img, x, yc + 2, haze);
            Dot(img, x, yc - 1, HazardRamp[2]);
            Dot(img, x, yc + 1, HazardRamp[2]);
            Dot(img, x, yc, new Color("fff7c0"));
        }
        for (int y = 6; y < 26; y++)
            for (int x = x0; x < x1; x++)
                if (Noise(x, y, 43 + frame) < 0.025f)
                    img.SetPixel(x, y, TealRamp[4]);
    }

    // Spear frame on a transparent tile: an iron ground plate with three
    // holes, and three short spears standing SpearRise[frame] pixels out of
    // them (frame 0 shows only the holes, frame 1 just the tips). Each
    // spear is a lit tip, a blade lit along its left edge, a steel guard and
    // a two-tone shaft.
    private static Image BuildSpearImage(int frame)
    {
        const int plateTop = 28;
        var img = Image.CreateEmpty(TileSize, TileSize, false, Image.Format.Rgba8);
        DrawBlock(img, new Rect2I(-2, plateTop, 36, 4), IronRamp, 44, wear: 0.03f);
        int rise = SpearRise[frame];
        int tip = plateTop - rise;
        foreach (int center in new[] { 6, 16, 26 })
        {
            for (int dx = -2; dx <= 2; dx++)
                img.SetPixel(center + dx, plateTop, HollowRamp[0]);
            for (int dx = -1; dx <= 1; dx++)
                img.SetPixel(center + dx, plateTop + 1, HollowRamp[1]);
            for (int y = tip; y <= plateTop && rise > 0; y++)
            {
                int dy = y - tip;
                if (y == plateTop && dy <= 7)
                    continue;   // only a shaft shows in the hole; a blade stays dark
                if (dy == 0)
                    img.SetPixel(center, y, Colors.White);
                else if (dy <= 6 && y < plateTop)
                {
                    int halfWidth = (dy + 1) / 3;
                    for (int dx = -halfWidth; dx <= halfWidth; dx++)
                        img.SetPixel(center + dx, y, BladeTone(dx, halfWidth));
                }
                else if (dy == 7 && y < plateTop)
                    for (int dx = -2; dx <= 2; dx++)
                        img.SetPixel(center + dx, y, SteelRamp[dx < 0 ? 3 : dx == 0 ? 2 : 1]);
                else
                {
                    img.SetPixel(center - 1, y, ClayRamp[3]);
                    img.SetPixel(center, y, ClayRamp[1]);
                }
            }
        }
        return img;
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

    // Pipe geometry, in pixels across the tile: the tube's half width around
    // the tile centre, the flanges standing proud of it, and the junction box.
    private const float PipeCenter = 16f;
    private const float PipeRadius = 7f;
    private const float FlangeRadius = 9f;
    private const int BoxLo = 6, BoxSize = 20;
    private const int Far = 1000;   // a run that carries on into the neighbour
    private static readonly Vector3 PipeLight = new Vector3(-0.45f, -0.6f, 0.66f).Normalized();

    // One thick pipe per tile that runs on into every neighbouring pipe:
    // straight through a line (capped where a run ends), a curved elbow at a
    // bend, a riveted junction box at a tee or cross. Each linked side ends
    // in half a flange, so two tiles meet in one whole coupling at their
    // seam. The pipe casts a short shadow down-right onto a dark backplate.
    private static Image BuildPipeImage(PipeLinks links)
    {
        bool up = links.HasFlag(PipeLinks.Up), right = links.HasFlag(PipeLinks.Right);
        bool down = links.HasFlag(PipeLinks.Down), left = links.HasFlag(PipeLinks.Left);
        bool upright = up || down, across = left || right;
        bool box = System.Numerics.BitOperations.PopCount((uint)links) >= 3;
        bool elbow = !box && upright && across;
        bool vertical = upright && !across;   // a lone pipe lies flat

        // Flanges as spans along the run they cut across: half of one at each
        // linked edge (the other half belongs to the neighbour), a cap at each
        // open end of a straight run.
        var flanges = new List<(bool Vertical, int From, int To)>();
        if (left) flanges.Add((false, -2, 1));
        if (right) flanges.Add((false, 30, 33));
        if (up) flanges.Add((true, -2, 1));
        if (down) flanges.Add((true, 30, 33));
        if (!box && !elbow)
        {
            if (vertical ? !up : !left) flanges.Add((vertical, 3, 5));
            if (vertical ? !down : !right) flanges.Add((vertical, 26, 28));
        }

        // The pipe colour at a pixel, or null for backplate. Pixels just
        // outside the tile resolve too, so shadows cross the seam.
        Color? Pipe(int x, int y)
        {
            foreach (var (v, from, to) in flanges)
            {
                int along = v ? y : x, through = v ? x : y;
                float u = (through + 0.5f - PipeCenter) / FlangeRadius;
                if (along >= from && along <= to && Mathf.Abs(u) <= 1f)
                {
                    float edge = along == from ? 0.8f : along == to ? -1.2f : 0f;
                    return Tone(SteelRamp, Lit(u, v ? Vector2.Right : Vector2.Down) * 4.6f - 0.3f + edge, x, y);
                }
            }
            if (box && x >= BoxLo && x < BoxLo + BoxSize && y >= BoxLo && y < BoxLo + BoxSize)
                return SteelRamp[2];   // drawn over by the box itself
            if (elbow)
            {
                // A quarter bend around the corner the two linked sides share.
                var offset = new Vector2(x + 0.5f - (right ? TileSize : 0), y + 0.5f - (down ? TileSize : 0));
                float r = offset.Length();
                return Mathf.Abs(r - PipeCenter) < PipeRadius
                    ? Tube((r - PipeCenter) / PipeRadius, offset / r, x, y)
                    : null;
            }
            bool horizontalRun = box ? (left && x < PipeCenter || right && x >= PipeCenter)
                : !vertical && x >= (left ? -Far : 3) && x <= (right ? Far : 28);
            bool verticalRun = box ? (up && y < PipeCenter || down && y >= PipeCenter)
                : vertical && y >= (up ? -Far : 3) && y <= (down ? Far : 28);
            float uy = (y + 0.5f - PipeCenter) / PipeRadius, ux = (x + 0.5f - PipeCenter) / PipeRadius;
            if (horizontalRun && Mathf.Abs(uy) < 1f)
                return Tube(uy, Vector2.Down, x, y);
            if (verticalRun && Mathf.Abs(ux) < 1f)
                return Tube(ux, Vector2.Right, x, y);
            return null;
        }

        var img = Image.CreateEmpty(TileSize, TileSize, false, Image.Format.Rgba8);
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
            {
                bool shadow = Pipe(x - 1, y - 1) != null || Pipe(x - 2, y - 2) != null;
                img.SetPixel(x, y, Pipe(x, y)
                    ?? (shadow ? PlateRamp[0] : Tone(PlateRamp, 1.4f + 0.8f * (Smooth(x, y, 8, 104) - 0.5f), x, y)));
            }
        if (box)
        {
            DrawBlock(img, new Rect2I(BoxLo, BoxLo, BoxSize, BoxSize), SteelRamp, 105, wear: 0.03f);
            int near = BoxLo + 2, far = BoxLo + BoxSize - 5;
            foreach (var (rx, ry) in new[] { (near, near), (far, near), (near, far), (far, far) })
                DrawRivet(img, rx, ry, SteelRamp);
        }
        return img;

        // A tube pixel: u runs -1..1 across the tube along `across`, and the
        // surface normal bends with it, so the light from the top left lands
        // on the upper or left flank whichever way the pipe runs.
        static Color Tube(float u, Vector2 across, int x, int y)
        {
            float t = Lit(u, across) * 4.8f - 0.4f;
            if (u > -0.3f && Noise(x, y, 100) < 0.04f)
                t -= 1f;
            return Tone(TealRamp, t, x, y);
        }

        static float Lit(float u, Vector2 across)
        {
            var normal = new Vector3(across.X * u, across.Y * u, Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)));
            return Mathf.Max(0f, normal.Dot(PipeLight));
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
    // A deep frame has no surface and holds the tone the surface tile ends
    // on, so a column of water reads as one body under one wave.
    private const float SurfaceTone = 2.6f, DeepTone = 0.6f;

    private static Image BuildWaterImage(int frame, bool surface)
    {
        var img = Image.CreateEmpty(TileSize, TileSize, false, Image.Format.Rgba8);
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
            {
                Color c;
                if (surface && y == 0)
                    c = (x + frame) % 4 < 2 ? WaterRamp[5] : WaterRamp[4];
                else if (surface && y == 1)
                    c = (x + y + frame / 2) % 2 == 0 ? WaterRamp[4] : WaterRamp[3];
                else if (surface && y == 2)
                    c = WaterRamp[3];
                else
                {
                    float depth = surface
                        ? SurfaceTone - (y - 3) * (SurfaceTone - DeepTone) / (TileSize - 4)
                        : DeepTone;
                    c = Tone(WaterRamp, depth, x, y);
                    if (y % 8 == 5 && (x + frame + y / 8 * 3) % 8 < 3)
                        c = WaterRamp[3];
                    else if (y % 8 == 6 && (x + frame + y / 8 * 3) % 8 < 3)
                        c = WaterRamp[0];
                    else if (surface && y < 14 && Noise(x, y, 150 + frame) < 0.012f)
                        c = WaterRamp[5];
                }
                img.SetPixel(x, y, c);
            }
        return img;
    }

    // Lava frame: a churn of crust and glow from drifting smooth noise,
    // with sparks that twinkle frame to frame. The churn loops every three
    // frames; a surface frame adds a glowing skin where small bubbles rise,
    // swell and pop over the full six-frame cycle, each on its own phase.
    private static readonly (int X, int Phase)[] LavaBubbles = { (5, 0), (16, 2), (26, 4) };

    private static Image BuildLavaImage(int frame, bool surface)
    {
        var img = Image.CreateEmpty(TileSize, TileSize, false, Image.Format.Rgba8);
        int churn = frame % 3;
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
            {
                Color c;
                if (surface && y == 0)
                    c = (x + frame) % 6 < 3 ? LavaRamp[5] : LavaRamp[4];
                else if (surface && y == 1)
                    c = (x + frame / 2) % 2 == 0 ? LavaRamp[4] : LavaRamp[3];
                else if (surface && y == 2)
                    c = (x + frame) % 3 == 0 ? LavaRamp[1] : LavaRamp[2];
                else
                {
                    float n = 0.6f * Smooth(x, y, 8, 160) + 0.4f * Smooth(x + churn * 3, y + churn, 4, 161);
                    c = Tone(LavaRamp, n * 7f - 0.8f, x, y);
                    if ((!surface || y > 11) && Noise(x, y, 162 + frame) < 0.012f)
                    {
                        c = LavaRamp[6];
                        Dot(img, x, y - 1, LavaRamp[5]);
                    }
                }
                img.SetPixel(x, y, c);
            }
        if (surface)
            foreach (var (bx, phase) in LavaBubbles)
                DrawLavaBubble(img, bx, (frame + phase) % LavaFrames);
        return img;
    }

    // One bubble's stage: a speck rising (0), a small then a grown bubble
    // under the skin (1-2), a dome breaking the surface (3), a burst crater
    // with splash (4) and the crater closing (5). Bubbles carry a dark rim
    // so they read against the churn.
    private static void DrawLavaBubble(Image img, int x, int stage)
    {
        void Bubble(int cy, int r)
        {
            for (int dy = -r - 1; dy <= r + 1; dy++)
                for (int dx = -r - 1; dx <= r + 1; dx++)
                {
                    int d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                    if (Mathf.Abs(dx) == r + 1 && Mathf.Abs(dy) == r + 1)
                        continue;   // round the corners
                    Dot(img, x + dx, cy + dy, d > r ? LavaRamp[1] : LavaRamp[5]);
                }
            Dot(img, x - r, cy - r, LavaRamp[6]);
        }
        switch (stage)
        {
            case 0:
                Dot(img, x, 11, LavaRamp[1]);
                Dot(img, x, 10, LavaRamp[6]);
                break;
            case 1:
                Bubble(8, 0);
                break;
            case 2:
                Bubble(5, 1);
                break;
            case 3:
                for (int dx = -2; dx <= 2; dx++)
                {
                    Dot(img, x + dx, 0, Mathf.Abs(dx) == 2 ? LavaRamp[1] : LavaRamp[6]);
                    Dot(img, x + dx, 1, dx == -1 ? LavaRamp[6] : LavaRamp[5]);
                    Dot(img, x + dx, 2, LavaRamp[5]);
                    Dot(img, x + dx, 3, LavaRamp[1]);
                }
                for (int dy = 1; dy <= 2; dy++)
                {
                    Dot(img, x - 3, dy, LavaRamp[1]);
                    Dot(img, x + 3, dy, LavaRamp[1]);
                }
                break;
            case 4:
                for (int dx = -1; dx <= 1; dx++)
                {
                    Dot(img, x + dx, 0, LavaRamp[0]);
                    Dot(img, x + dx, 1, LavaRamp[1]);
                }
                Dot(img, x - 2, 0, LavaRamp[1]);
                Dot(img, x + 2, 0, LavaRamp[1]);
                Dot(img, x - 3, 0, LavaRamp[6]);
                Dot(img, x + 3, 0, LavaRamp[6]);
                Dot(img, x - 4, 1, LavaRamp[5]);
                Dot(img, x + 4, 1, LavaRamp[5]);
                break;
            case 5:
                Dot(img, x, 0, LavaRamp[1]);
                Dot(img, x - 1, 0, LavaRamp[2]);
                Dot(img, x + 1, 0, LavaRamp[2]);
                break;
        }
    }

    // Spikes frame: a steel plate with two slots, one per tile half, and a
    // broad spike pushed up out of each slot by its level (0 = down,
    // SpikeLevels = fully raised). The palette icon shows both raised.
    private static Image BuildSpikesImage(int leftLevel, int rightLevel)
    {
        const int plateTop = 28;
        const int fullRise = 22;
        var img = Image.CreateEmpty(TileSize, TileSize, false, Image.Format.Rgba8);
        DrawBlock(img, new Rect2I(-2, plateTop, 36, 4), SteelRamp, 171, wear: 0.03f);
        foreach (int rx in new[] { 1, 15, 29 })
            DrawRivet(img, rx, 29, SteelRamp);
        for (int spike = 0; spike < 2; spike++)
        {
            int center = spike * 16 + 8;
            for (int dx = -4; dx <= 4; dx++)
                img.SetPixel(center + dx, plateTop, HollowRamp[0]);
            for (int dx = -2; dx <= 2; dx++)
                img.SetPixel(center + dx, plateTop + 1, HollowRamp[1]);
            int level = spike == 0 ? leftLevel : rightLevel;
            int tip = plateTop - fullRise * level / SpikeLevels;
            for (int y = tip; y < plateTop; y++)
            {
                int dy = y - tip;
                if (dy == 0)
                {
                    img.SetPixel(center, y, Colors.White);
                    continue;
                }
                int halfWidth = Mathf.Min((dy + 2) / 3, 7);
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
