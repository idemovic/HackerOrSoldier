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
    }

    public void EraseCell(Vector2I cell)
    {
        Main.EraseCell(cell);
        Hidden.EraseCell(cell);
        RefreshShadowsAround(cell);
    }

    public void Fill(LevelData level)
    {
        Main.Clear();
        Hidden.Clear();
        Shadow.Clear();
        for (int y = 0; y < level.Height; y++)
            for (int x = 0; x < level.Width; x++)
            {
                int id = level.GetTile(x, y);
                if (id != TileDefs.Empty)
                    Place(new Vector2I(x, y), id);
            }
        for (int y = 0; y < level.Height; y++)
            for (int x = 0; x < level.Width; x++)
                RefreshShadow(new Vector2I(x, y));
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
}
