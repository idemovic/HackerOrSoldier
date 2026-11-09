"""Builds levels/mexico-city.json, the 80x80 cartel cache under the old city.

The agent starts in a drainage outfall at the bottom and climbs three tiers
of mine tunnel to the plaza. On every tier the way forks: a straight tunnel
of cartel guards behind destructible walls for the soldier, or a quiet drain
beside it that needs USB keys, lasers and goggles. Both rejoin in a chamber
at the tier's end, and a climbing shaft leads to the next tier.

    python tools/levels/build_mexico_city.py [--preview]
"""

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from levelkit import *  # noqa: E402,F403

L = Level(
    "Cartel Cache",
    "Under the old city the cartel keeps the syndicate's weapons in a maze of mine tunnels. "
    "Start in the drainage outfall at the bottom and climb three levels to the plaza. "
    "On each level the soldier shoots through the guarded tunnel past the cartel's gunmen, "
    "while the hacker takes the quiet drain beside it and trades USB keys, laser switches and the goggles "
    "for locked doors. The password in the vault gets you through without a war, if you can manage it.",
    "night", 80, 80)


class Tier:
    """A tier drawn in west-flow coordinates: a vestibule at the east end, a
    guarded tunnel and a drain running west, an end chamber at the west end.
    flip mirrors it so the tier flows east. yv is the vestibule's top row."""

    def __init__(self, yv, flip):
        self.yv, self.flip = yv, flip
        self.g = yv + 5            # the tunnel's walking row
        self.lane = yv + 13        # the drain's walking row

    def x(self, x):
        return 79 - x if self.flip else x

    def rect(self, x0, y0, x1, y1, tile):
        a, b = sorted((self.x(x0), self.x(x1)))
        L.rect(a, y0, b, y1, tile)

    def carve(self, x0, y0, x1, y1, bg=BG_ROCK):
        a, b = sorted((self.x(x0), self.x(x1)))
        L.carve(a, y0, b, y1, bg)

    def put(self, kind, x, y, **props):
        if self.flip and props.get("facing"):
            props["facing"] = "left" if props["facing"] == "right" else "right"
        L.put(kind, self.x(x), y, **props)

    def doors(self, x, number):
        for y in (self.lane - 2, self.lane - 1, self.lane):
            self.put("security_door", x, y, number=number)

    def posts(self, xs):
        for x in xs:
            self.rect(x, self.yv + 1, x, self.yv + 5, EMPTY)
            self.rect(x, self.yv + 1, x, self.yv + 1, MINE_TIMBER)

    def pit(self, x0, x1, origin):
        """A lava pit in the drain's floor, crossed by a moving platform."""
        self.rect(x0, self.lane + 1, x1, self.lane + 1, LAVA)
        self.put("moving_platform", origin, self.lane)

    def rooms(self, hatch=False, chamber_bg=BG_PANEL):
        yv = self.yv
        self.carve(66, yv, 77, yv + 5)
        self.rect(66, yv + 6, 77, yv + 6, MINE_TIMBER)
        if hatch:
            self.carve(68, yv + 6, 69, yv + 13)
        else:
            self.carve(68, yv + 6, 75, yv + 6)
        self.carve(18, yv + 1, 64, yv + 5)
        self.carve(18, yv + 11, 69, yv + 13)
        self.rect(65, yv + 1, 65, yv + 5, DESTRUCTIBLE_STONE)
        self.carve(4, yv, 17, yv + 13, chamber_bg)
        self.rect(4, yv + 14, 17, yv + 14, STEEL)
        self.rect(17, yv + 1, 17, yv + 5, DESTRUCTIBLE_STEEL)
        self.put("light", 70, yv + 2)
        for x in (62, 50, 40, 30, 20):
            self.put("light", x, yv + 2)
        for x in (64, 52, 40, 28):
            self.put("light", x, self.lane - 1)
        self.put("light", 8, yv + 3)
        self.put("light", 14, yv + 3)


def ground():
    L.rect(1, 16, 78, 78, MINE_ROCK)
    L.rect(1, 15, 78, 15, STONE)
    L.border(WALL)
    L.bg_rect(1, 5, 17, 14, BG_BRICK)
    L.bg_rect(24, 4, 44, 14, BG_BRICK)
    L.bg_rect(48, 6, 78, 14, BG_BRICK)
    L.put("deco_clock", 34, 9)
    L.put("deco_poster", 54, 12)
    L.put("deco_plant", 20, 14)
    L.put("deco_plant", 46, 14)
    L.put("deco_crate_wood", 62, 14)
    L.put("deco_table", 12, 14)
    L.rect(26, 14, 28, 14, SANDBAGS)
    for x in (8, 20, 33, 47, 60):
        L.put("light", x, 9)
    L.put("letter_a", 40, 14)
    L.put("business_card", 8, 14)


def shaft(side, bottom_stand, top_row, r_rows, l_rows):
    """A climbing shaft x 70..75 (east) or 4..9 (west). The right ledges are
    the outer three columns, the left ledges the inner three."""
    t = Tier(0, flip=(side == "west"))
    t.carve(68, top_row, 75, bottom_stand)
    for y in r_rows:
        t.rect(73, y, 75, y, MINE_TIMBER)
    for y in l_rows:
        t.rect(68, y, 70, y, MINE_TIMBER)
    return t


def tier_a():
    # Bottom tier: start in the outfall and flow east.
    t = Tier(55, flip=True)
    t.rooms(hatch=True, chamber_bg=BG_CIRCUIT)
    g, lane = t.g, t.lane
    t.put("agent_start", 75, g)
    t.put("checkpoint", 72, g)
    t.rect(72, g - 1, 74, g - 1, MINE_TIMBER)
    t.put("usb_key", 73, g - 2, number=7)
    t.put("deco_crate_wood", 69, g)
    t.put("closet", 77, g)
    t.posts((22, 40, 58))
    t.put("weapon_minigun", 62, g)
    t.put("soldier", 60, g)
    t.put("robodog", 57, g)
    t.rect(53, 56, 53, 60, DESTRUCTIBLE_STONE)
    t.put("super_soldier", 49, g)
    t.put("turret", 46, 56)
    t.put("ultimate_soldier", 44, g)
    t.rect(42, g, 42, g, SANDBAGS)
    t.put("health_kit", 43, g)
    t.put("fighter", 40, g)
    t.put("landmine", 37, g)
    t.rect(35, 56, 35, 60, DESTRUCTIBLE_STEEL)
    t.put("ammo_pack", 38, g)
    t.put("weapon_dynamite", 56, g)
    t.put("ultimate_soldier", 30, g)
    t.put("super_soldier", 27, g)
    t.put("turret", 24, 56)
    t.put("drone", 21, 58)
    t.put("ammo_pack", 33, g)
    t.put("ammo_pack", 61, g)
    t.put("flame_trap", 48, 56)
    t.put("camera", 39, 56)
    t.put("deco_machine", 54, g)
    t.put("cash_stack", 29, 57)

    t.put("computer", 65, lane, number=7)
    t.doors(61, 7)
    t.put("infra_goggles", 56, lane)
    t.carve(40, 62, 47, 65)
    t.rect(43, lane - 1, 44, lane - 1, HIDDEN)
    t.rect(45, lane - 3, 46, lane - 3, HIDDEN)
    t.rect(41, lane - 5, 44, lane - 5, MINE_TIMBER)
    t.put("usb_key", 42, lane - 6, number=8)
    t.put("sim_card", 41, lane - 6)
    t.put("computer", 34, lane, number=8)
    t.put("laser", 30, lane, number=8)
    t.put("closet", 33, lane)
    t.pit(22, 29, 25)
    t.put("camera", 31, lane - 2, facing="left")
    t.put("audio_recorder", 20, lane - 2)
    t.put("health_kit", 20, lane)
    t.put("deco_machine", 58, lane)
    # The chamber at the tier's east end.
    t.put("checkpoint", 6, lane)
    t.put("deco_machine", 8, lane)
    t.put("deco_crate_steel", 12, lane)
    t.put("letter_g", 14, 56)


def tier_b():
    t = Tier(38, flip=False)
    t.rooms()
    g, lane = t.g, t.lane
    t.put("mobile", 72, 40)
    t.rect(71, 41, 73, 41, MINE_TIMBER)
    t.posts((22, 40, 58))
    t.put("weapon_dynamite", 66, g)
    t.put("ultimate_soldier", 59, g)
    t.put("super_soldier", 56, g)
    t.rect(53, 39, 53, 43, DESTRUCTIBLE_STONE)
    t.put("fighter", 48, g)
    t.put("soldier", 45, g)
    t.rect(43, g, 43, g, SANDBAGS)
    t.put("robodog", 40, g)
    t.put("super_soldier", 37, g)
    t.put("ammo_pack", 47, g)
    t.put("health_kit", 39, g)
    t.rect(34, 39, 34, 43, DESTRUCTIBLE_STONE)
    t.put("turret", 28, 39)
    t.put("soldier", 25, g)
    t.put("landmine", 28, g)
    t.put("ammo_pack", 21, g)
    t.put("cash_stack", 36, 40)
    t.put("flame_trap", 58, 39)
    t.put("camera", 45, 39)
    t.put("deco_crate_steel", 48, g)

    t.put("computer", 65, lane, number=4)
    t.doors(61, 4)
    t.put("closet", 63, lane)
    t.put("laser", 55, lane, number=5)
    t.put("switch", 52, lane, number=5)
    t.put("notebook", 43, lane - 2)
    t.put("camera", 56, lane - 2, facing="left")
    t.rect(40, lane, 46, lane, ELECTRIC)
    t.put("floor_circuit", 40, lane, number=6)
    t.put("computer", 48, lane, number=6)
    t.carve(49, 45, 54, 48)
    t.rect(49, 50, 50, 50, MINE_TIMBER)
    t.rect(52, 48, 54, 48, MINE_TIMBER)
    t.put("usb_key", 53, 47, number=6)
    t.put("letter_e", 50, 46)
    t.put("health_kit", 58, lane)
    t.pit(23, 30, 26)
    # The chamber at the tier's west end.
    t.put("checkpoint", 14, lane)
    t.put("deco_machine", 16, lane)
    t.put("deco_crate_steel", 12, lane)
    t.put("letter_n", 14, 40)


def tier_c():
    t = Tier(21, flip=True)
    t.rooms(chamber_bg=BG_CIRCUIT)
    g, lane = t.g, t.lane
    t.posts((22, 40, 58))
    t.put("soldier", 60, g)
    t.put("fighter", 57, g)
    t.put("ammo_pack", 62, g)
    t.put("super_soldier", 54, g)
    t.rect(51, 22, 51, 26, DESTRUCTIBLE_STONE)
    t.put("weapon_assault_rifle", 53, g)
    t.put("robodog", 46, g)
    t.put("landmine", 43, g)
    t.rect(41, g, 41, g, SANDBAGS)
    t.put("health_kit", 42, g)
    t.put("ultimate_soldier", 38, g)
    t.put("turret", 35, 22)
    t.put("super_soldier", 33, g)
    t.rect(29, 22, 29, 26, DESTRUCTIBLE_STONE)
    t.put("ammo_pack", 31, g)
    t.put("ultimate_soldier", 24, g)
    t.put("soldier", 21, g)
    t.put("flame_trap", 46, 22)
    t.put("camera", 37, 22)
    t.put("diamond", 55, 23)
    t.put("deco_crate_steel", 59, g)

    t.put("computer", 65, lane, number=1)
    t.doors(61, 1)
    t.put("laser", 56, lane, number=2)
    t.put("switch", 53, lane, number=2)
    t.put("infra_goggles", 50, lane)
    t.carve(44, 28, 52, 31)
    t.rect(45, lane - 1, 46, lane - 1, HIDDEN)
    t.rect(48, 31, 52, 31, MINE_TIMBER)
    t.put("usb_key", 50, 30, number=3)
    t.put("briefcase", 51, 30)
    t.put("computer", 36, lane, number=3)
    t.doors(31, 3)
    t.put("closet", 33, lane)
    t.pit(23, 30, 26)
    t.put("camera", 31, lane - 2, facing="left")
    t.put("sim_card", 21, lane - 2)
    t.put("health_kit", 20, lane)
    t.put("deco_machine", 58, lane)


def vault():
    # The chamber at tier C's east end holds the password up on steel steps.
    for y, x0, x1 in ((33, 62, 64), (31, 66, 68), (29, 62, 64), (27, 66, 68)):
        L.rect(x0, y, x1, y, STEEL)
    L.put("admin_password", 67, 26)
    L.put("checkpoint", 66, 34)
    L.put("deco_machine", 63, 34)
    L.put("deco_crate_steel", 68, 34)
    L.put("diamond", 63, 28)
    L.put("letter_t", 64, 32)


def shafts():
    # Tier A to B: east shaft from the vault floor up through the drain level.
    ab = shaft("east", 68, 44, [67, 63, 59, 55, 51, 47], [65, 61, 57, 53, 49, 45])
    ab.put("usb_key", 69, 56, number=4)
    # Tier B to C: west shaft.
    bc = shaft("west", 51, 28, [50, 46, 42, 38, 34, 30], [48, 44, 40, 36, 32, 28])
    bc.put("usb_key", 69, 39, number=1)
    # Tier C up to the plaza.
    shaft("east", 34, 16, [33, 29, 25, 21, 17], [31, 27, 23, 19])
    L.carve(68, 15, 75, 15)
    L.put("exit_door", 77, 14)
    L.put("deco_plant", 68, 14)


def build():
    ground()
    tier_a()
    tier_b()
    tier_c()
    shafts()
    vault()


if __name__ == "__main__":
    build()
    L.cli("mexico-city.json")
