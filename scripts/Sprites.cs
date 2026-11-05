using System.Collections.Generic;
using Godot;

namespace HackerOrSoldier;

// Runtime-generated pixel art for every object, in the README's aesthetic:
// stark saturated VGA-style colors. Maps are authored at 16x16 and finished
// at native 32x32 by PixelArt (EPX doubling, edge lighting, outline). The
// agent is not a map: BodyPoses draws it from a skeleton. '.' is
// transparent; 'X'/'x' are slots replaced by the door/key color variant.
public static class Sprites
{
    private static readonly Dictionary<char, Color> Palette = new()
    {
        ['k'] = new Color("141419"), // black
        ['d'] = new Color("2a2a33"), // dark grey
        ['g'] = new Color("55555f"), // grey
        ['l'] = new Color("9a9aa5"), // light grey
        ['w'] = new Color("f2f2f2"), // white
        ['s'] = new Color("e8b08a"), // skin
        ['h'] = new Color("2b2118"), // dark hair
        ['r'] = new Color("e03b2f"), // red
        ['R'] = new Color("8f1f1a"), // dark red
        ['o'] = new Color("f28022"), // orange
        ['y'] = new Color("f2d43b"), // yellow
        ['n'] = new Color("8a5a2b"), // brown
        ['N'] = new Color("5a3a1a"), // dark brown
        ['t'] = new Color("d9b070"), // tan
        ['e'] = new Color("3fae4e"), // green
        ['E'] = new Color("1d6e2c"), // dark green
        ['b'] = new Color("3b6fe0"), // blue
        ['B'] = new Color("1f3a8f"), // dark blue
        ['c'] = new Color("45d6d6"), // cyan
        ['C'] = new Color("1f8f8f"), // dark cyan
        ['m'] = new Color("d63bd6"), // magenta
        ['p'] = new Color("7a3be0"), // purple
        ['P'] = new Color("46208a"), // dark purple
        ['v'] = new Color("7a8aa5"), // steel
        ['V'] = new Color("4a5570"), // dark steel
        ['u'] = new Color("5f6c87"), // mid steel
        ['G'] = new Color("6cf27a"), // lit LED green
        ['q'] = new Color("173a20"), // unlit LED segment
    };

    private static readonly Dictionary<string, ImageTexture> Cache = new();

    public static ImageTexture Get(string key, string colorName = null)
    {
        string cacheKey = colorName == null ? key : $"{key}:{colorName}";
        if (Cache.TryGetValue(cacheKey, out var cached))
            return cached;
        if (!Maps.TryGetValue(key, out var map))
            return BodyPoses.Find(key) is BodyFrame frame ? BodyPoses.Texture(frame) : null;
        var slot = colorName != null ? ObjectDefs.SlotColor(colorName) : Colors.Magenta;
        var texture = ImageTexture.CreateFromImage(FromMap(key, map.Rows, map.Scale, map.ShadeBody, slot));
        Cache[cacheKey] = texture;
        return texture;
    }

    // Resolves any placeable type (object, enemy, collectible, decoration)
    // to its sprite; falls back to a flat swatch so a missing map can never
    // crash the editor.
    public static ImageTexture ForObject(string type, string colorName = null)
    {
        string key = type switch
        {
            ObjectDefs.AgentStart => "agent",
            ObjectDefs.Elevator or ObjectDefs.MovingPlatform => "platform",
            _ => type,
        };
        var texture = Get(key, ObjectDefs.HasColorProperty(type) ? ObjectDefs.ColorOf(type, colorName) : null);
        if (texture != null)
            return texture;
        var color = System.Array.Find(ObjectDefs.All, d => d.Type == type)?.Color
            ?? EnemyDefs.Get(type)?.Color
            ?? CollectibleDefs.Get(type)?.Color
            ?? DecorationDefs.Get(type)?.Color
            ?? Colors.Magenta;
        var img = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
        img.Fill(color);
        var fallback = ImageTexture.CreateFromImage(img);
        Cache[type] = fallback;
        return fallback;
    }

    // Small version for palette buttons.
    public static ImageTexture Icon(string type, string colorName = null)
    {
        string cacheKey = $"icon:{type}:{colorName}";
        if (Cache.TryGetValue(cacheKey, out var cached))
            return cached;
        var img = ForObject(type, colorName).GetImage();
        int side = Mathf.Min(img.GetWidth(), img.GetHeight());
        img = img.GetRegion(new Rect2I((img.GetWidth() - side) / 2, img.GetHeight() - side, side, side));
        img.Resize(16, 16, Image.Interpolation.Nearest);
        var texture = ImageTexture.CreateFromImage(img);
        Cache[cacheKey] = texture;
        return texture;
    }

    // A sprite cropped to its drawn pixels, so object art centred in a 16x16
    // cell can sit in a HUD row at the same pixel size as the hearts.
    public static ImageTexture Trimmed(string key)
    {
        string cacheKey = $"trimmed:{key}";
        if (Cache.TryGetValue(cacheKey, out var cached))
            return cached;
        var img = Get(key).GetImage();
        var texture = ImageTexture.CreateFromImage(img.GetRegion(img.GetUsedRect()));
        Cache[cacheKey] = texture;
        return texture;
    }

    private static Image FromMap(string key, string[] rows, int scale, bool shadeBody, Color slot)
    {
        int width = rows[0].Length;
        var img = Image.CreateEmpty(width, rows.Length, false, Image.Format.Rgba8);
        for (int y = 0; y < rows.Length; y++)
        {
            if (rows[y].Length != width)
                throw new System.Exception($"sprite '{key}' row {y} has length {rows[y].Length}, expected {width}");
            for (int x = 0; x < width; x++)
            {
                char ch = rows[y][x];
                if (ch == '.')
                    continue;
                Color color = ch switch
                {
                    'X' => slot,
                    'x' => slot.Darkened(0.4f),
                    _ => Palette.TryGetValue(ch, out var c)
                        ? c
                        : throw new System.Exception($"sprite '{key}' has unknown char '{ch}'"),
                };
                img.SetPixel(x, y, color);
            }
        }
        return PixelArt.Finish(img, upscale: scale == 2, shadeBody);
    }

    private record SpriteMap(string[] Rows, int Scale = 2, bool ShadeBody = true);

    private static SpriteMap Flag(char fill, char shade) => new(System.Array.ConvertAll(new[]
    {
        "................",
        "....gl..........",
        "....glFFFFFFF...",
        "....glFFFFFFFS..",
        "....glFFFFFFFS..",
        "....glFFFFFFS...",
        "....glSSSSSS....",
        "....gl..........",
        "....gl..........",
        "....gl..........",
        "....gl..........",
        "....gl..........",
        "....gl..........",
        "....gl..........",
        "...dggld........",
        "..dddddddd......",
    }, row => row.Replace('F', fill).Replace('S', shade)));

    // CCTV box: sun hood, record LED, glass lens, wall bracket.
    private static SpriteMap CameraBox(char led) => new(System.Array.ConvertAll(new[]
    {
        "................",
        "................",
        "................",
        "................",
        "....kkkkkkkkkk..",
        "....kvvvvvvvvvk.",
        "....kkkkkkkkkkk.",
        "....kLLlllldcdk.",
        "....kgllllldCdk.",
        "....kkkkkkkkkkk.",
        "kk.....kdk......",
        "kdkkkkkkdk......",
        "kkkkkkkkkk......",
        "................",
        "................",
        "................",
    }, row => row.Replace('L', led)));

    private static readonly Dictionary<string, SpriteMap> Maps = new()
    {
        [EnemyDefs.Drone] = new(new[]
        {
            "................",
            "................",
            "..llllllllllll..",
            ".......vv.......",
            ".....vvvvvvv....",
            "....vvvvvvvcv...",
            "....vvvvvvvvv...",
            ".....VVVVVVV....",
            "......v...v.....",
            ".....vv...vv....",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        ["drone_hover"] = new(new[]
        {
            "................",
            "................",
            "................",
            ".....llllll.....",
            ".......vv.......",
            ".....vvvvvvv....",
            "....vvvvvvvcv...",
            "....vvvvvvvvv...",
            ".....VVVVVVV....",
            "......v...v.....",
            ".....vv...vv....",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        [EnemyDefs.Robodog] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "..........v.v...",
            ".v........vvvv..",
            "..vvvvvvvvvvcv..",
            "..vvvvvvvvvvvvv.",
            "..VVVVVVVVVVVV..",
            "..vv.......vv...",
            "..vv.......vv...",
            ".vv.........vv..",
            ".kkk.......kkk..",
        }),
        ["robodog_walk"] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "..........v.v...",
            "..v.......vvvv..",
            "..vvvvvvvvvvcv..",
            "..vvvvvvvvvvvvv.",
            "..VVVVVVVVVVVV..",
            "...vv.....vv....",
            "...vv.....vv....",
            "...vv.....vv....",
            "..kkk.....kkk...",
        }),
        [EnemyDefs.Turret] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "......gggg......",
            ".....gggggg.....",
            ".....ggcggllll..",
            ".....gggggg.....",
            "......gggg......",
            "....dddddddd....",
            "...dddddddddd...",
            "..VVVVVVVVVVVV..",
            "..VVVVVVVVVVVV..",
        }),
        ["turret_fire"] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "......gggg......",
            ".....gggggg...y.",
            ".....ggcgglllloy",
            ".....gggggg...y.",
            "......gggg......",
            "....dddddddd....",
            "...dddddddddd...",
            "..VVVVVVVVVVVV..",
            "..VVVVVVVVVVVV..",
        }),
        [EnemyDefs.Robot] = new(new[]
        {
            "................",
            ".....V..........",
            "....VVVVVVV.....",
            "....VVccccV.....",
            "....VVVVVVV.....",
            ".....VVVVV......",
            ".....VVV........",
            "....VVVVVVV.....",
            "....VVVVVvv.....",
            "....VVVVVVV.....",
            "....VVVVVVV.....",
            "....VVVVVVVVv...",
            "....kkVVVV......",
            "...kk....VV.....",
            "..kk......VV....",
            "..kkkk...kkkk...",
        }),
        ["robot_walk"] = new(new[]
        {
            "................",
            ".....V..........",
            "....VVVVVVV.....",
            "....VVccccV.....",
            "....VVVVVVV.....",
            ".....VVVVV......",
            ".....VVV........",
            "....VVVVVVV.....",
            "....VVVVVvv.....",
            "....VVVVVVV.....",
            "...VVVVVVVV.....",
            "..vVVVVVVVV.....",
            "....kVVVV.......",
            "....kVVVV.......",
            "....kVVVV.......",
            "...kkkkkkkk.....",
        }),
        ["robot_fire"] = new(new[]
        {
            "................",
            ".....V..........",
            "....VVVVVVV.....",
            "....VVccccV.....",
            "....VVVVVVV.....",
            ".....VVVVV......",
            ".....VVV........",
            "....VVVVVVVVVvlo",
            "....VVVVVvv...ly",
            "....VVVVVVV.....",
            "....VVVVVVV.....",
            "....VVvvVVV.....",
            "....kkVVVV......",
            "....kk..VV......",
            "...kk....VV.....",
            "..kkkk...kkkk...",
        }),
        ["robot_off"] = new(new[]
        {
            "................",
            "................",
            ".....V..........",
            "....VVVVVVV.....",
            "....VVVVVVV.....",
            "....VVVVVVV.....",
            ".....VVVVV......",
            ".....VVV........",
            "....VVVVVVV.....",
            "....VVVVVvv.....",
            "....VVVVVVV.....",
            "....VVVVVVV.....",
            "....VVvvVVV.....",
            "....kVVVV.......",
            "....kVVVV.......",
            "...kkkkkkkk.....",
        }),
        [EnemyDefs.Landmine] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            ".......rr.......",
            ".....dddddd.....",
            "...dddddddddd...",
            "..VVVVVVVVVVVV..",
        }),
        ["landmine_blink"] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            ".......RR.......",
            ".....dddddd.....",
            "...dddddddddd...",
            "..VVVVVVVVVVVV..",
        }),
        [EnemyDefs.ElectricEel] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "......ccc.......",
            "....ccCCCc...cc.",
            ".cccCC...Cccccc.",
            "...C......CcwCC.",
            "...........CC...",
            "................",
            "................",
            "................",
            "................",
        }),
        ["electric_eel_swim1"] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "....ccc......cc.",
            "..ccCCCc...cccc.",
            ".c.C...CccccwCC.",
            "........CCCCC...",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        ["electric_eel_swim2"] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            ".ccc......ccc...",
            "...Ccc...cCcwcc.",
            "....CCcccC.CCcc.",
            "......CCC....CC.",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        ["electric_eel_swim3"] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            ".c......ccc.....",
            "..cc..ccCCCcc...",
            "...CccCC...cwcc.",
            "....CC.....CCcc.",
            ".............CC.",
            "................",
            "................",
            "................",
            "................",
        }),
        [ObjectDefs.ExitDoor] = new(new[]
        {
            "................",
            "...EEEEEEEEE....",
            "...EeeeeeeeE....",
            "...EeyyyyyeE....",
            "...EeeeeeeeE....",
            "...EeeeeeeeE....",
            "...EeeeeeeeE....",
            "...EeeeeeeeE....",
            "...EeeeeeweE....",
            "...EeeeeeeeE....",
            "...EeeeeeeeE....",
            "...EeeeeeeeE....",
            "...EeeeeeeeE....",
            "...EeeeeeeeE....",
            "...EEEEEEEEE....",
            "................",
        }),
        // Red until the agent touches it, green while it is the respawn point.
        [ObjectDefs.Checkpoint] = Flag('r', 'R'),
        ["checkpoint_lit"] = Flag('e', 'E'),
        [ObjectDefs.Door] = new(new[]
        {
            "................",
            "...XXXXXXXXX....",
            "...XxxxxxxxX....",
            "...XxxxxxxxX....",
            "...XxxxxxxxX....",
            "...XxxxxxxxX....",
            "...XxxxxxxxX....",
            "...XxxxxxxxX....",
            "...XxxxyxxxX....",
            "...XxxxyxxxX....",
            "...XxxxxxxxX....",
            "...XxxxxxxxX....",
            "...XxxxxxxxX....",
            "...XxxxxxxxX....",
            "...XXXXXXXXX....",
            "................",
        }),
        [ObjectDefs.DoorKey] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "..XXX...........",
            "..X.X...........",
            "..XXXXXXXXXXX...",
            ".........X.X.X..",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        [ObjectDefs.HealthKit] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "...wwwwwwwwww...",
            "...wwwwrrwwww...",
            "...wwwrrrrwww...",
            "...wwwrrrrwww...",
            "...wwwwrrwwww...",
            "...wwwwwwwwww...",
            "...gggggggggg...",
            "................",
            "................",
            "................",
            "................",
        }),
        [ObjectDefs.AmmoPack] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "...nnnnnnnnnn...",
            "...nyynyynyyn...",
            "...nnnnnnnnnn...",
            "...nNNNNNNNNn...",
            "...nnnnnnnnnn...",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        [ObjectDefs.Vest] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "...vv......vv...",
            "...vvv....vvv...",
            "...vvvvvvvvvv...",
            "...vvVVVVVVvv...",
            "...vvVVVVVVvv...",
            "...vvvvvvvvvv...",
            "...vvvvvvvvvv...",
            "....vvvvvvvv....",
            "................",
            "................",
            "................",
            "................",
        }),
        [ObjectDefs.Briefcase] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "......nnnn......",
            ".....n....n.....",
            "...nnnnnnnnnn...",
            "...nNNNNNNNNn...",
            "...nnnnttnnnn...",
            "...nnnnnnnnnn...",
            "...NNNNNNNNNN...",
            "................",
            "................",
            "................",
            "................",
        }),
        [ObjectDefs.InfraGoggles] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "...oooooooooo...",
            "...occoooocco...",
            "...occoooocco...",
            "...oooooooooo...",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        [ObjectDefs.UsbKey] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "....ggg.........",
            "....gggttttttt..",
            "....gggttttttt..",
            "....ggg.........",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        [ObjectDefs.Computer] = new(new[]
        {
            "...gggggggggg...",
            "...gccccccccg...",
            "...gcckkkkccg...",
            "...gccccccccg...",
            "...gggggggggg...",
            "......gg........",
            ".....gggg.......",
            ".nnnnnnnnnnnnnn.",
            ".NNNNNNNNNNNNNN.",
            "..nN.....nnnnn..",
            "..nN.....nntnn..",
            "..nN.....NNNNN..",
            "..nN.....nnnnn..",
            "..nN.....nntnn..",
            "..nN.....NNNNN..",
            "..nN.....nnnnn..",
        }),
        [ObjectDefs.Switch] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "....dddddddd....",
            "....d......d....",
            "....d..yy..d....",
            "....d..yy..d....",
            "....d..yy..d....",
            "....d......d....",
            "....dddddddd....",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        [ObjectDefs.Laser] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            ".....rrrrrr.....",
            "....RRrrrrRR....",
            "....RRRRRRRR....",
            "...RRRRRRRRRR...",
        }),
        // The red lamp marks a closed door; the circuit number is a label.
        [ObjectDefs.SecurityDoor] = new(new[]
        {
            "................",
            "...VVVVVVVVV....",
            "...VvvvvvvvV....",
            "...VvvrrvvvV....",
            "...VvvrrvvvV....",
            "...VvvvvvvvV....",
            "...VVVVVVVVV....",
            "...VvvvvvvvV....",
            "...VvvvvvvvV....",
            "...VvvvvvvvV....",
            "...VvvvvvvvV....",
            "...VvvvvvvvV....",
            "...VvvvvvvvV....",
            "...VvvvvvvvV....",
            "...VVVVVVVVV....",
            "................",
        }),
        // Editor-only marker: a bolt on the electric floor tile it names.
        [ObjectDefs.FloorCircuit] = new(new[]
        {
            "................",
            "................",
            "................",
            ".......yy.......",
            "......yy........",
            ".....yy.........",
            "....yyyyyy......",
            "........yy......",
            ".......yy.......",
            "......yy........",
            ".....y..........",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        // The record LED is dim, and ticks bright red / off during an alarm.
        [ObjectDefs.Camera] = CameraBox('R'),
        ["camera_alarm"] = CameraBox('r'),
        ["camera_alarm_off"] = CameraBox('d'),
        [ObjectDefs.Closet] = new(new[]
        {
            "..NNNNNNNNNNNN..",
            ".NttttttttttttN.",
            ".NNNNNNNNNNNNNN.",
            "..NnnnnNNnnnnN..",
            "..NnNNnNNnNNnN..",
            "..NnnnnNNnnnnN..",
            "..NnNNnNNnNNnN..",
            "..NnnnnNNnnnnN..",
            "..NnnnyNNynnnN..",
            "..NnnnnNNnnnnN..",
            "..NnNNnNNnNNnN..",
            "..NnNNnNNnNNnN..",
            "..NnNNnNNnNNnN..",
            "..NnnnnNNnnnnN..",
            "..NNNNNNNNNNNN..",
            "..NN........NN..",
        }),
        // Fills the cell like its 30x30 collision box so it rests on the
        // floor: square-cut wooden staves and two steel hoops.
        [ObjectDefs.Barrel] = new(new[]
        {
            "NNNNNNNNNNNNNNNN",
            "NnttnNnnnnNnnnnN",
            "VvvvvvvvvvvvvvvV",
            "VVVVVVVVVVVVVVVV",
            "NnttnNnnnnNnnnnN",
            "NnttnNnnnnNnnnnN",
            "NnttnNnnnnNnnnnN",
            "NnttnNnnnnNnnnnN",
            "NnttnNnnnnNnnnnN",
            "NnttnNnnnnNnnnnN",
            "NnttnNnnnnNnnnnN",
            "NnttnNnnnnNnnnnN",
            "VvvvvvvvvvvvvvvV",
            "VVVVVVVVVVVVVVVV",
            "NnttnNnnnnNnnnnN",
            "NNNNNNNNNNNNNNNN",
        }),
        // Taller than a cell: the emitter hangs over the agent's head and the
        // pad sits on the floor; sprites taller than 32 px are bottom-aligned.
        [ObjectDefs.Teleport] = new(new[]
        {
            "................",
            "................",
            "......VVVV......",
            "...vvvvvvvvvv...",
            "..vVVVVVVVVVVv..",
            "..VppppppppppV..",
            "...cccwcccccc...",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "...ccccccwccc...",
            "..VppppppppppV..",
            "..vVVVVVVVVVVv..",
            ".vvvvvvvvvvvvvv.",
        }),
        // Steel deck over a painted underside (the platform's color property).
        ["platform"] = new(new[]
        {
            "vvvvvvvvvvvvvvvv",
            "vlvvvvlvvvvlvvvv",
            "vvvvvvvvvvvvvvvv",
            "XXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXX",
            "xxxxxxxxxxxxxxxx",
        }),
        [ObjectDefs.ElectricDoor] = new(new[]
        {
            "..dddddddddddd..",
            "..dd........dd..",
            "..gg........gg..",
            "..dd........dd..",
            "..dd........dd..",
            "..gg........gg..",
            "..dd........dd..",
            "..dd........dd..",
            "..gg........gg..",
            "..dd........dd..",
            "..dd........dd..",
            "..gg........gg..",
            "..dd........dd..",
            "..dd........dd..",
            "..dddddddddddd..",
            "................",
        }),
        [ObjectDefs.FlameTrap] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "...dddddddddd...",
            "...dkdkdkdkdd...",
            "...dddddddddd...",
            "................",
        }),
        ["flame"] = new(new[]
        {
            "....y....",
            "....y....",
            "...yyy...",
            "...yyy...",
            "..yyyyy..",
            "..yyyyy..",
            "..yyyyy..",
            "..yyyyy..",
            ".yyooyy..",
            ".yooooy..",
            ".ooooooo.",
            ".ooooooo.",
            ".ooooooo.",
            ".ooooooo.",
            "ooooooooo",
            "ooooooooo",
            "ooooooooo",
            "ooroooroo",
            "ooroooroo",
            "orrooorro",
            "orrooorro",
            "orrooorro",
            "rrrooorrr",
            "rrrooorrr",
            "rrroorrrr",
            "rrrrorrrr",
            "rrrrrrrrr",
            "rrrrrrrrr",
        }),
        // Launch tube on a swivel mount: flared exhaust at the back, hazard
        // band, sight on top and a rocket nose in the muzzle. Faces right;
        // RocketLauncher flips it towards its next shot.
        [ObjectDefs.RocketLauncher] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "...g..kck.......",
            "...gVvvvvykykvd.",
            "...gkuuuuykykur.",
            "...gkuuuuykykrr.",
            "...gVVVVVykykVd.",
            "...g...gg.......",
            ".......gg.......",
            ".......gg.......",
            ".......gg.......",
            "......dlld......",
            ".....dddddd.....",
            "................",
        }),
        // A wall lamp: a bracket holding a shade over the glow.
        [ObjectDefs.Light] = new(new[]
        {
            "................",
            "................",
            "..dd............",
            "..dgllllllll....",
            "..dg.......l....",
            "..dg......VVV...",
            "..dg.....VvvvV..",
            "..dd....VvvvvvV.",
            "........yyyyyyy.",
            ".........wwwww..",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        // The starting weapon has no pickup; this is only its HUD icon.
        ["pistol"] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "...vvvvvvvvvvv..",
            "...VVVVVVVVVVV..",
            "...VVVV.k.......",
            "...VVV..........",
            "...VVV..........",
            "...VVV..........",
            "................",
            "................",
            "................",
            "................",
        }),
        [ObjectDefs.WeaponChainsaw] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "....oooo........",
            "...oooooollllll.",
            "...oooooo.k.k.k.",
            "....oooo........",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        [ObjectDefs.WeaponAssaultRifle] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "..EEEEEEEEEEE...",
            "..EE..EE........",
            "......EE........",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        [ObjectDefs.WeaponMinigun] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "..VVVVVVVVllll..",
            "..VVVVVVVVllll..",
            "..VVVVVVVVllll..",
            "....VV..........",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        [ObjectDefs.WeaponCrossbow] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "...n.......n....",
            "....n.....n.....",
            ".....n...n......",
            "...wwwwwwwww....",
            "......nn........",
            "......nn........",
            "................",
            "................",
            "................",
            "................",
        }),
        [ObjectDefs.WeaponDynamite] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            ".......y........",
            "......y.........",
            "....rrrrrr......",
            "....rrrrrr......",
            "....RRRRRR......",
            "....rrrrrr......",
            "....rrrrrr......",
            "................",
            "................",
            "................",
            "................",
        }),
        [CollectibleDefs.LetterA] = new(LetterBadge(new[]
        {
            ".kkkk.",
            "kk..kk",
            "kk..kk",
            "kkkkkk",
            "kk..kk",
            "kk..kk",
            "kk..kk",
            "......",
        })),
        [CollectibleDefs.LetterG] = new(LetterBadge(new[]
        {
            ".kkkk.",
            "kk....",
            "kk....",
            "kk.kkk",
            "kk..kk",
            "kk..kk",
            ".kkkk.",
            "......",
        })),
        [CollectibleDefs.LetterE] = new(LetterBadge(new[]
        {
            "kkkkkk",
            "kk....",
            "kkkkk.",
            "kk....",
            "kk....",
            "kkkkkk",
            "......",
            "......",
        })),
        [CollectibleDefs.LetterN] = new(LetterBadge(new[]
        {
            "kk..kk",
            "kkk.kk",
            "kk.kkk",
            "kk..kk",
            "kk..kk",
            "kk..kk",
            "......",
            "......",
        })),
        [CollectibleDefs.LetterT] = new(LetterBadge(new[]
        {
            "kkkkkk",
            "..kk..",
            "..kk..",
            "..kk..",
            "..kk..",
            "..kk..",
            "......",
            "......",
        })),
        [CollectibleDefs.AdminPassword] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "...eeeeeeeeee...",
            "...ewwweeeeee...",
            "...eeeeeeeeee...",
            "...eekkkkkeee...",
            "...eeeeeeeeee...",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        [CollectibleDefs.Diamond] = new(new[]
        {
            "................",
            "................",
            "................",
            ".....cccccc.....",
            "....cwcccccc....",
            "...cccccccccc...",
            "....cccccccc....",
            ".....cccccc.....",
            "......cccc......",
            ".......cc.......",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        [CollectibleDefs.Notebook] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "....llllllll....",
            "....lccccccl....",
            "....lccccccl....",
            "....llllllll....",
            "...gggggggggg...",
            "..gggggggggggg..",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        [CollectibleDefs.Mobile] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            ".....kkkkkk.....",
            ".....kcccck.....",
            ".....kcccck.....",
            ".....kcccck.....",
            ".....kcccck.....",
            ".....kkwwkk.....",
            ".....kkkkkk.....",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        [CollectibleDefs.Smartwatch] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            ".......bb.......",
            "......bbbb......",
            ".....bccccb.....",
            ".....bccccb.....",
            "......bbbb......",
            ".......bb.......",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        [CollectibleDefs.AudioRecorder] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "...RRRRRRRRRR...",
            "...RggRRRRggR...",
            "...RggRRRRggR...",
            "...RRRRRRRRRR...",
            "...RkkkkkkkkR...",
            "...RRRRRRRRRR...",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        [CollectibleDefs.SimCard] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "....ooooooo.....",
            "....oooooooo....",
            "....oottoooo....",
            "....oottoooo....",
            "....oooooooo....",
            "....oooooooo....",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        [CollectibleDefs.CashStack] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "...eeeeeeeeee...",
            "...eEEwwwwEEe...",
            "...eeeeeeeeee...",
            "...eeeeeeeeee...",
            "...EEEEEEEEEE...",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        [CollectibleDefs.BusinessCard] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "....wwwwwwww....",
            "....wkkkwwww....",
            "....wwwwwwww....",
            "....wkkkkkww....",
            "....wwwwwwww....",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        [CollectibleDefs.Tombstone] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            ".....gggggg.....",
            "....gggggggg....",
            "....gggggggg....",
            "....ggkkkkgg....",
            "....gggggggg....",
            "....gggggggg....",
            "....gggggggg....",
            "...dddddddddd...",
            "................",
            "................",
            "................",
            "................",
        }),
        // Decorations. The clock and poster hang on the wall and are drawn at
        // native 32x32 for the digits and the lettering; the rest stand on
        // the floor, so their art reaches the bottom row of the cell. Crates
        // fill the cell like their collision box; the desk top sits at the
        // table's collision height.
        ["deco_clock"] = new(new[]
        {
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "...ddddddddddddddddddddddddd....",
            "..ddddddddddddddddddddddddddd...",
            "..ddkkkkkkkkkkkkkkkkkkkkkkkdd...",
            "..ddkkGGkkkGGkkkkkkqqkkkGGkdd...",
            "..ddkqkkGkqkkGkkkkGkkGkGkkqdd...",
            "..ddkqkkGkqkkGkkGkGkkGkGkkqdd...",
            "..ddkkGGkkkGGkkkkkkGGkkkGGkdd...",
            "..ddkGkkqkqkkGkkGkqkkGkqkkGdd...",
            "..ddkGkkqkqkkGkkkkqkkGkqkkGdd...",
            "..ddkkGGkkkGGkkkkkkqqkkkGGkdd...",
            "..ddkkkkkkkkkkkkkkkkkkkkkkkdd...",
            "..ddkeeeeeeeeeeeeeeqqqqqqqkdd...",
            "..ddkkkkkkkkkkkkkkkkkkkkkkkdd...",
            "..ddddddddddddddddddddddddddd...",
            "...ddddddddddddddddddddddddd....",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
        }, Scale: 1),
        ["deco_poster"] = new(new[]
        {
            "................................",
            "................................",
            "...NNNNNNNNNNNNNNNNNNNNNNNNNN...",
            "...NttttttttrrtrrtrrttttttttN...",
            "...NttttttrrttBBBBttrrttttttN...",
            "...NtttttrttBBbbbbBBttrtttttN...",
            "...NtttttrtBbbbbbbbbBtrtttttN...",
            "...NtttrrtBbweebbbbebrtrrtttN...",
            "...NttrttBbbeeeebbeeerBtttttN...",
            "...NttrttBbeeeeebbeeeebBttttN...",
            "...NttttBbbbeeebbbrrerbrrtttN...",
            "...NtrrtBbbbbeebbbeeeebBttttN...",
            "...NttttBbbbbeebbbbeerbBttttN...",
            "...NtrttBbbbbbeebbbeerbBttttN...",
            "...NtrtttBbbbbeebbbbebBtttttN...",
            "...NtttttBbbbbbebbbbbbBtttttN...",
            "...NttttttBbbbbbbbbbbBttttttN...",
            "...NtttttttBbbbbbbbbBtttttttN...",
            "...NttttttttBBbbbbBBttttttttN...",
            "...NttttttttttBBBBttttttttttN...",
            "...NttttttttttttttttttttttttN...",
            "...NkkkkkkkkkkkkkkkkkkkkkkkkN...",
            "...NkkykkkyykyyykykykkyykykyN...",
            "...NkykykykkkykkkyyykykkkykyN...",
            "...NkyyykykykyykkyyykykkkkykN...",
            "...NkykykykykykkkyyykykkkkykN...",
            "...NkykykkyykyyykykykkyykkykN...",
            "...NkkkkkkkkkkkkkkkkkkkkkkkkN...",
            "...NttttttttttttttttttttttttN...",
            "...NNNNNNNNNNNNNNNNNNNNNNNNNN...",
            "................................",
            "................................",
        }, Scale: 1),
        ["deco_plant"] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            ".......e........",
            "....e..eE..e....",
            "....eE.eE.eE....",
            "..e..eEeEeE..e..",
            "..eE.eEeEEeEeE..",
            "...eEeeEEEeE....",
            ".....eEEEEe.....",
            "....ooooooo.....",
            ".....ooooo......",
            ".....RRRRR......",
        }),
        ["deco_toilet"] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "....llll........",
            "....wwwwg.......",
            "....wwww........",
            "....wwwwllllll..",
            ".....wwwwwwwww..",
            "......wwwwwww...",
            ".......wwww.....",
            "......wwwwww....",
        }),
        ["deco_machine"] = new(new[]
        {
            "................",
            "..VVVVVVVVVVVV..",
            "..VvvvvvvvvvvV..",
            "..VkkkkVVkkkkV..",
            "..VkllkVVkllkV..",
            "..VlkklVVlkklV..",
            "..VlkklVVlkklV..",
            "..VkllkVVkllkV..",
            "..VkkkkVVkkkkV..",
            "..VvvvvvvvvvvV..",
            "..VrvevyvrvevV..",
            "..VvvvvvvvvvvV..",
            "..VVVVVVVVVVVV..",
            "..VvVvVvVvVvVV..",
            "..VvvvvvvvvvvV..",
            "..VVVVVVVVVVVV..",
        }),
        ["deco_copier"] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "..ddddddddddd...",
            "..llllllllckel..",
            "..llllllllllll..",
            "wwlgggggggggll..",
            ".llllllllllll...",
            "..lgggggggggl...",
            "..lllllllllll...",
            "..lgggggggggl...",
            "..lllllllllll...",
            "..lllllllllll...",
            "..dd.......dd...",
            "..dd.......dd...",
        }),
        ["deco_table"] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            ".nnnnnnnnnnnnnn.",
            ".NNNNNNNNNNNNNN.",
            "..nN.....nnnnn..",
            "..nN.....nntnn..",
            "..nN.....NNNNN..",
            "..nN.....nnnnn..",
            "..nN.....nntnn..",
            "..nN.....NNNNN..",
            "..nN.....nnnnn..",
        }),
        ["deco_crate_wood"] = new(new[]
        {
            "NNNNNNNNNNNNNNNN",
            "NttttttttttttttN",
            "NtNNNNNNNNNNNNnN",
            "NtNnnnnnnnnttNnN",
            "NtNnnnnnnnttnNnN",
            "NtNnnnnnnttnnNnN",
            "NtNNNNNNttNNNNnN",
            "NtNnnnnttnnnnNnN",
            "NtNnnnttnnnnnNnN",
            "NtNNNttNNNNNNNnN",
            "NtNnttnnnnnnnNnN",
            "NtNttnnnnnnnnNnN",
            "NtNtnnnnnnnnnNnN",
            "NtNNNNNNNNNNNNnN",
            "NtnnnnnnnnnnnnnN",
            "NNNNNNNNNNNNNNNN",
        }),
        ["deco_crate_steel"] = new(new[]
        {
            "dddddddddddddddddddddddddddddddd",
            "dVllllllllllllllllllllllllllllVd",
            "dllvuvvvvvvvvvvvvvvvvvvvvvvlvuVd",
            "dlvvdvvvvvvvvvvvvvvvvvvvvvvvvdVd",
            "dludduuuuuuuuuuuuuuuuuuuuuuuddVd",
            "dlvvuddddddddddddddddddddddvuuVd",
            "dlvvudVVVVVVVVVVVVVVVVVvuudvuuVd",
            "dlvvudVVVVVVVVVVVVVVVVvuudVvuuVd",
            "dlvvudVVVVVVVVVVVVVVVvuudVVvuuVd",
            "dlvvudVVVVVVVVVVVVVVvuudVVVvuuVd",
            "dlvvudVVVVVVVVVVVVVvuudVVVVvuuVd",
            "dlvvudVVVVVVVVVVVVvuudVVVVVvuuVd",
            "dlvvudVVVVVVVVVVVvuudVVVVVVvuuVd",
            "dlvvudVVVVVVVVVVvuudVVVVVVVvuuVd",
            "dlvvudVVVVVVVVVvuudVVVVVVVVvuuVd",
            "dlvvudVVVVVVVVvuudVVVVVVVVVvuuVd",
            "dlvvudVVVVVVVvuudVVVVVVVVVVvuuVd",
            "dlvvudVVVVVVvuudVVVVVVVVVVVvuuVd",
            "dlvvudVVVVVvuudVVVVVVVVVVVVvuuVd",
            "dlvvudVVVVvuudVVVVVVVVVVVVVvuuVd",
            "dlvvudVVVvuudVVVVVVVVVVVVVVvuuVd",
            "dlvvudVVvuudVVVVVVVVVVVVVVVvuuVd",
            "dlvvudVvuudVVVVVVVVVVVVVVVVvuuVd",
            "dlvvudvuudVVVVVVVVVVVVVVVVVvuuVd",
            "dlvvuduudVVVVVVVVVVVVVVVVVVvuuVd",
            "dlvvududVVVVVVVVVVVVVVVVVVVvuuVd",
            "dlvvuddVVVVVVVVVVVVVVVVVVVVvuuVd",
            "dllvuvvvvvvvvvvvvvvvvvvvvvvlvuVd",
            "dlvvduuuuuuuuuuuuuuuuuuuuuuvvdVd",
            "dludduuuuuuuuuuuuuuuuuuuuuuuddVd",
            "dVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVd",
            "dddddddddddddddddddddddddddddddd",
        }, Scale: 1, ShadeBody: false),
        // HUD life counter.
        ["heart"] = new(new[]
        {
            ".rr..rr.",
            "rwrrrrRr",
            "rrrrrrrR",
            "rrrrrrrR",
            ".rrrrrR.",
            "..rrrR..",
            "...rR...",
            "........",
        }),
        // HUD score counter.
        ["score"] = new(new[]
        {
            "...y....",
            "..yyy...",
            "yyyyyyy.",
            ".yyyyy..",
            "..yyy...",
            ".yyoyy..",
            ".yo.oy..",
            "yo...oy.",
        }),
        ["bullet"] = new(new[]
        {
            ".yyyy.",
            "yywwyy",
            ".yyyy.",
        }, Scale: 1),
        ["plasma"] = new(new[]
        {
            ".cccc.",
            "cccccc",
            "ccwwcc",
            "ccwwcc",
            "cccccc",
            ".cccc.",
        }, Scale: 1),
        ["rocket"] = new(new[]
        {
            "....gggggg..",
            "oo.gggggggr.",
            "ooogggggggrr",
            "oo.gggggggr.",
            "....gggggg..",
        }, Scale: 1),
        ["dynamite"] = new(new[]
        {
            "....y...",
            "...y....",
            "...y....",
            "..rrrr..",
            "..rrrr..",
            "..RRRR..",
            "..rrrr..",
            "..rrrr..",
            "..rrrr..",
            "..RRRR..",
            "..rrrr..",
            "..rrrr..",
            "..rrrr..",
            "..rrrr..",
            "..RRRR..",
            "..rrrr..",
        }, Scale: 1),
    };

    // Wraps a 6-wide glyph into a 16x16 yellow badge.
    private static string[] LetterBadge(string[] glyph)
    {
        var rows = new string[16];
        rows[0] = "................";
        rows[1] = "................";
        rows[2] = "....yyyyyyyy....";
        rows[3] = "...yyyyyyyyyy...";
        for (int i = 0; i < 8; i++)
        {
            var g = glyph[i].Replace('k', 'K');
            rows[4 + i] = "...yy" + g.Replace('.', 'y').Replace('K', 'k') + "yy...";
        }
        rows[12] = "...yyyyyyyyyy...";
        rows[13] = "....yyyyyyyy....";
        rows[14] = "................";
        rows[15] = "................";
        return rows;
    }
}
