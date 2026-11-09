"""Builds levels/new-york.json, the 80x80 Wall Street level.

Nothing like a tower climb from the lobby up. The agent starts in the subway
and goes east, climbs the fire escape to street level and the rooftops, crosses
west to the firm's tower, climbs its outside to the roof and breaks in from
above through the skylight, then works down through the penthouse to the vault
at the bottom. Every stretch forks into a guarded soldier lane and a quiet
hacker lane: subway platform against the track bed, rooftops against the
street, the penthouse hall against its air duct, the vault floor against the
gantry.

    python tools/levels/build_new_york.py [--preview]
"""

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from levelkit import *  # noqa: E402,F403

L = Level(
    "The Board's Seat",
    "The syndicate has a seat on the board of a Wall Street firm, and the penthouse vault holds "
    "the twelfth password. Start in the subway, climb out to the street and the rooftops, then go "
    "up the firm's tower and drop into the penthouse through the skylight. The soldier shoots his "
    "way down the platform, over the roofs, through the penthouse hall and along the vault floor. "
    "The hacker takes the track bed, the street, the air duct and the gantry, switching off the "
    "third rail, the lasers and the security doors, and wearing the briefcase past the turrets.",
    "night", 80, 80)


def shaft(x0, x1, top, first_row, first_side, bg=BG_BRICK, bottom=None, width=3):
    """A climbable shaft whose ledges rise two rows at a time from first_row,
    alternating sides, the first on first_side ("left" or "right")."""
    bottom = first_row + 1 if bottom is None else bottom
    L.carve(x0, top, x1, bottom, bg)
    side = first_side
    for row in range(first_row, top, -2):
        if side == "left":
            L.rect(x0, row, x0 + width - 1, row, GIRDER)
        else:
            L.rect(x1 - width + 1, row, x1, row, GIRDER)
        side = "right" if side == "left" else "left"


def frame():
    L.rect(1, 56, 78, 78, CONCRETE)
    L.rect(1, 10, 30, 55, BRICK)
    L.rect(39, 46, 50, 55, BRICK)
    L.rect(60, 46, 69, 55, BRICK)
    L.border(WALL)
    L.rect(0, 0, 0, 79, WALL)
    L.rect(79, 0, 79, 79, WALL)
    # The skyline behind the street.
    L.bg_rect(31, 14, 38, 55, BG_MESH)
    L.bg_rect(51, 30, 59, 55, BG_BRICK)
    L.bg_rect(70, 22, 77, 43, BG_BRICK)


def subway():
    L.carve(1, 64, 69, 69, BG_PANEL)
    L.carve(1, 72, 69, 77, BG_BRICK)
    L.carve(5, 70, 6, 71, BG_BRICK)
    L.put("agent_start", 2, 69)
    L.put("closet", 4, 69)
    L.set(9, 69, SANDBAGS)
    L.set(69, 69, SANDBAGS)

    # Soldier: the platform, east under the turrets.
    L.put("soldier", 14, 69)
    L.put("turret", 17, 69)
    L.rect(21, 64, 25, 69, DESTRUCTIBLE_STEEL)
    L.put("robodog", 29, 69)
    L.put("ammo_pack", 32, 69)
    L.put("super_soldier", 36, 69)
    L.rect(40, 64, 44, 69, DESTRUCTIBLE_STEEL)
    L.put("landmine", 48, 69)
    L.put("health_kit", 50, 69)
    L.put("ultimate_soldier", 54, 69)
    L.rect(58, 64, 62, 69, DESTRUCTIBLE_STEEL)
    L.put("fighter", 65, 69)

    # Hacker: drop through the hatch to the track bed. Briefcase, goggles,
    # computer 1 and computer 2 by the hatch, a hidden step to the shelf with USB 1, the
    # third rail that computer 1 switches off, USB 2 at the far
    # end, and door 2 behind it that computer 2 opens. A camera turret on a
    # ledge wants the briefcase.
    L.put("infra_goggles", 10, 77)
    L.put("briefcase", 8, 77)
    L.put("computer", 12, 77, number=1)
    L.put("computer", 15, 77, number=2)
    L.rect(32, 76, 33, 76, HIDDEN)
    L.rect(34, 75, 40, 75, GIRDER)
    L.put("usb_key", 36, 74, number=1)
    L.rect(44, 78, 54, 78, ELECTRIC)
    L.put("floor_circuit", 44, 78, number=1)
    L.rect(22, 75, 27, 75, GIRDER)
    L.put("turret", 25, 74)
    L.put("usb_key", 64, 77, number=2)
    for y in range(72, 78):
        L.put("security_door", 67, y, number=2)
    L.put("camera", 58, 72, facing="left")
    L.put("deco_machine", 19, 77)
    L.put("sim_card", 47, 77)


def fire_escape():
    shaft(70, 77, 44, 76, "left", bottom=77)


def street():
    # Soldier: the roof of the building on the east, the platform over the
    # alley, the roof of the building on the west.
    L.put("soldier", 67, 45)
    L.rect(63, 41, 65, 45, DESTRUCTIBLE_STEEL)
    L.put("turret", 61, 45)
    L.put("moving_platform", 55, 46)
    L.put("super_soldier", 47, 45)
    L.rect(43, 41, 45, 45, DESTRUCTIBLE_STEEL)
    L.put("ammo_pack", 41, 45)
    L.set(39, 45, SANDBAGS)
    L.put("drone", 52, 38)
    L.put("health_kit", 49, 45)

    # Hacker: the street, through the lobbies of both buildings. Lobby C:
    # goggles, a hidden step to the shelf with USB 3, computer 3 for laser 3.
    L.carve(60, 49, 69, 55, BG_BRICK)
    L.put("infra_goggles", 68, 55)
    L.put("computer", 65, 55, number=3)
    L.put("computer", 62, 55, number=4)
    L.rect(66, 54, 67, 54, HIDDEN)
    L.rect(61, 52, 65, 52, GIRDER)
    L.put("usb_key", 62, 51, number=3)
    L.put("laser", 60, 55, number=3)
    L.put("camera", 61, 49, facing="right")
    # Lobby B: USB 4 on a shelf deeper in, but computer 4 is back in lobby C, so
    # it is across the alley and back before door 4 opens.
    L.carve(39, 49, 50, 55, BG_BRICK)
    L.rect(47, 54, 48, 54, HIDDEN)
    L.rect(42, 52, 46, 52, GIRDER)
    L.put("usb_key", 43, 51, number=4)
    for y in range(49, 56):
        L.put("security_door", 40, y, number=4)
    L.put("deco_crate_wood", 55, 55)
    L.put("deco_crate_steel", 57, 55)
    L.put("cash_stack", 52, 55)


def tower_climb():
    shaft(31, 38, 8, 54, "right", bg=BG_MESH, bottom=55)


def penthouse():
    # The skylight over the duct, the duct above the hall, a hatch between.
    L.carve(24, 10, 27, 11, BG_CIRCUIT)
    L.carve(9, 12, 29, 17, BG_CIRCUIT)
    L.carve(22, 18, 23, 19, BG_CIRCUIT)
    L.carve(9, 20, 29, 25, BG_PANEL)
    L.set(9, 25, SANDBAGS)

    # Soldier: the hall, west under the skylight hatch.
    L.put("ultimate_soldier", 27, 25)
    L.put("turret", 24, 25)
    L.rect(19, 20, 21, 25, DESTRUCTIBLE_RED)
    L.put("super_soldier", 16, 25)
    L.put("ammo_pack", 14, 25)
    L.put("soldier", 12, 25)

    # Hacker: the duct. Computer 5 by the skylight, USB 5 on a shelf at the far
    # end, back to computer 5, then past laser 5.
    L.put("computer", 26, 17, number=5)
    L.rect(20, 16, 21, 16, HIDDEN)
    L.rect(14, 14, 19, 14, GIRDER)
    L.put("usb_key", 16, 13, number=5)
    L.put("laser", 11, 17, number=5)
    L.put("camera", 28, 12, facing="left")
    L.put("deco_machine", 14, 17)
    L.put("deco_table", 28, 25)
    L.put("deco_plant", 10, 25)


def stair_shaft():
    shaft(1, 8, 12, 54, "right", bg=BG_CIRCUIT, bottom=55)
    L.put("admin_password", 5, 55)


def vault():
    L.carve(9, 42, 29, 55, BG_CIRCUIT)
    L.set(9, 55, SANDBAGS)
    L.rect(9, 50, 24, 50, GIRDER)

    # Soldier: the vault floor under the gantry.
    L.put("soldier", 12, 55)
    L.put("turret", 15, 55)
    L.put("robodog", 18, 55)
    L.rect(21, 51, 23, 55, DESTRUCTIBLE_STEEL)
    L.put("health_kit", 26, 55)
    L.put("exit_door", 28, 55)

    # Hacker: the gantry. Computer 6 at the start, USB 6 on the shelf behind a
    # hidden step, then back to computer 6 and past laser 6, down to the exit.
    L.put("computer", 11, 49, number=6)
    L.rect(13, 48, 14, 48, HIDDEN)
    L.rect(15, 46, 19, 46, GIRDER)
    L.put("usb_key", 17, 45, number=6)
    L.put("laser", 21, 49, number=6)
    L.put("camera", 25, 43, facing="left")
    L.put("deco_machine", 26, 55)


def checkpoints():
    for x, y in [(72, 55), (36, 53), (7, 33)]:
        L.put("checkpoint", x, y)


def dressing():
    for x, y in [(14, 67), (30, 67), (46, 67), (62, 67), (14, 75), (34, 73), (54, 75), (62, 75),
                 (64, 51), (44, 51), (53, 50), (34, 40), (34, 24), (60, 12), (14, 15), (26, 15),
                 (14, 23), (26, 23), (14, 52), (22, 48), (26, 52), (4, 28), (4, 44)]:
        L.put("light", x, y)
    L.put("letter_a", 7, 69)
    L.put("letter_g", 37, 75)
    L.put("letter_e", 53, 77)
    L.put("letter_n", 18, 13)
    L.put("letter_t", 46, 45)
    L.put("mobile", 58, 77)
    L.put("smartwatch", 66, 55)
    L.put("business_card", 70, 77)
    L.put("notebook", 20, 25)
    L.put("audio_recorder", 18, 17)
    L.put("diamond", 17, 53)
    L.put("deco_clock", 30, 67)
    L.put("deco_poster", 52, 67)
    L.put("deco_copier", 22, 25)
    L.put("deco_plant", 12, 69)
    L.put("deco_crate_steel", 62, 69)


def build():
    frame()
    subway()
    fire_escape()
    street()
    tower_climb()
    penthouse()
    stair_shaft()
    vault()
    checkpoints()
    dressing()


if __name__ == "__main__":
    build()
    L.cli(sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else "new-york.json")
