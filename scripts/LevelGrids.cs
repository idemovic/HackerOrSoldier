using Godot;

namespace HackerOrSoldier;

public sealed class LevelGrids
{
    public TileMapLayer Back { get; }
    public TileMapLayer Main { get; }
    public TileMapLayer Hidden { get; }
    public TileMapLayer Shadow { get; }

    public LevelGrids(Node parent)
    {
        var tileSet = TileSetBuilder.Build();
        Back = new TileMapLayer { TileSet = tileSet };
        Main = new TileMapLayer { TileSet = tileSet };
        Hidden = new TileMapLayer { TileSet = tileSet };
        Shadow = new TileMapLayer { TileSet = tileSet };
        parent.AddChild(Back);
        parent.AddChild(Main);
        parent.AddChild(Hidden);
        parent.AddChild(Shadow);
    }

    // A background wall goes to the back layer, any other tile replaces the
    // front one; the other layer is left as it is.
    public void SetCell(Vector2I cell, int tileId)
    {
        if (TileDefs.IsBackground(tileId))
        {
            Back.SetCell(cell, TileSetBuilder.SourceId, TileSetBuilder.GridCoordsFor(tileId, cell));
            RefreshShadow(cell);
            return;
        }
        Place(cell, tileId);
        RefreshShadowsAround(cell);
        RefreshElectricJoinsAround(cell);
        RefreshPipesAround(cell);
        RefreshWaterAround(cell);
        RefreshRimsAround(cell);
    }

    // Clears the front tile; the background behind it stays.
    public void EraseCell(Vector2I cell)
    {
        Main.EraseCell(cell);
        Hidden.EraseCell(cell);
        RefreshShadowsAround(cell);
        RefreshElectricJoinsAround(cell);
        RefreshPipesAround(cell);
        RefreshWaterAround(cell);
        RefreshRimsAround(cell);
    }

    public void EraseBackground(Vector2I cell)
    {
        Back.EraseCell(cell);
        RefreshShadow(cell);
    }

    // Colors the front and back walls alike; the alarm uses it.
    public void Tint(Color color)
    {
        Back.Modulate = color;
        Main.Modulate = color;
    }

    public void Clear()
    {
        Back.Clear();
        Main.Clear();
        Hidden.Clear();
        Shadow.Clear();
    }

    public void Fill(LevelData level)
    {
        Clear();
        for (int y = 0; y < level.Height; y++)
            for (int x = 0; x < level.Width; x++)
            {
                int id = level.GetTile(x, y);
                if (id != TileDefs.Empty)
                    Place(new Vector2I(x, y), id);
                int back = level.GetBackground(x, y);
                if (back != TileDefs.Empty)
                    Back.SetCell(new Vector2I(x, y), TileSetBuilder.SourceId, TileSetBuilder.GridCoordsFor(back, new Vector2I(x, y)));
            }
        for (int y = 0; y < level.Height; y++)
            for (int x = 0; x < level.Width; x++)
            {
                RefreshShadow(new Vector2I(x, y));
                RefreshElectricJoin(new Vector2I(x, y));
                RefreshPipe(new Vector2I(x, y));
                RefreshWater(new Vector2I(x, y));
                RefreshRim(new Vector2I(x, y));
            }
    }

    public int TileIdAt(Vector2I cell) => IdOn(Main, cell);

    public int BackgroundIdAt(Vector2I cell) => IdOn(Back, cell);

    private static int IdOn(TileMapLayer layer, Vector2I cell) =>
        layer.GetCellTileData(cell) is { } data ? data.GetCustomData(TileSetBuilder.TileIdData).AsInt32() : TileDefs.Empty;

    private void Place(Vector2I cell, int tileId)
    {
        var target = tileId == TileDefs.HiddenPlatform ? Hidden : Main;
        var other = tileId == TileDefs.HiddenPlatform ? Main : Hidden;
        other.EraseCell(cell);
        target.SetCell(cell, TileSetBuilder.SourceId, TileSetBuilder.GridCoordsFor(tileId, cell));
    }

    private static readonly (Vector2I Offset, ShadowMask Band)[] Casters =
    {
        (Vector2I.Up, ShadowMask.Above),
        (Vector2I.Left, ShadowMask.Left),
        (Vector2I.Down, ShadowMask.Below),
    };

    private void RefreshShadowsAround(Vector2I cell)
    {
        RefreshShadow(cell);
        foreach (var (offset, _) in Casters)
            RefreshShadow(cell - offset);
    }

    // Scenery takes shade: a non-solid front tile such as spikes, or a bare
    // background wall.
    private void RefreshShadow(Vector2I cell)
    {
        var mask = ShadowMask.None;
        int id = TileIdAt(cell);
        if (id == TileDefs.Empty ? BackgroundIdAt(cell) != TileDefs.Empty : TileDefs.ReceivesShadow(id))
            foreach (var (offset, band) in Casters)
                if (TileDefs.CastsShadow(TileIdAt(cell + offset)))
                    mask |= band;
        if (mask == ShadowMask.None)
            Shadow.EraseCell(cell);
        else
            Shadow.SetCell(cell, TileSetBuilder.SourceId, TileSetBuilder.ShadowCoordsFor(mask));
    }

    private void RefreshElectricJoinsAround(Vector2I cell)
    {
        RefreshElectricJoin(cell + Vector2I.Left);
        RefreshElectricJoin(cell);
        RefreshElectricJoin(cell + Vector2I.Right);
    }

    // A run of electric floor draws as one plate with one arc: each cell
    // picks the tile that joins the electric floor beside it.
    private void RefreshElectricJoin(Vector2I cell)
    {
        if (TileIdAt(cell) != TileDefs.ElectricFloor)
            return;
        bool left = TileIdAt(cell + Vector2I.Left) == TileDefs.ElectricFloor;
        bool right = TileIdAt(cell + Vector2I.Right) == TileDefs.ElectricFloor;
        Main.SetCell(cell, TileSetBuilder.SourceId, TileSetBuilder.ElectricFloorCoordsFor(left, right));
    }

    private static readonly (Vector2I Offset, PipeLinks Link)[] PipeSides =
    {
        (Vector2I.Up, PipeLinks.Up),
        (Vector2I.Right, PipeLinks.Right),
        (Vector2I.Down, PipeLinks.Down),
        (Vector2I.Left, PipeLinks.Left),
    };

    private void RefreshPipesAround(Vector2I cell)
    {
        RefreshPipe(cell);
        foreach (var (offset, _) in PipeSides)
            RefreshPipe(cell + offset);
    }

    // A pipe wall joins every pipe wall beside it, so painting or erasing a
    // cell redraws the pipes around it.
    private void RefreshPipe(Vector2I cell)
    {
        if (TileIdAt(cell) != TileDefs.PipeWall)
            return;
        var links = PipeLinks.None;
        foreach (var (offset, link) in PipeSides)
            if (TileIdAt(cell + offset) == TileDefs.PipeWall)
                links |= link;
        Main.SetCell(cell, TileSetBuilder.SourceId, TileSetBuilder.PipeCoordsFor(links));
    }

    private void RefreshWaterAround(Vector2I cell)
    {
        RefreshWater(cell);
        RefreshWater(cell + Vector2I.Down);
    }

    // Only the top cell of a water or lava column draws the surface; a cell
    // with the same liquid above it draws the body alone.
    private void RefreshWater(Vector2I cell)
    {
        int id = TileIdAt(cell);
        if (id != TileDefs.Water && id != TileDefs.Lava)
            return;
        bool sameAbove = TileIdAt(cell + Vector2I.Up) == id;
        Main.SetCell(cell, TileSetBuilder.SourceId, id == TileDefs.Water
            ? TileSetBuilder.WaterCoordsFor(sameAbove)
            : TileSetBuilder.LavaCoordsFor(sameAbove));
    }

    private static readonly (Vector2I Offset, WallJoins Join)[] RimSides =
    {
        (Vector2I.Up, WallJoins.Up),
        (Vector2I.Right, WallJoins.Right),
        (Vector2I.Down, WallJoins.Down),
        (Vector2I.Left, WallJoins.Left),
        (new Vector2I(1, -1), WallJoins.UpRight),
        (new Vector2I(1, 1), WallJoins.DownRight),
        (new Vector2I(-1, 1), WallJoins.DownLeft),
        (new Vector2I(-1, -1), WallJoins.UpLeft),
    };

    private void RefreshRimsAround(Vector2I cell)
    {
        RefreshRim(cell);
        foreach (var (offset, _) in RimSides)
            RefreshRim(cell + offset);
    }

    // A wall finishes every side that does not touch another solid tile, so
    // painting or erasing a cell redraws the walls around it.
    private void RefreshRim(Vector2I cell)
    {
        int id = TileIdAt(cell);
        if (!TileDefs.HasRim(id))
            return;
        var joins = WallJoins.None;
        foreach (var (offset, join) in RimSides)
            if (TileDefs.Get(TileIdAt(cell + offset)) is { Solid: true })
                joins |= join;
        Main.SetCell(cell, TileSetBuilder.SourceId, TileSetBuilder.RimCoordsFor(id, joins));
    }
}
