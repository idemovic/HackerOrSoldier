"""Builds levels/istanbul.json, the Bosphorus harbour and its records office.

A catwalk on cranes crosses the harbour, humped over two crane gaps: the
soldier runs it past the guards. The hacker dives into the water, fetches a
USB key from the seabed and hacks computer 1 on the first sluice pier to open
its door, then repeats it at the second pier and swims on into the flooded
cellar of the records office. Both routes meet in the office stairwell and
climb to the archive, where the admin password sits on a dais before the exit.

    python tools/levels/build_istanbul.py [--preview]
"""

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from levelkit import *  # noqa: E402,F403

L = Level(
    "Bosphorus Records",
    "Where Europe meets Asia, White Silence launders its money through a "
    "shipping company on the Bosphorus. Its records office holds the next "
    "password. Run the crane catwalk over the harbour past the dock guards "
    "and shoot your way through the office, or dive in and swim: hack the "
    "doors on two sluice piers with USB keys from the seabed and swim on "
    "into the flooded cellar. Both routes climb the stairwell to the archive.",
    "day", 80, 68)

DY = 24


class Ground:
    """The harbour and the office ground floor, drawn at the coordinates of a
    44-row map and moved down DY rows to make room for the tall stairwell."""

    def rect(self, x0, y0, x1, y1, tile):
        L.rect(x0, y0 + DY, x1, y1 + DY, tile)

    def set(self, x, y, tile):
        L.set(x, y + DY, tile)

    def bg_rect(self, x0, y0, x1, y1, tile=BG_PANEL):
        L.bg_rect(x0, y0 + DY, x1, y1 + DY, tile)

    def carve(self, x0, y0, x1, y1, bg=EMPTY):
        L.carve(x0, y0 + DY, x1, y1 + DY, bg)

    def put(self, kind, x, y, **props):
        L.put(kind, x, y + DY, **props)

    def lights(self, cells):
        L.lights([(x, y + DY) for x, y in cells])


G = Ground()


def harbour():
    G.rect(1, 33, 78, 42, CONCRETE)
    G.rect(1, 33, 13, 33, STONE)
    L.border(WALL)
    G.carve(14, 32, 53, 41)
    G.rect(14, 32, 53, 41, WATER)
    # Two sluice piers wall the basin from the seabed up to the catwalk. The
    # only way across is the walkway on top, shut by a security door.
    for x0, number in ((32, 1), (44, 3)):
        G.rect(x0, 32, x0 + 4, 41, STEEL)
        G.put("computer", x0 + 1, 31, number=number)
        for y in (30, 31):
            G.put("security_door", x0 + 3, y, number=number)
    # crane background: towers and a jib behind the catwalk
    for x in (16, 36, 48):
        G.bg_rect(x, 8, x + 1, 28, BG_MESH)
    G.bg_rect(16, 8, 49, 9, BG_MESH)
    G.bg_rect(18, 10, 47, 10, BG_MESH)


def quay_west():
    G.put("agent_start", 3, 32)
    G.put("closet", 2, 32)
    G.rect(7, 31, 9, 32, CONTAINER)
    G.put("deco_crate_wood", 5, 32)
    G.put("deco_crate_steel", 11, 32)
    G.put("letter_a", 8, 30)
    G.rect(12, 31, 13, 31, GIRDER)


def catwalk():
    G.rect(14, 29, 19, 29, GIRDER)
    G.rect(30, 29, 36, 29, GIRDER)
    G.rect(44, 29, 51, 29, GIRDER)
    for x in (16, 51):
        G.rect(x, 30, x, 31, STEEL)
    G.rect(52, 31, 53, 31, GIRDER)
    # the crane hump bridging the gap in the catwalk
    G.rect(20, 27, 21, 27, GIRDER)
    G.rect(23, 25, 26, 25, GIRDER)
    G.rect(28, 27, 29, 27, GIRDER)
    G.rect(37, 27, 38, 27, GIRDER)
    G.rect(40, 25, 41, 25, GIRDER)
    G.rect(42, 27, 43, 27, GIRDER)

    G.put("ammo_pack", 17, 28)
    G.put("soldier", 24, 24)
    G.put("cash_stack", 25, 24)
    G.rect(33, 27, 33, 28, CONTAINER)
    G.put("super_soldier", 31, 28)
    G.put("health_kit", 35, 28)
    G.put("super_soldier", 40, 24)
    G.put("robodog", 46, 28)
    G.put("soldier", 49, 28)
    G.rect(47, 27, 47, 28, CONTAINER)
    G.put("camera", 30, 22, facing="left")


def seabed():
    G.put("usb_key", 26, 41, number=1)
    G.put("sim_card", 17, 41)
    G.put("usb_key", 40, 41, number=3)
    G.put("checkpoint", 56, 32)
    G.put("diamond", 51, 41)


def cellar():
    G.carve(54, 38, 57, 41)
    G.rect(54, 38, 57, 41, WATER)
    G.carve(58, 35, 77, 41, BG_ROCK)
    G.rect(58, 38, 69, 41, WATER)
    G.rect(70, 39, 77, 41, CONCRETE)
    G.put("mobile", 75, 38)
    G.put("camera", 64, 35, facing="right")


def office():
    L.rect(57, 3, 78, 44, BRICK)
    G.carve(58, 27, 68, 32, BG_PANEL)
    G.rect(56, 27, 57, 32, DESTRUCTIBLE_RED)
    G.rect(67, 27, 69, 32, DESTRUCTIBLE_STEEL)
    G.put("soldier", 61, 32)
    G.put("ammo_pack", 59, 32)
    G.put("fighter", 64, 32)
    G.put("deco_table", 62, 32)
    G.put("deco_plant", 66, 32)
    G.put("camera", 62, 27, facing="left")
    G.lights([(60, 28), (65, 28)])


def stairwell():
    ground = 32 + DY
    assert (ground + 1) % 4 == 1, "ledge rows must line up with the archive floor"
    L.carve(70, 5, 77, ground + 6, BG_HANGAR)
    L.rect(70, ground + 7, 77, ground + 9, CONCRETE)
    for y in range(ground + 5, 12, -4):
        L.rect(70, y, 72, y, GIRDER)
    for y in range(ground + 3, 14, -4):
        L.rect(75, y, 77, y, GIRDER)
    L.put("checkpoint", 71, ground)
    L.put("checkpoint", 71, 12)


def archive():
    L.carve(58, 5, 69, 10, BG_BRICK)
    L.rect(62, 9, 64, 10, STEEL)
    L.put("admin_password", 63, 8)
    L.put("exit_door", 59, 10)
    L.rect(67, 9, 67, 10, STEEL)
    L.put("letter_e", 66, 10)
    L.put("notebook", 68, 10)
    for x in (61, 65):
        L.put("deco_crate_wood", x, 10)
    L.put("deco_clock", 60, 6)
    L.put("deco_poster", 65, 6)
    L.lights([(60, 6), (64, 6), (68, 6)])


def build():
    harbour()
    quay_west()
    catwalk()
    seabed()
    cellar()
    office()
    stairwell()
    archive()


if __name__ == "__main__":
    build()
    L.cli("istanbul.json")
