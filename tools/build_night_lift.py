"""Builds levels/operation-night-lift.json, the 80x80 test level.

Every floor of the tower forks into a short guarded soldier route and a
longer hacker route, and both rejoin at the shaft that climbs to the next
floor. Run it, then check the routes with tools/route_check.py.

    python tools/build_night_lift.py [--preview]
"""

import json
import pathlib
import sys

W = H = 80

EMPTY, WALL, DESTRUCTIBLE, ALARM, ELECTRIC, WATER, LAVA, SPIKES, SPEAR, HIDDEN = range(10)
STONE, BRICK, STEEL, GIRDER, BG_MESH, BG_ROCK, BG_PANEL, CONCRETE, SANDBAGS = range(10, 19)
CONTAINER, PIPE_WALL, BG_BRICK, BG_CIRCUIT, BG_HANGAR = range(19, 24)

tiles = [[EMPTY] * W for _ in range(H)]
objects = []


def rect(x0, y0, x1, y1, tile):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            tiles[y][x] = tile


def put(kind, x, y, **props):
    objects.append({"type": kind, "x": x, "y": y, "properties": {k: str(v) for k, v in props.items()}})


def chimney(x0, x1, top, bottom, bg, left_rows, right_rows, width=3):
    """A climbable shaft: girder ledges alternate sides two rows apart, which
    keeps every jump at a two-row rise with clear head room above."""
    rect(x0, top, x1, bottom, bg)
    for y in left_rows:
        rect(x0, y, x0 + width - 1, y, GIRDER)
    for y in right_rows:
        rect(x1 - width + 1, y, x1, y, GIRDER)


def frame():
    rect(1, 20, 78, 21, CONCRETE)
    rect(1, 22, 78, 39, STEEL)
    rect(1, 40, 78, 59, CONCRETE)
    rect(1, 60, 78, 78, STONE)
    rect(0, 0, 0, H - 1, WALL)
    rect(W - 1, 0, W - 1, H - 1, WALL)
    rect(0, H - 1, W - 1, H - 1, WALL)


def sewer():
    rect(1, 66, 9, 71, BG_BRICK)
    rect(7, 72, 9, 72, BG_ROCK)
    rect(10, 68, 69, 71, BG_BRICK)
    rect(1, 73, 69, 77, BG_ROCK)
    rect(5, 76, 6, 77, STONE)
    rect(7, 74, 8, 77, STONE)
    put("agent_start", 2, 71)

    # Soldier: the main sewer runs straight east under heavy guard.
    put("fighter", 16, 71)
    rect(24, 68, 24, 71, DESTRUCTIBLE)
    put("ammo_pack", 21, 71)
    put("soldier", 30, 71)
    put("robodog", 40, 71)
    tiles[71][46] = SANDBAGS
    put("weapon_assault_rifle", 44, 71)
    put("super_soldier", 54, 71)
    put("landmine", 60, 71)
    tiles[71][67] = SANDBAGS
    put("camera", 35, 68)

    # Hacker: drop into the cistern, fetch USB 1 from the east pillar, swim
    # back to computer 1 on the island, then through security door 1.
    rect(10, 75, 58, 77, WATER)
    rect(19, 75, 22, 77, STONE)
    put("computer", 21, 74, number=1)
    rect(47, 75, 48, 77, STONE)
    put("usb_key", 47, 74, number=1)
    rect(61, 73, 61, 76, STONE)
    put("security_door", 61, 77, number=1)
    put("laser", 65, 77, number=2)
    put("switch", 68, 77, number=2)
    put("camera", 30, 73)
    put("sim_card", 9, 77)
    put("closet", 3, 71)


def east_shaft_low():
    chimney(70, 77, 42, 77, BG_HANGAR,
            left_rows=[72, 68, 64, 60, 56, 52, 48, 44],
            right_rows=[74, 70, 66, 62, 58, 54, 50, 46])
    rect(72, 76, 73, 77, GIRDER)


def offices():
    rect(9, 52, 69, 55, BG_PANEL)
    rect(12, 42, 69, 47, BG_MESH)

    # Soldier: the office hall, barricaded at its west end.
    tiles[55][68] = SANDBAGS
    put("robodog", 62, 55)
    put("soldier", 54, 55)
    tiles[55][48] = SANDBAGS
    put("super_soldier", 40, 55)
    put("fighter", 30, 55)
    rect(24, 52, 24, 55, DESTRUCTIBLE)
    put("ammo_pack", 58, 55)
    put("weapon_minigun", 45, 55)
    put("camera", 44, 52)

    # Hacker: the vent. Computer 3 waits at the entry, its USB key sits on a
    # shelf only the goggles' hidden steps reach, laser 3 bars the way west,
    # and a guard covers the last stretch, so the briefcase gets you past.
    put("computer", 67, 47, number=3)
    put("infra_goggles", 63, 47)
    rect(58, 46, 59, 46, HIDDEN)
    rect(46, 44, 55, 44, GIRDER)
    put("usb_key", 47, 43, number=3)
    put("briefcase", 50, 43)
    put("notebook", 53, 43)
    put("laser", 40, 47, number=3)
    put("camera", 33, 42)
    put("soldier", 14, 47)
    rect(20, 48, 21, 51, BG_MESH)
    put("letter_g", 16, 55)


def west_shaft():
    chimney(1, 8, 22, 55, BG_HANGAR,
            left_rows=[54, 50, 46, 42, 38, 34, 30, 26],
            right_rows=[52, 48, 44, 40, 36, 32, 28, 24])


def vault():
    rect(9, 22, 69, 27, BG_CIRCUIT)
    rect(9, 31, 69, 35, BG_PANEL)

    # Soldier: the barracks, with a triple wall that wants the dynamite.
    tiles[35][10] = SANDBAGS
    put("weapon_dynamite", 12, 35)
    put("ultimate_soldier", 18, 35)
    put("fighter", 25, 35)
    rect(32, 31, 34, 35, DESTRUCTIBLE)
    put("ammo_pack", 30, 35)
    put("super_soldier", 42, 35)
    put("soldier", 50, 35)
    put("robodog", 56, 35)
    put("landmine", 61, 35)
    put("turret", 64, 31)
    tiles[35][68] = SANDBAGS
    put("health_kit", 36, 35)

    # Hacker: the server room. USB 5 sits on the shelf; the teleport reaches
    # the sealed crawlway with computer 5, which opens the floor hatch over
    # USB 4; computer 4 then kills the electric floor of the long crawl.
    rect(17, 26, 18, 27, STEEL)
    rect(12, 24, 15, 24, GIRDER)
    put("usb_key", 14, 23, number=5)
    rect(11, 29, 17, 29, BG_CIRCUIT)
    rect(13, 28, 14, 28, BG_CIRCUIT)
    put("security_door", 13, 28, number=5)
    put("security_door", 14, 28, number=5)
    put("usb_key", 16, 29, number=4)
    put("admin_password", 11, 29)
    put("teleport", 12, 23, number=1)
    rect(61, 29, 66, 29, BG_CIRCUIT)
    put("teleport", 62, 29, number=1)
    put("computer", 65, 29, number=5)
    put("computer", 27, 27, number=4)
    rect(30, 22, 58, 26, STEEL)
    rect(30, 28, 58, 28, ELECTRIC)
    put("floor_circuit", 30, 28, number=4)
    put("camera", 63, 22)
    put("closet", 66, 27)
    put("letter_e", 60, 27)


def east_shaft_high():
    chimney(70, 77, 20, 37, BG_HANGAR,
            left_rows=[36, 32, 28, 24, 20],
            right_rows=[34, 30, 26, 22])


def roof():
    # The machine house over the shaft: the soldier crosses its ground floor,
    # the hacker climbs it.
    rect(57, 10, 69, 19, BRICK)
    rect(58, 11, 68, 19, BG_BRICK)
    rect(69, 18, 69, 19, BG_BRICK)
    rect(57, 18, 57, 19, BG_BRICK)
    rect(58, 16, 64, 16, GIRDER)
    rect(66, 18, 67, 19, STEEL)
    rect(59, 14, 60, 14, GIRDER)
    rect(62, 12, 64, 12, GIRDER)
    rect(63, 10, 64, 10, EMPTY)
    rect(57, 3, 57, 9, GIRDER)

    # Hacker: computer 6 sits by security door 6; USB 6 is out on the water
    # tower, so it is up through the roof hatch and back down again.
    put("computer", 59, 15, number=6)
    put("security_door", 57, 15, number=6)
    tiles[15][57] = BG_BRICK
    rect(71, 12, 78, 12, GIRDER)
    rect(78, 13, 78, 19, GIRDER)
    put("usb_key", 76, 11, number=6)
    put("diamond", 77, 11)
    # The catwalk climbs west in two-row steps with nothing overhead, since a
    # girder above a takeoff spot caps the jump.
    rect(52, 16, 56, 16, GIRDER)
    rect(46, 14, 49, 14, GIRDER)
    rect(40, 12, 43, 12, GIRDER)
    rect(34, 10, 37, 10, GIRDER)
    rect(16, 8, 31, 8, GIRDER)
    put("electric_door", 26, 7)
    put("flame_trap", 20, 7)
    put("camera", 56, 4)
    put("letter_n", 30, 7)

    # Soldier: the open roof deck to the scaffold under the helipad.
    put("super_soldier", 50, 19)
    tiles[19][46] = SANDBAGS
    put("rocket_launcher", 42, 19)
    put("drone", 18, 16)
    put("drone", 26, 16)
    rect(31, 15, 31, 19, DESTRUCTIBLE)
    put("ultimate_soldier", 21, 19)
    put("turret", 11, 17)
    put("ammo_pack", 53, 19)
    put("health_kit", 34, 19)
    for y in (18, 14, 10):
        rect(12, y, 14, y, GIRDER)
    for y in (16, 12, 8):
        rect(8, y, 10, y, GIRDER)

    rect(2, 6, 9, 6, STEEL)
    rect(12, 6, 14, 6, STEEL)
    put("exit_door", 3, 5)
    put("letter_t", 6, 5)


def checkpoints():
    # One flag where each floor's two routes have rejoined in the shaft, the
    # lowest standing cell both routes cross, so a death costs one floor.
    for x, y in [(72, 71), (6, 55), (72, 27)]:
        put("checkpoint", x, y)


def dressing():
    for x, y in [(4, 67), (30, 69), (55, 69), (25, 74), (52, 74), (73, 60),
                 (40, 53), (60, 53), (20, 43), (50, 43), (4, 40), (20, 23),
                 (45, 32), (64, 23), (73, 30), (66, 13), (20, 4), (40, 4)]:
        put("light", x, y)
    put("letter_a", 1, 68)
    put("business_card", 76, 61)
    put("cash_stack", 13, 55)
    put("mobile", 2, 37)
    put("smartwatch", 65, 15)
    put("audio_recorder", 26, 7)
    put("deco_sign", 8, 70)
    put("deco_pipe", 40, 69)
    put("deco_machine", 64, 74)
    put("deco_clock", 50, 53)
    put("deco_poster", 34, 53)
    put("deco_plant", 12, 55)
    put("deco_machine", 25, 24)
    put("deco_machine", 64, 24)
    put("deco_crate_steel", 46, 35)
    put("deco_sign", 66, 13)
    put("deco_crate_wood", 4, 71)
    rect(1, 69, 2, 69, STONE)


def build():
    frame()
    sewer()
    east_shaft_low()
    offices()
    west_shaft()
    vault()
    east_shaft_high()
    roof()
    checkpoints()
    dressing()
    return {
        "formatVersion": 1,
        "name": "Operation Night Lift",
        "description": (
            "Helix Corp keeps its prototype codes in the tower's server vault. "
            "Come in through the sewer, climb the tower floor by floor and get out "
            "from the helipad on the roof. Every floor splits two ways: a short "
            "guarded route for the soldier, and a longer, quiet one for the hacker. "
            "Fetch USB keys, hack computers to open security doors and kill the "
            "lasers and floors, wear the briefcase past a guard, and let the goggles "
            "show you the hidden steps."
        ),
        "sky": "night",
        "width": W,
        "height": H,
        "tiles": [tiles[y][x] for y in range(H) for x in range(W)],
        "objects": objects,
    }


GLYPHS = {EMPTY: " ", WALL: "#", DESTRUCTIBLE: "%", ALARM: "a", ELECTRIC: "e", WATER: "~",
          LAVA: "L", SPIKES: "^", SPEAR: "!", HIDDEN: "h", STONE: "S", BRICK: "B",
          STEEL: "P", GIRDER: "=", BG_MESH: ".", BG_ROCK: ",", BG_PANEL: ":",
          CONCRETE: "C", SANDBAGS: "s", CONTAINER: "K", PIPE_WALL: "p",
          BG_BRICK: ";", BG_CIRCUIT: "c", BG_HANGAR: "'"}


def preview(level):
    grid = [[GLYPHS[level["tiles"][y * W + x]] for x in range(W)] for y in range(H)]
    for o in level["objects"]:
        if not o["type"].startswith("deco") and o["type"] != "light":
            grid[o["y"]][o["x"]] = "@"
    print("   " + "".join(str(x // 10) for x in range(W)))
    print("   " + "".join(str(x % 10) for x in range(W)))
    for y in range(H):
        print(f"{y:2d} " + "".join(grid[y]))


if __name__ == "__main__":
    level = build()
    out = pathlib.Path(__file__).resolve().parent.parent / "levels" / "operation-night-lift.json"
    out.write_text(json.dumps(level, indent=2) + "\n")
    if "--preview" in sys.argv:
        preview(level)
    print(f"wrote {out} ({len(level['objects'])} objects)")
