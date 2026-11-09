"""Builds levels/cairo.json, the 80x40 Cairo research lab by the pyramids.

The lab fills the whole width of the level, with the pyramids on the skyline
behind it. Three floors zig-zag down it, the hall (heading west), an upper
tomb (east) and a lower tomb (west), and a tower at the west end climbs back
up to the exit. On each floor guards patrol the floor between sandbags, and
a catwalk runs above them for the hacker, locked by a security door and, in
the lower tomb, broken by a gap bridged only with hidden steps. The admin
password sits in a vault off the tower behind a third security door.

    python tools/levels/build_cairo.py [--preview]
"""

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from levelkit import *  # noqa: E402,F403

L = Level(
    "Pyramid Protocol",
    "A research lab beside the pyramids is training the syndicate's machine. "
    "Its core vault lies in the tower at the west end, three floors of tomb "
    "and hall away. As the soldier, run the guarded floors, hall first, then "
    "the upper and lower tombs. As the hacker, stay on the catwalks above "
    "them: fetch USB keys, hack the security doors and put on the goggles to "
    "cross the gap on hidden steps. Both routes meet at the tower, where the "
    "password waits behind one more door.",
    "day", 80, 40)


def frame():
    L.rect(1, 20, 78, 38, STONE)
    L.border(WALL)
    L.rect(1, 10, 78, 19, CONCRETE)
    L.rect(1, 10, 78, 11, STEEL)
    L.rect(1, 6, 10, 9, STEEL)
    # The pyramids on the skyline.
    for apex_x, apex_y in [(31, 2), (56, 5), (70, 6)]:
        for y in range(apex_y, 10):
            L.bg_rect(apex_x - (y - apex_y), y, apex_x + (y - apex_y), y, BG_ROCK)


def hurdle(x0, x1, row):
    """Sink a stretch of catwalk two rows, so the hacker climbs back out of it."""
    L.rect(x0, row, x1, row, EMPTY)
    L.rect(x0, row + 2, x1, row + 2, GIRDER)


def hall():
    L.carve(18, 14, 77, 19, BG_PANEL)
    L.carve(18, 20, 23, 21, BG_PANEL)
    L.put("agent_start", 75, 19)
    # Hacker: USB 1 and the goggles on the lobby shelf, computer 1 and
    # security door 1 on the catwalk over the guards.
    L.rect(76, 18, 77, 18, GIRDER)
    L.put("usb_key", 77, 17, number=1)
    L.put("infra_goggles", 76, 17)
    L.rect(71, 18, 72, 18, GIRDER)
    L.rect(24, 16, 70, 16, GIRDER)
    L.put("computer", 66, 15, number=1)
    L.put("security_door", 50, 14, number=1)
    L.put("security_door", 50, 15, number=1)
    L.put("camera", 38, 14)
    # Soldier: the guards on the floor, between sandbags.
    L.set(69, 19, SANDBAGS)
    L.put("fighter", 64, 19)
    L.put("ammo_pack", 61, 19)
    L.put("soldier", 58, 19)
    L.put("robodog", 54, 19)
    L.rect(45, 17, 45, 19, DESTRUCTIBLE_SAND)
    L.put("soldier", 40, 19)
    L.put("health_kit", 37, 19)
    L.rect(26, 19, 27, 19, SANDBAGS)


def hurdles():
    for x0, x1 in [(56, 61), (30, 35)]:
        hurdle(x0, x1, 16)
    for x0, x1 in [(50, 55), (60, 64)]:
        hurdle(x0, x1, 24)
    for x0, x1 in [(56, 60), (22, 27)]:
        hurdle(x0, x1, 33)


def upper_tomb():
    L.carve(18, 22, 71, 27, BG_BRICK)
    L.carve(68, 28, 71, 30, BG_BRICK)
    # Hacker: USB 2 on a shelf at the landing, computer 2 and door 2 on the catwalk.
    L.rect(18, 26, 19, 26, GIRDER)
    L.put("usb_key", 18, 25, number=2)
    L.rect(24, 26, 25, 26, GIRDER)
    L.rect(26, 24, 67, 24, GIRDER)
    L.put("computer", 34, 23, number=2)
    L.put("security_door", 44, 22, number=2)
    L.put("security_door", 44, 23, number=2)
    L.put("camera", 56, 22)
    # Soldier.
    L.rect(26, 27, 27, 27, SANDBAGS)
    L.put("soldier", 31, 27)
    L.put("fighter", 36, 27)
    L.rect(40, 25, 40, 27, DESTRUCTIBLE_SAND)
    L.put("ammo_pack", 42, 27)
    L.put("soldier", 50, 27)
    L.put("health_kit", 56, 27)
    L.put("fighter", 58, 27)
    L.put("super_soldier", 62, 27)
    L.rect(66, 27, 67, 27, SANDBAGS)


def lower_tomb():
    L.carve(10, 31, 71, 36, BG_BRICK)
    L.carve(42, 29, 53, 30, BG_BRICK)
    # Hacker: the catwalk breaks over a pit, bridged by two hidden steps.
    L.rect(64, 35, 65, 35, GIRDER)
    L.rect(52, 33, 63, 33, GIRDER)
    L.rect(47, 33, 48, 33, HIDDEN)
    L.rect(14, 33, 43, 33, GIRDER)
    L.put("camera", 58, 31)
    # Soldier.
    L.rect(62, 36, 63, 36, SANDBAGS)
    L.put("soldier", 58, 36)
    L.put("ammo_pack", 56, 36)
    L.put("robodog", 50, 36)
    L.put("super_soldier", 42, 36)
    L.put("landmine", 38, 36)
    L.put("health_kit", 36, 36)
    L.rect(30, 34, 30, 36, DESTRUCTIBLE_SAND)
    L.rect(26, 36, 27, 36, SANDBAGS)
    L.put("tombstone", 20, 36)
    L.put("tombstone", 15, 36)


def tower():
    L.chimney(2, 9, 8, 36, BG_HANGAR,
              left_rows=[35, 31, 27, 23, 19, 15, 11],
              right_rows=[33, 29, 25, 21, 17, 13])
    L.put("exit_door", 3, 10)
    L.put("deco_machine", 6, 36)
    # The password vault off the chimney.
    L.carve(10, 26, 16, 28, BG_CIRCUIT)
    for y in (26, 27, 28):
        L.put("security_door", 10, y, number=3)
    L.put("admin_password", 14, 28)
    L.put("usb_key", 3, 26, number=3)
    L.put("computer", 8, 28, number=3)
    L.put("deco_machine", 12, 28)
    L.put("deco_machine", 16, 28)


def checkpoints():
    for x, y in [(70, 36), (21, 27), (6, 36)]:
        L.put("checkpoint", x, y)


def dressing():
    L.lights([(30, 15), (44, 15), (58, 15), (72, 15), (24, 23), (40, 23), (56, 23), (66, 23),
              (20, 33), (36, 33), (58, 33), (68, 33), (6, 30), (6, 20), (13, 27)])
    L.put("deco_machine", 52, 19)
    L.put("deco_machine", 62, 19)
    L.put("deco_clock", 56, 15)
    L.put("deco_plant", 73, 19)
    L.put("deco_copier", 71, 19)
    L.put("deco_poster", 30, 15)
    L.put("deco_machine", 40, 9)
    L.put("deco_machine", 60, 9)
    L.put("deco_crate_wood", 48, 27)
    L.put("deco_crate_steel", 22, 27)
    L.put("diamond", 24, 15)
    L.put("cash_stack", 66, 23)
    L.put("letter_a", 20, 33)
    L.put("mobile", 14, 32)
    L.put("business_card", 40, 32)
    L.put("smartwatch", 48, 32)
    L.put("audio_recorder", 70, 32)


def build():
    frame()
    hall()
    upper_tomb()
    lower_tomb()
    tower()
    hurdles()
    checkpoints()
    dressing()


if __name__ == "__main__":
    build()
    L.cli("cairo.json")
