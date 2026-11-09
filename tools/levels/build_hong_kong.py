"""Builds levels/hong-kong.json, the 80x52 Hong Kong harbour warehouse.

Three levels zig-zag up a dockside warehouse under the cranes. Level one is
a flooded loading dock: the soldier hops a container barge full of guards,
the hacker dives under it, through a sluice gate. Level two is the warehouse
hall: guards and container stacks on the floor, a crane-rail catwalk above
for the hacker. Level three is the open gantry on the roof, with spike pits,
a hidden bridge and the crane control room that holds the admin password.
Shafts at the two ends join the levels; the exit is the crane cab.

    python tools/levels/build_hong_kong.py [--preview]
"""

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from levelkit import *  # noqa: E402,F403

L = Level(
    "Neon Harbour",
    "Container cranes and neon over Victoria Harbour. The warehouse ships "
    "the machine's parts south, and its password is locked in the crane "
    "control room on the roof. Work up the dock, the hall and the gantry. "
    "As the soldier, hop the guarded container barge and fight down the "
    "hall. As the hacker, dive under the barge through the sluice gate and "
    "keep to the crane rail above the guards. Everyone needs the goggles for "
    "the hidden bridge and a USB key for the control room door.",
    "night", 80, 52)

L0, L1, L2 = 44, 31, 18       # standing rows of the dock, the hall and the gantry


def frame():
    L.rect(1, 22, 78, 50, CONCRETE)
    L.rect(1, 45, 78, 50, STONE)
    L.border(WALL)
    # The skyline and two cranes, behind the gantry.
    for x0, x1, top in [(3, 8, 8), (12, 17, 12), (20, 26, 5), (29, 34, 10), (44, 50, 7), (53, 59, 11), (62, 68, 6)]:
        L.bg_rect(x0, top, x1, 21, BG_MESH)
    for x in (22, 56):
        L.bg_rect(x, 2, x + 1, 21, BG_HANGAR)
        L.bg_rect(x - 14, 2, x + 12, 3, BG_HANGAR)


def dock():
    L.carve(2, 36, 69, 44, BG_BRICK)
    L.carve(22, 45, 62, 49, BG_ROCK)
    L.rect(22, 45, 62, 49, WATER)
    # Sunken pier walls make the swimmer dive under one and rise over the next.
    for x, top, bottom in [(34, 45, 47), (46, 47, 49), (56, 45, 47)]:
        L.rect(x, top, x, bottom, STONE)
    L.put("agent_start", 4, L0)
    # Hacker: USB 1 on the shelf, computer 1 on the seabed, then the sluice gate.
    L.rect(7, 43, 9, 43, GIRDER)
    L.rect(2, 41, 4, 41, GIRDER)
    L.rect(7, 39, 9, 39, GIRDER)
    L.put("usb_key", 8, 38, number=1)
    L.put("computer", 13, L0, number=1)
    for y in range(36, 45):
        L.put("security_door", 19, y, number=1)
    L.put("camera", 30, 36)
    # Soldier: the container barge, hopped across with a guard on each part.
    for x0, x1 in [(24, 27), (31, 34), (38, 41), (45, 48), (52, 55), (59, 62)]:
        L.rect(x0, 43, x1, 44, CONTAINER)
    for kind, x in [("fighter", 25), ("soldier", 32), ("robodog", 39), ("super_soldier", 46)]:
        L.put(kind, x, 42)
    L.put("ammo_pack", 26, 42)
    L.put("health_kit", 40, 42)
    L.put("checkpoint", 66, L0)
    L.put("deco_crate_wood", 12, L0)
    L.put("deco_crate_steel", 16, L0)
    L.put("barrel", 19, L0)


def east_shaft():
    L.chimney(70, 77, 23, 44, BG_HANGAR,
              left_rows=[41, 37, 33],
              right_rows=[43, 39, 35])
    L.put("checkpoint", 76, 42)
    L.put("infra_goggles", 76, 38)
    L.put("usb_key", 76, 34, number=2)


def container(x):
    L.rect(x, L1 - 1, x + 2, L1, CONTAINER)


def hall():
    L.carve(10, 23, 69, 31, BG_HANGAR)
    # Hacker: steps, then the crane-rail catwalk with computer 2, door 2 and
    # a gap bridged by hidden steps.
    L.rect(66, 30, 67, 30, GIRDER)
    L.rect(52, 28, 65, 28, GIRDER)
    L.rect(36, 28, 47, 28, GIRDER)
    L.rect(14, 28, 27, 28, GIRDER)
    for x0, x1 in [(58, 62), (39, 43), (18, 21), (24, 26)]:
        L.rect(x0, 28, x1, 28, EMPTY)
        L.rect(x0, 30, x1, 30, GIRDER)
    L.rect(31, 28, 32, 28, HIDDEN)
    L.carve(28, 25, 35, 26, BG_HANGAR)
    L.put("computer", 55, 27, number=2)
    for y in range(24, 28):
        L.put("security_door", 48, y, number=2)
    L.put("camera", 20, 24)
    # Soldier: guards between container stacks and cargo walls.
    L.rect(64, L1, 65, L1, SANDBAGS)
    L.rect(14, L1, 15, L1, SANDBAGS)
    container(54)
    container(34)
    L.rect(44, L1 - 2, 45, L1, DESTRUCTIBLE_MOSS)
    L.rect(24, L1 - 2, 25, L1, DESTRUCTIBLE_MOSS)
    for kind, x in [("soldier", 60), ("fighter", 50), ("robodog", 52), ("super_soldier", 40),
                    ("soldier", 30), ("fighter", 28), ("ultimate_soldier", 20), ("landmine", 42),
                    ("robodog", 62), ("fighter", 38), ("soldier", 22)]:
        L.put(kind, x, L1)
    L.put("ammo_pack", 62, L1)
    L.put("health_kit", 38, L1)
    L.put("ammo_pack", 18, L1)
    L.put("deco_crate_steel", 68, L1)
    L.put("deco_crate_wood", 11, L1)


def west_shaft():
    L.chimney(2, 9, 17, 31, BG_HANGAR,
              left_rows=[30, 26, 22],
              right_rows=[28, 24, 20])
    L.put("checkpoint", 5, 31)
    L.put("usb_key", 3, 25, number=3)
    L.put("deco_machine", 6, 31)


def gantry():
    # Deck segments with spike pits between, over the warehouse roof.
    for x0, x1 in [(10, 24), (33, 57), (65, 77)]:
        L.rect(x0, 19, x1, 21, STEEL)
    for x0, x1 in [(25, 32), (58, 64)]:
        L.rect(x0, 21, x1, 21, SPIKES)
    L.put("moving_platform", 28, 19)
    L.rect(60, 19, 62, 19, HIDDEN)
    L.put("checkpoint", 12, L2)
    # The crane control room: its west door is on the way.
    L.rect(40, 12, 50, 18, STEEL)
    L.carve(40, 13, 49, 18, BG_CIRCUIT)
    L.carve(50, 16, 50, 18, BG_CIRCUIT)
    L.put("computer", 37, L2, number=3)
    for y in range(13, 19):
        L.put("security_door", 40, y, number=3)
    L.put("admin_password", 46, L2)
    L.put("deco_machine", 43, L2)
    L.put("deco_machine", 48, L2)
    L.put("drone", 20, 14)
    L.put("drone", 33, 14)
    L.put("deco_crate_steel", 54, L2)
    L.put("flame_trap", 16, 18)
    L.put("checkpoint", 66, L2)


def boom():
    L.chimney(70, 77, 6, 18, BG_HANGAR,
              left_rows=[15, 11],
              right_rows=[17, 13])
    # The crane boom runs back west, with a short gap and a long one bridged
    # by a sliding platform, to the cab with the exit.
    for x0, x1 in [(14, 35), (44, 51), (56, 69)]:
        L.rect(x0, 9, x1, 9, GIRDER)
    L.put("moving_platform", 39, 9)
    L.put("flame_trap", 62, 8)
    L.put("checkpoint", 66, 8)
    L.rect(13, 3, 21, 8, STEEL)
    L.carve(14, 4, 20, 8, BG_CIRCUIT)
    L.carve(21, 6, 21, 8, BG_CIRCUIT)
    L.put("exit_door", 16, 8)
    L.put("deco_machine", 19, 8)


def dressing():
    L.lights([(6, 40), (14, 40), (20, 40), (36, 40), (50, 40), (60, 40), (66, 40), (74, 40),
              (14, 26), (28, 26), (44, 26), (58, 26), (66, 26), (5, 24), (73, 28), (44, 14), (12, 14),
              (30, 6), (50, 6), (64, 6), (72, 12), (17, 5)])
    L.put("diamond", 28, 36)
    L.put("cash_stack", 10, 27)
    L.put("letter_a", 47, 27)
    L.put("mobile", 14, 27)
    L.put("business_card", 24, 28)
    L.put("smartwatch", 50, 36)
    L.put("audio_recorder", 52, 18)
    L.put("deco_clock", 32, 38)
    L.put("deco_poster", 10, 38)


def build():
    frame()
    dock()
    east_shaft()
    hall()
    west_shaft()
    gantry()
    boom()
    dressing()


if __name__ == "__main__":
    build()
    L.cli("hong-kong.json")
