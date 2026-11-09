"""Builds levels/moscow.json, the Cold War bunker under a ministry.

The agent starts in the snowy courtyard and drops down three bunker tiers.
Each tier forks into a guarded corridor for the soldier and a quiet air vent
above it for the hacker, and both routes meet again at the shaft down to the
next tier, on a landing the soldier crosses on his way to a hole in its edge.
Blast doors close every corridor end. The last tier ends in the reactor-cold
vault, where hidden steps lead to the admin password before the exit.

    python tools/levels/build_moscow.py [--preview]
"""

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from levelkit import *  # noqa: E402,F403

L = Level(
    "Ministry Blast Doors",
    "Under a ministry building, an old Cold War bunker hums with new hardware. "
    "The third password sits behind its blast doors. Drop down the courtyard "
    "hatch and descend three tiers. On each one, blast through the guarded "
    "corridor, or crawl the air vent above it, fetching USB keys, hacking "
    "computers and switching off lasers. Wear the goggles in the vault to see "
    "the hidden steps to the password.",
    "night", 80, 64)


def shell():
    L.rect(1, 13, 78, 62, CONCRETE)
    L.rect(21, 0, 78, 12, BRICK)
    L.rect(1, 10, 16, 12, SNOW)
    L.border(WALL)


def courtyard():
    L.carve(17, 10, 20, 19, BG_BRICK)
    L.put("agent_start", 3, 9)
    L.put("deco_crate_wood", 8, 9)
    L.put("deco_crate_steel", 12, 9)
    L.put("camera", 20, 8, facing="left")
    L.put("letter_g", 14, 9)
    L.lights([(6, 5), (14, 5)])


def tier_a():
    L.carve(14, 19, 16, 22, BG_BRICK)
    L.carve(14, 23, 20, 27, BG_PANEL)
    L.carve(21, 16, 62, 19, BG_MESH)
    L.carve(22, 23, 61, 27, BG_PANEL)
    L.rect(21, 23, 21, 27, DESTRUCTIBLE_STEEL)
    L.rect(62, 23, 62, 27, DESTRUCTIBLE_STEEL)
    L.carve(63, 16, 66, 34, BG_ROCK)
    L.carve(67, 34, 69, 37, BG_ROCK)
    L.carve(63, 38, 69, 42, BG_PANEL)
    L.put("checkpoint", 19, 19)

    # Soldier: the long corridor under heavy guard.
    L.put("fighter", 28, 27)
    L.put("ammo_pack", 25, 27)
    L.put("soldier", 34, 27)
    L.set(40, 27, SANDBAGS)
    L.put("landmine", 44, 27)
    L.put("robodog", 48, 27)
    L.put("soldier", 54, 27)
    L.put("health_kit", 58, 27)
    L.put("camera", 30, 23, facing="right")

    # Hacker: the vent. Computer 1 waits at the entry, USB 1 sits on a
    # pillar halfway, and security door 1 shuts the last stretch.
    L.put("computer", 24, 19, number=1)
    L.rect(38, 18, 39, 19, STEEL)
    L.put("usb_key", 38, 17, number=1)
    for y in range(16, 20):
        L.put("security_door", 50, y, number=1)
    L.put("closet", 30, 19)
    L.put("camera", 44, 16, facing="left")
    L.put("cash_stack", 56, 19)
    L.lights([(26, 17), (42, 17), (58, 17), (30, 24), (50, 24)])


def tier_b():
    L.carve(14, 31, 62, 34, BG_MESH)
    L.carve(15, 38, 61, 42, BG_PANEL)
    L.rect(62, 38, 62, 42, DESTRUCTIBLE_STEEL)
    L.rect(14, 38, 14, 42, DESTRUCTIBLE_STEEL)
    L.carve(10, 31, 13, 49, BG_ROCK)
    L.carve(7, 49, 9, 52, BG_ROCK)
    L.carve(7, 53, 13, 57, BG_PANEL)
    L.put("checkpoint", 64, 34)

    # Soldier: west along the corridor.
    L.put("soldier", 57, 42)
    L.put("ammo_pack", 54, 42)
    L.put("super_soldier", 48, 42)
    L.put("soldier", 36, 42)
    L.put("turret", 30, 38)
    L.put("health_kit", 26, 42)
    L.put("robodog", 22, 42)
    L.put("camera", 50, 38, facing="left")

    # Hacker: computer 5 sits by the landing, switch 2 darkens the laser, USB 5
    # waits on a pillar at the far west and door 5 shuts the last stretch.
    L.put("computer", 60, 34, number=5)
    L.put("switch", 54, 34, number=2)
    L.put("laser", 48, 34, number=2)
    L.rect(18, 33, 19, 34, STEEL)
    L.put("usb_key", 18, 32, number=5)
    L.put("infra_goggles", 34, 34)
    for y in range(31, 35):
        L.put("security_door", 15, y, number=5)
    L.put("closet", 40, 34)
    L.put("camera", 46, 31, facing="right")
    L.put("mobile", 26, 34)
    L.lights([(24, 32), (44, 32), (60, 32), (30, 39), (52, 39)])


def tier_c():
    L.carve(14, 46, 62, 49, BG_MESH)
    L.carve(15, 53, 61, 57, BG_PANEL)
    L.rect(14, 53, 14, 57, DESTRUCTIBLE_STEEL)
    L.rect(62, 53, 62, 57, DESTRUCTIBLE_STEEL)
    L.carve(63, 46, 76, 57, BG_CIRCUIT)
    L.put("checkpoint", 11, 49)

    # Soldier: the last corridor, the hardest.
    L.put("super_soldier", 22, 57)
    L.put("ammo_pack", 18, 57)
    L.put("ultimate_soldier", 32, 57)
    L.set(38, 57, SANDBAGS)
    L.put("turret", 46, 53)
    L.put("robodog", 44, 57)
    L.put("health_kit", 40, 57)
    L.put("soldier", 52, 57)
    L.put("landmine", 48, 57)
    L.put("camera", 28, 53, facing="right")

    # Hacker: two computers by the entry and both USB keys far down the vent, so
    # each hack means a walk back. Switch 4 darkens laser 4 on the way.
    L.put("computer", 15, 49, number=3)
    L.put("computer", 16, 49, number=6)
    L.put("switch", 26, 49, number=4)
    L.put("laser", 32, 49, number=4)
    L.rect(44, 48, 45, 49, STEEL)
    L.put("usb_key", 44, 47, number=6)
    for y in range(46, 50):
        L.put("security_door", 50, y, number=6)
    L.rect(54, 48, 55, 49, STEEL)
    L.put("usb_key", 54, 47, number=3)
    for y in range(46, 50):
        L.put("security_door", 59, y, number=3)
    L.put("closet", 61, 49)
    L.put("camera", 38, 46, facing="left")
    L.put("diamond", 40, 49)
    L.lights([(24, 47), (46, 47), (60, 47), (28, 54), (50, 54)])


def vault():
    L.rect(66, 56, 67, 56, HIDDEN)
    L.rect(70, 54, 71, 54, HIDDEN)
    L.rect(66, 52, 67, 52, HIDDEN)
    L.put("admin_password", 66, 51)
    L.put("exit_door", 75, 57)
    L.put("checkpoint", 64, 57)
    L.put("deco_machine", 69, 57)
    L.put("deco_machine", 73, 57)
    L.put("deco_clock", 72, 49)
    L.put("letter_e", 71, 53)
    L.put("notebook", 74, 57)
    L.lights([(66, 47), (72, 47), (70, 55)])


def build():
    shell()
    courtyard()
    tier_a()
    tier_b()
    tier_c()
    vault()


if __name__ == "__main__":
    build()
    L.cli("moscow.json")
