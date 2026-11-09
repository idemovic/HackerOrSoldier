"""Builds levels/antarctica.json, the 110x80 finale: the base under the ice.

The agent starts on the snow above the base and works down through five
tiers: the surface camp, the ice caverns, the outer base, the inner base and
the machine room. On every tier the way forks. The soldier shoots through a
guarded tunnel; the hacker takes the drain beside it with USB keys, lasers,
hidden planks and teleports. Both meet in a chamber whose floor drops to the
next tier. The last chamber is the machine room: the admin password shuts
the machine down, and the exit is on its far side.

    python tools/levels/build_antarctica.py [--preview]
"""

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from levelkit import *  # noqa: E402,F403

W = 110
EXTRA = 30      # tiles added between the end chamber and the old tunnel layout

L = Level(
    "White Silence",
    "The base sits under two kilometres of ice, and the machine that can shut down every network on Earth runs at "
    "the bottom of it. Five tiers lead down: the surface camp, the ice caverns, the outer base, the inner base and "
    "the machine room. On each tier the soldier fights through the syndicate's best guards, and the hacker takes "
    "the quiet drain beside them with USB keys, laser switches, the goggles for the hidden planks and a teleport "
    "into a sealed cell. The admin password waits on a dais in the machine hall, above a pond of eels and "
    "piranhas and under two robots. It shuts the machine down for good. Then get out.",
    "night", W, 80)


class Tier:
    """A tier drawn in west-flow coordinates: a vestibule at the east end, a
    guarded tunnel and a drain running west, an end chamber at the west end.
    flip mirrors it so the tier flows east. yv is the vestibule top row."""

    wall_floor = STEEL

    def __init__(self, yv, flip, bg, wall, sky=False):
        self.yv, self.flip, self.bg, self.wall, self.sky = yv, flip, bg, wall, sky
        self.sh = EXTRA     # tier code is written for an 80 wide level; sh = 0 draws in final coordinates
        self.g = yv + 5
        self.lane = yv + 13

    def x(self, x):
        if x >= 18:
            x += self.sh
        return W - 1 - x if self.flip else x

    def rect(self, x0, y0, x1, y1, tile):
        a, b = sorted((self.x(x0), self.x(x1)))
        L.rect(a, y0, b, y1, tile)

    def carve(self, x0, y0, x1, y1, bg=None):
        a, b = sorted((self.x(x0), self.x(x1)))
        L.carve(a, y0, b, y1, self.bg if bg is None else bg)

    def put(self, kind, x, y, **props):
        if self.flip and props.get("facing"):
            props["facing"] = "left" if props["facing"] == "right" else "right"
        L.put(kind, self.x(x), y, **props)

    def doors(self, x, number):
        for y in (self.lane - 2, self.lane - 1, self.lane):
            self.put("security_door", x, y, number=number)

    def posts(self, xs, tile=MINE_TIMBER):
        for x in xs:
            self.rect(x, self.yv + 1, x, self.yv + 5, EMPTY)
            self.rect(x, self.yv + 1, x, self.yv + 1, tile)

    def pit(self, x0, x1, origin):
        """A lava pit in the drain floor, crossed by a moving platform."""
        self.rect(x0, self.lane + 1, x1, self.lane + 1, LAVA)
        self.put("moving_platform", origin, self.lane)

    def bridge(self, x0, x1):
        """A lava gap too wide to jump with a hidden plank bridge floating over it: it needs the goggles."""
        self.rect(x0, self.lane + 1, x1, self.lane + 1, LAVA)
        self.rect(x0 + 2, self.lane, x1 - 2, self.lane, HIDDEN)

    def trail(self, x, span, number):
        """A key corridor in the rock above the drain. The step up is at x and the USB key lies span tiles west
        of it, so the hacker walks there and back before the door."""
        yv = self.yv
        x = x + self.sh if x >= 18 else x
        sh, self.sh = self.sh, 0
        self.carve(x - span, yv + 8, x + 1, yv + 9)
        self.carve(x, yv + 10, x + 1, yv + 10)
        self.rect(x, yv + 12, x + 1, yv + 12, self.wall_floor)
        self.put("usb_key", x - span + 1, yv + 9, number=number)
        self.sh = sh

    def rooms(self, chamber_bg):
        yv = self.yv
        top = 1 if self.sky else yv
        self.carve(66, top, 77, yv + 5)
        self.rect(66, yv + 6, 77, yv + 6, self.wall_floor)
        self.carve(68, yv + 6, 69, yv + 13)
        self.carve(18, 1 if self.sky else yv + 1, 64, yv + 5)
        self.carve(18, yv + 11, 69, yv + 13)
        self.rect(65, yv + 1, 65, yv + 5, self.wall)
        self.carve(4, top, 17, yv + 13, chamber_bg)
        self.rect(4, yv + 14, 17, yv + 14, self.wall_floor)
        self.rect(17, yv + 1, 17, yv + 5, self.wall)
        for x in (62, 50, 40, 30, 20):
            self.put("light", x, yv + 2)
        for x in (64, 52, 40, 28):
            self.put("light", x, self.lane - 1)
        self.put("light", 8, yv + 3)
        self.put("light", 14, yv + 3)
        self.put("light", 72, yv + 2)
        sh, self.sh = self.sh, 0
        self.carve(18, 1 if self.sky else yv + 1, 47, yv + 5)
        self.carve(18, yv + 11, 47, yv + 13)
        self.sh = sh


def drop(yv_from, flip_from):
    """The hole in a tier end chamber floor that drops into the next tier vestibule."""
    x0, x1 = (4, 9) if not flip_from else (W - 10, W - 5)
    L.carve(x0, yv_from + 14, x1, yv_from + 14, BG_ROCK)


def ground():
    L.rect(1, 0, W - 2, 32, SNOW)
    L.rect(1, 33, W - 2, 62, CONCRETE)
    L.rect(1, 63, W - 2, 78, STEEL)
    L.border(WALL)
    L.rect(1, 0, W - 2, 8, EMPTY)
    L.rect(1, 9, W - 2, 9, SNOW)


def tier_one():
    t = Tier(3, False, BG_ROCK, DESTRUCTIBLE_SAND, sky=True)
    t.wall_floor = SNOW
    t.rooms(BG_ROCK)
    g, lane = t.g, t.lane
    t.put("agent_start", 74, g)
    t.put("checkpoint", 71, g)
    t.put("deco_crate_wood", 76, g)
    t.put("closet", 67, g)
    t.put("soldier", 61, g)
    t.put("fighter", 58, g)
    t.put("ammo_pack", 62, g)
    t.rect(55, g, 56, g, SANDBAGS)
    t.put("robodog", 53, g)
    t.rect(49, 4, 49, g, DESTRUCTIBLE_SAND)
    t.put("super_soldier", 46, g)
    t.put("drone", 44, 6)
    t.put("ultimate_soldier", 41, g)
    t.put("weapon_assault_rifle", 51, g)
    t.put("landmine", 38, g)
    t.put("health_kit", 43, g)
    t.rect(34, g, 34, g, SANDBAGS)
    t.put("super_soldier", 31, g)
    t.put("ultimate_soldier", 27, g)
    t.rect(24, 4, 24, g, DESTRUCTIBLE_SAND)
    t.put("drone", 21, 5)
    t.put("ammo_pack", 29, g)
    t.put("deco_crate_steel", 36, g)
    t.put("letter_a", 45, 4)

    t.put("computer", 65, lane, number=1)
    t.doors(61, 1)
    t.trail(63, 17, 1)
    t.put("laser", 57, lane, number=2)
    t.put("switch", 54, lane, number=2)
    t.put("infra_goggles", 51, lane)
    t.bridge(30, 37)
    t.pit(41, 48, 44)
    t.put("camera", 38, lane - 2, facing="left")
    t.put("sim_card", 33, lane - 2)
    t.put("health_kit", 22, lane)
    t.put("closet", 26, lane)
    t.put("diamond", 66, lane - 7)
    t.put("checkpoint", 14, lane)
    t.put("deco_machine", 8, lane)
    t.put("deco_crate_steel", 12, lane)
    t.put("letter_g", 14, 6)

    t.sh = 0
    t.put("light", 40, 6)
    t.put("light", 28, 6)
    t.rect(44, g, 45, g, SANDBAGS)
    t.put("super_soldier", 41, g)
    t.put("ultimate_soldier", 37, g)
    t.put("ammo_pack", 39, g)
    t.rect(33, 4, 33, g, DESTRUCTIBLE_SAND)
    t.put("robodog", 30, g)
    t.put("drone", 28, 5)
    t.put("ultimate_soldier", 25, g)
    t.put("health_kit", 22, g)
    t.pit(24, 31, 27)
    t.put("health_kit", 42, lane)
    t.put("closet", 38, lane)


def tier_two(yv, flip):
    t = Tier(yv, flip, BG_ROCK, DESTRUCTIBLE_SAND)
    t.wall_floor = SNOW
    t.rooms(BG_ROCK)
    g, lane = t.g, t.lane
    top = yv + 1
    t.posts((22, 40, 58), SNOW)
    t.put("closet", 67, g)
    t.put("super_soldier", 61, g)
    t.put("ultimate_soldier", 58, g)
    t.rect(56, top, 56, g, DESTRUCTIBLE_SAND)
    t.put("health_kit", 57, g)
    t.rect(38, g - 1, 51, g, WATER)
    t.rect(38, g + 1, 51, g + 1, WATER)
    t.put("electric_eel", 44, g)
    t.put("electric_eel", 48, g)
    t.put("robot_piranha", 41, g + 1)
    t.put("fighter", 53, g)
    t.put("robodog", 36, g)
    t.put("super_soldier", 33, g)
    t.put("turret", 30, top)
    t.rect(28, top, 28, g, DESTRUCTIBLE_SAND)
    t.put("ultimate_soldier", 25, g)
    t.put("soldier", 22, g)
    t.put("ammo_pack", 35, g)
    t.put("weapon_dynamite", 60, g)
    t.put("ammo_pack", 54, g)
    t.put("flame_trap", 46, top)
    t.put("camera", 33, top)
    t.put("diamond", 55, top + 2)

    t.put("computer", 65, lane, number=4)
    t.put("notebook", 66, lane - 2)
    t.put("closet", 63, lane)
    t.doors(61, 4)
    t.trail(63, 4, 4)
    t.rect(46, lane, 52, lane, ELECTRIC)
    t.put("floor_circuit", 46, lane, number=3)
    t.put("computer", 54, lane, number=3)
    t.trail(56, 8, 3)
    t.bridge(28, 35)
    t.put("laser", 22, lane, number=5)
    t.put("switch", 19, lane, number=5)
    t.put("camera", 38, lane - 2, facing="right")
    t.put("letter_e", 34, lane - 2)
    t.put("health_kit", 44, lane)
    t.put("checkpoint", 6, lane)
    t.put("deco_machine", 8, lane)
    t.put("deco_crate_steel", 12, lane)
    t.put("sim_card", 14, top + 3)

    t.sh = 0
    t.put("light", 40, top + 1)
    t.put("light", 28, top + 1)
    t.rect(30, g - 1, 44, g, WATER)
    t.rect(30, g + 1, 44, g + 1, WATER)
    t.put("electric_eel", 33, g)
    t.put("electric_eel", 41, g)
    t.put("robot_piranha", 37, g + 1)
    t.put("ultimate_soldier", 46, g)
    t.put("super_soldier", 26, g)
    t.put("drone", 24, top + 2)
    t.put("ammo_pack", 28, g)
    t.pit(24, 31, 27)
    t.put("health_kit", 40, lane)
    t.put("diamond", 36, lane - 2)


def tier_three(yv, flip):
    t = Tier(yv, flip, BG_PANEL, DESTRUCTIBLE_STEEL)
    t.wall_floor = STEEL
    t.rooms(BG_PANEL)
    g, lane = t.g, t.lane
    top = yv + 1
    t.posts((22, 40, 58), STEEL)
    t.put("closet", 67, g)
    t.put("ultimate_soldier", 61, g)
    t.put("super_soldier", 58, g)
    t.put("ammo_pack", 62, g)
    t.put("turret", 56, top)
    t.rect(53, top, 53, g, DESTRUCTIBLE_STEEL)
    t.put("weapon_minigun", 54, g)
    t.put("robodog", 50, g)
    t.put("ultimate_soldier", 47, g)
    t.put("drone", 44, top + 2)
    t.rect(41, g, 41, g, SANDBAGS)
    t.put("super_soldier", 38, g)
    t.put("health_kit", 40, g)
    t.put("landmine", 35, g)
    t.put("ultimate_soldier", 32, g)
    t.rect(29, top, 29, g, DESTRUCTIBLE_STEEL)
    t.put("turret", 26, top)
    t.put("super_soldier", 24, g)
    t.put("ammo_pack", 33, g)
    t.put("flame_trap", 48, top)
    t.put("camera", 36, top)
    t.put("deco_machine", 52, g)

    t.put("computer", 65, lane, number=6)
    t.doors(61, 6)
    t.trail(63, 16, 6)
    t.put("teleport", 49, lane, number=1)
    t.carve(36, yv + 8, 42, yv + 9)
    t.put("teleport", 38, yv + 9, number=1)
    t.put("usb_key", 41, yv + 9, number=7)
    t.put("computer", 34, lane, number=7)
    t.doors(30, 7)
    t.pit(18, 25, 21)
    t.put("laser", 27, lane, number=8)
    t.put("switch", 24, lane, number=8)
    t.put("camera", 45, lane - 2, facing="left")
    t.put("audio_recorder", 32, lane - 2)
    t.put("health_kit", 56, lane)
    t.put("checkpoint", 14, lane)
    t.put("deco_machine", 8, lane)
    t.put("deco_crate_steel", 12, lane)
    t.put("letter_n", 10, top + 2)

    t.sh = 0
    t.put("light", 40, top + 1)
    t.put("light", 28, top + 1)
    t.put("ultimate_soldier", 46, g)
    t.put("turret", 42, top)
    t.rect(39, g, 39, g, SANDBAGS)
    t.put("ultimate_soldier", 36, g)
    t.put("robodog", 33, g)
    t.put("landmine", 29, g)
    t.put("super_soldier", 26, g)
    t.put("drone", 22, top + 2)
    t.put("ammo_pack", 31, g)
    t.bridge(32, 39)
    t.put("health_kit", 44, lane)
    t.put("laser", 28, lane, number=14)
    t.put("switch", 25, lane, number=14)


def tier_four(yv, flip):
    # The inner base: pits, planks and a last key corridor.
    t = Tier(yv, flip, BG_CIRCUIT, DESTRUCTIBLE_STEEL)
    t.wall_floor = STEEL
    t.rooms(BG_HANGAR)
    g, lane = t.g, t.lane
    top = yv + 1
    t.posts((22, 40, 58), STEEL)
    t.put("closet", 67, g)
    t.put("ultimate_soldier", 61, g)
    t.put("ultimate_soldier", 58, g)
    t.put("ammo_pack", 63, g)
    t.rect(55, top, 55, g, DESTRUCTIBLE_STEEL)
    t.put("weapon_dynamite", 56, g)
    t.put("super_soldier", 52, g)
    t.put("turret", 49, top)
    t.put("drone", 46, top + 2)
    t.put("robodog", 44, g)
    t.rect(41, g, 41, g, SANDBAGS)
    t.put("ultimate_soldier", 38, g)
    t.put("health_kit", 40, g)
    t.rect(36, top, 36, g, DESTRUCTIBLE_STEEL)
    t.put("super_soldier", 31, g)
    t.put("landmine", 28, g)
    t.put("turret", 26, top)
    t.put("ultimate_soldier", 24, g)
    t.put("ammo_pack", 29, g)
    t.put("health_kit", 22, g)
    t.put("flame_trap", 52, top)
    t.put("camera", 30, top)
    t.put("deco_machine", 60, g)

    t.put("computer", 65, lane, number=9)
    t.doors(61, 9)
    t.trail(63, 18, 9)
    t.pit(54, 61, 57)
    t.bridge(44, 51)
    t.pit(30, 37, 33)
    t.put("laser", 22, lane, number=10)
    t.put("switch", 19, lane, number=10)
    t.put("camera", 46, lane - 2, facing="left")
    t.put("smartwatch", 48, lane - 2)
    t.put("health_kit", 40, lane)
    t.put("closet", 25, lane)
    t.put("checkpoint", 14, lane)
    t.put("deco_machine", 8, lane)
    t.put("deco_crate_steel", 12, lane)
    t.put("letter_t", 10, top + 2)

    t.sh = 0
    t.put("light", 40, top + 1)
    t.put("light", 28, top + 1)
    t.put("ultimate_soldier", 46, g)
    t.put("super_soldier", 41, g)
    t.rect(38, g, 38, g, SANDBAGS)
    t.put("turret", 35, top)
    t.put("ultimate_soldier", 32, g)
    t.put("robodog", 28, g)
    t.put("drone", 24, top + 2)
    t.put("ammo_pack", 36, g)
    t.pit(30, 37, 33)
    t.put("health_kit", 42, lane)


def machine_room():
    # The last tier ends in the machine hall: a pond with eels and piranhas under a catwalk, robots on a ledge,
    # the password on a dais and the exit behind a security door and a laser in the low corridor beyond.
    t = Tier(63, False, BG_CIRCUIT, DESTRUCTIBLE_STEEL)
    t.wall_floor = STEEL
    t.rooms(BG_HANGAR)
    g, lane = t.g, t.lane
    t.posts((22, 40, 58), STEEL)
    t.put("closet", 67, g)
    t.put("ultimate_soldier", 61, g)
    t.put("ultimate_soldier", 58, g)
    t.put("ammo_pack", 63, g)
    t.rect(55, 64, 55, g, DESTRUCTIBLE_STEEL)
    t.put("super_soldier", 52, g)
    t.put("turret", 49, 64)
    t.put("drone", 46, 66)
    t.put("robodog", 44, g)
    t.rect(41, g, 41, g, SANDBAGS)
    t.put("ultimate_soldier", 38, g)
    t.put("health_kit", 40, g)
    t.rect(35, 64, 35, g, DESTRUCTIBLE_STEEL)
    t.put("super_soldier", 31, g)
    t.put("landmine", 28, g)
    t.put("turret", 26, 64)
    t.put("ultimate_soldier", 24, g)
    t.put("ammo_pack", 33, g)
    t.put("flame_trap", 52, 64)
    t.put("camera", 30, 64)
    t.put("deco_machine", 60, g)

    t.put("computer", 65, lane, number=11)
    t.doors(61, 11)
    t.trail(63, 14, 11)
    t.bridge(36, 43)
    t.pit(24, 31, 27)
    t.put("camera", 46, lane - 2, facing="left")
    t.put("health_kit", 50, lane)

    # The hall: one open space x 4..47, rows 63..76.
    t.sh = 0
    L.carve(4, 63, 47, 76, BG_HANGAR)
    L.rect(4, 77, 47, 77, STEEL)
    t.put("checkpoint", 45, lane)
    t.put("ammo_pack", 46, lane)
    t.put("health_kit", 44, lane)
    t.rect(30, 75, 41, 77, WATER)
    for x in (33, 38):
        t.put("electric_eel", x, 76)
    for x in (31, 36, 40):
        t.put("robot_piranha", x, 76)
    t.rect(42, 75, 43, 76, STEEL)
    t.rect(44, 76, 44, 76, STEEL)
    t.rect(28, 73, 41, 73, GIRDER)
    t.rect(26, 75, 27, 76, STEEL)
    t.rect(20, 73, 25, 76, STEEL)
    t.put("admin_password", 20, 72)
    t.put("usb_key", 23, 72, number=13)
    t.put("deco_machine", 22, 72)
    t.rect(30, 68, 40, 68, GIRDER)
    t.put("robot", 35, 67)
    t.rect(14, 68, 24, 68, GIRDER)
    t.put("robot", 19, 67)
    t.put("drone", 30, 65)
    t.put("drone", 12, 66)
    t.rect(4, 73, 18, 73, STEEL)
    t.rect(4, 74, 18, 76, EMPTY)
    t.carve(19, 73, 19, 76, BG_HANGAR)
    t.put("computer", 17, lane, number=13)
    t.put("switch", 15, lane, number=12)
    t.doors(12, 13)
    t.put("laser", 9, lane, number=12)
    t.put("exit_door", 5, lane)
    t.put("deco_machine", 8, 72)
    t.put("deco_machine", 14, 72)
    for x in (6, 16, 26, 36):
        t.put("light", x, 66)
    t.put("diamond", 31, 71)
    t.put("letter_t", 11, 71)


def build():
    ground()
    tier_one()
    tier_two(18, True)
    tier_three(33, False)
    tier_four(48, True)
    machine_room()
    L.bg_rect(1, 0, W - 2, 8, EMPTY)
    drop(3, False)
    drop(18, True)
    drop(33, False)
    drop(48, True)


if __name__ == "__main__":
    build()
    L.cli("antarctica.json")
