using Godot;

namespace HackerOrSoldier;

// The three tile layers a level renders with, owned together so the editor
// and the game keep them in step. Solid and background tiles sit on the
// main grid, hidden platforms on their own grid (play mode hides it, the
// goggles reveal it), and the shadow grid above both. The shadow grid is
// derived from the main grid and never stored in the level: a receiving
// cell gets the shadow tile for whichever of its upper, left and lower
// neighbours is solid, so it follows painting, erasing, walls destroyed in
// play and the restore on returning to the editor.
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

    // The tile id drawn on the main grid at a cell, Empty when there is none.
    public int TileIdAt(Vector2I cell) =>
        Main.GetCellTileData(cell) is { } data ? data.GetCustomData(TileSetBuilder.TileIdData).AsInt32() : TileDefs.Empty;

    // Cells with no tile are the outdoors: the level's sky (night or day)
    // fills the whole level rect behind the tile layers, and painted
    // background tiles cover it per cell.
    public static void UpdateSky(Sprite2D sky, LevelData level)
    {
        sky.Texture = level.Sky == "day"
            ? TileSetBuilder.TileTexture(TileDefs.BackgroundSkyDay)
            : TileSetBuilder.NightSkySheet();
        sky.TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled;
        sky.Centered = false;
        sky.RegionEnabled = true;
        sky.RegionRect = new Rect2(0, 0,
            level.Width * TileSetBuilder.TileSize, level.Height * TileSetBuilder.TileSize);
    }

    private void Place(Vector2I cell, int tileId)
    {
        var target = tileId == TileDefs.HiddenPlatform ? Hidden : Main;
        var other = tileId == TileDefs.HiddenPlatform ? Main : Hidden;
        other.EraseCell(cell);
        target.SetCell(cell, TileSetBuilder.SourceId, TileSetBuilder.GridCoordsFor(tileId, cell));
    }

    // A changed cell shades itself and the cells it sits above, left of and below.
    private void RefreshShadowsAround(Vector2I cell)
    {
        RefreshShadow(cell);
        RefreshShadow(cell + Vector2I.Down);
        RefreshShadow(cell + Vector2I.Right);
        RefreshShadow(cell + Vector2I.Up);
    }

    private void RefreshShadow(Vector2I cell)
    {
        var mask = ShadowMask.None;
        if (TileDefs.ReceivesShadow(TileIdAt(cell)))
        {
            if (TileDefs.CastsShadow(TileIdAt(cell + Vector2I.Up)))
                mask |= ShadowMask.Above;
            if (TileDefs.CastsShadow(TileIdAt(cell + Vector2I.Left)))
                mask |= ShadowMask.Left;
            if (TileDefs.CastsShadow(TileIdAt(cell + Vector2I.Down)))
                mask |= ShadowMask.Below;
        }
        if (mask == ShadowMask.None)
            Shadow.EraseCell(cell);
        else
            Shadow.SetCell(cell, TileSetBuilder.SourceId, TileSetBuilder.ShadowCoordsFor(mask));
    }
}
