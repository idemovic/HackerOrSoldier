using Godot;

namespace HackerOrSoldier;

public sealed class LevelGrids
{
    public TileMapLayer Main { get; }
    public TileMapLayer Hidden { get; }
    public TileMapLayer Shadow { get; }

    public LevelGrids(Node parent)
    {
        var tileSet = TileSetBuilder.Build();
        Main = new TileMapLayer { TileSet = tileSet };
        Hidden = new TileMapLayer { TileSet = tileSet };
        Shadow = new TileMapLayer { TileSet = tileSet };
        parent.AddChild(Main);
        parent.AddChild(Hidden);
        parent.AddChild(Shadow);
    }

    public void SetCell(Vector2I cell, int tileId)
    {
        Place(cell, tileId);
        RefreshShadowsAround(cell);
        RefreshElectricJoinsAround(cell);
        RefreshPipesAround(cell);
        RefreshWaterAround(cell);
    }

    public void EraseCell(Vector2I cell)
    {
        Main.EraseCell(cell);
        Hidden.EraseCell(cell);
        RefreshShadowsAround(cell);
        RefreshElectricJoinsAround(cell);
        RefreshPipesAround(cell);
        RefreshWaterAround(cell);
    }

    public void Clear()
    {
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
            }
        for (int y = 0; y < level.Height; y++)
            for (int x = 0; x < level.Width; x++)
            {
                RefreshShadow(new Vector2I(x, y));
                RefreshElectricJoin(new Vector2I(x, y));
                RefreshPipe(new Vector2I(x, y));
                RefreshWater(new Vector2I(x, y));
            }
    }

    public int TileIdAt(Vector2I cell) =>
        Main.GetCellTileData(cell) is { } data ? data.GetCustomData(TileSetBuilder.TileIdData).AsInt32() : TileDefs.Empty;

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

    private void RefreshShadow(Vector2I cell)
    {
        var mask = ShadowMask.None;
        if (TileDefs.ReceivesShadow(TileIdAt(cell)))
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
}
