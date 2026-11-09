using System.Collections.Generic;
using Godot;

namespace HackerOrSoldier;

// The campaign's world map: the continents, a pin per stop, the route flown
// so far and the plane. It fills the 640x360 base screen (centered when the
// window is larger) and is the backdrop of every screen outside a level.
// Stop indexes are the campaign's; -1 is the Agency HQ the plane leaves from.
public partial class WorldMap : Node2D
{
    [Signal]
    public delegate void LandedEventHandler();

    public const int Width = 640;
    public const int Height = 360;
    // Crop: far enough north for Reykjavik and the Arctic coast, far enough
    // south for the Antarctic coast and the base at -76.
    public const float LatTop = 80f;
    public const float LatBottom = -84f;
    public const float FlightSeconds = 2.6f;

    private const float BlinkSeconds = 0.3f;
    private const int PinSize = 5;

    private static readonly Color Ink = new("e8ecff");
    private static readonly Color PanelBg = new("0b0e1c", 0.94f);
    private static readonly Color PanelEdge = new("2c3463");
    private static readonly Color LitFill = new(0.30f, 0.95f, 0.55f);
    private static readonly Color NowFill = new(1f, 0.85f, 0.25f);
    private static readonly Color DimFill = new(0.42f, 0.45f, 0.58f);
    private static readonly Color TargetFill = new(0.95f, 0.3f, 0.3f);
    private static readonly Color Outline = new(0.04f, 0.04f, 0.1f);
    private static readonly Color RouteOld = new(0.75f, 0.72f, 0.5f);
    private static readonly Color RouteNow = new(1f, 0.9f, 0.4f);

    private static readonly string[] PinMask =
    {
        ".ooo.",
        "ohooo",
        "ooooo",
        ".ooo.",
        "..o..",
    };

    private static readonly string[] TargetMask =
    {
        "....o....",
        "..ooooo..",
        ".oo.o.oo.",
        ".o..o..o.",
        "ooooooooo",
        ".o..o..o.",
        ".oo.o.oo.",
        "..ooooo..",
        "....o....",
    };

    private static readonly string[] StarMask =
    {
        "..o..",
        ".ooo.",
        "ooooo",
        ".ooo.",
        ".o.o.",
    };

    private static readonly Dictionary<string, ImageTexture> Icons = new();
    private static ImageTexture _land;

    private readonly Sprite2D _plane = new();
    private readonly Sprite2D _planeSeam = new();   // the same plane one map width over, for the date line
    private int _taken;
    private int _routeThrough = -1;
    private int _from = -1;
    private int _to = -1;
    private float _flight;
    private bool _flying;
    private bool _blink;
    private float _time;
    private float _heading;

    public bool Flying => _flying;
    public int Taken => _taken;
    public Vector2 PlanePosition => _plane.Position;
    public float PlaneHeading => _heading;

    // The one projection: equirectangular, longitude across the full width.
    public static Vector2 Project(float lat, float lon) => new(
        (lon + 180f) / 360f * Width,
        (LatTop - lat) / (LatTop - LatBottom) * Height);

    public static Vector2 StopPoint(int index)
    {
        var stop = Campaign.StopAt(index);
        return Project(stop.Lat, stop.Lon);
    }

    // The end of a leg in map pixels, unwrapped: the longitude difference is
    // taken the short way round, so a leg may end past either map edge.
    public static Vector2 LegEnd(int from, int to)
    {
        var a = Campaign.StopAt(from);
        var b = Campaign.StopAt(to);
        float dLon = Mathf.Wrap(b.Lon - a.Lon, -180f, 180f);
        return new Vector2(StopPoint(from).X + dLon * Width / 360f, StopPoint(to).Y);
    }

    // A city dialog docks on the half of the screen away from its stop.
    public static bool DialogAtTop(int stop) => StopPoint(stop).Y > Height / 2f;

    public override void _Ready()
    {
        _land ??= ImageTexture.CreateFromImage(WorldGeography.Render(Width, Height));
        TextureFilter = TextureFilterEnum.Nearest;
        var planeTexture = Sprites.Get("world_plane");
        foreach (var plane in new[] { _plane, _planeSeam })
        {
            plane.Texture = planeTexture;
            plane.ZIndex = 2;
            AddChild(plane);
        }
        GetViewport().SizeChanged += Recenter;
        Recenter();
        Park(-1);
    }

    public override void _ExitTree() => GetViewport().SizeChanged -= Recenter;

    // Pins before index n are lit, the pin at n is the one to reach next
    // (none once all fifteen are taken).
    public void SetProgress(int taken)
    {
        _taken = Mathf.Clamp(taken, 0, Campaign.Stops.Length);
        QueueRedraw();
    }

    public void Park(int stop)
    {
        _flying = false;
        _from = _to = stop;
        _routeThrough = stop;
        _heading = Heading(Mathf.Max(stop, 0) - 1, Mathf.Max(stop, 0));
        PlacePlane(StopPoint(stop));
        QueueRedraw();
    }

    public void Fly(int from, int to)
    {
        _from = from;
        _to = to;
        _routeThrough = to;
        _flight = 0f;
        _flying = true;
        _heading = Heading(from, to);
        PlacePlane(StopPoint(from));
        QueueRedraw();
    }

    // Puts the flight at a fraction of its time, for screenshots and tests.
    public void Seek(float t)
    {
        _flight = Mathf.Clamp(t, 0f, 1f);
        PlacePlane(StopPoint(_from).Lerp(LegEnd(_from, _to), Ease(_flight)));
    }

    private static float Ease(float t) => t * t * (3f - 2f * t);

    // Any click or key ends the flight at its destination.
    public void Skip()
    {
        if (_flying)
            Land();
    }

    public override void _Input(InputEvent @event)
    {
        if (!_flying)
            return;
        bool press = @event is InputEventMouseButton { Pressed: true } or InputEventKey { Pressed: true, Echo: false }
            or InputEventJoypadButton { Pressed: true };
        if (!press)
            return;
        GetViewport().SetInputAsHandled();
        Skip();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        bool blink = (int)(_time / BlinkSeconds) % 2 == 0;
        if (blink != _blink)
        {
            _blink = blink;
            QueueRedraw();
        }
        if (!_flying)
            return;
        _flight += dt / FlightSeconds;
        if (_flight >= 1f)
        {
            Land();
            return;
        }
        Seek(_flight);
    }

    // Draws are in map pixels; the node sits where the map is centered.
    public override void _Draw()
    {
        var screen = GetViewportRect().Size;
        DrawRect(new Rect2(-Position, screen), new Color("0e1f66"));
        DrawTexture(_land, Vector2.Zero);

        for (int leg = 0; leg <= _routeThrough; leg++)
            DrawDashed(StopPoint(leg - 1), LegEnd(leg - 1, leg), leg == _routeThrough ? RouteNow : RouteOld);

        DrawIcon(Icon(StarMask, NowFill), StopPoint(-1).Round(), new Vector2(3, 3));
        for (int i = 0; i < Campaign.Stops.Length; i++)
            DrawStop(i);
        DrawLabel();
        DrawCounter();
    }

    private void DrawStop(int i)
    {
        bool final = i == Campaign.Stops.Length - 1;
        bool lit = i < _taken;
        bool now = i == _taken;
        var fill = lit ? LitFill : now ? (_blink ? NowFill : Colors.White) : final ? TargetFill : DimFill;
        var p = StopPoint(i).Round();
        if (final)
            DrawIcon(Icon(TargetMask, fill), p, new Vector2(5, 5));
        else
            DrawIcon(Icon(PinMask, fill), p, new Vector2(3, 5));   // the pin's tip sits on the city
    }

    private void DrawLabel()
    {
        if (_taken >= Campaign.Stops.Length)
            return;
        var font = ThemeDB.FallbackFont;
        string text = Campaign.Stops[_taken].City;
        float width = font.GetStringSize(text, HorizontalAlignment.Left, -1, 8).X;
        var p = StopPoint(_taken).Round();
        float x = p.X + 7 + width > Width - 2 ? p.X - 7 - width : p.X + 7;
        var at = new Vector2(Mathf.Round(x), p.Y - 4);
        foreach (var shift in new[] { Vector2.Left, Vector2.Right, Vector2.Up, Vector2.Down })
            DrawString(font, at + shift, text, HorizontalAlignment.Left, -1, 8, Colors.Black);
        DrawString(font, at, text, HorizontalAlignment.Left, -1, 8, Colors.White);
    }

    private void DrawCounter()
    {
        var font = ThemeDB.FallbackFont;
        var icon = Sprites.Trimmed(CollectibleDefs.AdminPassword);
        string text = $"Admin passwords {_taken}/{Campaign.Stops.Length}";
        float textWidth = font.GetStringSize(text, HorizontalAlignment.Left, -1, 8).X;
        // Opposite the dialog, which docks away from the stop being shown.
        float top = DialogAtTop(_taken < Campaign.Stops.Length ? _taken : Campaign.Stops.Length - 1)
            ? Height - icon.GetHeight() - 10 : 4;
        var rect = new Rect2(4, top, icon.GetWidth() + textWidth + 14, icon.GetHeight() + 6);
        DrawRect(rect, PanelBg);
        DrawRect(rect, PanelEdge, filled: false);
        DrawTexture(icon, rect.Position + new Vector2(4, 3));
        var at = new Vector2(rect.Position.X + icon.GetWidth() + 8, rect.Position.Y + rect.Size.Y / 2f + 3).Round();
        DrawString(font, at + Vector2.One, text, HorizontalAlignment.Left, -1, 8, Colors.Black);
        DrawString(font, at, text, HorizontalAlignment.Left, -1, 8, _taken == Campaign.Stops.Length ? LitFill : Ink);
    }

    // A pixel-stepped dashed line, three on and three off, wrapped at the map edges.
    private void DrawDashed(Vector2 from, Vector2 to, Color color)
    {
        int steps = Mathf.CeilToInt(from.DistanceTo(to));
        for (int s = 0; s <= steps; s++)
        {
            if (s % 6 >= 3)
                continue;
            var p = from.Lerp(to, steps == 0 ? 0f : s / (float)steps).Round();
            DrawRect(new Rect2(Mathf.PosMod(p.X, Width), p.Y, 1, 1), color);
        }
    }

    // anchor is the icon pixel that lands on the point (outline included).
    private void DrawIcon(Texture2D icon, Vector2 point, Vector2 anchor) => DrawTexture(icon, point - anchor);

    private static ImageTexture Icon(string[] mask, Color fill)
    {
        string key = $"{mask[0]}{mask.Length}{fill.ToHtml()}";
        if (Icons.TryGetValue(key, out var cached))
            return cached;
        int w = mask[0].Length, h = mask.Length;
        var img = Image.CreateEmpty(w + 2, h + 2, false, Image.Format.Rgba8);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (mask[y][x] == '.')
                    continue;
                for (int dy = 0; dy <= 2; dy++)
                    for (int dx = 0; dx <= 2; dx++)
                        if (img.GetPixel(x + dx, y + dy).A == 0f)
                            img.SetPixel(x + dx, y + dy, Outline);
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (mask[y][x] != '.')
                    img.SetPixel(x + 1, y + 1, mask[y][x] == 'h' ? fill.Lightened(0.5f) : fill);
        var texture = ImageTexture.CreateFromImage(img);
        Icons[key] = texture;
        return texture;
    }

    private static float Heading(int from, int to)
    {
        var d = LegEnd(from, to) - StopPoint(from);
        return d.LengthSquared() < 1f ? 0f : Mathf.Atan2(d.Y, d.X);
    }

    private void Land()
    {
        _flying = false;
        PlacePlane(StopPoint(_to));
        EmitSignal(SignalName.Landed);
    }

    // The plane flies in unwrapped coordinates; it shows at the wrapped
    // position, with a second copy while it straddles the date line.
    private void PlacePlane(Vector2 unwrapped)
    {
        float x = Mathf.PosMod(unwrapped.X, Width);
        _plane.Position = new Vector2(Mathf.Round(x), Mathf.Round(unwrapped.Y));
        _plane.Rotation = _heading;
        const float Reach = 10f;
        _planeSeam.Visible = x < Reach || x > Width - Reach;
        _planeSeam.Position = _plane.Position + new Vector2(x < Width / 2f ? Width : -Width, 0);
        _planeSeam.Rotation = _heading;
    }

    private void Recenter()
    {
        var screen = GetViewportRect().Size;
        Position = ((screen - new Vector2(Width, Height)) / 2f).Floor();
        QueueRedraw();
    }
}
