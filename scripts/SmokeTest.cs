using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace HackerOrSoldier;

// Headless regression check for the editor and play loop:
//   godot --headless --path . res://scenes/smoke_test.tscn
// Exits 0 and prints SMOKE OK on success.
public partial class SmokeTest : Node
{
    private int _failures;
    private Editor _editor;

    public override async void _Ready()
    {
        DataModelChecks();
        AutosaveCheck();
        await EditorLoopChecks();
        await UndoRedoCheck();
        await ShortcutAndPanCheck();
        await PaletteKeysCheck();
        await ToolsCheck();
        await SelectCopyPasteCheck();
        await InvincibleCheck();
        await CorridorCheck();
        await WallContactCheck();
        await DefaultLevelCheck();
        await WallTilesCheck();
        await ShadowCheck();
        await PipeCheck();
        await RimCheck();
        await SkyCheck();
        await LivesAndFailCheck();
        await VestCheck();
        await KillPlaneCheck();
        await CheckpointCheck();
        await CheckpointJumpCheck();
        await ItemCourseCheck();
        await WaterCheck();
        await SpikeCheck();
        await SpearCheck();
        await ElectricFloorCheck();
        await GogglesCheck();
        await HiddenPlatformCheck();
        await FighterContactCheck();
        await DegradationCheck();
        await TurretCheck();
        await RobotCheck();
        await DropCheck();
        await EelCheck();
        await PiranhaCheck();
        await AlarmFloorCheck();
        await MineCheck();
        await ExplosionCheck();
        await ComputerHackCheck();
        await LaserDamageCheck();
        await CircuitSevenCheck();
        await SecurityDoorCheck();
        await FloorCircuitCheck();
        await SwitchCheck();
        await CameraAlarmCheck();
        await CameraFacingCheck();
        await WallColorCheck();
        await DisguiseCheck();
        await EnemyDisguiseCheck();
        await ClosetCheck();
        await PlaytestHelpCheck();
        await LookDownCheck();
        await BarrelCheck(ObjectDefs.Barrel);
        await BarrelCheck(ObjectDefs.SteelBarrel);
        await CollectibleCheck();
        await LettersBonusCheck();
        await StatsCheck();
        await TombstoneScoreCheck();
        await DifficultyCheck();
        ProfileShopChecks();
        HelpChecks();
        await HackingToolCheck();
        await TeleportCheck();
        await ElevatorCheck();
        await AssaultRifleCheck();
        await CrossbowCheck();
        await ChainsawCheck();
        await DynamiteWeaponCheck();
        await DynamiteLandingCheck();
        await ElectricDoorCheck();
        await FlameTrapCheck();
        await RocketLauncherCheck();
        await DecorationCheck();
        await AgentAnimationCheck();
        await AgentSwimCheck();
        await AgentDeathCheck();
        await EnemyAnimationCheck();
        await MenuSceneCheck();
        SfxChecks();

        GD.Print(_failures == 0 ? "SMOKE OK" : $"SMOKE FAILED ({_failures})");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private static LevelData LoadRaw(string fileName, string json)
    {
        DirAccess.MakeDirRecursiveAbsolute(LevelStore.DirPath);
        string path = $"{LevelStore.DirPath}/{fileName}";
        using (var file = FileAccess.Open(path, FileAccess.ModeFlags.Write))
            file.StoreString(json);
        var level = LevelStore.Load(fileName);
        DirAccess.RemoveAbsolute(path);
        return level;
    }

    // Saved levels store tile numbers and object type names, so changing
    // one silently breaks every level that uses it. A new tile or type must
    // be added here; an existing one must never be renumbered or renamed
    // (bump LevelData.CurrentFormatVersion and convert old files instead).
    private void FormatPinChecks()
    {
        var tiles = new (int Pinned, int Actual, string Name)[]
        {
            (0, TileDefs.Empty, "Empty"), (1, TileDefs.Wall, "Wall"), (2, TileDefs.DestructibleWall, "DestructibleWall"),
            (3, TileDefs.AlarmFloor, "AlarmFloor"), (4, TileDefs.ElectricFloor, "ElectricFloor"), (5, TileDefs.Water, "Water"),
            (6, TileDefs.Lava, "Lava"), (7, TileDefs.Spikes, "Spikes"), (8, TileDefs.Spear, "Spear"),
            (9, TileDefs.HiddenPlatform, "HiddenPlatform"), (10, TileDefs.StoneBlocks, "StoneBlocks"),
            (11, TileDefs.RedBrick, "RedBrick"), (12, TileDefs.SteelPlate, "SteelPlate"), (13, TileDefs.Girder, "Girder"),
            (14, TileDefs.BackgroundMesh, "BackgroundMesh"), (15, TileDefs.BackgroundRock, "BackgroundRock"),
            (16, TileDefs.BackgroundPanel, "BackgroundPanel"), (17, TileDefs.Concrete, "Concrete"),
            (18, TileDefs.Sandbags, "Sandbags"), (19, TileDefs.Container, "Container"), (20, TileDefs.PipeWall, "PipeWall"),
            (21, TileDefs.BackgroundBrick, "BackgroundBrick"), (22, TileDefs.BackgroundCircuit, "BackgroundCircuit"),
            (23, TileDefs.BackgroundHangar, "BackgroundHangar"), (26, TileDefs.DestructibleStone, "DestructibleStone"),
            (27, TileDefs.DestructibleRed, "DestructibleRed"), (28, TileDefs.DestructibleSteel, "DestructibleSteel"),
            (29, TileDefs.DestructibleMoss, "DestructibleMoss"), (30, TileDefs.DestructibleSand, "DestructibleSand"),
        };
        var moved = tiles.Where(t => t.Pinned != t.Actual).Select(t => $"{t.Name} {t.Pinned}->{t.Actual}").ToList();
        Check(moved.Count == 0, $"tile numbers match the pinned ones{(moved.Count > 0 ? ": " + string.Join(", ", moved) : "")}");
        var unpinnedTiles = TileDefs.All.Select(d => d.Id).Except(tiles.Select(t => t.Pinned)).ToList();
        Check(unpinnedTiles.Count == 0, $"every tile number is pinned{(unpinnedTiles.Count > 0 ? "; add " + string.Join(", ", unpinnedTiles) + " to FormatPinChecks" : "")}");

        var pinnedTypes = new[]
        {
            "agent_start", "exit_door", "checkpoint", "door", "door_key", "health_kit", "ammo_pack", "vest", "briefcase",
            "infra_goggles", "computer", "usb_key", "switch", "laser", "security_door", "floor_circuit", "camera", "closet",
            "barrel", "barrel_steel", "teleport", "elevator", "moving_platform", "electric_door", "flame_trap", "rocket_launcher", "light",
            "weapon_chainsaw", "weapon_assault_rifle", "weapon_minigun", "weapon_crossbow", "weapon_dynamite",
            "fighter", "soldier", "super_soldier", "ultimate_soldier", "drone", "robodog", "turret", "robot", "landmine",
            "electric_eel", "robot_piranha",
            "letter_a", "letter_g", "letter_e", "letter_n", "letter_t", "admin_password", "diamond", "notebook", "mobile",
            "smartwatch", "audio_recorder", "sim_card", "cash_stack", "business_card", "tombstone",
            "deco_clock", "deco_poster", "deco_plant", "deco_toilet", "deco_machine", "deco_copier",
            "deco_table", "deco_crate_wood", "deco_crate_steel",
        };
        var types = ObjectDefs.All.Select(d => d.Type).Concat(EnemyDefs.All.Select(d => d.Type))
            .Concat(CollectibleDefs.All.Select(d => d.Type)).Concat(DecorationDefs.All.Select(d => d.Type)).ToList();
        var missing = pinnedTypes.Except(types).ToList();
        Check(missing.Count == 0, $"every pinned object type still exists{(missing.Count > 0 ? "; renamed or removed: " + string.Join(", ", missing) : "")}");
        var unpinned = types.Except(pinnedTypes).ToList();
        Check(unpinned.Count == 0, $"every object type is pinned{(unpinned.Count > 0 ? "; add " + string.Join(", ", unpinned) + " to FormatPinChecks" : "")}");
    }

    private void AutosaveCheck()
    {
        const string dir = "user://smoke_autosave";
        string path = ProjectSettings.GlobalizePath(dir);
        if (System.IO.Directory.Exists(path))
            System.IO.Directory.Delete(path, true);
        var store = new AutosaveStore(dir, keep: 10);
        Check(store.Latest() == null && store.PendingRestore() == null, "an empty autosave folder offers nothing");

        var level = LevelData.NewDefault();
        level.Name = "Smoke Autosave Only";
        for (int i = 0; i < 12; i++)
        {
            level.SetTile(i, 2, 3);
            store.Write(level);
        }
        Check(System.IO.Directory.GetFiles(path, "autosave-*.json").Length == 10, "autosaves are capped at the keep count");
        Check(store.Latest()?.Level.GetTile(11, 2) == 3, "the latest autosave is the last one written");
        var pending = store.PendingRestore();
        Check(pending != null, "an autosave with no saved level is offered for restore");
        store.MarkHandled(pending.FileName);
        Check(store.PendingRestore() == null, "a handled autosave is not offered again");

        level.Name = "Smoke Autosave Saved";
        store.Write(level);
        level.SetTile(12, 2, 3);
        LevelStore.Save(level);
        Check(store.PendingRestore() == null, "an autosave older than the saved level is not offered");
        System.IO.File.Delete(ProjectSettings.GlobalizePath($"{LevelStore.DirPath}/{LevelStore.FileNameFor(level.Name)}"));
        System.IO.Directory.Delete(path, true);
    }

    private void DataModelChecks()
    {
        var level = LevelData.NewDefault();
        Check(level.Validate() == null, "default level is valid");
        level.Name = "Smoke Test";
        level.Description = "generated by SmokeTest";
        level.SetTile(5, 5, 3);
        Check(LevelStore.Save(level), "saving a level succeeds");
        var loaded = LevelStore.Load(LevelStore.FileNameFor(level.Name));
        Check(loaded != null, "saved level loads back");
        Check(loaded != null && loaded.GetTile(5, 5) == 3, "round trip preserves tiles");
        Check(loaded != null && loaded.Description == "generated by SmokeTest", "round trip preserves description");
        Check(loaded != null && loaded.Objects.Count == 2, "round trip preserves objects");
        Check(loaded != null && loaded.FormatVersion == LevelData.CurrentFormatVersion, "a saved level carries the current format version");
        string firstJson = LevelStore.Serialize(level);
        level.SetTile(6, 6, 3);
        LevelStore.Save(level);
        string saved = ProjectSettings.GlobalizePath($"{LevelStore.DirPath}/{LevelStore.FileNameFor(level.Name)}");
        string backup = ProjectSettings.GlobalizePath($"user://backups/{LevelStore.FileNameFor(level.Name)}");
        Check(!System.IO.File.Exists(saved + ".tmp"), "saving leaves no temp file behind");
        Check(System.IO.File.Exists(backup) && System.IO.File.ReadAllText(backup) == firstJson, "saving over a level backs up the previous version");
        System.IO.File.Delete(backup);
        DirAccess.RemoveAbsolute($"{LevelStore.DirPath}/{LevelStore.FileNameFor(level.Name)}");

        // A file without the field predates it and reads as version 1; one
        // from a newer build is refused rather than misread.
        string unversioned = "{ \"name\": \"Old\", \"width\": 1, \"height\": 1, \"tiles\": [0], \"objects\": [] }";
        Check(LoadRaw("_format-old.json", unversioned)?.FormatVersion == 1, "a level without formatVersion loads as version 1");
        Check(LoadRaw("_format-new.json", unversioned.Replace("{ ", $"{{ \"formatVersion\": {LevelData.CurrentFormatVersion + 1}, ")) == null,
            "a level from a newer format version is refused");
        Check(LoadRaw("_format-v1-deco.json", unversioned.Replace("[] }", "[{ \"type\": \"deco_pipe\", \"x\": 0, \"y\": 0 }] }"))?.Objects.Count == 0,
            "a version 1 level drops the removed pipe and sign decorations");
        FormatPinChecks();

        var tileSet = TileSetBuilder.Build();
        Check(tileSet.GetSource(TileSetBuilder.SourceId) is TileSetAtlasSource, "tileset builds with atlas source");

        // Force-build every sprite so a malformed pixel map fails the run.
        try
        {
            foreach (var def in ObjectDefs.All)
                Sprites.ForObject(def.Type, "red");
            foreach (var def in EnemyDefs.All)
                Sprites.ForObject(def.Type);
            foreach (var def in CollectibleDefs.All)
                Sprites.ForObject(def.Type);
            foreach (var def in DecorationDefs.All)
                Sprites.ForObject(def.Type);
            foreach (var def in TileDefs.All)
                TileSetBuilder.TileIcon(def.Id);
            foreach (var key in new[] { "platform", "flame", "bullet", "plasma", "rocket", "dynamite",
                         "turret_base", TurretGun.Idle, TurretGun.Firing }
                .Concat(AgentAnimator.AllFrames).Concat(EnemyAnimator.AllFrames))
                Check(Sprites.Get(key) != null, $"sprite '{key}' exists");
            Check(true, "all object sprites build");
        }
        catch (System.Exception ex)
        {
            Check(false, $"sprite build failed: {ex.Message}");
        }
    }

    // Ctrl+Z / Ctrl+Y. Direct edits here are not grouped by a mouse stroke,
    // so each undo step is one edit; undo and redo restore tiles, objects
    // and the sky, and a fresh edit drops the redo history.
    private async Task UndoRedoCheck()
    {
        Check(InputMap.HasAction("ui_undo") && InputMap.HasAction("ui_redo"), "built-in undo/redo actions exist");
        var editor = await FreshEditor();
        editor.SetBrushTile(1);
        editor.PaintCell(new Vector2I(4, 8));
        editor.Undo();
        Check(editor.Level.GetTile(4, 8) == TileDefs.Empty, "undo reverts a painted tile");
        editor.Redo();
        Check(editor.Level.GetTile(4, 8) == 1, "redo restores the tile");

        editor.SetBrushObject(ObjectDefs.Barrel);
        editor.PaintCell(new Vector2I(6, 8));
        editor.ToggleSky();
        editor.Undo();
        Check(editor.Level.Sky == "night" && editor.Level.Objects.Any(o => o.Type == ObjectDefs.Barrel),
            "undo reverts the sky toggle and keeps the placed object");
        editor.Undo();
        Check(!editor.Level.Objects.Any(o => o.Type == ObjectDefs.Barrel), "undo removes the placed object");
        editor.Redo();
        editor.Redo();
        Check(editor.Level.Sky == "day" && editor.Level.Objects.Any(o => o.Type == ObjectDefs.Barrel),
            "redo replays both steps");

        editor.EraseCell(new Vector2I(6, 8));
        editor.Redo();
        Check(!editor.Level.Objects.Any(o => o.Type == ObjectDefs.Barrel), "a new edit clears the redo history");
    }

    private async Task ShortcutAndPanCheck()
    {
        var editor = await FreshEditor();
        editor.SetLevel(LevelData.NewDefault(80, 40));

        editor._UnhandledInput(KeyEvent(Key.K, ctrl: true));
        Check(editor.Level.Sky == "day", "Ctrl+K toggles the sky");

        Pan(editor, -150);
        var tiles = editor.Level.Tiles.ToArray();
        int objects = editor.Level.Objects.Count;
        var before = editor.ViewCenter;
        Pan(editor, -50);
        Check(Mathf.IsEqualApprox(editor.ViewCenter.X, before.X + 50), "middle drag pans the camera against the drag");
        Check(editor.Level.Tiles.SequenceEqual(tiles) && editor.Level.Objects.Count == objects,
            "middle drag does not paint");

        editor._UnhandledInput(KeyEvent(Key.P, ctrl: true, shift: true));
        Check(!editor.IsPlaying, "Ctrl+Shift+P does not start play");
        editor._UnhandledInput(KeyEvent(Key.P, ctrl: true));
        Check(editor.IsPlaying, "Ctrl+P starts play");
        editor.StopPlay();

        Pan(editor, 5000);
        var pinned = editor.ViewCenter.X;
        Pan(editor, -30);
        Check(editor.ViewCenter.X > pinned, "panning back from the left edge moves the view at once");
    }

    // Every palette item and category is reachable from the keyboard: 1-0,
    // Shift+1-0, Alt+1-0 and Tab, matched by key position for the digits.
    private async Task PaletteKeysCheck()
    {
        var editor = await FreshEditor();
        Check(editor.Brush.TileId == TileDefs.Wall, "editor opens with the wall brush");
        editor._UnhandledInput(KeyEvent(Key.Key2));
        Check(editor.Brush.TileId == TileDefs.DestructibleWall, "2 picks the second item of the category");
        editor._UnhandledInput(KeyEvent(Key.Key1, shift: true));
        Check(editor.Brush.TileId == TileDefs.BackgroundMesh, "Shift+1 picks the eleventh item");
        editor._UnhandledInput(new InputEventKey { Keycode = Key.Plus, PhysicalKeycode = Key.Key3, Pressed = true });
        Check(editor.Brush.TileId == TileDefs.StoneBlocks, "digits match by key position on layouts without a number row");
        editor._UnhandledInput(KeyEvent(Key.Key3, alt: true));
        editor._UnhandledInput(KeyEvent(Key.Key1));
        Check(editor.Brush.TileId == TileDefs.AlarmFloor, "Alt+3 opens Terrain and 1 picks its first item");
        editor._UnhandledInput(KeyEvent(Key.Tab));
        editor._UnhandledInput(KeyEvent(Key.Key1));
        Check(editor.Brush.ObjectType == ObjectDefs.Computer, "Tab moves to the next category (Devices)");
        editor._UnhandledInput(KeyEvent(Key.Tab, shift: true));
        editor._UnhandledInput(KeyEvent(Key.Key5));
        Check(editor.Brush.TileId == TileDefs.Spikes, "Shift+Tab moves back to Terrain");
        editor._UnhandledInput(KeyEvent(Key.Key5, shift: true));
        Check(editor.Brush.TileId == TileDefs.Spikes, "a key past the category's last item does nothing");
    }

    // Eraser, rectangle fill, picker and the color/number property keys.
    private async Task ToolsCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushTile(TileDefs.Wall);
        editor.PaintCell(new Vector2I(5, 5));
        editor._UnhandledInput(KeyEvent(Key.E));
        Check(editor.CurrentTool == Editor.Tool.Eraser, "E selects the eraser");
        editor.PaintCell(new Vector2I(5, 5));
        Check(editor.Level.GetTile(5, 5) == TileDefs.Empty, "painting with the eraser clears the cell");
        editor._UnhandledInput(KeyEvent(Key.B));
        Check(editor.CurrentTool == Editor.Tool.Brush, "B returns to the brush");

        editor.SetBrushTile(TileDefs.RedBrick);
        editor.ApplyRect(new Vector2I(8, 4), new Vector2I(6, 5), erase: false);
        int filled = 0;
        for (int x = 6; x <= 8; x++)
            for (int y = 4; y <= 5; y++)
                filled += editor.Level.GetTile(x, y) == TileDefs.RedBrick ? 1 : 0;
        Check(filled == 6, "rectangle fill covers every cell between the corners");
        editor.Undo();
        Check(editor.Level.GetTile(6, 4) == TileDefs.Empty && editor.Level.GetTile(8, 5) == TileDefs.Empty,
            "one undo reverts the whole rectangle");

        FillCheck(editor);

        editor.SetBrushObject(ObjectDefs.Door, "red");
        editor._UnhandledInput(KeyEvent(Key.Bracketright));
        Check(editor.BrushColor == "blue", "] steps the door color");
        editor.PaintCell(new Vector2I(3, 9));
        editor.SetBrushTile(TileDefs.Lava);
        editor._UnhandledInput(KeyEvent(Key.Q));
        editor.Pick(new Vector2I(3, 9));
        Check(editor.Brush.ObjectType == ObjectDefs.Door && editor.BrushColor == "blue",
            "the picker copies the object and its color into the brush");
        Check(editor.CurrentTool == Editor.Tool.Brush, "the picker hands back to the brush");

        editor.SetLevel(LevelData.NewDefault(80, 80));
        Pan(editor, -1000, -1200);
        var center = editor.ViewCenter;
        editor._UnhandledInput(KeyEvent(Key.Minus));
        editor._UnhandledInput(KeyEvent(Key.Equal));
        Check(editor.ViewCenter.DistanceTo(center) < 1f, "zooming out and back in keeps the view");
    }

    // The Fill tool floods the clicked tile's connected area with the brush tile.
    private void FillCheck(Editor editor)
    {
        // A 5x4 box of walls with a red brick inside and a health kit inside.
        var level = LevelData.NewDefault(20, 20);
        for (int x = 2; x <= 6; x++)
        {
            level.SetTile(x, 2, TileDefs.Wall);
            level.SetTile(x, 5, TileDefs.Wall);
        }
        for (int y = 2; y <= 5; y++)
        {
            level.SetTile(2, y, TileDefs.Wall);
            level.SetTile(6, y, TileDefs.Wall);
        }
        level.SetTile(3, 3, TileDefs.RedBrick);
        level.Objects.Add(new LevelObject { Type = ObjectDefs.HealthKit, X = 5, Y = 4 });
        editor.SetLevel(level);

        editor._UnhandledInput(KeyEvent(Key.F));
        Check(editor.CurrentTool == Editor.Tool.Fill, "F picks the Fill tool");
        editor.SetBrushTile(TileDefs.Lava);
        Check(editor.CurrentTool == Editor.Tool.Fill, "choosing a tile keeps the Fill tool");

        editor.FillAt(new Vector2I(4, 3));
        Check(editor.Level.GetTile(4, 3) == TileDefs.Lava && editor.Level.GetTile(3, 4) == TileDefs.Lava
                && editor.Level.GetTile(4, 4) == TileDefs.Lava,
            "fill covers the empty cells inside the box");
        Check(editor.Level.GetTile(3, 3) == TileDefs.RedBrick, "fill skips cells of another tile");
        Check(editor.Level.GetTile(5, 4) == TileDefs.Empty, "fill leaves a cell holding an object alone");
        Check(editor.Level.GetTile(2, 3) == TileDefs.Wall && editor.Level.GetTile(1, 1) == TileDefs.Empty,
            "fill stops at the walls");

        editor.SetBrushTile(TileDefs.RedBrick);
        editor.FillAt(new Vector2I(2, 2));
        int bricks = 0;
        for (int x = 2; x <= 6; x++)
            for (int y = 2; y <= 5; y++)
                bricks += editor.Level.GetTile(x, y) == TileDefs.RedBrick ? 1 : 0;
        Check(bricks == 15, "filling a wall repaints the whole connected wall");
        editor.Undo();
        Check(editor.Level.GetTile(2, 2) == TileDefs.Wall && editor.Level.GetTile(6, 5) == TileDefs.Wall,
            "one undo reverts the whole fill");

        editor.SetBrushObject(ObjectDefs.Door, "red");
        Check(editor.CurrentTool == Editor.Tool.Brush, "choosing an object leaves the Fill tool");
        editor.SetTool(Editor.Tool.Brush);
    }

    // The Select tool picks an area; Ctrl+C / Ctrl+V move it around.
    private async Task SelectCopyPasteCheck()
    {
        var editor = await FreshEditor(legacySize: false);
        var level = editor.Level;
        level.SetTile(10, 10, TileDefs.RedBrick);
        level.SetTile(11, 10, TileDefs.Wall);
        level.SetTile(12, 11, TileDefs.Lava);
        level.Objects.Add(new LevelObject
        {
            Type = ObjectDefs.Door, X = 11, Y = 11, Properties = new() { ["color"] = "blue" },
        });

        editor._UnhandledInput(KeyEvent(Key.R));
        Check(editor.CurrentTool == Editor.Tool.Rect, "R picks the Select tool");
        editor._UnhandledInput(KeyEvent(Key.C, ctrl: true));
        Check(editor.Clipboard == null, "Ctrl+C with nothing selected copies nothing");
        editor.SelectArea(new Vector2I(12, 11), new Vector2I(10, 10));
        Check(editor.Selection == new Rect2I(10, 10, 3, 2), "dragging corners in any order selects the cells between");
        editor._UnhandledInput(KeyEvent(Key.C, ctrl: true));
        Check(editor.Clipboard?.Size == new Vector2I(3, 2), "Ctrl+C copies the selection");

        editor.PasteAt(new Vector2I(20, 30));
        Check(level.GetTile(20, 30) == TileDefs.RedBrick && level.GetTile(21, 30) == TileDefs.Wall
            && level.GetTile(22, 31) == TileDefs.Lava, "paste puts the tiles at the offset");
        var pastedDoor = level.Objects.Find(o => o.X == 21 && o.Y == 31);
        Check(pastedDoor?.Type == ObjectDefs.Door && pastedDoor.Properties["color"] == "blue", "paste keeps the door and its color");
        Check(level.GetTile(10, 10) == TileDefs.RedBrick && level.Objects.Any(o => o.X == 11 && o.Y == 11),
            "the source stays untouched");
        Check(editor.Selection == new Rect2I(20, 30, 3, 2), "the pasted area becomes the selection");
        editor.Undo();
        Check(editor.Level.GetTile(20, 30) == TileDefs.Empty && !editor.Level.Objects.Any(o => o.X == 21 && o.Y == 31),
            "one undo reverts the whole paste");
        level = editor.Level;

        level.SetTile(22, 30, TileDefs.Wall);
        editor.PasteAt(new Vector2I(20, 30));
        Check(level.GetTile(22, 30) == TileDefs.Empty, "paste replaces a wall with the clip's empty cell");

        editor.SetLevel(LevelData.NewDefault(80, 80));
        level = editor.Level;
        level.SetTile(1, 1, TileDefs.Wall);
        level.SetTile(2, 1, TileDefs.Wall);
        level.SetTile(1, 2, TileDefs.Wall);
        level.Objects.Add(new LevelObject { Type = ObjectDefs.Door, X = 2, Y = 2, Properties = new() { ["color"] = "red" } });
        level.Objects.Add(new LevelObject { Type = ObjectDefs.Door, X = 1, Y = 1, Properties = new() { ["color"] = "red" } });
        level.SetTile(79, 79, TileDefs.RedBrick);
        editor.SelectArea(new Vector2I(1, 1), new Vector2I(3, 3));
        editor.CopySelection();
        int before = level.Objects.Count;
        editor.PasteAt(new Vector2I(79, 79));
        Check(level.GetTile(79, 79) == TileDefs.Wall && level.Objects.Count == before + 1
            && level.Objects.Any(o => o.X == 79 && o.Y == 79), "paste at the corner keeps only what fits");
        Check(editor.Selection == new Rect2I(79, 79, 1, 1), "the selection after a cut-off paste is what landed");
        int undoDepth = editor.Level.Objects.Count;
        editor.PasteAt(new Vector2I(80, 80));
        editor.PasteAt(new Vector2I(-9, -9));
        Check(editor.Level.Objects.Count == undoDepth, "a paste that lands outside the level changes nothing");
        editor.Undo();
        Check(editor.Level.GetTile(79, 79) == TileDefs.RedBrick, "no undo step was recorded for the failed pastes");

        editor.SetLevel(LevelData.NewDefault(80, 80));
        editor.SelectArea(new Vector2I(0, 76), new Vector2I(5, 79));
        editor.CopySelection();
        editor.PasteAt(new Vector2I(30, 70));
        Check(editor.Level.Objects.Count(o => o.Type == ObjectDefs.AgentStart) == 1 && editor.Level.Validate() == null,
            "pasting a copied agent start keeps exactly one");
        editor.PasteAt(new Vector2I(2, 78));
        Check(editor.Level.Validate() == null, "pasting over the agent start leaves it in place");

        Check(editor.Selection != null, "the paste left a selection");
        editor._UnhandledInput(KeyEvent(Key.Escape));
        Check(editor.Selection == null && editor.Clipboard == null, "Esc clears the selection and the copy");

        void SelectAndCopy()
        {
            editor.SetTool(Editor.Tool.Rect);
            editor.SelectArea(new Vector2I(3, 3), new Vector2I(5, 5));
            editor.CopySelection();
        }
        SelectAndCopy();
        editor._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true });
        editor._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false });
        Check(editor.Selection == null && editor.Clipboard == null, "a right click with the Select tool clears the selection and the copy");
        SelectAndCopy();
        editor._UnhandledInput(KeyEvent(Key.B));
        Check(editor.Selection == null && editor.Clipboard == null, "switching tools clears the selection and the copy");
        SelectAndCopy();
        editor.SetBrushTile(TileDefs.Wall);
        Check(editor.Clipboard == null && editor.CurrentTool == Editor.Tool.Brush, "picking a brush leaves Select and clears the copy");
    }

    // Ctrl+I (or I) makes the playtest agent take no damage; it applies when
    // play starts and can flip mid-play.
    private async Task InvincibleCheck()
    {
        var editor = await FreshEditor();
        editor._UnhandledInput(KeyEvent(Key.I, ctrl: true));
        Check(editor.Invincible, "Ctrl+I turns on invincible playtest");
        Check(editor.StartPlay(), "invincible playtest starts");
        Check(editor.Session.Invincible, "the play session starts invincible");
        int lives = editor.Session.Lives;
        editor.Session.AgentHit();
        await Frames(2);
        Check(editor.Session.Lives == lives && !editor.Session.AgentDown, "hits do not cost an invincible agent a life");
        editor.PlayerAgent.Position = new Vector2(12 * 32, 40 * 32);
        editor.Session.AgentFell();
        Check(editor.Session.Lives == lives && editor.PlayerAgent.Position.Y < 12 * 32,
            "falling out puts an invincible agent back at the start");
        editor._UnhandledInput(KeyEvent(Key.I, ctrl: true));
        Check(!editor.Session.Invincible, "Ctrl+I during play turns it off");
        editor.Session.AgentHit();
        Check(editor.Session.Lives == lives - 1, "hits count again once it is off");
        editor.StopPlay();
    }

    private static InputEventKey KeyEvent(Key key, bool ctrl = false, bool shift = false, bool alt = false) =>
        new() { Keycode = key, PhysicalKeycode = key, Pressed = true, CtrlPressed = ctrl, ShiftPressed = shift, AltPressed = alt };

    // Positive dx drags the mouse right, which moves the camera left.
    private static void Pan(Editor editor, float dx, float dy = 0)
    {
        editor._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = true });
        editor._UnhandledInput(new InputEventMouseMotion
        {
            ButtonMask = MouseButtonMask.Middle,
            Relative = new Vector2(dx, dy),
        });
        editor._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = false });
    }

    private async Task EditorLoopChecks()
    {
        var editor = await FreshEditor();
        editor.SetBrushTile(1);
        editor.PaintCell(new Vector2I(4, 8));
        Check(editor.Level.GetTile(4, 8) == 1, "painting sets a tile");
        editor.EraseCell(new Vector2I(4, 8));
        Check(editor.Level.GetTile(4, 8) == TileDefs.Empty, "erasing clears a tile");

        Check(editor.StartPlay(), "play mode starts on a valid level");
        var agent = editor.PlayerAgent;
        Check(agent != null, "agent spawns at the start position");
        float startY = agent?.Position.Y ?? 0;
        await Frames(20);
        Check(editor.IsPlaying, "play mode survives physics frames");
        Check(agent != null && Mathf.Abs(agent.Position.Y - startY) < 8,
            "agent stands on solid tiles (tile collision works)");
        editor.StopPlay();
        Check(!editor.IsPlaying, "stop returns to edit mode");
    }

    // The agent must fit through a corridor exactly 1 tile high.
    private async Task CorridorCheck()
    {
        var editor = _editor;
        editor.SetBrushTile(TileDefs.Wall);
        for (int x = 4; x <= 12; x++)
            editor.PaintCell(new Vector2I(x, 9));
        Check(editor.StartPlay(), "corridor test level starts");
        var runner = editor.PlayerAgent;
        Input.ActionPress("move_right");
        await Frames(120);
        Input.ActionRelease("move_right");
        Check(runner != null && runner.Position.X > 13 * 32,
            "agent fits through a 1-tile corridor");
        editor.StopPlay();
    }

    // Collision boxes fit the drawn bodies: the agent and a soldier walk up
    // to a wall, while a robot's wider body keeps it further off.
    private async Task WallContactCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushTile(TileDefs.Wall);
        foreach (int x in new[] { 8, 20, 27 })
            for (int y = 8; y <= 10; y++)
                editor.PaintCell(new Vector2I(x, y));
        editor.SetBrushObject(EnemyDefs.Soldier);
        editor.PaintCell(new Vector2I(23, 10));
        editor.SetBrushObject(EnemyDefs.Robot);
        editor.PaintCell(new Vector2I(31, 10));
        Check(editor.StartPlay(), "wall contact level starts");
        var soldier = editor.Session.Enemies.First(e => e.Def.Type == EnemyDefs.Soldier);
        var robot = editor.Session.Enemies.First(e => e.Def.Type == EnemyDefs.Robot);
        float soldierMin = soldier.Position.X, robotMin = robot.Position.X;
        Input.ActionPress("move_right");
        for (int i = 0; i < 150; i++)
        {
            await Frames(1);
            soldierMin = Mathf.Min(soldierMin, soldier.Position.X);
            robotMin = Mathf.Min(robotMin, robot.Position.X);
        }
        Input.ActionRelease("move_right");
        float agentGap = 8 * 32 - editor.PlayerAgent.Position.X;
        Check(agentGap <= 9, $"the agent walks up to a wall (center {agentGap:0.0} px off it)");
        float soldierGap = soldierMin - 21 * 32;
        Check(soldierGap <= 9, $"a patrolling soldier walks up to a wall (center {soldierGap:0.0} px off it)");
        float robotGap = robotMin - 28 * 32;
        Check(robotGap >= 13, $"a robot's wide body keeps it off the wall (center {robotGap:0.0} px off it)");
        editor.StopPlay();
    }

    // The default level is 80x80 with the floor and agent start at the
    // bottom; the agent spawns and stands there.
    private async Task DefaultLevelCheck()
    {
        var editor = await FreshEditor(legacySize: false);
        Check(editor.Level.Width == 80 && editor.Level.Height == 80, "default level is 80x80");
        Check(editor.StartPlay(), "default 80x80 level starts");
        var agent = editor.PlayerAgent;
        Check(agent != null && agent.Position.Y > 77 * 32,
            "agent starts near the bottom of the tall level");
        float startY = agent?.Position.Y ?? 0;
        await Frames(20);
        Check(agent != null && Mathf.Abs(agent.Position.Y - startY) < 8,
            "agent stands on the floor of the tall level");
        editor.StopPlay();
    }

    // Background walls are pure scenery (the agent walks through them);
    // the new solid wall variants block like any wall.
    private async Task WallTilesCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushTile(TileDefs.BackgroundMesh);
        editor.PaintCell(new Vector2I(5, 9));
        editor.PaintCell(new Vector2I(5, 10));
        editor.SetBrushTile(TileDefs.StoneBlocks);
        editor.PaintCell(new Vector2I(9, 9));
        editor.PaintCell(new Vector2I(9, 10));
        Check(editor.StartPlay(), "wall variants level starts");
        var agent = editor.PlayerAgent;
        Input.ActionPress("move_right");
        await Frames(90);
        Input.ActionRelease("move_right");
        Check(agent != null && agent.Position.X > 6 * 32,
            "background wall is not solid (agent walks through)");
        Check(agent != null && agent.Position.X < 9 * 32 - 8,
            "stone block wall stops the agent");
        editor.StopPlay();
    }

    private async Task ShadowCheck()
    {
        var editor = await FreshEditor();
        var wall = new Vector2I(6, 5);
        var beneath = new Vector2I(6, 6);
        editor.SetBrushTile(TileDefs.BackgroundBrick);
        editor.PaintCell(beneath);
        Check(!HasShadow(editor, beneath), "scenery with no solid neighbour gets no shade");
        editor.SetBrushTile(TileDefs.DestructibleWall);
        editor.PaintCell(wall);
        Check(HasShadow(editor, beneath), "painting a solid tile shades the scenery beneath it");
        Check(!HasShadow(editor, wall), "the solid tile itself gets no shade");
        Check(editor.Level.GetTile(beneath.X, beneath.Y) == TileDefs.BackgroundBrick,
            "the shade is not written into the level");
        editor.EraseCell(wall);
        Check(!HasShadow(editor, beneath), "erasing the solid tile removes the shade");

        editor.SetBrushTile(TileDefs.Water);
        editor.PaintCell(new Vector2I(8, 6));
        editor.SetBrushTile(TileDefs.Wall);
        editor.PaintCell(new Vector2I(8, 5));
        Check(!HasShadow(editor, new Vector2I(8, 6)), "water under a ceiling gets no shade");

        editor.SetBrushTile(TileDefs.BackgroundBrick);
        editor.PaintCell(new Vector2I(10, 6));
        editor.SetBrushTile(TileDefs.HiddenPlatform);
        editor.PaintCell(new Vector2I(10, 5));
        Check(!HasShadow(editor, new Vector2I(10, 6)), "a hidden platform casts no shade that would reveal it");

        editor.SetBrushTile(TileDefs.DestructibleWall);
        editor.PaintCell(wall);
        Check(editor.StartPlay(), "shadow level starts");
        editor.Session.DestroyTile(wall);
        Check(!HasShadow(editor, beneath), "a wall destroyed in play stops shading");
        editor.StopPlay();
        Check(HasShadow(editor, beneath), "stopping play restores the wall's shade");
    }

    private static bool HasShadow(Editor editor, Vector2I cell) =>
        editor.Grids.Shadow.GetCellSourceId(cell) != -1;

    private async Task PipeCheck()
    {
        var editor = await FreshEditor();
        var a = new Vector2I(6, 5);
        var b = a + Vector2I.Right;
        var c = b + Vector2I.Down;
        editor.SetBrushTile(TileDefs.PipeWall);
        editor.PaintCell(a);
        Check(PipeAt(editor, a) == PipeLinks.None, "a lone pipe has no links");
        editor.PaintCell(b);
        Check(PipeAt(editor, a) == PipeLinks.Right && PipeAt(editor, b) == PipeLinks.Left,
            "painting a pipe beside another joins both");
        editor.PaintCell(c);
        Check(PipeAt(editor, b) == (PipeLinks.Left | PipeLinks.Down) && PipeAt(editor, c) == PipeLinks.Up,
            "a pipe below turns its neighbour into an elbow");
        Check(editor.Grids.TileIdAt(b) == TileDefs.PipeWall, "a joined pipe still reads back as a pipe wall");
        editor.SetBrushTile(TileDefs.Wall);
        editor.PaintCell(b);
        Check(PipeAt(editor, a) == PipeLinks.None && PipeAt(editor, c) == PipeLinks.None,
            "painting over a pipe with another tile unlinks its neighbours");
        editor.SetBrushTile(TileDefs.PipeWall);
        editor.PaintCell(b);
        editor.EraseCell(b);
        Check(PipeAt(editor, a) == PipeLinks.None && PipeAt(editor, c) == PipeLinks.None,
            "erasing a pipe unlinks its neighbours");

        editor.PaintCell(b);
        editor.SetLevel(editor.Level);
        Check(PipeAt(editor, b) == (PipeLinks.Left | PipeLinks.Down), "redrawing a whole level joins pipes too");
    }

    // A wall rims every side without a solid neighbour; any solid tile
    // joins it, a background tile does not.
    private async Task RimCheck()
    {
        var editor = await FreshEditor();
        var a = new Vector2I(6, 5);
        var b = a + Vector2I.Right;
        var c = a + Vector2I.Down;
        var d = b + Vector2I.Down;
        Vector2I At(Vector2I cell) => editor.Grids.Main.GetCellAtlasCoords(cell);
        editor.SetBrushTile(TileDefs.Wall);
        editor.PaintCell(a);
        Check(At(a) == TileSetBuilder.RimCoordsFor(TileDefs.Wall, WallJoins.None), "a lone wall is rimmed all round");
        editor.PaintCell(b);
        Check(At(a) == TileSetBuilder.RimCoordsFor(TileDefs.Wall, WallJoins.Right)
            && At(b) == TileSetBuilder.RimCoordsFor(TileDefs.Wall, WallJoins.Left),
            "two walls side by side drop the rim between them");
        Check(editor.Grids.TileIdAt(b) == TileDefs.Wall, "a rimmed wall still reads back as a wall");
        editor.SetBrushTile(TileDefs.RedBrick);
        editor.PaintCell(c);
        Check(At(a) == TileSetBuilder.RimCoordsFor(TileDefs.Wall, WallJoins.Right | WallJoins.Down)
            && At(c) == TileSetBuilder.RimCoordsFor(TileDefs.RedBrick, WallJoins.Up),
            "a different solid tile joins the wall too");
        Check(At(a) != TileSetBuilder.RimCoordsFor(TileDefs.Wall, WallJoins.Right | WallJoins.Down | WallJoins.DownRight)
            && TileSetBuilder.RimCoordsFor(TileDefs.Wall, WallJoins.Right) == TileSetBuilder.RimCoordsFor(TileDefs.Wall, WallJoins.Right | WallJoins.DownRight),
            "an open diagonal is an inner corner, one beside an open side changes nothing");
        editor.PaintCell(d);
        Check(At(a) == TileSetBuilder.RimCoordsFor(TileDefs.Wall, WallJoins.Right | WallJoins.Down | WallJoins.DownRight),
            "filling the diagonal closes the inner corner");
        editor.EraseCell(b);
        Check(At(a) == TileSetBuilder.RimCoordsFor(TileDefs.Wall, WallJoins.Down), "erasing a wall rims its neighbour");
        editor.SetBrushTile(TileDefs.BackgroundBrick);
        editor.PaintCell(b);
        Check(At(a) == TileSetBuilder.RimCoordsFor(TileDefs.Wall, WallJoins.Down), "a background tile leaves the rim on");
        editor.SetLevel(editor.Level);
        Check(At(a) == TileSetBuilder.RimCoordsFor(TileDefs.Wall, WallJoins.Down), "redrawing a whole level rims walls too");
    }

    // The links a pipe cell is drawn with, or -1 when it shows something else.
    private static PipeLinks PipeAt(Editor editor, Vector2I cell)
    {
        var coords = editor.Grids.Main.GetCellAtlasCoords(cell);
        for (int links = 0; links < 16; links++)
            if (coords == TileSetBuilder.PipeCoordsFor((PipeLinks)links))
                return (PipeLinks)links;
        return (PipeLinks)(-1);
    }

    // Empty cells are the outdoors: a per-level night/day sky fills the
    // level behind the tiles, toggled from the toolbar and saved with the
    // level.
    private async Task SkyCheck()
    {
        var editor = await FreshEditor();
        Check(editor.Level.Sky == "night", "new levels default to the night sky");
        editor.ToggleSky();
        Check(editor.Level.Sky == "day", "sky toggle switches to day");
        editor.Level.Name = "Sky Test";
        LevelStore.Save(editor.Level);
        var loaded = LevelStore.Load(LevelStore.FileNameFor("Sky Test"));
        Check(loaded != null && loaded.Sky == "day", "sky choice round-trips through save/load");
        DirAccess.RemoveAbsolute($"{LevelStore.DirPath}/{LevelStore.FileNameFor("Sky Test")}");
    }

    // Lava on the start cell drains all three lives, which ends the run.
    // Each of them costs a full death sequence, so this takes a while.
    private async Task LivesAndFailCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushTile(TileDefs.Lava);
        editor.PaintCell(new Vector2I(2, 10));
        Check(editor.StartPlay(), "lava level starts");
        await Frames(270);
        Check(!editor.IsPlaying, "losing all lives returns to edit mode");
    }

    // A picked-up vest absorbs the first hit without costing a life.
    private async Task VestCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.Vest);
        editor.PaintCell(new Vector2I(3, 10));
        editor.SetBrushTile(TileDefs.Lava);
        editor.PaintCell(new Vector2I(6, 10));
        Check(editor.StartPlay(), "vest level starts");
        Input.ActionPress("move_right");
        await Frames(70);
        Input.ActionRelease("move_right");
        Check(editor.IsPlaying && editor.Session.Lives == 2,
            "vest absorbed the first hit (two hits cost one life)");
        editor.StopPlay();
    }

    // Falling below the level costs a life and respawns at the start.
    private async Task KillPlaneCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushEraser();
        for (int x = 6; x <= 9; x++)
            editor.PaintCell(new Vector2I(x, 11));
        Check(editor.StartPlay(), "pit level starts");
        Input.ActionPress("move_right");
        await Frames(40);
        Input.ActionRelease("move_right");
        await Frames(140);   // the fall, then the death sequence before the respawn
        Check(editor.IsPlaying && editor.Session.Lives == 2, "falling below the level costs one life");
        Check(editor.PlayerAgent != null && editor.PlayerAgent.Position.X < 6 * 32,
            "agent respawned at the start");
        editor.StopPlay();
    }

    // Running past two checkpoints into the pit lights only the second and
    // respawns the agent there, not at the start.
    private async Task CheckpointCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.Checkpoint);
        editor.PaintCell(new Vector2I(3, 10));
        editor.PaintCell(new Vector2I(5, 10));
        editor.SetBrushEraser();
        for (int x = 6; x <= 9; x++)
            editor.PaintCell(new Vector2I(x, 11));
        Check(editor.StartPlay(), "checkpoint level starts");
        Input.ActionPress("move_right");
        await Frames(40);
        Input.ActionRelease("move_right");
        await Frames(140);
        var flags = new List<Checkpoint>();
        foreach (var child in editor.Session.GetChildren())
            if (child is Checkpoint flag)
                flags.Add(flag);
        flags.Sort((a, b) => a.Position.X.CompareTo(b.Position.X));
        Check(flags.Count == 2 && !flags[0].Lit && flags[1].Lit, "only the last checkpoint touched is lit");
        Check(editor.IsPlaying && editor.Session.Lives == 2, "the fall past the checkpoints costs one life");
        Check(editor.PlayerAgent != null && Mathf.Abs(editor.PlayerAgent.Position.X - (5 * 32 + 16)) < 4,
            "agent respawned at the last checkpoint");
        editor.StopPlay();
    }

    // N / Shift+N in playtest step the agent through the agent start and the
    // checkpoints in placement order, lighting each stop as its respawn point.
    private async Task CheckpointJumpCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.Checkpoint);
        editor.PaintCell(new Vector2I(20, 10));
        editor.PaintCell(new Vector2I(10, 10));
        Check(editor.StartPlay(), "checkpoint jump level starts");
        var start = editor.PlayerAgent.Position;
        static Vector2 Center(int x, int y) => new(x * 32 + 16, y * 32 + 16);
        Checkpoint FlagAt(Vector2 at) =>
            editor.Session.GetChildren().OfType<Checkpoint>().First(f => f.Position == at);

        editor._UnhandledInput(KeyEvent(Key.N));
        Check(editor.PlayerAgent.Position == Center(20, 10) && FlagAt(Center(20, 10)).Lit,
            "N jumps to the first placed checkpoint and lights it");
        editor._UnhandledInput(KeyEvent(Key.N));
        Check(editor.PlayerAgent.Position == Center(10, 10) && FlagAt(Center(10, 10)).Lit && !FlagAt(Center(20, 10)).Lit,
            "N again jumps to the second placed checkpoint");
        editor._UnhandledInput(KeyEvent(Key.N));
        Check(editor.PlayerAgent.Position == start && !FlagAt(Center(10, 10)).Lit, "N wraps back to the agent start");
        editor._UnhandledInput(KeyEvent(Key.N, shift: true));
        Check(editor.PlayerAgent.Position == Center(10, 10), "Shift+N steps back to the last checkpoint");

        editor.PlayerAgent.Position = new Vector2(12 * 32, 40 * 32);
        editor.Session.AgentFell();
        await Frames(90);
        Check(editor.PlayerAgent.Position.DistanceTo(Center(10, 10)) < 4, "a jumped-to checkpoint is the respawn point");
        Check(editor.IsPlaying, "jumping does not stop play");
        editor.StopPlay();
    }

    // Health kit, key + matching door, and shooting a destructible wall,
    // all in one left-to-right run.
    private async Task ItemCourseCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.HealthKit);
        editor.PaintCell(new Vector2I(5, 10));
        editor.SetBrushObject(ObjectDefs.DoorKey, "red");
        editor.PaintCell(new Vector2I(7, 10));
        editor.SetBrushObject(ObjectDefs.Door, "red");
        editor.PaintCell(new Vector2I(10, 10));
        editor.SetBrushTile(TileDefs.DestructibleWall);
        editor.PaintCell(new Vector2I(13, 8));
        editor.PaintCell(new Vector2I(13, 9));
        editor.PaintCell(new Vector2I(13, 10));
        Check(editor.StartPlay(), "item course starts");
        var agent = editor.PlayerAgent;
        Input.ActionPress("move_right");
        var keyDoors = editor.Session.GetChildren().OfType<LockedDoor>().ToList();
        for (int i = 0; i < 200 && !keyDoors.Any(d => d.IsOpen); i++)
            await Frames(1);
        await CheckDoorSlide(keyDoors, "key door");
        await Frames(10);
        Input.ActionPress("shoot");
        await Frames(2);
        Input.ActionRelease("shoot");
        await Frames(80);
        Input.ActionRelease("move_right");
        Check(editor.Session.Lives == 4, "health kit added a life");
        Check(editor.Session.Ammo == 9, "shooting spent one ammo");
        Check(agent != null && agent.Position.X > 14 * 32,
            "key opened the door and the shot cleared the wall");
        editor.StopPlay();
        Check(editor.Level.GetTile(13, 10) == TileDefs.DestructibleWall,
            "destroyed wall is restored in edit mode");
    }

    // Submerged fall speed is capped; a jump from the top water tile gets
    // the full jump velocity (README: jump from the surface line).
    private async Task WaterCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushTile(TileDefs.Water);
        for (int y = 3; y <= 10; y++)
            editor.PaintCell(new Vector2I(20, y));
        Vector2I WaterAt(int y) => editor.Grids.Main.GetCellAtlasCoords(new Vector2I(20, y));
        Check(WaterAt(3) == TileSetBuilder.WaterCoordsFor(waterAbove: false)
            && WaterAt(4) == TileSetBuilder.WaterCoordsFor(waterAbove: true)
            && WaterAt(10) == TileSetBuilder.WaterCoordsFor(waterAbove: true),
            "only the top water cell draws the surface");
        Check(editor.Grids.TileIdAt(new Vector2I(20, 4)) == TileDefs.Water, "deep water still reads back as water");
        editor.EraseCell(new Vector2I(20, 3));
        Check(WaterAt(4) == TileSetBuilder.WaterCoordsFor(waterAbove: false), "erasing the top cell moves the surface down");
        editor.PaintCell(new Vector2I(20, 3));
        editor.SetBrushTile(TileDefs.Lava);
        editor.PaintCell(new Vector2I(22, 9));
        editor.PaintCell(new Vector2I(22, 10));
        Vector2I LavaAt(int y) => editor.Grids.Main.GetCellAtlasCoords(new Vector2I(22, y));
        Check(LavaAt(9) == TileSetBuilder.LavaCoordsFor(lavaAbove: false)
            && LavaAt(10) == TileSetBuilder.LavaCoordsFor(lavaAbove: true),
            "only the top lava cell draws the bubbling surface");
        editor.EraseCell(new Vector2I(22, 9));
        editor.EraseCell(new Vector2I(22, 10));
        Check(editor.StartPlay(), "water level starts");
        var agent = editor.PlayerAgent;
        agent.Position = new Vector2(20 * 32 + 16, 3 * 32 + 16);
        agent.Velocity = Vector2.Zero;
        float maxFall = 0;
        for (int i = 0; i < 40; i++)
        {
            await Frames(1);
            maxFall = Mathf.Max(maxFall, agent.Velocity.Y);
        }
        Check(maxFall > 0 && maxFall < 100, "water caps sink speed");

        agent.Position = new Vector2(20 * 32 + 16, 3 * 32 + 16);
        agent.Velocity = Vector2.Zero;
        await Frames(1);
        Input.ActionPress("jump");
        await Frames(2);   // an injected action press registers on the following physics tick
        Input.ActionRelease("jump");
        Check(agent.Velocity.Y < -300, "jump from the surface line gets full jump velocity");
        editor.StopPlay();
    }

    // The two spikes take turns and each only hurts over its own half of the
    // tile while raised; the cycle clock is settable in tests.
    private async Task SpikeCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushTile(TileDefs.Spikes);
        var cell = new Vector2I(5, 10);
        editor.PaintCell(cell);
        Check(editor.StartPlay(), "spike level starts");
        var agent = editor.PlayerAgent;
        agent.Position = new Vector2(5 * 32 + 30, 10 * 32 + 16);   // over the right half only
        agent.Velocity = Vector2.Zero;
        await Frames(5);
        Check(editor.Session.Lives == 3, "lowered spikes are safe");
        Check(editor.Grids.Main.GetCellAtlasCoords(cell) == TileSetBuilder.SpikeCoordsFor(0, 0), "lowered spikes show the bare plate");
        editor.Session.Time = 3.5f;
        await Frames(5);
        Check(editor.Session.Lives == 3, "the raised left spike does not hurt over the right half");
        Check(editor.Grids.Main.GetCellAtlasCoords(cell) == TileSetBuilder.SpikeCoordsFor(0, TileSetBuilder.SpikeLevels), "the left spike shows raised");
        editor.Session.Time = 8.5f;
        await Frames(5);
        Check(editor.Session.Lives == 2, "the raised right spike takes a life");
        Check(editor.Grids.Main.GetCellAtlasCoords(cell) == TileSetBuilder.SpikeCoordsFor(1, TileSetBuilder.SpikeLevels), "the right spike shows raised");
        editor.StopPlay();
    }

    // Spears cycle holes, tips, out (3s each) and hurt only while out; the
    // grid shows the matching frame.
    private async Task SpearCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushTile(TileDefs.Spear);
        var cell = new Vector2I(5, 10);
        editor.PaintCell(cell);
        Check(editor.StartPlay(), "spear level starts");
        var agent = editor.PlayerAgent;
        agent.Position = new Vector2(5 * 32 + 16, 10 * 32 + 16);
        agent.Velocity = Vector2.Zero;
        await Frames(5);
        Check(editor.Session.Lives == 3, "retracted spears are safe");
        Check(editor.Grids.Main.GetCellAtlasCoords(cell) == TileSetBuilder.SpearCoordsFor(0), "retracted spears show bare holes");
        editor.Session.Time = 4.5f;
        await Frames(5);
        Check(editor.Session.Lives == 3, "spear tips in the holes are safe");
        Check(editor.Grids.Main.GetCellAtlasCoords(cell) == TileSetBuilder.SpearCoordsFor(1), "the warning state shows the tips");
        editor.Session.Time = 7.5f;
        await Frames(5);
        Check(editor.Session.Lives == 2, "spears out take a life");
        Check(editor.Grids.Main.GetCellAtlasCoords(cell) == TileSetBuilder.SpearCoordsFor(TileSetBuilder.SpearFrames - 1), "spears out show the full frame");
        editor.StopPlay();
    }

    // Electric floor takes a life after 3 seconds of standing on it.
    private async Task ElectricFloorCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushTile(TileDefs.ElectricFloor);
        editor.PaintCell(new Vector2I(2, 11));
        Check(editor.StartPlay(), "electric level starts");
        await Frames(60);
        Check(editor.Session.Lives == 3, "electric floor does not hurt instantly");
        await Frames(150);
        Check(editor.Session.Lives == 2, "electric floor takes a life after 3 seconds");
        editor.StopPlay();
    }

    // Manual items: activate (C) turns picked-up goggles on.
    private async Task GogglesCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.InfraGoggles);
        editor.PaintCell(new Vector2I(3, 10));
        Check(editor.StartPlay(), "goggles level starts");
        Input.ActionPress("move_right");
        await Frames(20);
        Input.ActionRelease("move_right");
        Input.ActionPress("activate");
        await Frames(2);
        Input.ActionRelease("activate");
        Check(editor.Session.GogglesActive, "activate turns on picked-up infra goggles");
        editor.StopPlay();
    }

    // Hidden platforms only exist while the goggles reveal them: hidden, the
    // agent walks straight through; revealed, they block. Stopping play
    // brings the editor's semi-transparent tint back.
    private async Task HiddenPlatformCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushTile(TileDefs.HiddenPlatform);
        editor.PaintCell(new Vector2I(8, 10));
        Check(editor.StartPlay(), "hidden platform level starts");
        Input.ActionPress("move_right");
        await Frames(90);
        Input.ActionRelease("move_right");
        Check(editor.PlayerAgent.Position.X > 9 * 32, "a hidden platform does not block the agent");
        editor.StopPlay();
        await Frames(2);
        Check(editor.Grids.Hidden.Modulate.A > 0.1f, "hidden platforms show again in the editor after play");

        editor.SetBrushObject(ObjectDefs.InfraGoggles);
        editor.PaintCell(new Vector2I(3, 10));
        Check(editor.StartPlay(), "goggles and hidden platform level starts");
        Input.ActionPress("move_right");
        await Frames(20);
        Input.ActionRelease("move_right");
        Input.ActionPress("activate");
        await Frames(2);
        Input.ActionRelease("activate");
        Check(editor.Session.GogglesActive, "goggles reveal the platform");
        Input.ActionPress("move_right");
        await Frames(60);
        Input.ActionRelease("move_right");
        Check(editor.PlayerAgent.Position.X < 8 * 32, "a revealed platform blocks the agent");
        editor.StopPlay();

        editor = await FreshEditor();
        editor.SetBrushTile(TileDefs.HiddenPlatform);
        editor.PaintCell(new Vector2I(8, 10));
        editor._UnhandledInput(KeyEvent(Key.H));
        Check(editor.RevealHidden, "H turns on solid hidden platforms");
        Check(editor.StartPlay(), "solid hidden platform playtest starts");
        Input.ActionPress("move_right");
        await Frames(60);
        Input.ActionRelease("move_right");
        Check(editor.PlayerAgent.Position.X < 8 * 32, "the switch makes a hidden platform block the agent without goggles");
        Check(editor.Grids.Hidden.Modulate.A > 0.9f, "the switch shows hidden platforms in play");
        editor._UnhandledInput(KeyEvent(Key.H));
        Check(!editor.Session.RevealHidden, "H during play turns it off");
        Input.ActionPress("move_right");
        await Frames(60);
        Input.ActionRelease("move_right");
        Check(editor.PlayerAgent.Position.X > 9 * 32, "with the switch off the agent walks through again");
        editor.StopPlay();
    }

    // A patrolling fighter spots the agent (row LoS), chases and hits on contact.
    private async Task FighterContactCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(EnemyDefs.Fighter);
        editor.PaintCell(new Vector2I(8, 10));
        Check(editor.StartPlay(), "fighter level starts");
        Check(editor.Session.Enemies.Count == 1, "enemy spawns in play mode");
        await Frames(150);
        Check(editor.IsPlaying && editor.Session.Lives == 2, "enemy contact costs a life");
        editor.StopPlay();
    }

    // Ultimate -> Super -> Soldier -> dead, three hits across a pit that the
    // melee stages cannot cross. The first gunshot also raises the alarm.
    private async Task DegradationCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushEraser();
        editor.PaintCell(new Vector2I(6, 11));
        editor.PaintCell(new Vector2I(7, 11));
        editor.SetBrushObject(EnemyDefs.UltimateSoldier);
        editor.PaintCell(new Vector2I(11, 10));
        Check(editor.StartPlay(), "degradation level starts");
        for (int shot = 0; shot < 3; shot++)
        {
            Input.ActionPress("shoot");
            await Frames(2);
            Input.ActionRelease("shoot");
            await Frames(10);
        }
        Check(editor.Session.AlarmActive, "gunshot triggers the alarm");
        await Frames(60);
        Check(editor.Session.Enemies.Count == 0, "ultimate soldier degrades and dies after three hits");
        editor.StopPlay();
    }

    // The turret starts aimed right; the agent stands to its left. It swings
    // its barrel over the top onto the agent before it fires.
    private async Task TurretCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(EnemyDefs.Turret);
        editor.PaintCell(new Vector2I(8, 10));
        Check(editor.StartPlay(), "turret level starts");
        await Frames(45);
        Check(!editor.Session.GetChildren().OfType<EnemyBullet>().Any() && editor.Session.Lives == 3,
            "the turret holds fire while its barrel swings round");
        await Frames(120);
        Check(editor.IsPlaying && editor.Session.Lives == 2, "turret plasma takes a life");
        editor.StopPlay();
    }

    // Robots shrug off bullets except during their 1s off-window every 10s.
    private async Task RobotCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(EnemyDefs.Robot);
        editor.PaintCell(new Vector2I(8, 10));
        Check(editor.StartPlay(), "robot level starts");
        Input.ActionPress("shoot");
        await Frames(2);
        Input.ActionRelease("shoot");
        await Frames(40);
        Check(editor.Session.Enemies.Count == 1, "robot shrugs off bullets while active");
        editor.Session.Time = 9.0f;   // inside the off-window (9..10)
        Input.ActionPress("shoot");
        await Frames(2);
        Input.ActionRelease("shoot");
        await Frames(40);
        Check(editor.Session.Enemies.Count == 0, "robot dies while turned off");
        editor.StopPlay();
    }

    // A dead robodog drops an ammo pack (drop chance forced to 100%).
    private async Task DropCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushEraser();
        editor.PaintCell(new Vector2I(6, 11));
        editor.PaintCell(new Vector2I(7, 11));
        editor.SetBrushObject(EnemyDefs.Robodog);
        editor.PaintCell(new Vector2I(11, 10));
        Check(editor.StartPlay(), "drop level starts");
        editor.Session.DropChance = 1f;
        for (int shot = 0; shot < 2; shot++)
        {
            Input.ActionPress("shoot");
            await Frames(2);
            Input.ActionRelease("shoot");
            await Frames(10);
        }
        await Frames(40);
        Check(editor.Session.Enemies.Count == 0, "robodog dies after two hits");
        Check(editor.Session.GetChildren().OfType<Pickup>().Any(p => p.Source.Type == ObjectDefs.AmmoPack),
            "dead robodog dropped an ammo pack");
        editor.StopPlay();
    }

    // The eel patrols inside the water and shocks the agent on contact.
    private async Task EelCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushTile(TileDefs.Water);
        for (int x = 8; x <= 12; x++)
            for (int y = 8; y <= 10; y++)
                editor.PaintCell(new Vector2I(x, y));
        editor.SetBrushObject(EnemyDefs.ElectricEel);
        editor.PaintCell(new Vector2I(10, 10));
        Check(editor.StartPlay(), "eel level starts");
        Input.ActionPress("move_right");
        await Frames(90);
        Input.ActionRelease("move_right");
        await Frames(10);
        Check(editor.IsPlaying && editor.Session.Lives == 2, "electric eel shock costs a life");
        editor.StopPlay();
    }

    // The piranha lurks harmless under the water around the agent, then leaps
    // out of it and bites, and settles back at its spawn point.
    private async Task PiranhaCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushTile(TileDefs.Water);
        editor.PaintCell(new Vector2I(4, 10));
        editor.SetBrushObject(EnemyDefs.RobotPiranha);
        editor.PaintCell(new Vector2I(4, 10));
        Check(editor.StartPlay(), "piranha level starts");
        var piranha = editor.Session.Enemies[0];
        var home = piranha.Position;
        editor.Session.Agent.Position = home;
        await Frames(30);
        piranha.TakeHit();
        Check(editor.Session.Enemies.Count == 1 && piranha.Submerged, "submerged piranha ignores hits");
        await Frames(30);
        Check(editor.Session.Lives == 3, "submerged piranha is harmless");
        float highest = home.Y;
        for (int i = 0; i < 60; i++)
        {
            await Frames(1);
            highest = Mathf.Min(highest, piranha.Position.Y);
        }
        Check(editor.Session.Lives == 2, "leaping piranha costs a life");
        Check(Mathf.Abs(home.Y - highest - 96f) < 8f, "piranha leaps 3 tiles");
        await Frames(90);
        Check(piranha.Position == home, "piranha returns to its spawn point");
        editor.StopPlay();
    }

    // Standing on an alarm floor tile raises the alarm.
    private async Task AlarmFloorCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushTile(TileDefs.AlarmFloor);
        editor.PaintCell(new Vector2I(2, 11));
        Check(editor.StartPlay(), "alarm floor level starts");
        await Frames(5);
        Check(editor.Session.AlarmActive, "alarm floor triggers the alarm");
        editor.StopPlay();
    }

    // Landmines explode on impact and are consumed.
    private async Task MineCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(EnemyDefs.Landmine);
        editor.PaintCell(new Vector2I(6, 10));
        Check(editor.StartPlay(), "landmine level starts");
        Input.ActionPress("move_right");
        await Frames(40);
        Input.ActionRelease("move_right");
        await Frames(20);
        Check(editor.IsPlaying && editor.Session.Lives == 2, "landmine explosion costs a life");
        Check(editor.Session.Enemies.Count == 0, "landmine is consumed by its explosion");
        editor.StopPlay();
    }

    // Explosions destroy destructible walls, kill enemies in the radius,
    // hurt the agent and raise the alarm.
    private async Task ExplosionCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushTile(TileDefs.DestructibleWall);
        editor.PaintCell(new Vector2I(5, 9));
        editor.SetBrushObject(EnemyDefs.Fighter);
        editor.PaintCell(new Vector2I(8, 10));
        Check(editor.StartPlay(), "explosion level starts");
        await Frames(2);
        editor.Session.Explode(new Vector2(6 * 32, 10 * 32 + 16));
        await Frames(2);
        Check(editor.Session.TileAt(5, 9) == TileDefs.Empty, "explosion destroys destructible walls");
        Check(editor.Session.Enemies.Count == 0, "explosion kills enemies in the radius");
        Check(editor.Session.Lives == 2, "explosion hurts the agent");
        Check(editor.Session.AlarmActive, "explosion triggers the alarm");
        editor.StopPlay();
    }

    // USB key 2 hacks computer 2 on proximity, turning off laser circuit 2.
    // Computer 3 must stay locked.
    private async Task ComputerHackCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.UsbKey, number: "2");
        editor.PaintCell(new Vector2I(5, 10));
        editor.SetBrushObject(ObjectDefs.Computer, number: "2");
        editor.PaintCell(new Vector2I(8, 10));
        editor.SetBrushObject(ObjectDefs.Computer, number: "3");
        editor.PaintCell(new Vector2I(10, 10));
        editor.SetBrushObject(ObjectDefs.Laser, number: "2");
        editor.PaintCell(new Vector2I(12, 10));
        Check(editor.StartPlay(), "computer level starts");
        Input.ActionPress("move_right");
        await Frames(120);
        Input.ActionRelease("move_right");
        Check(editor.Session.IsCircuitOff("2"), "usb key with the matching number hacks the computer");
        Check(!editor.Session.IsCircuitOff("3"), "computer with a different number stays locked");
        Check(editor.PlayerAgent.Position.X > 13 * 32, "agent passes the switched-off laser");
        editor.StopPlay();
    }

    // Touching an active laser beam costs a life at once, not after a delay.
    private async Task LaserDamageCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.Laser, number: "1");
        editor.PaintCell(new Vector2I(5, 10));
        Check(editor.StartPlay(), "laser level starts");
        Input.ActionPress("move_right");
        await Frames(25);
        Input.ActionRelease("move_right");
        await Frames(5);
        Check(editor.Session.Lives == 2, "laser beam takes a life the moment it is touched");
        editor.StopPlay();
    }

    // Circuit numbers go up to 8: a USB key 7 hacks computer 7 and the
    // laser 7 beyond it goes dark, while circuit 8 stays on.
    private async Task CircuitSevenCheck()
    {
        Check(ObjectDefs.Numbers.Length == 8 && ObjectDefs.Numbers.Contains("7") && ObjectDefs.Numbers.Contains("8"),
            "the editor offers circuit numbers 1 to 8");
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.UsbKey, number: "7");
        editor.PaintCell(new Vector2I(5, 10));
        editor.SetBrushObject(ObjectDefs.Computer, number: "7");
        editor.PaintCell(new Vector2I(8, 10));
        editor.SetBrushObject(ObjectDefs.Laser, number: "7");
        editor.PaintCell(new Vector2I(12, 10));
        Check(editor.StartPlay(), "circuit 7 level starts");
        Input.ActionPress("move_right");
        await Frames(120);
        Input.ActionRelease("move_right");
        Check(editor.Session.IsCircuitOff("7"), "usb key 7 hacks computer 7");
        Check(!editor.Session.IsCircuitOff("8"), "circuit 8 stays on");
        Check(editor.Session.Lives == 3 && editor.PlayerAgent.Position.X > 13 * 32,
            "agent passes the switched-off laser 7 unharmed");
        editor.StopPlay();
    }

    // A security door is solid while its circuit is on, and opens for good
    // when the circuit goes off by hacking its computer or shooting a switch.
    private async Task SecurityDoorCheck()
    {
        var editor = await FreshEditor();
        PaintDoorColumn(editor, 8, "5");
        Check(editor.StartPlay(), "security door level starts");
        Input.ActionPress("move_right");
        await Frames(100);
        Input.ActionRelease("move_right");
        Check(editor.PlayerAgent.Position.X < 8 * 32, "a closed security door blocks the agent");
        Check(!OpenDoor(editor), "the security door stays closed while its circuit is on");
        editor.StopPlay();

        editor = await FreshEditor();
        PaintDoorColumn(editor, 8, "5");
        editor.SetBrushObject(ObjectDefs.UsbKey, number: "5");
        editor.PaintCell(new Vector2I(3, 10));
        editor.SetBrushObject(ObjectDefs.Computer, number: "5");
        editor.PaintCell(new Vector2I(5, 10));
        Check(editor.StartPlay(), "security door hack level starts");
        Input.ActionPress("move_right");
        for (int i = 0; i < 300 && !OpenDoor(editor); i++)
            await Frames(1);
        Check(OpenDoor(editor), "hacking its computer opens the security door");
        await CheckDoorSlide(editor.Session.GetChildren().OfType<SlidingDoor>().ToList(), "security door");
        await Frames(60);
        Input.ActionRelease("move_right");
        Check(editor.PlayerAgent.Position.X > 10 * 32, "the agent walks through the opened security door");
        editor.StopPlay();

        editor = await FreshEditor();
        PaintDoorColumn(editor, 8, "6");
        editor.SetBrushObject(ObjectDefs.Switch, number: "6");
        editor.PaintCell(new Vector2I(4, 10));
        Check(editor.StartPlay(), "security door switch level starts");
        Input.ActionPress("shoot");
        await Frames(2);
        Input.ActionRelease("shoot");
        Input.ActionPress("move_right");
        await Frames(100);
        Input.ActionRelease("move_right");
        Check(OpenDoor(editor), "shooting a switch of its number opens the security door");
        Check(editor.PlayerAgent.Position.X > 10 * 32, "the agent walks through after the switch");
        editor.StopPlay();

        // The agent can walk into a switch's cell; a shot from there still hits it.
        editor = await FreshEditor();
        PaintDoorColumn(editor, 8, "7");
        editor.SetBrushObject(ObjectDefs.Switch, number: "7");
        editor.PaintCell(new Vector2I(3, 10));
        Check(editor.StartPlay(), "switch under the agent level starts");
        float switchX = editor.Session.Switches[0].Position.X;
        Input.ActionPress("move_right");
        for (int i = 0; i < 60 && editor.PlayerAgent.Position.X < switchX; i++)
            await Frames(1);
        Input.ActionRelease("move_right");
        await Frames(2);
        Check(Mathf.Abs(editor.PlayerAgent.Position.X - switchX) < 12, "the agent stands on the switch");
        Input.ActionPress("shoot");
        await Frames(2);
        Input.ActionRelease("shoot");
        await Frames(10);
        Check(OpenDoor(editor), "shooting while standing on a switch opens its security door");
        editor.StopPlay();

        // Only a shot flips a switch: standing on one, the activate key does
        // nothing and no prompt offers it.
        editor = await FreshEditor();
        PaintDoorColumn(editor, 8, "8");
        editor.SetBrushObject(ObjectDefs.Switch, number: "8");
        editor.PaintCell(new Vector2I(3, 10));
        Check(editor.StartPlay(), "activate switch level starts");
        await Frames(2);
        Check(ActivatePrompt(editor) == null, "no activate prompt away from the switch");
        switchX = editor.Session.Switches[0].Position.X;
        Input.ActionPress("move_right");
        for (int i = 0; i < 60 && editor.PlayerAgent.Position.X < switchX; i++)
            await Frames(1);
        Input.ActionRelease("move_right");
        await Frames(2);
        Check(Mathf.Abs(editor.PlayerAgent.Position.X - switchX) < 12, "the agent stands on the switch");
        Check(ActivatePrompt(editor) == null, $"no activate prompt on a switch: {ActivatePrompt(editor)}");
        Input.ActionPress("activate");
        await Frames(2);
        Input.ActionRelease("activate");
        await Frames(10);
        Check(!OpenDoor(editor), "the activate key on a switch leaves its security door shut");
        editor.StopPlay();
    }

    private static string ActivatePrompt(Editor editor) =>
        editor.Session.GetChildren().OfType<CanvasLayer>()
            .SelectMany(c => c.GetChildren().OfType<Label>())
            .FirstOrDefault(l => l.Visible && l.Text.StartsWith("Press "))?.Text;

    // An opened door sinks into the floor over a few frames instead of vanishing,
    // the agent is not blocked while it does, and once down only its solid top
    // lip shows, with no see-through ghost of the door left behind.
    private async Task CheckDoorSlide(IReadOnlyCollection<SlidingDoor> doors, string kind)
    {
        Check(doors.Count > 0 && doors.All(d => d.IsOpen), $"the {kind} opened");
        await Frames(5);
        Check(doors.All(d => d.Drop > 0 && d.Drop < SlidingDoor.MaxDrop), $"the {kind} is sliding into the floor");
        Check(doors.All(d => d.GetChildren().OfType<CollisionShape2D>().All(c => c.Disabled)), $"the {kind} no longer blocks mid-slide");
        await Frames(40);
        Check(doors.All(d => d.Drop == SlidingDoor.MaxDrop), $"the {kind} is fully down");
        Check(doors.All(d => d.GetChildren().OfType<Sprite2D>().ToList() is [var lip]
                && lip.Visible && lip.Modulate.A == 1f && lip.RegionRect.Size.Y == SlidingDoor.OpenLip),
            $"the open {kind} shows only its solid top lip");
    }

    // Four tiles tall so the agent cannot jump over it.
    private static void PaintDoorColumn(Editor editor, int x, string number)
    {
        editor.SetBrushObject(ObjectDefs.SecurityDoor, number: number);
        for (int y = 7; y <= 10; y++)
            editor.PaintCell(new Vector2I(x, y));
    }

    private static bool OpenDoor(Editor editor)
    {
        var doors = editor.Session.GetChildren().OfType<SecurityDoor>().ToList();
        return doors.Count > 0 && doors.All(d => d.IsOpen);
    }

    // Electric floor regions marked 1 and 2 plus an unmarked one: hacking
    // circuit 1 turns off only region 1.
    private async Task FloorCircuitCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushTile(TileDefs.ElectricFloor);
        foreach (int x in new[] { 2, 3, 6, 7, 10, 11 })
            editor.PaintCell(new Vector2I(x, 11));
        editor.SetBrushObject(ObjectDefs.FloorCircuit, number: "1");
        editor.PaintCell(new Vector2I(3, 11));
        editor.SetBrushObject(ObjectDefs.FloorCircuit, number: "2");
        editor.PaintCell(new Vector2I(6, 11));
        editor.SetBrushObject(ObjectDefs.UsbKey, number: "1");
        editor.PaintCell(new Vector2I(3, 10));
        editor.SetBrushObject(ObjectDefs.Computer, number: "1");
        editor.PaintCell(new Vector2I(4, 10));
        Check(editor.StartPlay(), "floor circuit level starts");
        var session = editor.Session;
        Input.ActionPress("move_right");
        await Frames(35);
        Input.ActionRelease("move_right");
        Check(session.IsCircuitOff("1") && !session.IsCircuitOff("2"), "hacking computer 1 turns off only circuit 1");

        await StandOn(editor, 2);
        Check(session.Lives == 3, "the electric floor region marked 1 is off after the hack");
        await StandOn(editor, 6);
        Check(session.Lives == 2, "the electric floor region marked 2 still hurts");
        await Frames(100);   // the death sequence finishes and the agent respawns
        await StandOn(editor, 10);
        Check(session.Lives == 1, "an unmarked electric floor region still hurts");
        editor.StopPlay();
    }

    private async Task StandOn(Editor editor, int cellX)
    {
        editor.PlayerAgent.Position = new Vector2(cellX * 32 + 16, 10 * 32 + 16);
        await Frames(200);
    }

    // Shooting a switch turns off its circuit, which also disables
    // electric floors before they can hurt the agent.
    private async Task SwitchCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushTile(TileDefs.ElectricFloor);
        editor.PaintCell(new Vector2I(2, 11));
        editor.SetBrushObject(ObjectDefs.FloorCircuit, number: "1");
        editor.PaintCell(new Vector2I(2, 11));
        editor.SetBrushObject(ObjectDefs.Switch, number: "1");
        editor.PaintCell(new Vector2I(8, 10));
        Check(editor.StartPlay(), "switch level starts");
        Input.ActionPress("shoot");
        await Frames(2);
        Input.ActionRelease("shoot");
        await Frames(240);
        Check(editor.Session.IsCircuitOff("1"), "shooting the switch turns off its circuit");
        Check(editor.Session.Lives == 3, "switched-off circuit disables electric floors");
        editor.StopPlay();
    }

    // A security camera that sees the agent raises the alarm.
    private async Task CameraAlarmCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.Camera);
        editor.PaintCell(new Vector2I(8, 8));
        Check(editor.StartPlay(), "camera level starts");
        await Frames(5);
        Check(editor.Session.AlarmActive, "security camera raises the alarm");
        editor.StopPlay();
    }

    // A camera faces right unless the brush says left; the facing survives
    // placing, picking, copy/paste, play, and the alarm blink.
    private async Task CameraFacingCheck()
    {
        var editor = await FreshEditor();
        var right = new Vector2I(8, 8);
        var left = new Vector2I(12, 8);
        editor.SetBrushObject(ObjectDefs.Camera);
        Check(editor.BrushFacing == "right" && !editor.GhostFlipped, "a camera brush faces right by default");
        editor.PaintCell(right);
        Check(editor.Level.Objects.Find(o => o.X == right.X && o.Y == right.Y).Properties["facing"] == "right",
            "a right-facing camera stores facing right");
        Check(!editor.MarkerFlipped(right), "the editor marker of a right-facing camera is not flipped");

        editor._UnhandledInput(KeyEvent(Key.Bracketright));
        Check(editor.BrushFacing == "left" && editor.GhostFlipped, "] turns the camera brush to face left");
        editor.PaintCell(left);
        Check(editor.Level.Objects.Find(o => o.X == left.X && o.Y == left.Y).Properties["facing"] == "left",
            "a left-facing camera stores facing left");
        Check(editor.MarkerFlipped(left) && !editor.MarkerFlipped(right), "only the left-facing camera marker is flipped");

        editor.SetBrushTile(TileDefs.RedBrick);
        editor.Pick(left);
        Check(editor.Brush.ObjectType == ObjectDefs.Camera && editor.BrushFacing == "left", "the picker copies the facing into the brush");
        editor.PaintCell(right);
        Check(editor.MarkerFlipped(right), "repainting a camera with the other facing replaces it");

        editor.SetBrushObject(ObjectDefs.Camera, facing: "right");
        editor.PaintCell(right);
        editor.SelectArea(left, left);
        editor.CopySelection();
        editor.PasteAt(new Vector2I(20, 8));
        Check(editor.MarkerFlipped(new Vector2I(20, 8)), "pasting a selection carries the camera facing");

        Check(editor.StartPlay(), "camera facing level starts");
        await Frames(5);
        Check(editor.Session.AlarmActive, "the alarm is raised so the sprite blinks");
        var cameras = editor.Session.GetChildren().OfType<SecurityCamera>().OrderBy(c => c.Position.X).ToArray();
        Check(cameras.Length == 3 && !cameras[0].Sprite.FlipH && cameras[1].Sprite.FlipH && cameras[2].Sprite.FlipH,
            "in play the left-facing camera sprites are flipped, alarm blink included");
        editor.StopPlay();
    }

    // One destructible wall slot paints every brick color; the color comes
    // back through the picker, keeps the help line, and every color breaks.
    private async Task WallColorCheck()
    {
        var editor = await FreshEditor();
        var stone = new Vector2I(5, 9);
        var red = new Vector2I(7, 9);
        editor.SetBrushTile(TileDefs.DestructibleWall);
        Check(editor.BrushTile == TileDefs.DestructibleWall, "the destructible wall brush starts as plain clay");
        editor._UnhandledInput(KeyEvent(Key.Bracketright));
        Check(editor.Brush.TileId == TileDefs.DestructibleWall && editor.BrushTile == TileDefs.DestructibleStone,
            "] picks the next wall color on the same palette slot");
        editor.PaintCell(stone);
        Check(editor.Level.GetTile(stone.X, stone.Y) == TileDefs.DestructibleStone, "the brush paints the chosen wall color");

        editor.SetBrushTile(TileDefs.RedBrick);
        editor.Pick(stone);
        Check(editor.Brush.TileId == TileDefs.DestructibleWall && editor.BrushTile == TileDefs.DestructibleStone,
            "the picker selects the wall slot with the picked color");

        editor.SetBrushTile(TileDefs.DestructibleRed);
        editor.PaintCell(red);
        var level = new LevelData();
        level.SetTile(0, 0, TileDefs.DestructibleMoss);
        Check(HelpOverlay.EntriesFor(level).Any(e => e.Name == TileDefs.Get(TileDefs.DestructibleWall).Name),
            "a colored destructible wall gets the destructible wall help line");

        Check(editor.StartPlay(), "wall color level starts");
        await Frames(2);
        editor.Session.Explode(new Vector2(6 * 32 + 16, 9 * 32 + 16));
        await Frames(2);
        Check(editor.Session.TileAt(stone.X, stone.Y) == TileDefs.Empty && editor.Session.TileAt(red.X, red.Y) == TileDefs.Empty,
            "explosions break colored destructible walls");
        var debris = editor.Session.GetChildren().OfType<WallDebris>().ToArray();
        Check(debris.Length == 2 && debris.Select(d => d.TileId).OrderBy(id => id)
                .SequenceEqual(new[] { TileDefs.DestructibleRed, TileDefs.DestructibleStone }.OrderBy(id => id)),
            "each broken wall throws debris made from its own tile");
        await Frames(Mathf.CeilToInt(debris[0].Lifetime * 60) + 10);
        Check(!editor.Session.GetChildren().OfType<WallDebris>().Any(), "wall debris clears itself away");
        editor.StopPlay();
    }

    // With the briefcase disguise active, the camera does not see the agent.
    private async Task DisguiseCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.Briefcase);
        editor.PaintCell(new Vector2I(3, 10));
        editor.SetBrushObject(ObjectDefs.Camera);
        editor.PaintCell(new Vector2I(14, 6));
        Check(editor.StartPlay(), "disguise level starts");
        Input.ActionPress("move_right");
        await Frames(20);
        Input.ActionPress("activate");
        await Frames(2);
        Input.ActionRelease("activate");
        await Frames(38);
        Input.ActionRelease("move_right");
        await Frames(10);
        Check(editor.Session.DisguiseActive, "activate turns on the briefcase disguise");
        Check(!editor.Session.AlarmActive, "disguised agent is not detected by the camera");
        editor.StopPlay();
    }

    // A disguised agent is not spotted: the soldier across a pit holds fire
    // for the whole 30s, then shoots once the disguise ends.
    private async Task EnemyDisguiseCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushEraser();
        foreach (int x in new[] { 5, 6, 10, 11, 12, 13, 14, 15 })
            editor.PaintCell(new Vector2I(x, 11));
        editor.SetBrushObject(ObjectDefs.Briefcase);
        editor.PaintCell(new Vector2I(3, 10));
        editor.SetBrushObject(EnemyDefs.Soldier);
        editor.PaintCell(new Vector2I(8, 10));
        Check(editor.StartPlay(), "enemy disguise level starts");
        var session = editor.Session;
        Input.ActionPress("move_right");
        await Frames(12);
        Input.ActionPress("activate");
        await Frames(2);
        Input.ActionRelease("activate");
        Input.ActionRelease("move_right");
        Check(session.DisguiseActive, "disguise is active before the soldier can fire");

        bool fired = false;
        while (session.DisguiseActive && !fired)
        {
            await Frames(1);
            // The frame the disguise runs out the soldier may fire at once; only
            // a shot while it is still on counts.
            fired = session.DisguiseActive && session.GetChildren().OfType<EnemyBullet>().Any();
        }
        Check(!fired && session.Lives == 3, "a soldier with the agent in its row does not fire while disguised");

        for (int i = 0; i < 600 && !fired; i++)
        {
            await Frames(1);
            fired = session.GetChildren().OfType<EnemyBullet>().Any();
        }
        Check(fired, "the soldier fires once the disguise ends");
        editor.StopPlay();
    }

    // Holding the help key in an editor playtest shows the help screen and
    // freezes the level; releasing it resumes play.
    private async Task PlaytestHelpCheck()
    {
        var editor = await FreshEditor();
        Check(editor.StartPlay(), "playtest help level starts");
        await Frames(10);
        Input.ActionPress(HelpOverlay.Action);
        Input.ActionPress("move_right");
        await Frames(5);
        float frozenX = editor.PlayerAgent.Position.X;
        await Frames(20);
        Check(editor.GetChildren().OfType<HelpOverlay>().Any(), "holding help in a playtest shows the help screen");
        Check(editor.PlayerAgent.Position.X == frozenX, "the level stays frozen under the help screen");
        Input.ActionRelease(HelpOverlay.Action);
        await Frames(20);
        Input.ActionRelease("move_right");
        Check(!editor.GetChildren().OfType<HelpOverlay>().Any(), "releasing help closes the help screen");
        Check(editor.PlayerAgent.Position.X > frozenX, "play resumes after the help screen closes");
        editor.StopPlay();
    }

    // Holding look_down slides the view about three tiles below the agent and
    // keeps it there; releasing it brings the view back.
    private async Task LookDownCheck()
    {
        var editor = await FreshEditor();
        var level = LevelData.NewDefault(40, 40);
        for (int x = 0; x < level.Width; x++)
            level.SetTile(x, 20, TileDefs.All[0].Id);
        level.Objects.First(o => o.Type == ObjectDefs.AgentStart).Y = 19;
        editor.SetLevel(level);
        Check(editor.StartPlay(), "look-down level starts");
        await Frames(20);
        var camera = editor.GetViewport().GetCamera2D();
        float CenterY() { camera.ForceUpdateScroll(); return camera.GetScreenCenterPosition().Y; }
        float restY = CenterY();
        Input.ActionPress("look_down");
        await Frames(40);
        float lookY = CenterY();
        await Frames(30);
        Check(Mathf.Abs(lookY - restY - 3 * 32) < 1 && Mathf.Abs(CenterY() - lookY) < 1,
            $"holding look down moves the view 3 tiles down and holds it ({restY} -> {lookY})");
        Input.ActionRelease("look_down");
        await Frames(40);
        Check(Mathf.Abs(CenterY() - restY) < 1, "releasing look down brings the view back");
        editor.StopPlay();
    }

    // Hiding in a closet protects from patrolling enemies.
    private async Task ClosetCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.Closet);
        editor.PaintCell(new Vector2I(6, 10));
        editor.SetBrushObject(EnemyDefs.Fighter);
        editor.PaintCell(new Vector2I(12, 10));
        Check(editor.StartPlay(), "closet level starts");
        Input.ActionPress("move_right");
        await Frames(30);
        Input.ActionRelease("move_right");
        Check(ActivatePrompt(editor) == "Press C to hide", $"prompt next to the closet: {ActivatePrompt(editor)}");
        Input.ActionPress("activate");
        await Frames(2);
        Input.ActionRelease("activate");
        Check(editor.PlayerAgent.IsHiding, "agent hides in the closet");
        await Frames(1);
        Check(ActivatePrompt(editor) == "Press C to leave the closet", $"prompt while hidden: {ActivatePrompt(editor)}");
        await Frames(150);
        Check(editor.Session.Lives == 3, "hidden agent is not hurt by patrolling enemies");
        Input.ActionPress("activate");
        await Frames(2);
        Input.ActionRelease("activate");
        Check(!editor.PlayerAgent.IsHiding, "activate leaves the closet");
        editor.StopPlay();
    }

    // The agent pushes a wooden or steel barrel by walking into it.
    private async Task BarrelCheck(string type)
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(type);
        editor.PaintCell(new Vector2I(6, 10));
        Check(editor.StartPlay(), $"{type} level starts");
        Input.ActionPress("move_right");
        await Frames(60);
        var pushFrames = new HashSet<string>();
        for (int i = 0; i < 40; i++)
        {
            await Frames(1);
            pushFrames.Add(editor.PlayerAgent.AnimationFrame);
        }
        Input.ActionRelease("move_right");
        Check(pushFrames.All(f => f?.StartsWith("agent_push") == true) && pushFrames.Count > 3,
            $"pushing a {type} plays the push cycle without flickering");
        var barrel = editor.Session.GetChildren().OfType<Barrel>().FirstOrDefault();
        Check(barrel?.GetChildren().OfType<Sprite2D>().Single().Texture == Sprites.Get(type), $"{type} shows its own sprite");
        Check(barrel != null && barrel.Position.X > 6 * 32 + 60, $"agent pushes the {type}");
        Check(editor.PlayerAgent.Position.X > 6 * 32, $"agent advances behind the {type}");
        editor.StopPlay();
    }

    // Collectibles convert to score and coins instantly; completeness
    // counts placed collectibles.
    private async Task CollectibleCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(CollectibleDefs.Diamond);
        editor.PaintCell(new Vector2I(4, 10));
        editor.SetBrushObject(CollectibleDefs.BusinessCard);
        editor.PaintCell(new Vector2I(12, 10));
        Check(editor.StartPlay(), "collectible level starts");
        var scoreLabel = editor.Session.FindChildren("*", "", true, false).OfType<RollingLabel>().Last();
        var shownScores = new HashSet<string>();
        bool popupSeen = false;
        Input.ActionPress("move_right");
        for (int i = 0; i < 40; i++)
        {
            await Frames(1);
            shownScores.Add(scoreLabel.Text);
            popupSeen |= editor.Session.GetChildren().OfType<FloatingNumber>().Any(p => p.Amount == 25000);
        }
        Input.ActionRelease("move_right");
        Check(popupSeen, "the diamond's +25000 rises off the agent");
        for (int i = 0; i < 80; i++)
        {
            await Frames(1);
            shownScores.Add(scoreLabel.Text);
        }
        Check(shownScores.Any(s => int.TryParse(s, out int n) && n > 0 && n < 25000),
            "the HUD score ticks up through the values in between");
        Check(scoreLabel.Text == "25000", "the HUD score settles on the new total");
        Check(!editor.Session.GetChildren().OfType<FloatingNumber>().Any(), "the score popup goes away");
        Check(editor.Session.Score == 25000, "diamond is worth 25k score");
        Check(editor.Session.CoinsCollected == 7, "diamond is worth 7 coins");
        Check(editor.Session.CompletenessPercent == 50, "completeness counts collected collectibles");
        editor.StopPlay();
    }

    // Collecting all five AGENT letters is worth 40k score and 5 coins.
    private async Task LettersBonusCheck()
    {
        var editor = await FreshEditor();
        for (int i = 0; i < CollectibleDefs.LetterTypes.Length; i++)
        {
            editor.SetBrushObject(CollectibleDefs.LetterTypes[i]);
            editor.PaintCell(new Vector2I(4 + i, 10));
        }
        Check(editor.StartPlay(), "letters level starts");
        Input.ActionPress("move_right");
        await Frames(70);
        Input.ActionRelease("move_right");
        Check(editor.Session.Score == 40000, "full AGENT set is worth 40k score");
        Check(editor.Session.CoinsCollected == 5, "full AGENT set is worth 5 coins");
        editor.StopPlay();
    }

    // Kills, accuracy and the Hacker tag.
    private async Task StatsCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushEraser();
        editor.PaintCell(new Vector2I(6, 11));
        editor.PaintCell(new Vector2I(7, 11));
        editor.SetBrushObject(EnemyDefs.Fighter);
        editor.PaintCell(new Vector2I(11, 10));
        Check(editor.StartPlay(), "stats level starts");
        Check(editor.Session.IsHackerRun, "run starts as a hacker run");
        for (int shot = 0; shot < 2; shot++)
        {
            Input.ActionPress("shoot");
            await Frames(2);
            Input.ActionRelease("shoot");
            await Frames(10);
        }
        await Frames(40);
        Check(editor.Session.Kills == 1, "killing the fighter counts one kill");
        Check(editor.Session.AccuracyPercent == 100, "two shots, two hits: accuracy 100");
        Input.ActionPress("shoot");
        await Frames(2);
        Input.ActionRelease("shoot");
        await Frames(5);
        Check(editor.Session.AccuracyPercent == 67, "a missed shot drops accuracy to 67");
        Check(!editor.Session.IsHackerRun, "a kill removes the hacker tag");
        editor.StopPlay();
    }

    // The tombstone left by a dead soldier is a 1k collectible, but does
    // not count toward completeness.
    private async Task TombstoneScoreCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(EnemyDefs.Soldier);
        editor.PaintCell(new Vector2I(6, 10));
        Check(editor.StartPlay(), "tombstone level starts");
        Input.ActionPress("shoot");
        await Frames(2);
        Input.ActionRelease("shoot");
        await Frames(20);
        Input.ActionPress("move_right");
        await Frames(45);
        Input.ActionRelease("move_right");
        Check(editor.Session.Score == 1000, "picked-up tombstone is worth 1k score");
        Check(editor.Session.CompletenessPercent == 100, "tombstones do not count toward completeness");
        editor.StopPlay();
    }

    // Difficulty: agent lives 4/3/2, enemies -1/+1 life, and on low the
    // super soldier dies outright instead of degrading.
    private async Task DifficultyCheck()
    {
        Profile.Current.Difficulty = "low";
        var editor = await FreshEditor();
        editor.SetBrushEraser();
        editor.PaintCell(new Vector2I(6, 11));
        editor.PaintCell(new Vector2I(7, 11));
        editor.SetBrushObject(EnemyDefs.Fighter);
        editor.PaintCell(new Vector2I(11, 10));
        editor.SetBrushObject(EnemyDefs.SuperSoldier);
        editor.PaintCell(new Vector2I(13, 10));
        Check(editor.StartPlay(), "low difficulty level starts");
        Check(editor.Session.Lives == 4, "low difficulty grants an extra life");
        Input.ActionPress("shoot");
        await Frames(2);
        Input.ActionRelease("shoot");
        await Frames(40);
        Check(editor.Session.Enemies.Count == 1, "fighter dies in one hit on low difficulty");
        Input.ActionPress("shoot");
        await Frames(2);
        Input.ActionRelease("shoot");
        await Frames(100);
        Check(editor.Session.Enemies.Count == 0, "super soldier dies outright on low difficulty");
        editor.StopPlay();

        Profile.Current.Difficulty = "hard";
        editor = await FreshEditor();
        Check(editor.StartPlay(), "hard difficulty level starts");
        Check(editor.Session.Lives == 2, "hard difficulty removes a life");
        editor.StopPlay();
        Profile.Current.Difficulty = "medium";
    }

    // The Tab help screen lists what the level holds: the default level
    // shows only its exit door, and a level holding every palette entry
    // explains each one except scenery, with collectibles folded into one.
    private void HelpChecks()
    {
        string Names(LevelData level) => string.Join(", ", HelpOverlay.EntriesFor(level).Select(e => e.Name));

        var plain = LevelData.NewDefault();
        Check(Names(plain) == "Exit door", $"help for the default level lists the exit door only ({Names(plain)})");

        var full = LevelData.NewDefault();
        var all = EditorPalette.Categories.SelectMany(c => c.Entries).ToArray();
        for (int i = 0; i < all.Length; i++)
            if (all[i].IsTile)
                full.SetTile(i, 0, all[i].TileId);
            else
                full.Objects.Add(new LevelObject { Type = all[i].ObjectType, X = i, Y = 1 });
        var scenery = new[] { "Walls", "Decorations", "Collectibles" };
        var expected = EditorPalette.Categories
            .Where(c => !scenery.Contains(c.Name))
            .SelectMany(c => c.Entries)
            .Where(e => e.ObjectType is not (ObjectDefs.AgentStart or ObjectDefs.FloorCircuit))
            .Select(e => e.Name)
            .Prepend(TileDefs.Get(TileDefs.DestructibleWall).Name)
            .Append("Collectibles")
            .OrderBy(n => n);
        var listed = HelpOverlay.EntriesFor(full).Select(e => e.Name).OrderBy(n => n);
        Check(listed.SequenceEqual(expected),
            $"help explains every gameplay palette entry (missing: {string.Join(", ", expected.Except(listed))})");
    }

    // Every sound renders, playing is safe headless, and the toggles drive the
    // buses.
    private void SfxChecks()
    {
        var empty = Enum.GetValues<Sound>().Where(sound => Sfx.Synthesize(sound).Data.Length == 0).ToList();
        Check(empty.Count == 0, "every sound synthesizes a non-empty stream");

        Sfx.Play(Sound.UiClick);
        Sfx.PlayAt(Sound.Explosion, new Vector2(100, 100));
        Check(true, "playing sounds headless does not throw");

        var saved = Profile.Current;
        Profile.Current = new Profile();
        int sfx = AudioServer.GetBusIndex(Sfx.SfxBus);
        int music = AudioServer.GetBusIndex(Sfx.MusicBus);
        Check(sfx >= 0 && music >= 0, "the SFX and Music buses exist");
        Sfx.ApplySettings();
        Check(!AudioServer.IsBusMute(sfx) && !AudioServer.IsBusMute(music), "default settings leave both buses audible");
        Profile.Current.SoundOn = false;
        Sfx.ApplySettings();
        Check(AudioServer.IsBusMute(sfx) && !AudioServer.IsBusMute(music), "Sound off mutes only the SFX bus");
        Profile.Current.SoundOn = true;
        Profile.Current.MusicOn = false;
        Sfx.ApplySettings();
        Check(!AudioServer.IsBusMute(sfx) && AudioServer.IsBusMute(music), "Music off mutes only the Music bus");
        Profile.Current.MusicOn = true;
        Profile.Current.SoundVolume = 0;
        Sfx.ApplySettings();
        Check(AudioServer.IsBusMute(sfx), "volume 0 mutes the bus");
        Profile.Current = saved;
        Sfx.ApplySettings();

        // A profile saved before the toggles existed loads with both on.
        using (var file = FileAccess.Open("user://profile_old.json", FileAccess.ModeFlags.Write))
            file.StoreString("{\"coins\": 7, \"musicVolume\": 3}");
        var old = Profile.Load("profile_old.json");
        DirAccess.RemoveAbsolute("user://profile_old.json");
        Check(old.Coins == 7 && old.SoundOn && old.MusicOn, "an old profile without the toggles loads with sound and music on");
    }

    // Profile persistence and the shop economy (pure logic, no UI).
    private void ProfileShopChecks()
    {
        var profile = new Profile
        {
            Coins = 42,
            LevelIndex = 3,
            Difficulty = "hard",
            MusicVolume = 7,
            HasHackingTool = true,
        };
        profile.Save("profile_test.json");
        var loaded = Profile.Load("profile_test.json");
        Check(loaded.Coins == 42 && loaded.LevelIndex == 3 && loaded.Difficulty == "hard"
            && loaded.MusicVolume == 7 && loaded.HasHackingTool, "profile round trip preserves values");

        var saved = Profile.Current;
        Profile.Current = new Profile();   // 100 coins, medium
        var run = RunState.NewRun();
        Check(run.Lives == 3 && run.Ammo == 10, "new run starts with default lives and ammo");
        Check(Shop.TryBuy(Shop.ExtraLife, run) && run.Lives == 4 && Profile.Current.Coins == 50,
            "shop sells an extra life for 50 coins");
        Check(Shop.TryBuy(Shop.HackingTool, run) && Profile.Current.HasHackingTool && Profile.Current.Coins == 20,
            "shop sells the hacking tool for 30 coins");
        Check(!Shop.TryBuy(Shop.ExtraLife, run) && Profile.Current.Coins == 20,
            "shop rejects purchases the player cannot afford");
        Check(Shop.TryBuy(Shop.AmmoPack, run) && run.Ammo == 20 && Profile.Current.Coins == 15,
            "shop sells 10 ammo for 5 coins");
        Profile.Current.Coins = 20;   // top up: the briefcase costs 20
        Check(Shop.TryBuy(Shop.BriefcaseItem, run) && run.Briefcases == 1 && Profile.Current.Coins == 0,
            "shop sells briefcases");
        Profile.Current = saved;
    }

    // The purchased hacking tool hacks computers without a USB key.
    private async Task HackingToolCheck()
    {
        Profile.Current.HasHackingTool = true;
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.Computer, number: "1");
        editor.PaintCell(new Vector2I(8, 10));
        Check(editor.StartPlay(), "hacking tool level starts");
        Input.ActionPress("move_right");
        await Frames(60);
        Input.ActionRelease("move_right");
        Check(editor.Session.IsCircuitOff("1"), "hacking tool hacks computers without a usb key");
        editor.StopPlay();
        Profile.Current.HasHackingTool = false;
    }

    // Entering a teleport capsule moves the agent to the paired capsule.
    private async Task TeleportCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.Teleport, number: "1");
        editor.PaintCell(new Vector2I(5, 10));
        editor.PaintCell(new Vector2I(15, 10));
        Check(editor.StartPlay(), "teleport level starts");
        Input.ActionPress("move_right");
        await Frames(40);
        Input.ActionRelease("move_right");
        Check(editor.PlayerAgent.Position.X > 14 * 32, "teleport moves the agent to the paired capsule");
        editor.StopPlay();
    }

    // Elevators move vertically and stay within their 4-tile range.
    private async Task ElevatorCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.Elevator);
        editor.PaintCell(new Vector2I(8, 8));
        Check(editor.StartPlay(), "elevator level starts");
        var platform = editor.Session.GetChildren().OfType<MovingPlatform>().FirstOrDefault();
        Check(platform != null, "elevator spawns in play mode");
        float y0 = platform.Position.Y;
        float minY = y0, maxY = y0;
        for (int i = 0; i < 240; i++)
        {
            await Frames(1);
            minY = Mathf.Min(minY, platform.Position.Y);
            maxY = Mathf.Max(maxY, platform.Position.Y);
        }
        Check(minY < y0 - 20, "elevator moves upward");
        Check(y0 - minY <= 134 && maxY - y0 <= 134, "elevator stays within its range");
        editor.StopPlay();

        // Touching elevators are one body, so they cannot drift apart.
        editor.PaintCell(new Vector2I(9, 8));
        editor.PaintCell(new Vector2I(9, 9));
        Check(editor.StartPlay(), "adjacent elevator level starts");
        var lifts = editor.Session.GetChildren().OfType<MovingPlatform>().ToList();
        Check(lifts.Count == 1 && lifts[0].Cells.Count == 3, "adjacent elevators form one platform");
        editor.StopPlay();

        // Platform paint is a brush color, so the editor and play draw the same sprite.
        editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.Door, "blue");
        editor.SetBrushObject(ObjectDefs.Elevator);
        Check(editor.BrushColor == "steel", "elevators start with the steel paint");
        editor._UnhandledInput(KeyEvent(Key.Bracketright));
        Check(editor.BrushColor == "hazard", "] steps the elevator paint");
        editor.PaintCell(new Vector2I(9, 9));
        Check(editor.Level.Objects.Exists(o => o.Type == ObjectDefs.Elevator && o.Properties.GetValueOrDefault("color") == "hazard"),
            "the elevator stores its paint");
        Check(Sprites.ForObject(ObjectDefs.Elevator, "hazard") == Sprites.Get("platform", "hazard"),
            "the editor marker uses the painted platform sprite");
        editor.SetBrushObject(ObjectDefs.Door);
        Check(editor.BrushColor == "blue", "the door brush keeps its own color");
        Check(editor.StartPlay(), "painted elevator level starts");
        var painted = editor.Session.GetChildren().OfType<MovingPlatform>().Single();
        Check(painted.GetChildren().OfType<Sprite2D>().Single().Texture == Sprites.Get("platform", "hazard"),
            "play mode draws the elevator in its paint");
        editor.StopPlay();
    }

    // The assault rifle fires a burst of three bullets from the shared pool.
    private async Task AssaultRifleCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.WeaponAssaultRifle);
        editor.PaintCell(new Vector2I(4, 10));
        Check(editor.StartPlay(), "assault rifle level starts");
        Input.ActionPress("move_right");
        await Frames(20);
        Input.ActionRelease("move_right");
        Check(editor.Session.Weapon == Weapon.AssaultRifle, "weapon pickup equips the assault rifle");
        Input.ActionPress("shoot");
        await Frames(2);
        Input.ActionRelease("shoot");
        Check(editor.Session.Ammo == 17, "assault rifle fires a burst of three bullets");
        editor.StopPlay();
    }

    // README: the crossbow does not trigger the alarm.
    private async Task CrossbowCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.WeaponCrossbow);
        editor.PaintCell(new Vector2I(4, 10));
        Check(editor.StartPlay(), "crossbow level starts");
        Input.ActionPress("move_right");
        await Frames(20);
        Input.ActionRelease("move_right");
        Input.ActionPress("shoot");
        await Frames(2);
        Input.ActionRelease("shoot");
        Check(editor.Session.Arrows == 19, "crossbow spends an arrow");
        Check(!editor.Session.AlarmActive, "crossbow does not trigger the alarm");
        Check(editor.PlayerAgent.AnimationFrame?.Contains("_crossbow_") == true, "the agent aims the crossbow it picked up");
        Check(editor.Session.GetChildren().OfType<Bullet>().Any(b => b.SpriteKey == "bolt"), "the crossbow shoots a bolt, not a bullet");
        editor.StopPlay();
    }

    // The chainsaw kills in melee range without ammo.
    private async Task ChainsawCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.WeaponChainsaw);
        editor.PaintCell(new Vector2I(3, 10));
        editor.SetBrushObject(EnemyDefs.Fighter);
        editor.PaintCell(new Vector2I(12, 10));
        Check(editor.StartPlay(), "chainsaw level starts");
        Input.ActionPress("move_right");
        await Frames(12);
        Input.ActionRelease("move_right");
        Check(editor.Session.Weapon == Weapon.Chainsaw, "weapon pickup equips the chainsaw");
        // Swinging as fast as the input allows. The chainsaw reaches 44px and
        // the fighter closes to 28px before it can touch the agent, so the
        // kill has to land inside that window - swinging on a slower cadence
        // trades the melee check for a test of what happens after a death.
        for (int i = 0; i < 200 && editor.Session.Enemies.Count > 0; i++)
        {
            Input.ActionPress("shoot");
            await Frames(1);
            Input.ActionRelease("shoot");
            await Frames(1);
        }
        Check(editor.Session.Enemies.Count == 0, "chainsaw kills the charging fighter in melee range");
        Check(editor.Session.Lives == 3, "the fighter dies before it lands a contact hit");
        Check(editor.Session.Ammo == 10, "chainsaw uses no ammo");
        editor.StopPlay();
    }

    // Placed dynamite explodes after its fuse and destroys destructible walls.
    private async Task DynamiteWeaponCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.WeaponDynamite);
        editor.PaintCell(new Vector2I(4, 10));
        editor.SetBrushTile(TileDefs.DestructibleWall);
        editor.PaintCell(new Vector2I(8, 10));
        Check(editor.StartPlay(), "dynamite level starts");
        Input.ActionPress("move_right");
        await Frames(32);
        Input.ActionRelease("move_right");
        Input.ActionPress("shoot");
        await Frames(2);
        Input.ActionRelease("shoot");
        Check(editor.Session.DynamiteSticks == 2, "placing dynamite spends a stick");
        Input.ActionPress("move_left");
        await Frames(40);
        Input.ActionRelease("move_left");
        await Frames(60);
        Check(editor.Session.TileAt(8, 10) == TileDefs.DestructibleWall, "dynamite waits out its fuse");
        await Frames(70);
        Check(editor.Session.TileAt(8, 10) == TileDefs.Empty, "dynamite destroys destructible walls after its 2.5s fuse");
        editor.StopPlay();
    }

    // A stick dropped from high up lands flush on the floor instead of
    // sinking into it after a fast last step.
    private async Task DynamiteLandingCheck()
    {
        var editor = await FreshEditor();
        Check(editor.StartPlay(), "dynamite landing level starts");
        int tile = TileSetBuilder.TileSize;
        float x = 20 * tile + tile / 2f;
        editor.Session.SpawnDynamite(new Vector2(x, tile));
        await Frames(60);
        var stick = editor.Session.GetChildren().OfType<Dynamite>().FirstOrDefault();
        Check(stick != null, "the dropped stick is still burning");
        int floorRow = Enumerable.Range(0, 12).First(y => TileDefs.Get(editor.Session.TileAt(20, y))?.Solid == true);
        Check(stick != null && Mathf.IsEqualApprox(stick.Position.Y + 8, floorRow * tile),
            "dropped dynamite rests on top of the floor tile");
        editor.StopPlay();
    }

    // The electric door field follows the 10s cycle.
    private async Task ElectricDoorCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.ElectricDoor);
        editor.PaintCell(new Vector2I(5, 10));
        Check(editor.StartPlay(), "electric door level starts");
        Input.ActionPress("move_right");
        await Frames(30);
        Input.ActionRelease("move_right");
        Check(editor.Session.Lives == 3, "electric door is safe between cycles");
        var agent = editor.PlayerAgent;
        agent.Position = new Vector2(5 * 32 + 16, 10 * 32 + 16);
        agent.Velocity = Vector2.Zero;
        editor.Session.Time = 8.5f;
        await Frames(5);
        Check(editor.Session.Lives == 2, "engaged electric door field takes a life");
        editor.StopPlay();
    }

    // The flame trap burns during seconds 4-6 of each cycle.
    private async Task FlameTrapCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.FlameTrap);
        editor.PaintCell(new Vector2I(5, 10));
        Check(editor.StartPlay(), "flame trap level starts");
        Input.ActionPress("move_right");
        await Frames(30);
        Input.ActionRelease("move_right");
        Check(editor.Session.Lives == 3, "flame trap is safe between bursts");
        var agent = editor.PlayerAgent;
        agent.Position = new Vector2(5 * 32 + 16, 10 * 32 + 16);
        agent.Velocity = Vector2.Zero;
        editor.Session.Time = 4.5f;
        await Frames(5);
        Check(editor.Session.Lives == 2, "flame burst takes a life");
        editor.StopPlay();
    }

    // The rocket launcher fires rockets that take a life.
    private async Task RocketLauncherCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(ObjectDefs.RocketLauncher);
        editor.PaintCell(new Vector2I(10, 10));
        Check(editor.StartPlay(), "rocket launcher level starts");
        await Frames(180);
        Check(editor.Session.Lives == 2, "rocket takes a life");
        editor.StopPlay();
    }

    // Plants are walk-through, crates are solid and block the agent.
    private async Task DecorationCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject("deco_plant");
        editor.PaintCell(new Vector2I(4, 10));
        editor.SetBrushObject("deco_crate_wood");
        editor.PaintCell(new Vector2I(6, 10));
        editor.SetBrushObject(ObjectDefs.Light);
        editor.PaintCell(new Vector2I(8, 8));
        Check(editor.StartPlay(), "decoration level starts");
        Input.ActionPress("move_right");
        await Frames(60);
        Input.ActionRelease("move_right");
        float x = editor.PlayerAgent.Position.X;
        Check(x > 6 * 32 + 32, $"plant and crate are walk-through (agent x {x})");
        var layer = editor.Session.GetNode<Node2D>("Decorations");
        Check(layer.GetChildCount() == 2 && layer.GetIndex() < editor.PlayerAgent.GetIndex() && layer.ZIndex == editor.PlayerAgent.ZIndex,
            "decorations draw behind the agent");
        Check(editor.Session.GetChildren().OfType<LevelLight>().Any(), "light spawns in play mode");
        editor.StopPlay();
    }

    // The menu and campaign scenes build their UI without errors.
    private async Task MenuSceneCheck()
    {
        var menu = GD.Load<PackedScene>("res://scenes/main_menu.tscn").Instantiate();
        AddChild(menu);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(IsInstanceValid(menu), "main menu scene builds");
        menu.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var game = GD.Load<PackedScene>("res://scenes/game.tscn").Instantiate();
        AddChild(game);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(IsInstanceValid(game), "game scene builds");
        await Frames(3);   // the containers settle their layout
        var panel = game.FindChildren("*", nameof(PanelContainer), true, false).OfType<PanelContainer>().FirstOrDefault();
        var screen = GetViewport().GetVisibleRect();
        var box = panel?.GetGlobalRect() ?? default;
        Check(panel != null && screen.Encloses(box), "the campaign panel fits on screen");
        Check(panel != null && box.GetCenter().DistanceTo(screen.GetCenter()) < 2, "the campaign panel is centered");
        game.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    // Scenarios are written against the legacy 12-row layout (floor at
    // row 11, agent start at (2, 10)); pass legacySize: false to keep the
    // 40x40 default level.
    // The agent animator picks a pose from the movement state alone, so the
    // whole state machine is reachable by driving the normal inputs.
    // Idle with no gun overlay: the breathing cycle and nothing else.
    private static bool IsIdleFrame(string frame) =>
        frame == "agent" || System.Text.RegularExpressions.Regex.IsMatch(frame ?? "", @"^agent_idle\d+$");

    private async Task AgentAnimationCheck()
    {
        var editor = await FreshEditor();
        Check(editor.StartPlay(), "animation level starts");
        var agent = editor.PlayerAgent;

        await Frames(4);
        Check(IsIdleFrame(agent.AnimationFrame), "standing still shows an idle frame");

        Input.ActionPress("move_right");
        await Frames(10);
        Check(agent.AnimationFrame?.StartsWith("agent_run") == true, "running shows the run cycle");
        string first = agent.AnimationFrame;
        await Frames(10);
        Check(agent.AnimationFrame != first, "the run cycle advances between frames");
        Input.ActionRelease("move_right");

        await Frames(4);
        Input.ActionPress("jump");
        await Frames(2);
        Input.ActionRelease("jump");
        Check(agent.AnimationFrame?.StartsWith("agent_jump") == true, "rising shows the jump pose");
        await Frames(30);
        Check(agent.AnimationFrame?.StartsWith("agent_fall") == true, "falling shows the fall pose");

        await Frames(40);   // land before shooting

        // Sample the pose frame by frame: the muzzle flash leads, the aim
        // pose holds, then the agent drops back to idle. Holding the button
        // fires once, so the whole sequence comes from a single shot.
        var seen = new string[30];
        Input.ActionPress("shoot");
        for (int i = 0; i < seen.Length; i++)
        {
            await Frames(1);
            seen[i] = agent.AnimationFrame;
        }
        Input.ActionRelease("shoot");
        int flash = System.Array.FindIndex(seen, f => f?.EndsWith("_fire") == true);
        int aim = System.Array.FindIndex(seen, f => f?.EndsWith("_aim") == true);
        Check(flash >= 0, "firing flashes the muzzle");
        Check(aim > flash, "the aim pose holds after the flash");
        Check(IsIdleFrame(seen[^1]), "the agent returns to idle after shooting");

        // An empty magazine must not play the shoot pose.
        while (editor.Session.Ammo > 0)
        {
            Input.ActionPress("shoot");
            await Frames(2);
            Input.ActionRelease("shoot");
            await Frames(2);
        }
        await Frames(30);   // let the last shoot pose expire
        Input.ActionPress("shoot");
        await Frames(1);
        Input.ActionRelease("shoot");
        Check(IsIdleFrame(agent.AnimationFrame), "shooting with no ammo stays idle");
        editor.StopPlay();
    }

    // In water the agent treads in place, crawls when it moves sideways and
    // dives head-down once it has been sinking for a moment, all from the
    // normal inputs in a pool around the start position.
    private async Task AgentSwimCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushTile(TileDefs.Water);
        for (int x = 1; x <= 14; x++)
            for (int y = 3; y <= 10; y++)
                editor.PaintCell(new Vector2I(x, y));
        Check(editor.StartPlay(), "swim level starts");
        var agent = editor.PlayerAgent;
        bool Shows(string clip) => agent.AnimationFrame?.StartsWith($"agent_{clip}") == true;

        await Frames(10);
        Check(Shows("tread"), "standing still in water treads water");

        Input.ActionPress("move_right");
        await Frames(20);
        Check(Shows("swim"), "moving sideways in water swims");
        string first = agent.AnimationFrame;
        await Frames(10);
        Check(agent.AnimationFrame != first, "the swim stroke advances between frames");
        Input.ActionRelease("move_right");

        // Tapping jump strokes upward; the gaps between taps are too short
        // to read as a dive.
        bool divedBetweenStrokes = false;
        for (int stroke = 0; stroke < 6; stroke++)
        {
            Input.ActionPress("jump");
            await Frames(1);
            Input.ActionRelease("jump");
            for (int i = 0; i < 12; i++)
            {
                await Frames(1);
                divedBetweenStrokes |= Shows("dive");
            }
        }
        Check(Shows("tread") && !divedBetweenStrokes, "stroking upward treads water");

        bool dived = false;
        for (int i = 0; i < 90 && !dived; i++)
        {
            await Frames(1);
            dived = Shows("dive");
        }
        Check(dived, "sinking without a stroke dives head-down");

        await Frames(90);
        Check(agent.IsOnFloor() && Shows("tread"), "back on the bottom the agent treads water");
        editor.StopPlay();
    }

    // Losing a life plays out as a sequence rather than an instant respawn:
    // the life goes on the hit, the agent crumples where it stood, the screen
    // effect covers the level, and the respawn happens behind it.
    private async Task AgentDeathCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushTile(TileDefs.Lava);
        editor.PaintCell(new Vector2I(6, 10));
        Check(editor.StartPlay(), "death level starts");
        var agent = editor.PlayerAgent;

        Input.ActionPress("move_right");
        await Frames(40);
        Input.ActionRelease("move_right");
        Check(editor.Session.Lives == 2, "the life goes the moment the hit lands");
        Check(agent.IsDead && editor.Session.AgentDown, "a lethal hit puts the agent down");
        Check(editor.Session.GetChildren().OfType<DeathEffect>().Any(),
            "the death effect covers the screen");

        float fellAt = agent.Position.X;
        await Frames(6);
        Check(Mathf.Abs(agent.Position.X - fellAt) < 1f, "the body stays where it fell");
        Check(agent.AnimationFrame?.StartsWith("agent_die") == true && agent.AnimationFrame != BodyPoses.Dead.Key,
            "the death sequence plays through its poses");

        await Frames(30);
        Check(agent.AnimationFrame == BodyPoses.Dead.Key, "the death sequence holds the final pose");
        editor.Session.AgentHit();
        Check(editor.Session.Lives == 2, "a downed agent cannot lose a second life");

        await Frames(60);
        Check(!agent.IsDead && agent.Position.X < 6 * 32, "the respawn follows the death sequence");
        Check(IsIdleFrame(agent.AnimationFrame), "the respawned agent is back on its feet");
        Check(!editor.Session.GetChildren().OfType<DeathEffect>().Any(),
            "the screen effect clears with the respawn");
        editor.StopPlay();
    }

    // Enemy poses are picked from the same state the behaviour already uses,
    // so spawning, patrolling, firing and powering down covers the table.
    private async Task EnemyAnimationCheck()
    {
        var editor = await FreshEditor();
        editor.SetBrushObject(EnemyDefs.Fighter);
        editor.PaintCell(new Vector2I(14, 10));
        Check(editor.StartPlay(), "enemy animation level starts");
        var fighter = editor.Session.Enemies[0];
        var walked = await SampleFrames(fighter, 60);
        Check(walked.Count(f => f?.StartsWith("fighter_walk") == true) >= 6,
            "a patrolling fighter cycles its walk frames");
        editor.StopPlay();

        // The turret's gun holds its plain frame while it swings, then flashes.
        editor = await FreshEditor();
        editor.SetBrushObject(EnemyDefs.Turret);
        editor.PaintCell(new Vector2I(8, 10));
        Check(editor.StartPlay(), "turret animation level starts");
        var fired = await SampleFrames(editor.Session.Enemies[0], 150);
        Check(fired.Contains(TurretGun.Idle), "an aiming turret holds its plain gun frame");
        Check(fired.Contains(TurretGun.Firing), "a firing turret shows the muzzle flash");
        editor.StopPlay();

        // The robot has a dedicated powered-down pose for its off window.
        editor = await FreshEditor();
        editor.SetBrushObject(EnemyDefs.Robot);
        editor.PaintCell(new Vector2I(8, 10));
        Check(editor.StartPlay(), "robot animation level starts");
        var robot = editor.Session.Enemies[0];
        await Frames(4);
        Check(robot.AnimationFrame != "robot_off", "an active robot is not in the off pose");
        editor.Session.Time = 9.0f;   // inside the off-window (9..10)
        await Frames(4);
        Check(robot.AnimationFrame == "robot_off", "a turned-off robot slumps");
        editor.StopPlay();

        // Degrading swaps the whole pose set, not just one texture.
        editor = await FreshEditor();
        editor.SetBrushObject(EnemyDefs.UltimateSoldier);
        editor.PaintCell(new Vector2I(8, 10));
        Check(editor.StartPlay(), "degradation animation level starts");
        var trooper = editor.Session.Enemies[0];
        await Frames(4);
        Check(trooper.AnimationFrame?.StartsWith(EnemyDefs.UltimateSoldier) == true,
            "the ultimate soldier uses its own pose set");
        trooper.TakeHit();
        await Frames(4);
        Check(trooper.AnimationFrame?.StartsWith(EnemyDefs.SuperSoldier) == true,
            "degrading swaps the pose set with the type");
        editor.StopPlay();
    }

    private async Task<HashSet<string>> SampleFrames(Enemy enemy, int frames)
    {
        var seen = new HashSet<string>();
        for (int i = 0; i < frames; i++)
        {
            await Frames(1);
            seen.Add(enemy.AnimationFrame);
        }
        return seen;
    }

    private async Task<Editor> FreshEditor(bool legacySize = true)
    {
        _editor?.QueueFree();
        _editor = GD.Load<PackedScene>("res://scenes/editor.tscn").Instantiate<Editor>();
        AddChild(_editor);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (legacySize)
            _editor.SetLevel(LevelData.NewDefault(40, 12));
        return _editor;
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private void Check(bool condition, string what)
    {
        if (condition)
        {
            GD.Print($"ok: {what}");
        }
        else
        {
            _failures++;
            GD.PrintErr($"FAIL: {what}");
        }
    }
}
