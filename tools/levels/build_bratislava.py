"""Builds levels/bratislava.json, the night-shift server farm on the Danube.

From the quay the soldier crosses the bridge, shoots through the hall wall and
fights down the server hall past a laser and its switch. The hacker dives off
the bridge into the Danube, swims into the cable duct under the hall, hacks
computer 1 with a USB key from the far shelf and walks the dry duct past a
second laser. Both routes meet in the east shaft and climb to the roof, where
the admin password sits on a water tank before the exit.

    python tools/levels/build_bratislava.py [--preview]
"""

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from levelkit import *  # noqa: E402,F403

L = Level(
    "Danube Night Shift",
    "A quiet server farm on the Danube relays the syndicate's traffic across "
    "Europe, and the night shift knows nothing. The guards know everything. "
    "Cross the bridge and shoot your way down the server hall, switching off "
    "its laser from afar, or dive into the Danube and swim into the cable duct "
    "below, where a USB key, a computer and a security door keep the way quiet. "
    "Both routes climb the east shaft to the roof, where the admin password "
    "waits on the water tank.",
    "night", 80, 40)


def shell():
    L.rect(1, 29, 78, 38, CONCRETE)
    L.rect(13, 17, 78, 28, BRICK)
    L.rect(76, 12, 78, 16, BRICK)
    L.border(WALL)
    L.carve(14, 20, 67, 28, BG_CIRCUIT)
    L.carve(13, 33, 67, 36, BG_ROCK)
    L.carve(68, 8, 75, 36, BG_MESH)
    L.rect(13, 20, 13, 28, DESTRUCTIBLE_RED)
    L.rect(67, 20, 67, 28, DESTRUCTIBLE_STEEL)


def river():
    L.carve(5, 29, 12, 37)
    L.rect(5, 36, 12, 37, WATER)
    L.rect(5, 29, 8, 29, GIRDER)
    L.rect(11, 29, 12, 29, GIRDER)
    L.put("agent_start", 3, 28)
    L.put("closet", 2, 28)
    L.put("deco_crate_wood", 4, 28)
    L.put("letter_a", 9, 28)
    L.put("sim_card", 7, 37)


def hall():
    L.put("soldier", 22, 28)
    L.put("ammo_pack", 18, 28)
    L.set(28, 28, SANDBAGS)
    L.put("soldier", 34, 28)
    L.put("camera", 30, 20, facing="left")
    L.put("switch", 40, 28, number=2)
    L.put("laser", 44, 28, number=2)
    L.put("robodog", 50, 28)
    L.put("health_kit", 47, 28)
    L.put("landmine", 54, 28)
    L.set(57, 28, SANDBAGS)
    L.put("fighter", 16, 28)
    L.put("super_soldier", 60, 28)
    L.put("business_card", 65, 28)
    for x in (16, 26, 38, 52, 62):
        L.put("deco_machine", x, 28)
    L.lights([(18, 21), (30, 21), (42, 21), (54, 21), (64, 21)])


def duct():
    L.put("computer", 16, 36, number=1)
    L.rect(35, 35, 36, 36, STEEL)
    L.put("usb_key", 35, 34, number=1)
    for y in range(33, 37):
        L.put("security_door", 46, y, number=1)
    L.put("camera", 28, 33, facing="right")
    L.put("switch", 54, 36, number=3)
    L.put("laser", 58, 36, number=3)
    L.put("cash_stack", 62, 36)
    L.put("mobile", 25, 36)
    L.lights([(20, 34), (40, 34), (56, 34), (65, 34)])


def shaft():
    for y in (37, 33, 29, 25, 21, 17):
        L.rect(68, y, 70, y, GIRDER)
    for y in (35, 31, 27, 23, 19):
        L.rect(73, y, 75, y, GIRDER)
    L.put("checkpoint", 69, 28)


def roof():
    L.rect(38, 15, 42, 16, STEEL)
    L.put("admin_password", 40, 14)
    L.put("exit_door", 15, 16)
    L.put("checkpoint", 66, 16)
    L.put("diamond", 41, 14)
    L.put("camera", 52, 12, facing="left")
    L.put("letter_g", 28, 16)
    for x in (22, 48, 58):
        L.put("deco_machine", x, 16)
    L.put("deco_crate_steel", 32, 16)
    L.lights([(18, 12), (34, 12), (50, 12), (62, 12)])


def build():
    shell()
    river()
    hall()
    duct()
    shaft()
    roof()


if __name__ == "__main__":
    build()
    L.cli("bratislava.json")
