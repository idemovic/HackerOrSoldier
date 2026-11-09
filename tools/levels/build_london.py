"""Builds levels/london.json, the small first level: a bank tower on the Thames.

The hall is two storeys tall. The soldier shoots through the foyer wall and
walks the ground floor past three guards. The hacker climbs the foyer ledges
to the gallery, fetches the USB key from the gallery's far end, hacks computer 1 and walks
through security door 1. Both routes meet in the east shaft and climb to the
trading floor, where the admin password sits on a dais before the exit.

    python tools/levels/build_london.py [--preview]
"""

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from levelkit import *  # noqa: E402,F403

L = Level(
    "Thames Trading Floor",
    "White Silence keeps its first password in a bank tower on the Thames, "
    "on a trading floor that never sleeps. Shoot through the foyer wall and "
    "walk the lobby past three guards, or climb to the gallery, fetch the USB "
    "key from its far end and hack computer 1 to open the security door. Both routes meet in "
    "the stairwell. Take the admin password from the trading floor, then "
    "leave by the exit.",
    "night", 72, 36)


def tower():
    L.rect(1, 6, 70, 34, STONE)
    L.border(WALL)
    L.rect(1, 6, 70, 8, BRICK)
    # ground hall, two storeys tall, with the foyer at its west end
    L.carve(1, 22, 59, 32, BG_BRICK)
    L.carve(60, 10, 67, 32, BG_HANGAR)
    # trading floor above, joined to the shaft
    L.carve(1, 10, 59, 18, BG_CIRCUIT)


def foyer_climb():
    # Dubai-style ledges, four rows apart per side, rising to the gallery.
    L.rect(1, 31, 3, 31, GIRDER)
    L.rect(6, 29, 8, 29, GIRDER)
    L.rect(1, 27, 3, 27, GIRDER)
    L.rect(6, 25, 59, 25, GIRDER)
    L.rect(11, 26, 11, 32, DESTRUCTIBLE)


def lobby():
    L.put("agent_start", 5, 32)
    L.put("closet", 9, 32)
    L.put("soldier", 22, 32)
    L.put("ammo_pack", 15, 32)
    L.set(30, 32, SANDBAGS)
    L.put("soldier", 38, 32)
    L.put("landmine", 46, 32)
    L.put("fighter", 52, 32)
    L.put("health_kit", 57, 32)
    L.put("camera", 34, 22, facing="left")


def gallery():
    L.put("usb_key", 46, 24, number=1)
    L.put("computer", 10, 24, number=1)
    for y in (22, 23, 24):
        L.put("security_door", 52, y, number=1)
    L.put("diamond", 56, 24)
    L.put("cash_stack", 20, 24)
    L.put("camera", 22, 22, facing="right")


def stairwell():
    L.rect(65, 31, 67, 31, GIRDER)
    L.rect(60, 29, 62, 29, GIRDER)
    L.rect(65, 27, 67, 27, GIRDER)
    L.rect(60, 25, 62, 25, GIRDER)
    L.rect(65, 23, 67, 23, GIRDER)
    L.rect(60, 21, 62, 21, GIRDER)
    L.put("checkpoint", 62, 24)


def trading_floor():
    L.rect(1, 19, 59, 21, CONCRETE)
    L.rect(24, 17, 28, 18, STEEL)
    L.put("admin_password", 26, 16)
    L.put("exit_door", 3, 18)
    L.put("letter_g", 46, 18)
    L.put("business_card", 14, 18)
    L.put("deco_clock", 20, 12)
    L.put("deco_table", 34, 18)
    L.put("deco_table", 10, 18)
    L.put("deco_machine", 52, 18)
    L.put("deco_machine", 16, 18)
    L.put("deco_poster", 8, 14)


def dressing():
    L.lights([(8, 23), (24, 23), (40, 23), (56, 23), (64, 12), (12, 12), (30, 12), (50, 12), (20, 30), (48, 30)])
    L.put("deco_plant", 3, 32)
    L.put("deco_crate_wood", 24, 32)


def build():
    tower()
    foyer_climb()
    lobby()
    gallery()
    stairwell()
    trading_floor()
    dressing()


if __name__ == "__main__":
    build()
    L.cli("london.json")
