using System.Collections.Generic;
using Godot;

namespace HackerOrSoldier;

// Runtime-generated pixel art for every object, in the README's aesthetic:
// stark saturated VGA-style colors, chunky pixels (16x16 maps upscaled 2x).
// The agent is an original take on the classic pixel spy: black suit, white
// shirt, red tie, shades. '.' is transparent; 'X'/'x' are slots replaced by
// the door/key color variant.
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
        ['v'] = new Color("7a8aa5"), // steel
        ['V'] = new Color("4a5570"), // dark steel
    };

    private static readonly Dictionary<string, ImageTexture> Cache = new();

    public static ImageTexture Get(string key, string colorName = null)
    {
        string cacheKey = colorName == null ? key : $"{key}:{colorName}";
        if (Cache.TryGetValue(cacheKey, out var cached))
            return cached;
        if (!Maps.TryGetValue(key, out var map))
            return null;
        var slot = colorName != null ? ObjectDefs.KeyColor(colorName) : Colors.Magenta;
        var texture = ImageTexture.CreateFromImage(FromMap(key, map.Rows, map.Scale, slot));
        Cache[cacheKey] = texture;
        return texture;
    }

    // Resolves any placeable type (object, enemy, collectible, decoration)
    // to its sprite; falls back to a flat swatch so a missing map can never
    // crash the editor.
    public static ImageTexture ForObject(string type, string colorName = null)
    {
        string key = type == ObjectDefs.AgentStart ? "agent" : type;
        var texture = Get(key, ObjectDefs.HasColorProperty(type) ? colorName ?? "red" : null);
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
        img.Resize(16, 16, Image.Interpolation.Nearest);
        var texture = ImageTexture.CreateFromImage(img);
        Cache[cacheKey] = texture;
        return texture;
    }

    private static Image FromMap(string key, string[] rows, int scale, Color slot)
    {
        int width = rows[0].Length;
        var img = Image.CreateEmpty(width * scale, rows.Length * scale, false, Image.Format.Rgba8);
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
                img.FillRect(new Rect2I(x * scale, y * scale, scale, scale), color);
            }
        }
        return img;
    }

    private record SpriteMap(string[] Rows, int Scale = 2);

    private static readonly Dictionary<string, SpriteMap> Maps = new()
    {
        // Agent animation set (see AgentAnimator for the timing). Every pose
        // is drawn in profile facing right - hair mass at the back, shades
        // band and nose bump at the front, shoe toes pointing forward - so
        // the agent visibly looks where it is heading once FlipH mirrors it
        // for the other direction. The white collar and red tie stay on the
        // same two rows in every frame so the suit never flickers.
        // "agent" doubles as idle frame 0, so editor markers and palette
        // icons keep showing the neutral standing pose.
        ["agent"] = new(new[]
        {
            "................",
            ".....hhhhh......",
            "....hhhhhhh.....",
            "....hhssss......",
            "....hhkkkk......",
            "....hhsssss.....",
            ".....hsss.......",
            "....kkkkkkk.....",
            "....kkkkkww.....",
            "....kkkkkrw.....",
            "....kkddkkk.....",
            "....kksskkk.....",
            "....kdddd.......",
            "....kdddd.......",
            "....kdddd.......",
            "...kkkkkkkk.....",
        }),
        // Idle frame 1: head and torso settle one pixel - a slow breath.
        ["agent_breathe"] = new(new[]
        {
            "................",
            "................",
            ".....hhhhh......",
            "....hhhhhhh.....",
            "....hhssss......",
            "....hhkkkk......",
            "....hhsssss.....",
            ".....hsss.......",
            "....kkkkkkk.....",
            "....kkkkkww.....",
            "....kkkkkrw.....",
            "....kkddkkk.....",
            "....kksskkk.....",
            "....kdddd.......",
            "....kdddd.......",
            "...kkkkkkkk.....",
        }),
        // Four-frame run. The head leans a pixel into the direction of
        // travel, the near arm swings forward / neutral / back, and the near
        // leg is dark grey against the black far leg so the two contact
        // frames read as opposite strides. The pass frames sit a pixel
        // higher, which gives the bob.
        ["agent_run0"] = new(new[]
        {
            "................",
            "......hhhhh.....",
            ".....hhhhhhh....",
            ".....hhssss.....",
            ".....hhkkkk.....",
            ".....hhsssss....",
            "......hsss......",
            "....kkkkkkk.....",
            "....kkkkkww.....",
            "....kkkkkrw.....",
            "....kkkkkdd.....",
            "....kkkkkkdds...",
            "....kkdddd......",
            "...kk....dd.....",
            "..kk......dd....",
            "..kkkk...kkkk...",
        }),
        ["agent_run1"] = new(new[]
        {
            "......hhhhh.....",
            ".....hhhhhhh....",
            ".....hhssss.....",
            ".....hhkkkk.....",
            ".....hhsssss....",
            "......hsss......",
            "....kkkkkkk.....",
            "....kkkkkww.....",
            "....kkkkkrw.....",
            "....kkddkkk.....",
            "....kksskkk.....",
            "....kkkddd......",
            "....kk..dd......",
            "....kk..dd......",
            "...kkkk..kkk....",
            "................",
        }),
        ["agent_run2"] = new(new[]
        {
            "................",
            "......hhhhh.....",
            ".....hhhhhhh....",
            ".....hhssss.....",
            ".....hhkkkk.....",
            ".....hhsssss....",
            "......hsss......",
            "....kkkkkkk.....",
            "....kkkkkww.....",
            "....kkkkkrw.....",
            "...ddkkkkkk.....",
            "..sddkkkkkk.....",
            "....ddddkk......",
            "...dd....kk.....",
            "..dd......kk....",
            "..kkkk...kkkk...",
        }),
        ["agent_run3"] = new(new[]
        {
            "......hhhhh.....",
            ".....hhhhhhh....",
            ".....hhssss.....",
            ".....hhkkkk.....",
            ".....hhsssss....",
            "......hsss......",
            "....kkkkkkk.....",
            "....kkkkkww.....",
            "....kkkkkrw.....",
            "....kkddkkk.....",
            "....kksskkk.....",
            "....dddkkk......",
            "....dd..kk......",
            "....dd..kk......",
            "...kkkk..kkk....",
            "................",
        }),
        // Rising: lead arm thrown up and forward, trailing arm behind,
        // both feet off the ground and split fore and aft.
        ["agent_jump"] = new(new[]
        {
            "................",
            "......hhhhh.....",
            ".....hhhhhhh....",
            ".....hhssss.....",
            ".....hhkkkk.....",
            ".....hhsssss..s.",
            "......hsss...d..",
            "....kkkkkkkdd...",
            "....kkkkkww.....",
            "...ddkkkkkrw....",
            "..sddkkkkkk.....",
            "....kkkkkkk.....",
            "...kkkdddd......",
            "..kk.....ddd....",
            ".kkkk.....kkk...",
            "................",
        }),
        // Falling: both arms flung out fore and aft, legs braced for the
        // landing.
        ["agent_fall"] = new(new[]
        {
            "................",
            ".....hhhhh......",
            "....hhhhhhh.....",
            "....hhssss......",
            "....hhkkkk......",
            "....hhsssss.....",
            ".....hsss.......",
            "....kkkkkkk.....",
            "....kkkkkww.....",
            ".sddkkkkkrwdds..",
            "....kkkkkkk.....",
            "....kkkkkkk.....",
            "....kkdddd......",
            "...kk....dd.....",
            "..kk......dd....",
            ".kkkk....kkkk...",
        }),
        // Shooting: lead arm punched straight out with the pistol, feet
        // braced. The muzzle flash burns off the barrel on the fire frame.
        ["agent_aim"] = new(new[]
        {
            "................",
            ".....hhhhh......",
            "....hhhhhhh.....",
            "....hhssss......",
            "....hhkkkk......",
            "....hhsssss.....",
            ".....hsss.......",
            "....kkkkkkkddsgg",
            "....kkkkkww...g.",
            "....kkkkkrw.....",
            "....kkddkkk.....",
            "....kksskkk.....",
            "....kkdddd......",
            "....kk..dd......",
            "...kk....dd.....",
            "..kkkk...kkkk...",
        }),
        ["agent_fire"] = new(new[]
        {
            "................",
            ".....hhhhh......",
            "....hhhhhhh.....",
            "....hhssss......",
            "....hhkkkk......",
            "....hhsssss.....",
            ".....hsss......y",
            "....kkkkkkkddsgo",
            "....kkkkkww...gy",
            "....kkkkkrw.....",
            "....kkddkkk.....",
            "....kksskkk.....",
            "....kkdddd......",
            "....kk..dd......",
            "...kk....dd.....",
            "..kkkk...kkkk...",
        }),
        [EnemyDefs.Fighter] = new(new[]
        {
            "................",
            "......hhhh......",
            ".....hssssh.....",
            ".....skssks.....",
            ".....ssssss.....",
            "......ssss......",
            ".....oooooo.....",
            "....soooooos....",
            "....soooooos....",
            "....ss.oo.ss....",
            ".....dddddd.....",
            ".....dd..dd.....",
            ".....dd..dd.....",
            ".....kk..kk.....",
            "................",
            "................",
        }),
        [EnemyDefs.Soldier] = new(new[]
        {
            "................",
            "......EEEE......",
            ".....EEEEEE.....",
            ".....ssssss.....",
            ".....skssks.....",
            "......ssss......",
            ".....eeeeee.....",
            "....eeeeeeee....",
            "....eeeeeeee....",
            "....edddddde....",
            "....e.eeee.e....",
            ".....EEEEEE.....",
            ".....EE..EE.....",
            ".....EE..EE.....",
            ".....kk..kk.....",
            "................",
        }),
        [EnemyDefs.SuperSoldier] = new(new[]
        {
            "................",
            "......vvvv......",
            ".....vvvvvv.....",
            ".....vccccv.....",
            ".....vvvvvv.....",
            "......vvvv......",
            ".....vvvvvv.....",
            "....vvVvvVvv....",
            "....vvvvvvvv....",
            "....vVVVVVVv....",
            "....vv.vv.vv....",
            ".....VVVVVV.....",
            ".....VV..VV.....",
            ".....VV..VV.....",
            ".....kk..kk.....",
            "................",
        }),
        [EnemyDefs.UltimateSoldier] = new(new[]
        {
            "................",
            "......pppp......",
            ".....pppppp.....",
            ".....pmmmmp.....",
            ".....pppppp.....",
            "......pppp......",
            "....pppppppp....",
            "...pppmppmppp...",
            "...pppppppppp...",
            "...ppmppppmpp...",
            "....pp.pp.pp....",
            ".....pppppp.....",
            ".....pp..pp.....",
            ".....pp..pp.....",
            ".....kk..kk.....",
            "................",
        }),
        [EnemyDefs.Drone] = new(new[]
        {
            "................",
            "................",
            "..llllllllllll..",
            ".......vv.......",
            "......vvvv......",
            ".....vvvvvv.....",
            "....vvccccvv....",
            "....vvvvvvvv....",
            ".....vvvvvv.....",
            "......v..v......",
            ".....vv..vv.....",
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
            "...vv...........",
            "...vcv..........",
            "...vvvvvvvvvvv..",
            "....vvvvvvvvvv..",
            "....vv..vv..vv..",
            "....kk..kk..kk..",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        [EnemyDefs.Turret] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "......gggg......",
            ".....gggggg.....",
            "..llllggcggg....",
            ".....gggggg.....",
            "......gggg......",
            "....dddddddd....",
            "...dddddddddd...",
            "..VVVVVVVVVVVV..",
            "................",
            "................",
            "................",
            "................",
        }),
        [EnemyDefs.Robot] = new(new[]
        {
            "................",
            "....vvvvvvvv....",
            "....vccvvccv....",
            "....vvvvvvvv....",
            ".....vvvvvv.....",
            "...vvVVVVVVvv...",
            "...vv.VVVV.vv...",
            "...vv.VVVV.vv...",
            "......VVVV......",
            ".....vv..vv.....",
            ".....vv..vv.....",
            ".....VV..VV.....",
            "................",
            "................",
            "................",
            "................",
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
            "......rr........",
            ".....dddddd.....",
            "...dddddddddd...",
            "..VVVVVVVVVVVV..",
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
            "....cccccccccc..",
            "..ccCCCCCCCCwc..",
            "...cccccccccc...",
            "..cc............",
            "................",
            "................",
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
            "................",
            "...gggggggggg...",
            "...gccccccccg...",
            "...gcckkkkccg...",
            "...gccccccccg...",
            "...gggggggggg...",
            "......gg........",
            ".....gggg.......",
            "...dddddddddd...",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
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
            ".....rrrrrr.....",
            "....RRrrrrRR....",
            "....RRRRRRRR....",
            "...RRRRRRRRRR...",
            "................",
            "................",
        }),
        [ObjectDefs.Camera] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "..gg............",
            "..gggggggggg....",
            "..ggggggggrg....",
            "..gggggggggg....",
            "....gg..........",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        [ObjectDefs.Closet] = new(new[]
        {
            "...NNNNNNNNNN...",
            "...NnnnnNnnnN...",
            "...NnnnnNnnnN...",
            "...NnnnnNnnnN...",
            "...NnnnnNnnnN...",
            "...NnnnnNnnnN...",
            "...NnnnnNnnnN...",
            "...NnnwnNnwnN...",
            "...NnnnnNnnnN...",
            "...NnnnnNnnnN...",
            "...NnnnnNnnnN...",
            "...NnnnnNnnnN...",
            "...NnnnnNnnnN...",
            "...NnnnnNnnnN...",
            "...NNNNNNNNNN...",
            "................",
        }),
        [ObjectDefs.Barrel] = new(new[]
        {
            "................",
            "................",
            "................",
            "....nnnnnnnn....",
            "...nnnnnnnnnn...",
            "...NNNNNNNNNN...",
            "...nnnnnnnnnn...",
            "...nnnnnnnnnn...",
            "...NNNNNNNNNN...",
            "...nnnnnnnnnn...",
            "...nnnnnnnnnn...",
            "....nnnnnnnn....",
            "................",
            "................",
            "................",
            "................",
        }),
        [ObjectDefs.Teleport] = new(new[]
        {
            "....pppppppp....",
            "...pp......pp...",
            "...p.cccccc.p...",
            "...p.cccccc.p...",
            "...p.ccwccc.p...",
            "...p.cccccc.p...",
            "...p.cccccc.p...",
            "...p.cccccc.p...",
            "...p.cccwcc.p...",
            "...p.cccccc.p...",
            "...p.cccccc.p...",
            "...p.cccccc.p...",
            "...p.cccccc.p...",
            "...pp......pp...",
            "....pppppppp....",
            "................",
        }),
        ["platform"] = new(new[]
        {
            "vvvvvvvvvvvvvvvv",
            "vlvvvvlvvvvlvvvv",
            "vvvvvvvvvvvvvvvv",
            "VVVVVVVVVVVVVVVV",
            "VVVVVVVVVVVVVVVV",
            "VVVVVVVVVVVVVVVV",
        }),
        [ObjectDefs.ElectricDoor] = new(new[]
        {
            "..dddddddddddd..",
            "..dd........dd..",
            "..yy........yy..",
            "..dd........dd..",
            "..dd........dd..",
            "..yy........yy..",
            "..dd........dd..",
            "..dd........dd..",
            "..yy........yy..",
            "..dd........dd..",
            "..dd........dd..",
            "..yy........yy..",
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
        [ObjectDefs.RocketLauncher] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "..VVVVVVVVVVVV..",
            "..VVVVVVVVVVVV..",
            "llllllllllVVVV..",
            "llllllllllVVVV..",
            "..VVVVVVVVVVVV..",
            "..VVVVVVVVVVVV..",
            "...VV......VV...",
            "................",
            "................",
            "................",
            "................",
        }),
        [ObjectDefs.Light] = new(new[]
        {
            "......gggg......",
            ".....gyyyyg.....",
            "....gyyyyyyg....",
            ".....wwwwww.....",
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
        ["deco_clock"] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "...kkkkkkkkkk...",
            "...keekeekeek...",
            "...kkkkkkkkkk...",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        ["deco_poster"] = new(new[]
        {
            "................",
            "................",
            "....tttttttt....",
            "....trrrrttt....",
            "....tttttttt....",
            "....tkkkkktt....",
            "....tttttttt....",
            "....tkkkkktt....",
            "....tttttttt....",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        ["deco_plant"] = new(new[]
        {
            "................",
            "................",
            "................",
            "......ee.e......",
            ".....eeeee......",
            "....eeeeeee.....",
            ".....eeeee......",
            "......eee.......",
            ".....NNNNN......",
            ".....NNNNN......",
            "......NNN.......",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        ["deco_toilet"] = new(new[]
        {
            "................",
            "................",
            "................",
            "................",
            ".....wwww.......",
            ".....wwww.......",
            ".....wwwwww.....",
            "....wwwwwwww....",
            "....wwwwwwww....",
            ".....wwwww......",
            "......ww........",
            ".....wwww.......",
            "................",
            "................",
            "................",
            "................",
        }),
        ["deco_pipe"] = new(new[]
        {
            "......gggg......",
            "......gggg......",
            "......gggg......",
            ".....VVVVVV.....",
            ".....VVVVVV.....",
            "......gggg......",
            "......gggg......",
            "......gggg......",
            "......gggg......",
            "......gggg......",
            "......gggg......",
            ".....VVVVVV.....",
            ".....VVVVVV.....",
            "......gggg......",
            "......gggg......",
            "......gggg......",
        }),
        ["deco_sign"] = new(new[]
        {
            "................",
            "................",
            "................",
            "...yyyyyyyyyy...",
            "...yykkkkkyy....",
            "...yyyyyyyyyy...",
            "......gg........",
            "......gg........",
            "......gg........",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
        }),
        ["deco_machine"] = new(new[]
        {
            "................",
            "................",
            "...VVVVVVVVVV...",
            "...VrycVVVVVV...",
            "...VVVVVVVVVV...",
            "...VkkkkkkkkV...",
            "...VkkkkkkkkV...",
            "...VVVVVVVVVV...",
            "...VVVVVVVVVV...",
            "...VVVVVVVVVV...",
            "...VVVVVVVVVV...",
            "...VVVVVVVVVV...",
            "...VVVVVVVVVV...",
            "...VV......VV...",
            "................",
            "................",
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
            "................",
            "..nnnnnnnnnnnn..",
            "..nnnnnnnnnnnn..",
            "..NN........NN..",
            "..NN........NN..",
            "..NN........NN..",
            "..NN........NN..",
            "..NN........NN..",
            "..NN........NN..",
        }),
        ["deco_crate_wood"] = new(new[]
        {
            "................",
            "..NNNNNNNNNNNN..",
            "..nNnnnnnnnnNn..",
            "..nnNnnnnnnNnn..",
            "..nnnNnnnnNnnn..",
            "..nnnnNnnNnnnn..",
            "..nnnnnNNnnnnn..",
            "..nnnnnNNnnnnn..",
            "..nnnnNnnNnnnn..",
            "..nnnNnnnnNnnn..",
            "..nnNnnnnnnNnn..",
            "..nNnnnnnnnnNn..",
            "..nnnnnnnnnnnn..",
            "..NNNNNNNNNNNN..",
            "................",
            "................",
        }),
        ["deco_crate_steel"] = new(new[]
        {
            "................",
            "..vvvvvvvvvvvv..",
            "..vVvvvvvvvvVv..",
            "..vvvvvvvvvvvv..",
            "..vvvVVVVVVvvv..",
            "..vvvVVVVVVvvv..",
            "..vvvVVVVVVvvv..",
            "..vvvVVVVVVvvv..",
            "..vvvVVVVVVvvv..",
            "..vvvVVVVVVvvv..",
            "..vvvvvvvvvvvv..",
            "..vVvvvvvvvvVv..",
            "..vvvvvvvvvvvv..",
            "..VVVVVVVVVVVV..",
            "................",
            "................",
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
