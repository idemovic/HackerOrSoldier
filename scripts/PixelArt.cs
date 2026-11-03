using Godot;

namespace HackerOrSoldier;

// Turns a flat-colour sprite map into finished native-resolution art:
// doubles it with EPX (Scale2x) so diagonals get real steps instead of
// 2x2 blocks, lights the top-left edges and shades the bottom-right ones
// like the tiles, dithers shade into the lower part of the form, and rings
// the silhouette with a dark outline. Hand-drawn maps keep their colours
// and layout; only the rendering gets finer.
public static class PixelArt
{
    private const float RimLight = 0.22f;
    private const float EdgeShade = 0.3f;
    private const float BodyShade = 0.16f;
    private const float OutlineDarken = 0.72f;

    public static Image Finish(Image img, bool upscale)
    {
        if (upscale)
            img = Scale2x(img);
        int w = img.GetWidth(), h = img.GetHeight();
        var src = (Image)img.Duplicate();
        var used = src.GetUsedRect();
        float shadeFrom = used.Position.Y + used.Size.Y * 0.6f;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = src.GetPixel(x, y);
                if (c.A == 0f)
                {
                    var neighbour = OpaqueNeighbour(src, x, y);
                    if (neighbour is Color n && n.Luminance > 0.12f)
                        img.SetPixel(x, y, n.Darkened(OutlineDarken));
                    continue;
                }
                // Outlines and near-black detail stay as drawn: lighting
                // them would grey out the line work.
                if (c.Luminance < 0.12f)
                    continue;
                bool lit = Clear(src, x, y - 1) || Clear(src, x - 1, y);
                bool shaded = Clear(src, x, y + 1) || Clear(src, x + 1, y);
                if (lit && !shaded)
                    c = c.Lightened(RimLight);
                else if (shaded && !lit)
                    c = c.Darkened(EdgeShade);
                else if (y >= shadeFrom && (x + y) % 2 == 0)
                    c = c.Darkened(BodyShade);
                img.SetPixel(x, y, c);
            }
        return img;
    }

    private static bool Clear(Image img, int x, int y) =>
        x < 0 || y < 0 || x >= img.GetWidth() || y >= img.GetHeight() || img.GetPixel(x, y).A == 0f;

    private static Color? OpaqueNeighbour(Image img, int x, int y)
    {
        foreach (var (dx, dy) in new[] { (0, 1), (-1, 0), (1, 0), (0, -1) })
            if (!Clear(img, x + dx, y + dy))
                return img.GetPixel(x + dx, y + dy);
        return null;
    }

    // EPX: each pixel becomes four, and a corner takes a neighbour's colour
    // where two neighbours agree, so staircases become smooth diagonals.
    private static Image Scale2x(Image src)
    {
        int w = src.GetWidth(), h = src.GetHeight();
        var dst = Image.CreateEmpty(w * 2, h * 2, false, Image.Format.Rgba8);
        Color At(int x, int y) => src.GetPixel(Mathf.Clamp(x, 0, w - 1), Mathf.Clamp(y, 0, h - 1));
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color p = At(x, y), a = At(x, y - 1), b = At(x + 1, y), c = At(x - 1, y), d = At(x, y + 1);
                dst.SetPixel(2 * x, 2 * y, c == a && c != d && a != b ? a : p);
                dst.SetPixel(2 * x + 1, 2 * y, a == b && a != c && b != d ? b : p);
                dst.SetPixel(2 * x, 2 * y + 1, d == c && d != b && c != a ? c : p);
                dst.SetPixel(2 * x + 1, 2 * y + 1, b == d && b != a && d != c ? d : p);
            }
        return dst;
    }
}
