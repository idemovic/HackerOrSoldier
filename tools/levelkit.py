"""Builder library for Hacker or Soldier levels (format version 3).

Each campaign city is a standalone script, tools/levels/build_<slug>.py, that
imports this module, draws the level and saves it:

    import pathlib, sys
    sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
    from levelkit import *

    L = Level("Tokyo", "A robotics plant ...", "night", 80, 40)
    L.border(WALL)                                  # left, right and bottom walls
    L.rect(1, 30, 78, 38, CONCRETE)                 # solid tiles
    L.bg_rect(2, 20, 60, 29, BG_PANEL)              # background wall, no collision
    L.put("agent_start", 3, 29)
    L.put("computer", 20, 29, number=1)             # properties become strings
    L.put("admin_password", 70, 29)
    L.put("exit_door", 76, 29)
    L.cli("tokyo.json")                             # --preview prints the map; writes levels/tokyo.json

Then check it: python tools/route_check.py levels/tokyo.json, and the whole
campaign with python tools/check_campaign.py.

Coordinates are tile cells, (0, 0) top left, y grows downwards. rect() takes
inclusive corners. Tile ids and solidity come from scripts/TileDefs.cs, so
this module cannot drift from the game (asserts below check the names).
"""

import json
import pathlib
import sys

import tiledefs

ROOT = pathlib.Path(__file__).resolve().parent.parent
LEVELS_DIR = ROOT / "levels"

EMPTY, WALL, DESTRUCTIBLE, ALARM, ELECTRIC, WATER, LAVA, SPIKES, SPEAR, HIDDEN = range(10)
STONE, BRICK, STEEL, GIRDER, BG_MESH, BG_ROCK, BG_PANEL, CONCRETE, SANDBAGS = range(10, 19)
CONTAINER, PIPE_WALL, BG_BRICK, BG_CIRCUIT, BG_HANGAR = range(19, 24)
# Destructible walls in each brick color, shot or blown open like DESTRUCTIBLE.
DESTRUCTIBLE_STONE, DESTRUCTIBLE_RED, DESTRUCTIBLE_STEEL, DESTRUCTIBLE_MOSS, DESTRUCTIBLE_SAND = range(26, 31)
SNOW, MINE_ROCK, MINE_TIMBER = range(31, 34)

_IDS = tiledefs.IDS
assert (WALL, DESTRUCTIBLE, HIDDEN, STONE, GIRDER, SANDBAGS, PIPE_WALL, BG_HANGAR) == (
    _IDS["Wall"], _IDS["DestructibleWall"], _IDS["HiddenPlatform"], _IDS["StoneBlocks"], _IDS["Girder"],
    _IDS["Sandbags"], _IDS["PipeWall"], _IDS["BackgroundHangar"]), "levelkit tile ids drifted from TileDefs.cs"
assert (DESTRUCTIBLE_STONE, DESTRUCTIBLE_SAND, SNOW, MINE_TIMBER) == (
    _IDS["DestructibleStone"], _IDS["DestructibleSand"], _IDS["Snow"], _IDS["MineTimber"]), "levelkit ids drifted"

# Solid for the agent: walls, floors, hidden steps. Liquids and hazards are not.
SOLID = tiledefs.SOLID
# Background walls live in the separate "background" array.
BACKGROUND = tiledefs.BACKGROUND

GLYPHS = {EMPTY: " ", WALL: "#", DESTRUCTIBLE: "%", ALARM: "a", ELECTRIC: "e", WATER: "~",
          LAVA: "L", SPIKES: "^", SPEAR: "!", HIDDEN: "h", STONE: "S", BRICK: "B",
          STEEL: "P", GIRDER: "=", BG_MESH: ".", BG_ROCK: ",", BG_PANEL: ":",
          CONCRETE: "C", SANDBAGS: "s", CONTAINER: "K", PIPE_WALL: "p",
          BG_BRICK: ";", BG_CIRCUIT: "c", BG_HANGAR: "'",
          DESTRUCTIBLE_STONE: "1", DESTRUCTIBLE_RED: "2", DESTRUCTIBLE_STEEL: "3",
          DESTRUCTIBLE_MOSS: "4", DESTRUCTIBLE_SAND: "5", SNOW: "n", MINE_ROCK: "r", MINE_TIMBER: "t"}
assert set(GLYPHS) == {0} | set(tiledefs.IDS.values()), "a tile id has no preview glyph"

FORMAT_VERSION = 3


class Level:
    def __init__(self, name, description, sky="night", w=80, h=80):
        assert sky in ("night", "day"), "sky is 'night' or 'day'"
        self.name, self.description, self.sky, self.w, self.h = name, description, sky, w, h
        self.tiles = [[EMPTY] * w for _ in range(h)]
        self.background = [[EMPTY] * w for _ in range(h)]
        self.objects = []

    def _check(self, x, y):
        if not (0 <= x < self.w and 0 <= y < self.h):
            raise IndexError("(%d,%d) is outside the %dx%d level" % (x, y, self.w, self.h))

    def rect(self, x0, y0, x1, y1, tile):
        """Fill the inclusive rectangle on the tile's own layer: background
        ids go to the background array, everything else to the solid/hazard
        array. A background id does not clear solid tiles under it (see
        carve) and EMPTY clears the tile array only (bg_rect(..., EMPTY)
        clears the background)."""
        assert tile in GLYPHS, "unknown tile id %r" % tile
        self._check(x0, y0)
        self._check(x1, y1)
        layer = self.background if tile in BACKGROUND else self.tiles
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                layer[y][x] = tile

    def bg_rect(self, x0, y0, x1, y1, tile=BG_PANEL):
        """rect() for a background wall, or EMPTY to clear the background."""
        assert tile == EMPTY or tile in BACKGROUND, "bg_rect needs a background tile id, got %r" % tile
        self._check(x0, y0)
        self._check(x1, y1)
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.background[y][x] = tile

    def carve(self, x0, y0, x1, y1, bg=EMPTY):
        """Hollow a room: clear the solid/hazard tiles in the rectangle and
        paint a background wall in it (or none). Use it to cut a room out of a
        filled block, since rect() with a background id keeps the solid tiles."""
        assert bg == EMPTY or bg in BACKGROUND, "carve needs a background tile id or EMPTY, got %r" % bg
        self.rect(x0, y0, x1, y1, EMPTY)
        self.bg_rect(x0, y0, x1, y1, bg)

    def set(self, x, y, tile):
        self.rect(x, y, x, y, tile)

    def tile(self, x, y):
        self._check(x, y)
        return self.tiles[y][x]

    def border(self, tile=WALL):
        """Left, right and bottom walls, the usual level frame."""
        self.rect(0, 0, 0, self.h - 1, tile)
        self.rect(self.w - 1, 0, self.w - 1, self.h - 1, tile)
        self.rect(0, self.h - 1, self.w - 1, self.h - 1, tile)

    def put(self, kind, x, y, **props):
        """Place an object. Properties are stored as strings: number=1 for
        computers, usb keys, security doors, lasers, switches, floor circuits
        and teleports (a pair shares a number); color="red" for doors and keys;
        facing="left" for cameras."""
        if kind not in tiledefs.OBJECT_TYPES:
            raise ValueError("unknown object type %r (a typo would load as a pickup)" % kind)
        self._check(x, y)
        self.objects.append({"type": kind, "x": x, "y": y, "properties": {k: str(v) for k, v in props.items()}})

    def lights(self, cells):
        for x, y in cells:
            self.put("light", x, y)

    def chimney(self, x0, x1, top, bottom, bg, left_rows, right_rows, width=3):
        """A climbable shaft: girder ledges alternate sides two rows apart, which
        keeps every jump at a two-row rise with clear head room above."""
        self.carve(x0, top, x1, bottom, bg)
        for y in left_rows:
            self.rect(x0, y, x0 + width - 1, y, GIRDER)
        for y in right_rows:
            self.rect(x1 - width + 1, y, x1, y, GIRDER)

    def validate(self):
        """The game's Validate rules plus the campaign's: one agent start, an
        exit door, exactly one admin password (the exit stays locked until it
        is taken)."""
        def count(kind):
            return sum(o["type"] == kind for o in self.objects)
        if count("agent_start") != 1:
            raise ValueError("%s: needs exactly one agent_start, has %d" % (self.name, count("agent_start")))
        if count("exit_door") < 1:
            raise ValueError("%s: needs at least one exit_door" % self.name)
        if count("admin_password") != 1:
            raise ValueError("%s: needs exactly one admin_password, has %d" % (self.name, count("admin_password")))

    def to_json(self):
        return {
            "formatVersion": FORMAT_VERSION,
            "name": self.name,
            "description": self.description,
            "sky": self.sky,
            "width": self.w,
            "height": self.h,
            "tiles": [self.tiles[y][x] for y in range(self.h) for x in range(self.w)],
            "background": [self.background[y][x] for y in range(self.h) for x in range(self.w)],
            "objects": self.objects,
        }

    def save(self, file):
        """Validate and write the level. file is a bare name inside levels/
        (the campaign's LevelFile, e.g. "tokyo.json") or a path with a folder.
        Returns the path."""
        self.validate()
        path = pathlib.Path(file)
        if path.parent == pathlib.Path("."):
            path = LEVELS_DIR / path
        path.write_text(json.dumps(self.to_json(), indent=2) + "\n", encoding="utf-8", newline="\n")
        return path

    def preview(self):
        """ASCII map: solid and hazard tiles, background where the cell is
        otherwise empty, @ for gameplay objects (decorations and lights hidden)."""
        grid = [[GLYPHS[self.tiles[y][x]] if self.tiles[y][x] != EMPTY else GLYPHS[self.background[y][x]]
                 for x in range(self.w)] for y in range(self.h)]
        for o in self.objects:
            if not o["type"].startswith("deco") and o["type"] != "light":
                grid[o["y"]][o["x"]] = "@"
        print("   " + "".join(str(x // 10 % 10) for x in range(self.w)))
        print("   " + "".join(str(x % 10) for x in range(self.w)))
        for y in range(self.h):
            print("%2d " % y + "".join(grid[y]))

    def cli(self, file):
        """The tail of a builder script: --preview prints the map, then save."""
        if "--preview" in sys.argv:
            self.preview()
        path = self.save(file)
        print("wrote %s (%d objects)" % (path, len(self.objects)))
