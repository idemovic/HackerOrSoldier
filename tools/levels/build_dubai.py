"""Builds levels/operation-night-lift.json, the 80x80 Dubai tower level.

Every floor of the tower forks into a short guarded soldier route and a
longer hacker route, and both rejoin at the shaft that climbs to the next
floor. Run it, then check the routes with tools/route_check.py.

    python tools/levels/build_dubai.py [--preview]
"""

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from levelkit import *  # noqa: E402,F403

L = Level(
    "Operation Night Lift",
    "Helix Corp keeps its prototype codes in the tower's server vault. "
    "Come in through the sewer, climb the tower floor by floor and get out "
    "from the helipad on the roof. Every floor splits two ways: a short "
    "guarded route for the soldier, and a longer, quiet one for the hacker. "
    "Fetch USB keys, hack computers to open security doors and kill the "
    "lasers and floors, wear the briefcase past a guard, and let the goggles "
    "show you the hidden steps.",
    "night", 80, 80)
W = H = 80


def frame():
    L.rect(1, 20, 78, 21, CONCRETE)
    L.rect(1, 22, 78, 39, STEEL)
    L.rect(1, 40, 78, 59, CONCRETE)
    L.rect(1, 60, 78, 78, STONE)
    L.border(WALL)


def sewer():
    L.carve(1, 66, 9, 71, BG_BRICK)
    L.carve(7, 72, 9, 72, BG_ROCK)
    L.carve(10, 68, 69, 71, BG_BRICK)
    L.carve(1, 73, 69, 77, BG_ROCK)
    L.rect(5, 76, 6, 77, STONE)
    L.rect(7, 74, 8, 77, STONE)
    L.put("agent_start", 2, 71)

    # Soldier: the main sewer runs straight east under heavy guard.
    L.put("fighter", 16, 71)
    L.rect(24, 68, 24, 71, DESTRUCTIBLE)
    L.put("ammo_pack", 21, 71)
    L.put("soldier", 30, 71)
    L.put("robodog", 40, 71)
    L.set(46, 71, SANDBAGS)
    L.put("weapon_assault_rifle", 44, 71)
    L.put("super_soldier", 54, 71)
    L.put("landmine", 60, 71)
    L.set(67, 71, SANDBAGS)
    L.put("camera", 35, 68)

    # Hacker: drop into the cistern, fetch USB 1 from the east pillar, swim
    # back to computer 1 on the island, then through security door 1.
    L.rect(10, 75, 58, 77, WATER)
    L.rect(19, 75, 22, 77, STONE)
    L.put("computer", 21, 74, number=1)
    L.rect(47, 75, 48, 77, STONE)
    L.put("usb_key", 47, 74, number=1)
    L.rect(61, 73, 61, 76, STONE)
    L.put("security_door", 61, 77, number=1)
    L.put("laser", 65, 77, number=2)
    L.put("switch", 68, 77, number=2)
    L.put("camera", 30, 73)
    L.put("sim_card", 9, 77)
    L.put("closet", 3, 71)


def east_shaft_low():
    L.chimney(70, 77, 42, 77, BG_HANGAR,
            left_rows=[72, 68, 64, 60, 56, 52, 48, 44],
            right_rows=[74, 70, 66, 62, 58, 54, 50, 46])
    L.rect(72, 76, 73, 77, GIRDER)


def offices():
    L.carve(9, 52, 69, 55, BG_PANEL)
    L.carve(12, 42, 69, 47, BG_MESH)

    # Soldier: the office hall, barricaded at its west end.
    L.set(68, 55, SANDBAGS)
    L.put("robodog", 62, 55)
    L.put("soldier", 54, 55)
    L.set(48, 55, SANDBAGS)
    L.put("super_soldier", 40, 55)
    L.put("fighter", 30, 55)
    L.rect(24, 52, 24, 55, DESTRUCTIBLE)
    L.put("ammo_pack", 58, 55)
    L.put("weapon_minigun", 45, 55)
    L.put("camera", 44, 52)

    # Hacker: the vent. Computer 3 waits at the entry, its USB key sits on a
    # shelf only the goggles' hidden steps reach, laser 3 bars the way west,
    # and a guard covers the last stretch, so the briefcase gets you past.
    L.put("computer", 67, 47, number=3)
    L.put("infra_goggles", 63, 47)
    L.rect(58, 46, 59, 46, HIDDEN)
    L.rect(46, 44, 55, 44, GIRDER)
    L.put("usb_key", 47, 43, number=3)
    L.put("briefcase", 50, 43)
    L.put("notebook", 53, 43)
    L.put("laser", 40, 47, number=3)
    L.put("camera", 33, 42)
    L.put("soldier", 14, 47)
    L.carve(20, 48, 21, 51, BG_MESH)
    L.put("letter_g", 16, 55)


def west_shaft():
    L.chimney(1, 8, 22, 55, BG_HANGAR,
            left_rows=[54, 50, 46, 42, 38, 34, 30, 26],
            right_rows=[52, 48, 44, 40, 36, 32, 28, 24])


def vault():
    L.carve(9, 22, 69, 27, BG_CIRCUIT)
    L.carve(9, 31, 69, 35, BG_PANEL)

    # Soldier: the barracks, with a triple wall that wants the dynamite.
    L.set(10, 35, SANDBAGS)
    L.put("weapon_dynamite", 12, 35)
    L.put("ultimate_soldier", 18, 35)
    L.put("fighter", 25, 35)
    L.rect(32, 31, 34, 35, DESTRUCTIBLE)
    L.put("ammo_pack", 30, 35)
    L.put("super_soldier", 42, 35)
    L.put("soldier", 50, 35)
    L.put("robodog", 56, 35)
    L.put("landmine", 61, 35)
    L.put("turret", 64, 31)
    L.set(68, 35, SANDBAGS)
    L.put("health_kit", 36, 35)

    # Hacker: the server room. USB 5 sits on the shelf; the teleport reaches
    # the sealed crawlway with computer 5, which opens the floor hatch over
    # USB 4; computer 4 then kills the electric floor of the long crawl.
    L.rect(17, 26, 18, 27, STEEL)
    L.rect(12, 24, 15, 24, GIRDER)
    L.put("usb_key", 14, 23, number=5)
    L.carve(11, 29, 17, 29, BG_CIRCUIT)
    L.carve(13, 28, 14, 28, BG_CIRCUIT)
    L.put("security_door", 13, 28, number=5)
    L.put("security_door", 14, 28, number=5)
    L.put("usb_key", 16, 29, number=4)
    L.put("admin_password", 11, 29)
    L.put("teleport", 12, 23, number=1)
    L.carve(61, 29, 66, 29, BG_CIRCUIT)
    L.put("teleport", 62, 29, number=1)
    L.put("computer", 65, 29, number=5)
    L.put("computer", 27, 27, number=4)
    L.rect(30, 22, 58, 26, STEEL)
    L.rect(30, 28, 58, 28, ELECTRIC)
    L.put("floor_circuit", 30, 28, number=4)
    L.put("camera", 63, 22)
    L.put("closet", 66, 27)
    L.put("letter_e", 60, 27)


def east_shaft_high():
    L.chimney(70, 77, 20, 37, BG_HANGAR,
            left_rows=[36, 32, 28, 24, 20],
            right_rows=[34, 30, 26, 22])


def roof():
    # The machine house over the shaft: the soldier crosses its ground floor,
    # the hacker climbs it.
    L.rect(57, 10, 69, 19, BRICK)
    L.carve(58, 11, 68, 19, BG_BRICK)
    L.carve(69, 18, 69, 19, BG_BRICK)
    L.carve(57, 18, 57, 19, BG_BRICK)
    L.rect(58, 16, 64, 16, GIRDER)
    L.rect(66, 18, 67, 19, STEEL)
    L.rect(59, 14, 60, 14, GIRDER)
    L.rect(62, 12, 64, 12, GIRDER)
    L.rect(63, 10, 64, 10, EMPTY)
    L.rect(57, 3, 57, 9, GIRDER)

    # Hacker: computer 6 sits by security door 6; USB 6 is out on the water
    # tower, so it is up through the roof hatch and back down again.
    L.put("computer", 59, 15, number=6)
    L.put("security_door", 57, 15, number=6)
    L.carve(57, 15, 57, 15, BG_BRICK)
    L.rect(71, 12, 78, 12, GIRDER)
    L.rect(78, 13, 78, 19, GIRDER)
    L.put("usb_key", 76, 11, number=6)
    L.put("diamond", 77, 11)
    # The catwalk climbs west in two-row steps with nothing overhead, since a
    # girder above a takeoff spot caps the jump.
    L.rect(52, 16, 56, 16, GIRDER)
    L.rect(46, 14, 49, 14, GIRDER)
    L.rect(40, 12, 43, 12, GIRDER)
    L.rect(34, 10, 37, 10, GIRDER)
    L.rect(16, 8, 31, 8, GIRDER)
    L.put("electric_door", 26, 7)
    L.put("flame_trap", 20, 7)
    L.put("camera", 56, 4)
    L.put("letter_n", 30, 7)

    # Soldier: the open roof deck to the scaffold under the helipad.
    L.put("super_soldier", 50, 19)
    L.set(46, 19, SANDBAGS)
    L.put("rocket_launcher", 42, 19)
    L.put("drone", 18, 16)
    L.put("drone", 26, 16)
    L.rect(31, 15, 31, 19, DESTRUCTIBLE)
    L.put("ultimate_soldier", 21, 19)
    L.put("turret", 11, 17)
    L.put("ammo_pack", 53, 19)
    L.put("health_kit", 34, 19)
    for y in (18, 14, 10):
        L.rect(12, y, 14, y, GIRDER)
    for y in (16, 12, 8):
        L.rect(8, y, 10, y, GIRDER)

    L.rect(2, 6, 9, 6, STEEL)
    L.rect(12, 6, 14, 6, STEEL)
    L.put("exit_door", 3, 5)
    L.put("letter_t", 6, 5)


def checkpoints():
    # One flag where each floor's two routes have rejoined in the shaft, the
    # lowest standing cell both routes cross, so a death costs one floor.
    for x, y in [(72, 71), (6, 55), (72, 27)]:
        L.put("checkpoint", x, y)


def dressing():
    for x, y in [(4, 67), (30, 69), (55, 69), (25, 74), (52, 74), (73, 60),
                 (40, 53), (60, 53), (20, 43), (50, 43), (4, 40), (20, 23),
                 (45, 32), (64, 23), (73, 30), (66, 13), (20, 4), (40, 4)]:
        L.put("light", x, y)
    L.put("letter_a", 1, 68)
    L.put("business_card", 76, 61)
    L.put("cash_stack", 13, 55)
    L.put("mobile", 2, 37)
    L.put("smartwatch", 65, 15)
    L.put("audio_recorder", 26, 7)
    L.put("deco_machine", 64, 74)
    L.put("deco_clock", 50, 53)
    L.put("deco_poster", 34, 53)
    L.put("deco_plant", 12, 55)
    L.put("deco_machine", 25, 24)
    L.put("deco_machine", 64, 24)
    L.put("deco_crate_steel", 46, 35)
    L.put("deco_crate_wood", 4, 71)
    L.rect(1, 69, 2, 69, STONE)


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


if __name__ == "__main__":
    build()
    L.cli("operation-night-lift.json")
