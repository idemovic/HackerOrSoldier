"""Builds levels/mumbai.json, the 80x47 Mumbai call centre.

Three open-plan floors of desks, zig-zagging up a tower: the first heads
east, the second west, the third east to the roof. Guards patrol each floor
between sandbags and cubicle walls, which is the soldier's way. Above every
ceiling runs a one-tile-high cable duct, the hacker's way, entered up a
ledge climb at one end of the floor and left by a drop at the other. Both
routes meet in the shafts at the ends of the floors. The admin password
sits in the supervisor's vault off the west shaft, behind a security door.

    python tools/levels/build_mumbai.py [--preview]
"""

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from levelkit import *  # noqa: E402,F403

L = Level(
    "Dial Tone",
    "The machine's code is written on a call centre floor that answers no "
    "calls. Three floors of desks stand between you and the roof, and the "
    "supervisor's vault with the password lies halfway up. As the soldier, "
    "fight along each floor past the guards and through the cubicle walls. "
    "As the hacker, climb into the cable ducts above the ceilings: put on "
    "the goggles for the hidden ledge, fetch USB keys, hack the computers "
    "that open the duct doors and switch off the laser. Both routes meet at "
    "the shafts between the floors.",
    "night", 80, 47)

F1, F2, F3 = 44, 31, 18      # standing rows of the three floors


def frame():
    L.rect(1, 7, 78, 45, CONCRETE)
    L.rect(1, 7, 78, 7, STEEL)
    L.rect(70, 3, 78, 5, STEEL)
    L.border(WALL)
    # The skyline behind the tower.
    for x0, x1, top in [(2, 8, 3), (12, 17, 4), (22, 28, 1), (33, 38, 3), (44, 50, 2), (54, 59, 4), (62, 67, 2)]:
        L.bg_rect(x0, top, x1, 6, BG_MESH)


def lane(s, x0, x1, bg, height=9):
    """A floor of `height` rows ending on row s, with its two-row duct above the ceiling."""
    ceiling = s - height
    L.carve(x0, ceiling + 1, x1, s, bg)
    L.carve(x0, ceiling - 2, x1, ceiling - 1, BG_CIRCUIT)


def duct_gap(s, height, x0, x1):
    """A hole in the ceiling under the duct, bridged by a platform sliding across it."""
    row = s - height
    L.rect(x0, row, x1, row, EMPTY)
    L.put("moving_platform", (x0 + x1) // 2, row - 1)


def desk(s, x):
    L.rect(x, s - 1, x + 2, s, CONCRETE)
    L.put("deco_table", x + 1, s - 2)


def partition(s, x):
    L.rect(x, s - 2, x + 2, s, DESTRUCTIBLE_SAND)


def guards(s, spec):
    for kind, x in spec:
        L.put(kind, x, s)


def floor_one():
    L.carve(2, 33, 9, 44, BG_PANEL)
    lane(F1, 10, 69, BG_PANEL)
    L.put("agent_start", 4, F1)
    # Hacker: goggles by the start, then up the ledges (one of them hidden)
    # to the duct. USB 1 on the top ledge, computer 1 and door 1 in the duct.
    L.put("infra_goggles", 6, F1)
    L.rect(7, 43, 9, 43, GIRDER)
    L.rect(2, 41, 4, 41, HIDDEN)
    L.rect(7, 39, 9, 39, GIRDER)
    L.rect(2, 37, 4, 37, GIRDER)
    L.rect(7, 35, 9, 35, GIRDER)
    L.put("usb_key", 8, 34, number=1)
    L.put("computer", 20, 34, number=1)
    for y in (33, 34):
        L.put("security_door", 40, y, number=1)
    duct_gap(F1, 9, 52, 59)
    duct_gap(F1, 9, 24, 31)
    L.put("camera", 63, 33)
    # Soldier: the call floor, desks and a cubicle wall.
    L.rect(11, F1, 12, F1, SANDBAGS)
    for x in (20, 41):
        desk(F1, x)
    for x in (31, 52):
        partition(F1, x)
    guards(F1, [("fighter", 16), ("soldier", 26), ("robodog", 36), ("soldier", 47),
                ("super_soldier", 58), ("fighter", 64), ("landmine", 29)])
    L.put("ammo_pack", 24, F1)
    L.put("health_kit", 38, F1)
    L.put("deco_clock", 30, 38)
    L.put("deco_poster", 56, 38)


def east_shaft():
    L.chimney(70, 77, 20, 44, BG_HANGAR,
              left_rows=[41, 37, 33, 29, 25],
              right_rows=[43, 39, 35, 31, 27])
    L.put("checkpoint", 76, 42)
    L.put("usb_key", 76, 34, number=3)
    L.put("usb_key", 76, 30, number=2)


def floor_two():
    lane(F2, 20, 69, BG_PANEL, height=8)
    L.carve(10, 29, 19, 31, BG_PANEL)
    L.carve(10, 21, 19, 22, BG_CIRCUIT)
    L.rect(66, F2, 67, F2, SANDBAGS)
    # Hacker: computer 2 first, then door 2, in the duct.
    L.put("computer", 62, 22, number=2)
    for y in (21, 22):
        L.put("security_door", 45, y, number=2)
    duct_gap(F2, 8, 28, 35)
    duct_gap(F2, 8, 50, 57)
    L.put("camera", 50, 21)
    # Soldier.
    for x in (34, 55):
        desk(F2, x)
    for x in (24, 45):
        partition(F2, x)
    guards(F2, [("soldier", 22), ("fighter", 29), ("super_soldier", 40), ("robodog", 50),
                ("ultimate_soldier", 60), ("soldier", 64), ("landmine", 38)])
    L.put("health_kit", 27, F2)
    L.put("ammo_pack", 48, F2)
    L.put("deco_clock", 40, 26)
    L.put("deco_poster", 24, 26)


def west_shaft():
    L.chimney(2, 9, 8, 31, BG_HANGAR,
              left_rows=[30, 26, 22, 18, 14],
              right_rows=[28, 24, 20, 16, 12])
    L.put("checkpoint", 5, 31)
    # The supervisor's vault, its door level with the row-28 ledge.
    L.carve(10, 24, 19, 27, BG_CIRCUIT)
    for y in (24, 25, 26, 27):
        L.put("security_door", 10, y, number=3)
    L.put("admin_password", 17, 27)
    L.put("computer", 8, 27, number=3)
    L.put("deco_machine", 13, 27)
    L.put("deco_plant", 15, 27)
    L.put("usb_key", 3, 13, number=4)


def floor_three():
    lane(F3, 10, 69, BG_PANEL, height=8)
    L.rect(11, F3, 12, F3, SANDBAGS)
    # Hacker: computer 4 and door 4, then a laser with its switch.
    L.put("computer", 18, 9, number=4)
    for y in (8, 9):
        L.put("security_door", 34, y, number=4)
    L.put("laser", 52, 9, number=5)
    L.put("switch", 60, 9, number=5)
    duct_gap(F3, 8, 40, 47)
    duct_gap(F3, 8, 20, 27)
    L.put("camera", 56, 8)
    # Soldier.
    for x in (20, 41, 62):
        desk(F3, x)
    for x in (31, 52):
        partition(F3, x)
    guards(F3, [("soldier", 16), ("robodog", 26), ("super_soldier", 36), ("ultimate_soldier", 47),
                ("soldier", 57), ("fighter", 67), ("landmine", 44), ("turret", 66)])
    L.put("ammo_pack", 28, F3)
    L.put("health_kit", 38, F3)
    L.put("deco_clock", 50, 13)
    L.put("deco_poster", 20, 13)


def tower():
    L.chimney(70, 77, 6, 18, BG_HANGAR,
              left_rows=[15, 11],
              right_rows=[17, 13, 9])
    L.put("exit_door", 76, 8)
    L.put("checkpoint", 74, 18)


def dressing():
    L.lights([(16, 38), (30, 38), (44, 38), (58, 38), (66, 38), (24, 26), (38, 26), (52, 26),
              (66, 26), (16, 13), (30, 13), (44, 13), (58, 13), (5, 40), (73, 30), (5, 24), (73, 12)])
    L.put("diamond", 30, 34)
    L.put("cash_stack", 25, 22)
    L.put("letter_a", 55, 22)
    L.put("mobile", 28, 9)
    L.put("smartwatch", 42, 9)
    L.put("business_card", 66, 9)
    L.put("deco_copier", 14, F1)
    L.put("deco_plant", 66, F1)
    L.put("deco_copier", 66, F2)
    L.put("deco_plant", 14, F2)
    L.put("deco_copier", 14, F3)
    L.put("deco_plant", 66, F3)
    L.put("deco_machine", 8, F1)
    L.put("deco_crate_steel", 72, 44)
    L.put("deco_machine", 66, 44)


def build():
    frame()
    floor_one()
    east_shaft()
    floor_two()
    west_shaft()
    floor_three()
    tower()
    dressing()


if __name__ == "__main__":
    build()
    L.cli("mumbai.json")
