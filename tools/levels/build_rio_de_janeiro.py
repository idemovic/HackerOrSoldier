"""Builds levels/rio-de-janeiro.json, the 80x72 favela hill under a day sky.

Three terraces climb the hill, each reached by a stairwell from the one
below. On every terrace the soldier shoots his way along the street past the
gang's guards, while the hacker climbs onto the rooftops above and works
across them with USB keys, lasers and the goggles. Both meet again at the
stairwell. The last stairwell leads to the relay hut on top of the hill.

    python tools/levels/build_rio_de_janeiro.py [--preview]
"""

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from levelkit import *  # noqa: E402,F403

L = Level(
    "Favela Relay",
    "A relay hut on the top of Rio's hill talks to the Antarctic base, and the favela below is the gang's. "
    "Climb three terraces by their stairwells to the roof of the hill. On every terrace the street is for the "
    "soldier, who shoots through the gang's gunmen and their barricades, and the rooftops are for the hacker, who "
    "crosses them with USB keys, laser switches and the goggles for the hidden planks. The relay's password "
    "opens the way south.",
    "day", 80, 72)

S1, S2, S3 = 66, 46, 26


class Terrace:
    """A terrace in east-flow coordinates: a plaza at x 2..17, the street
    x 19..58 between two walls, a landing x 60..69 and a stairwell x 70..77.
    flip mirrors it so the terrace flows west."""

    def __init__(self, s, flip):
        self.s, self.flip = s, flip
        self.r = s - 8             # the rooftop's walking row

    def x(self, x):
        return 79 - x if self.flip else x

    def rect(self, x0, y0, x1, y1, tile):
        a, b = sorted((self.x(x0), self.x(x1)))
        L.rect(a, y0, b, y1, tile)

    def carve(self, x0, y0, x1, y1, bg=EMPTY):
        a, b = sorted((self.x(x0), self.x(x1)))
        L.carve(a, y0, b, y1, bg)

    def bg(self, x0, y0, x1, y1, tile):
        a, b = sorted((self.x(x0), self.x(x1)))
        L.bg_rect(a, y0, b, y1, tile)

    def put(self, kind, x, y, **props):
        if self.flip and props.get("facing"):
            props["facing"] = "left" if props["facing"] == "right" else "right"
        L.put(kind, self.x(x), y, **props)

    def doors(self, x, number):
        for y in range(self.r - 3, self.r + 1):
            self.put("security_door", x, y, number=number)

    def slab(self, x0, x1):
        self.rect(x0, self.s - 7, x1, self.s - 6, BRICK)
        self.bg(x0, self.s - 5, x1, self.s, BG_BRICK)

    def bridge(self, x0, x1):
        self.rect(x0, self.s - 7, x1, self.s - 7, HIDDEN)

    def ledge(self, x0, x1, row):
        self.rect(x0, row, x1, row, GIRDER)

    def base(self):
        s = self.s
        self.rect(2, s + 1, 77, s + 4, STONE)
        self.rect(18, s - 3, 18, s, DESTRUCTIBLE_RED)
        self.rect(59, s - 3, 59, s, DESTRUCTIBLE_RED)
        self.put("checkpoint", 64, s)
        for x in (24, 34, 44, 54):
            self.put("light", x, s - 4)
        # The ledges from the plaza up to the roof.
        self.ledge(10, 12, s - 1)
        self.ledge(13, 15, s - 3)
        self.ledge(10, 12, s - 5)
        self.ledge(13, 15, s - 7)


def stairwell(s_low, s_high, flip):
    """The stairwell x 70..77 from a landing at s_low up to the plaza floor of
    the terrace above (stand row s_high)."""
    t = Terrace(s_low, flip)
    t.carve(70, s_high + 1, 77, s_low)
    t.carve(67, s_high + 1, 69, s_high + 1)
    k = 0
    for row in range(s_low - 1, s_high + 2, -2):
        if k % 2 == 0:
            t.rect(70, row, 72, row, GIRDER)
        else:
            t.rect(75, row, 77, row, GIRDER)
        k += 1


def terrace_one():
    t = Terrace(S1, False)
    s, r = S1, S1 - 8
    t.base()
    t.put("agent_start", 4, s)
    t.put("deco_crate_wood", 7, s)
    t.put("deco_plant", 15, s)
    t.put("letter_a", 8, s - 3)
    # Street: the gang's block.
    t.put("soldier", 56, s)
    t.put("ammo_pack", 57, s)
    t.put("fighter", 52, s)
    t.put("super_soldier", 48, s)
    t.rect(45, s - 3, 45, s, DESTRUCTIBLE_RED)
    t.put("weapon_assault_rifle", 46, s)
    t.put("robodog", 41, s)
    t.put("landmine", 38, s)
    t.put("ultimate_soldier", 35, s)
    t.rect(33, s, 33, s, SANDBAGS)
    t.put("health_kit", 34, s)
    t.put("drone", 29, s - 3)
    t.put("super_soldier", 26, s)
    t.put("soldier", 22, s)
    t.put("ammo_pack", 21, s)
    t.put("deco_crate_steel", 31, s)
    # Rooftops: a hidden bridge between A and B, a short jump to C.
    for x0, x1 in ((16, 24), (33, 43), (47, 58)):
        t.slab(x0, x1)
    t.ledge(16, 18, r - 1)
    t.ledge(20, 22, r - 3)
    t.ledge(16, 18, r - 5)
    t.put("usb_key", 17, r - 6, number=1)
    t.put("infra_goggles", 19, r)
    t.put("computer", 23, r, number=1)
    t.bridge(27, 29)
    t.doors(34, 1)
    t.put("laser", 49, r, number=2)
    t.put("switch", 54, r, number=2)
    t.doors(58, 2)
    t.put("sim_card", 17, r - 2)
    t.put("diamond", 41, r - 4)
    t.put("closet", 26, s)


def terrace_two():
    t = Terrace(S2, True)
    s, r = S2, S2 - 8
    t.base()
    t.put("deco_crate_wood", 7, s)
    t.put("deco_plant", 15, s)
    t.put("letter_g", 12, s - 7)
    t.put("fighter", 56, s)
    t.put("soldier", 53, s)
    t.put("health_kit", 57, s)
    t.rect(50, s - 3, 50, s, DESTRUCTIBLE_RED)
    t.put("weapon_dynamite", 51, s)
    t.put("super_soldier", 47, s)
    t.put("ultimate_soldier", 43, s)
    t.rect(40, s, 40, s, SANDBAGS)
    t.put("landmine", 37, s)
    t.put("robodog", 34, s)
    t.rect(31, s - 3, 31, s, DESTRUCTIBLE_RED)
    t.put("drone", 28, s - 3)
    t.put("super_soldier", 25, s)
    t.put("ammo_pack", 29, s)
    t.put("ultimate_soldier", 22, s)
    t.put("deco_crate_steel", 36, s)
    for x0, x1 in ((16, 24), (28, 38), (42, 58)):
        t.slab(x0, x1)
    t.ledge(16, 18, r - 1)
    t.ledge(20, 22, r - 3)
    t.ledge(16, 18, r - 5)
    t.put("usb_key", 17, r - 6, number=4)
    t.put("computer", 23, r, number=4)
    t.doors(29, 4)
    t.put("infra_goggles", 31, r)
    t.put("laser", 53, r, number=5)
    t.put("switch", 56, r, number=5)
    t.put("notebook", 40, r - 1)
    t.put("health_kit", 57, r)
    t.put("closet", 24, s)


def terrace_three():
    t = Terrace(S3, False)
    s, r = S3, S3 - 8
    t.base()
    t.put("deco_crate_wood", 7, s)
    t.put("letter_n", 14, s - 7)
    t.put("ultimate_soldier", 56, s)
    t.put("super_soldier", 53, s)
    t.put("ammo_pack", 57, s)
    t.rect(50, s - 3, 50, s, DESTRUCTIBLE_STEEL)
    t.put("weapon_minigun", 51, s)
    t.put("fighter", 47, s)
    t.put("drone", 44, s - 3)
    t.put("super_soldier", 42, s)
    t.rect(39, s, 39, s, SANDBAGS)
    t.put("health_kit", 40, s)
    t.put("robodog", 36, s)
    t.put("landmine", 33, s)
    t.rect(31, s - 3, 31, s, DESTRUCTIBLE_STEEL)
    t.put("ultimate_soldier", 28, s)
    t.put("soldier", 25, s)
    t.put("super_soldier", 22, s)
    t.put("ammo_pack", 23, s)
    t.put("deco_machine", 35, s)
    for x0, x1 in ((16, 24), (28, 40), (44, 58)):
        t.slab(x0, x1)
    t.ledge(16, 18, r - 1)
    t.ledge(20, 22, r - 3)
    t.ledge(16, 18, r - 5)
    t.put("usb_key", 17, r - 6, number=7)
    t.put("computer", 23, r, number=7)
    t.doors(29, 7)
    t.put("infra_goggles", 31, r)
    t.bridge(41, 43)
    t.put("laser", 52, r, number=8)
    t.put("switch", 55, r, number=8)
    t.put("mobile", 49, r - 1)
    t.put("smartwatch", 36, r - 2)
    t.put("closet", 24, s)


def relay():
    # The hut on the top of the hill, above the last stairwell.
    L.rect(62, 7, 66, 7, STEEL)
    L.rect(62, 8, 69, 8, STEEL)
    L.bg_rect(60, 2, 69, 6, BG_BRICK)
    L.rect(60, 2, 61, 6, BRICK)
    L.put("admin_password", 63, 6)
    L.put("checkpoint", 68, 7)
    L.put("exit_door", 66, 6)
    L.put("deco_machine", 65, 6)
    L.put("deco_table", 62, 6)
    L.put("letter_t", 64, 4)
    L.rect(62, 5, 64, 5, GIRDER)
    L.put("light", 66, 3)


def hill():
    L.border(WALL)
    # Day-sky backdrop: facades up the hillside.
    for s in (S1, S2, S3):
        L.bg_rect(2, s - 13, 77, s - 1, BG_BRICK)
    L.rect(1, 68, 78, 70, STONE)


def build():
    hill()
    terrace_one()
    terrace_two()
    terrace_three()
    stairwell(S1, S2, False)
    stairwell(S2, S3, True)
    stairwell(S3, 6, False)
    relay()


if __name__ == "__main__":
    build()
    L.cli("rio-de-janeiro.json")
