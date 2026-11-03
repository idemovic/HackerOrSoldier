using Godot;

namespace HackerOrSoldier;

// Shared tile-to-layer routing for the editor and the game scene: hidden
// platforms live on their own layer, everything else on the main grid.
public static class LevelGrids
{
    public static void SetCell(TileMapLayer grid, TileMapLayer hiddenGrid, Vector2I cell, int tileId)
    {
        var target = tileId == TileDefs.HiddenPlatform ? hiddenGrid : grid;
        var other = tileId == TileDefs.HiddenPlatform ? grid : hiddenGrid;
        other.EraseCell(cell);
        target.SetCell(cell, TileSetBuilder.SourceId, TileSetBuilder.GridCoordsFor(tileId, cell));
    }

    public static void Fill(TileMapLayer grid, TileMapLayer hiddenGrid, LevelData level)
    {
        grid.Clear();
        hiddenGrid.Clear();
        for (int y = 0; y < level.Height; y++)
            for (int x = 0; x < level.Width; x++)
            {
                int id = level.GetTile(x, y);
                if (id != TileDefs.Empty)
                    SetCell(grid, hiddenGrid, new Vector2I(x, y), id);
            }
    }
}
