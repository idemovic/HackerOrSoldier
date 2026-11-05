using System.Collections.Generic;
using Godot;

namespace HackerOrSoldier;

// The editor's look: flat VGA-style panels with hard 1px borders, a cyan
// accent for the active choice, and 12x12 pixel icons drawn from ASCII masks
// like the pixel font. Icons are white so the theme tints them per state.
public static class EditorTheme
{
    public static readonly Color Ink = new("e8ecff");
    public static readonly Color Dim = new("7d86b3");
    public static readonly Color Accent = new("3ee6f0");
    public static readonly Color Gold = new("ffd23e");
    public static readonly Color Danger = new("ff4d6d");
    public static readonly Color Go = new("35d07f");

    private static readonly Color PanelBg = new("0b0e1c", 0.94f);
    private static readonly Color PanelEdge = new("2c3463");
    private static readonly Color ButtonBg = new("161b33");
    private static readonly Color ButtonHover = new("252d57");
    private static readonly Color ButtonOn = new("0d4c5c");
    private static readonly Color Shadow = new("000000", 0.85f);

    public const string Slot = "SlotButton";
    public const string Tab = "TabButton";
    public const string Play = "PlayButton";
    public const string Chip = "ChipButton";
    public const string Badge = "BadgeLabel";
    public const string ShiftBadge = "ShiftBadgeLabel";
    public const string Heading = "HeadingLabel";
    public const string DimLabel = "DimLabel";
    public const string KeyLabel = "KeyLabel";

    public static Theme Build()
    {
        var theme = new Theme { DefaultFontSize = 8 };

        theme.SetStylebox("panel", "PanelContainer", Box(PanelBg, PanelEdge, margin: 3));
        theme.SetStylebox("panel", "Panel", Box(PanelBg, PanelEdge));

        StyleButton(theme, "Button", ButtonBg, ButtonHover, ButtonOn, Accent, margin: 3);
        theme.SetColor("font_color", "Button", Ink);
        theme.SetColor("font_hover_color", "Button", Colors.White);
        theme.SetColor("font_pressed_color", "Button", Accent);
        theme.SetColor("font_hover_pressed_color", "Button", Accent);
        theme.SetColor("icon_normal_color", "Button", Ink);
        theme.SetColor("icon_hover_color", "Button", Colors.White);
        theme.SetColor("icon_pressed_color", "Button", Accent);
        theme.SetColor("icon_hover_pressed_color", "Button", Accent);
        theme.SetConstant("h_separation", "Button", 3);

        // Palette slots and category tabs keep their art untinted.
        foreach (var type in new[] { Slot, Tab })
        {
            theme.SetTypeVariation(type, "Button");
            StyleButton(theme, type, ButtonBg, ButtonHover, ButtonOn, Accent, margin: 2);
            foreach (var state in new[] { "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_hover_pressed_color" })
                theme.SetColor(state, type, Colors.White);
        }

        theme.SetTypeVariation(Play, "Button");
        StyleButton(theme, Play, new Color("1d6b45"), new Color("27905c"), new Color("7a1f33"), Go, margin: 4);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color",
                     "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_hover_pressed_color" })
            theme.SetColor(state, Play, Colors.White);

        theme.SetTypeVariation(Chip, "Button");
        StyleButton(theme, Chip, ButtonBg, ButtonHover, ButtonOn, Accent, margin: 1);

        theme.SetColor("font_color", "Label", Ink);
        theme.SetColor("font_shadow_color", "Label", Shadow);
        theme.SetConstant("shadow_offset_x", "Label", 1);
        theme.SetConstant("shadow_offset_y", "Label", 1);
        LabelVariation(theme, Badge, Ink);
        LabelVariation(theme, ShiftBadge, Gold);
        LabelVariation(theme, Heading, Accent);
        LabelVariation(theme, DimLabel, Dim);
        LabelVariation(theme, KeyLabel, Gold);

        theme.SetStylebox("panel", "TooltipPanel", Box(new Color("05070f"), Accent, margin: 3));
        theme.SetColor("font_color", "TooltipLabel", Ink);

        theme.SetStylebox("panel", "PopupMenu", Box(PanelBg with { A = 1 }, Accent, margin: 3));
        theme.SetStylebox("hover", "PopupMenu", Box(ButtonOn, ButtonOn));
        theme.SetColor("font_color", "PopupMenu", Ink);
        theme.SetColor("font_hover_color", "PopupMenu", Colors.White);
        theme.SetColor("font_accelerator_color", "PopupMenu", Gold);
        theme.SetConstant("v_separation", "PopupMenu", 4);
        theme.SetConstant("h_separation", "PopupMenu", 8);

        // Dialogs are embedded windows: their frame comes from "Window".
        theme.SetStylebox("embedded_border", "Window", Box(PanelBg with { A = 1 }, Accent, margin: 6, top: 18));
        theme.SetStylebox("embedded_unfocused_border", "Window", Box(PanelBg with { A = 1 }, PanelEdge, margin: 6, top: 18));
        theme.SetColor("title_color", "Window", Accent);
        theme.SetConstant("title_height", "Window", 16);
        theme.SetStylebox("panel", "AcceptDialog", Box(PanelBg with { A = 1 }, PanelBg with { A = 1 }, margin: 6));

        var field = Box(new Color("05070f"), PanelEdge, margin: 3);
        var fieldFocus = Box(new Color("05070f"), Accent, margin: 3);
        foreach (var type in new[] { "LineEdit", "TextEdit", "ItemList" })
        {
            theme.SetStylebox("normal", type, field);
            theme.SetStylebox("focus", type, fieldFocus);
            theme.SetStylebox("panel", type, field);
            theme.SetColor("font_color", type, Ink);
        }
        theme.SetStylebox("selected", "ItemList", Box(ButtonOn, Accent));
        theme.SetStylebox("selected_focus", "ItemList", Box(ButtonOn, Accent));
        theme.SetStylebox("hovered", "ItemList", Box(ButtonHover, ButtonHover));
        theme.SetColor("font_selected_color", "ItemList", Accent);

        theme.SetStylebox("separator", "VSeparator", new StyleBoxLine { Color = PanelEdge, Vertical = true, Thickness = 1 });
        theme.SetConstant("separation", "VSeparator", 5);
        theme.SetConstant("separation", "HBoxContainer", 2);
        theme.SetConstant("separation", "VBoxContainer", 2);
        return theme;
    }

    private static void StyleButton(Theme theme, string type, Color normal, Color hover, Color on, Color onEdge, int margin)
    {
        theme.SetStylebox("normal", type, Box(normal, normal.Lightened(0.12f), margin));
        theme.SetStylebox("hover", type, Box(hover, hover.Lightened(0.2f), margin));
        theme.SetStylebox("pressed", type, Box(on, onEdge, margin));
        theme.SetStylebox("hover_pressed", type, Box(on.Lightened(0.1f), onEdge, margin));
        theme.SetStylebox("disabled", type, Box(normal, normal, margin));
        theme.SetStylebox("focus", type, new StyleBoxEmpty());
    }

    private static void LabelVariation(Theme theme, string type, Color color)
    {
        theme.SetTypeVariation(type, "Label");
        theme.SetColor("font_color", type, color);
    }

    private static StyleBoxFlat Box(Color fill, Color edge, int margin = 2, int top = -1) => new()
    {
        BgColor = fill,
        BorderColor = edge,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        ContentMarginLeft = margin,
        ContentMarginRight = margin,
        ContentMarginTop = top >= 0 ? top : margin,
        ContentMarginBottom = margin,
        AntiAliasing = false,
    };

    private static readonly Dictionary<string, string[]> IconMasks = new()
    {
        ["play"] = new[]
        {
            "............",
            "...#........",
            "...##.......",
            "...###......",
            "...####.....",
            "...#####....",
            "...#####....",
            "...####.....",
            "...###......",
            "...##.......",
            "...#........",
            "............",
        },
        ["stop"] = new[]
        {
            "............",
            "............",
            "..########..",
            "..########..",
            "..########..",
            "..########..",
            "..########..",
            "..########..",
            "..########..",
            "..########..",
            "............",
            "............",
        },
        ["brush"] = new[]
        {
            "............",
            ".........##.",
            "........####",
            ".......####.",
            "......####..",
            ".....####...",
            "....####....",
            "...####.....",
            "..####......",
            "..###.......",
            ".##.........",
            ".#..........",
        },
        ["rect"] = new[]
        {
            "............",
            ".#.#.#.#.#..",
            ".#........#.",
            ".#........#.",
            "............",
            ".#........#.",
            ".#........#.",
            "............",
            ".#........#.",
            ".#........#.",
            "..#.#.#.#.#.",
            "............",
        },
        ["eraser"] = new[]
        {
            "............",
            "......####..",
            ".....######.",
            "....#.#####.",
            "...#...###..",
            "..#...#.#...",
            ".#...#..#...",
            ".#..#..#....",
            "..##..#.....",
            "...####.....",
            "............",
            ".##########.",
        },
        ["picker"] = new[]
        {
            "............",
            ".........##.",
            "........####",
            "......######",
            ".......####.",
            "......#.##..",
            ".....#.#....",
            "....#.#.....",
            "...#.#......",
            "..#.#.......",
            ".###........",
            ".#..........",
        },
        ["undo"] = new[]
        {
            "............",
            "...#........",
            "..##........",
            ".#########..",
            "..##.....##.",
            "...#......##",
            "...........#",
            "...........#",
            "..........##",
            ".....######.",
            "............",
            "............",
        },
        ["grid"] = new[]
        {
            "............",
            "..#..#..#...",
            "############",
            "..#..#..#...",
            "..#..#..#...",
            "############",
            "..#..#..#...",
            "..#..#..#...",
            "############",
            "..#..#..#...",
            "..#..#..#...",
            "............",
        },
        ["sun"] = new[]
        {
            "............",
            ".....##.....",
            ".#...##...#.",
            "..#......#..",
            "....####....",
            "...######...",
            "##.######.##",
            "...######...",
            "....####....",
            "..#......#..",
            ".#...##...#.",
            ".....##.....",
        },
        ["moon"] = new[]
        {
            "............",
            "....####....",
            "..####......",
            ".####.......",
            ".###........",
            ".###........",
            ".###........",
            ".###........",
            ".####.......",
            "..####......",
            "....####....",
            "............",
        },
        ["shield"] = new[]
        {
            "............",
            ".##########.",
            ".#........#.",
            ".#..####..#.",
            ".#..####..#.",
            ".#..####..#.",
            ".#..####..#.",
            "..#..##..#..",
            "..#......#..",
            "...#....#...",
            "....#..#....",
            ".....##.....",
        },
        ["eye"] = new[]
        {
            "............",
            "............",
            "....####....",
            "..##....##..",
            ".#...##...#.",
            "#...####...#",
            "#...####...#",
            ".#...##...#.",
            "..##....##..",
            "....####....",
            "............",
            "............",
        },
        ["menu"] = new[]
        {
            "............",
            "............",
            ".##########.",
            ".##########.",
            "............",
            ".##########.",
            ".##########.",
            "............",
            ".##########.",
            ".##########.",
            "............",
            "............",
        },
        ["help"] = new[]
        {
            "............",
            "...######...",
            "..##....##..",
            "........##..",
            ".......##...",
            "......##....",
            ".....##.....",
            ".....##.....",
            "............",
            ".....##.....",
            ".....##.....",
            "............",
        },
    };

    private static readonly Dictionary<string, ImageTexture> IconCache = new();

    // "redo" is "undo" mirrored.
    public static ImageTexture Icon(string name)
    {
        if (IconCache.TryGetValue(name, out var cached))
            return cached;
        bool mirror = name == "redo";
        var rows = IconMasks[mirror ? "undo" : name];
        var img = Image.CreateEmpty(12, 12, false, Image.Format.Rgba8);
        for (int y = 0; y < 12; y++)
        {
            if (rows.Length != 12 || rows[y].Length != 12)
                throw new System.Exception($"editor icon '{name}' row {y} is not 12x12");
            for (int x = 0; x < 12; x++)
                if (rows[y][x] == '#')
                    img.SetPixel(mirror ? 11 - x : x, y, Colors.White);
        }
        var texture = ImageTexture.CreateFromImage(img);
        IconCache[name] = texture;
        return texture;
    }
}
