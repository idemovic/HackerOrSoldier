using Godot;

namespace HackerOrSoldier;

// Builds the placeholder TileSet at runtime: one solid-color 32x32 tile per
// TileDef, with a collision square on solid tiles. Swap for a real atlas
// texture later without touching the editor or level format.
public static class TileSetBuilder
{
    public const int TileSize = 32;
    public const int SourceId = 0;

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
        {
            image.FillRect(new Rect2I(i * TileSize, 0, TileSize, TileSize), defs[i].Color.Darkened(0.35f));
            image.FillRect(new Rect2I(i * TileSize + 2, 2, TileSize - 4, TileSize - 4), defs[i].Color);
        }

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
}
