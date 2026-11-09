"""Builds levels/tokyo.json, the 80x60 robotics plant.

Three tiers of the plant, each split into a short guarded soldier route and a
longer quiet hacker route: the assembly hall with its overhead gantry, the
sleeping bay with its vent, and the control floor under the roof. Two climbing
shafts link the tiers. Run it, then check with tools/route_check.py.

    python tools/levels/build_tokyo.py [--preview]
"""

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from levelkit import *  # noqa: E402,F403

L = Level(
    "Night Shift at Kuroda Robotics",
    "Kuroda Robotics builds the soldiers that will guard the ice, and the night shift is asleep. "
    "Cross the assembly hall, the sleeping bay and the control floor, then climb to the roof where "
    "the plant's admin password waits. Every tier splits two ways: a short guarded route for the "
    "soldier along the assembly line, and a longer, quiet one for the hacker over the gantries and "
    "through the vents. Hack the lasers and security doors, ride the moving platforms, wear the "
    "briefcase past the turret and step through the teleport.",
    "night", 80, 60)


def shaft(x0, x1, top, first_row, first_side, bg=BG_HANGAR, bottom=None, width=3):
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
    L.rect(1, 0, 78, 58, STEEL)
    L.border(WALL)
    L.rect(1, 56, 78, 58, CONCRETE)


def assembly_hall():
    L.carve(1, 45, 69, 55, BG_HANGAR)
    # The agent starts on the loading catwalk, above the line.
    L.rect(1, 50, 11, 50, GIRDER)
    L.put("agent_start", 2, 49)
    L.put("closet", 4, 49)

    # Soldier route: drop off the catwalk and fight along the assembly line.
    L.put("soldier", 16, 55)
    L.put("ammo_pack", 20, 55)
    L.put("robodog", 26, 55)
    L.set(31, 55, SANDBAGS)
    L.rect(36, 51, 38, 55, DESTRUCTIBLE_STEEL)
    L.put("super_soldier", 44, 55)
    L.put("health_kit", 40, 55)
    L.put("landmine", 51, 55)
    L.put("turret", 58, 55)
    L.put("fighter", 65, 55)

    # Hacker route: the gantry. Computer 1 is by the catwalk, goggles show the
    # hidden step to the shelf with USB 1, and then it is back to computer 1 to
    # drop laser 1. A moving platform bridges the press pit, USB 2 and
    # computer 2 open the last door.
    L.rect(15, 50, 45, 50, GIRDER)
    L.rect(55, 50, 69, 50, GIRDER)
    L.put("computer", 17, 49, number=1)
    L.put("infra_goggles", 22, 49)
    L.rect(27, 48, 28, 48, HIDDEN)
    L.rect(29, 47, 37, 47, GIRDER)
    L.put("usb_key", 34, 46, number=1)
    L.put("laser", 41, 49, number=1)
    L.put("moving_platform", 50, 50)
    L.put("usb_key", 58, 49, number=2)
    L.put("computer", 62, 49, number=2)
    for y in range(45, 50):
        L.put("security_door", 68, y, number=2)
    L.put("camera", 40, 46, facing="left")
    L.put("cash_stack", 31, 46)
    L.rect(48, 46, 52, 46, GIRDER)
    L.put("robot", 50, 45)
    L.put("deco_machine", 12, 55)
    L.put("deco_machine", 48, 55)
    L.put("deco_crate_steel", 33, 55)


def east_shaft_low():
    shaft(70, 77, 27, 54, "left", bottom=55)


def east_shaft_high():
    shaft(70, 77, 4, 24, "left", bottom=25)


def sleeping_bay():
    L.carve(9, 36, 69, 41, BG_PANEL)
    L.carve(9, 27, 69, 33, BG_CIRCUIT)
    L.set(69, 41, SANDBAGS)
    L.set(9, 41, SANDBAGS)
    # Soldier: the bay, walking west.
    L.put("ultimate_soldier", 62, 41)
    L.put("ammo_pack", 58, 41)
    L.put("landmine", 54, 41)
    L.put("super_soldier", 48, 41)
    L.set(44, 41, SANDBAGS)
    L.put("robodog", 40, 41)
    L.rect(34, 36, 36, 41, DESTRUCTIBLE_STEEL)
    L.put("fighter", 26, 41)
    L.put("health_kit", 22, 41)
    L.put("soldier", 16, 41)
    L.put("deco_table", 30, 41)
    L.put("deco_table", 52, 41)

    # Hacker: the vent above the bay.
    L.put("computer", 66, 33, number=3)
    L.rect(52, 32, 53, 32, GIRDER)
    L.rect(40, 30, 50, 30, GIRDER)
    L.put("usb_key", 42, 29, number=3)
    L.put("briefcase", 47, 29)
    L.rect(26, 31, 34, 31, GIRDER)
    L.put("turret", 26, 30)
    L.put("laser", 21, 33, number=3)
    L.put("camera", 56, 28, facing="left")


def west_shaft():
    shaft(1, 8, 11, 40, "right", bottom=41)


def control_floor():
    L.carve(9, 20, 69, 25, BG_PANEL)
    L.carve(9, 11, 69, 17, BG_CIRCUIT)
    L.set(69, 25, SANDBAGS)
    L.set(9, 25, SANDBAGS)
    L.put("ultimate_soldier", 14, 25)
    L.put("robodog", 22, 25)
    L.rect(28, 20, 30, 25, DESTRUCTIBLE_STEEL)
    L.put("super_soldier", 36, 25)
    L.put("ammo_pack", 33, 25)
    L.put("fighter", 44, 25)
    L.put("landmine", 50, 25)
    L.put("soldier", 56, 25)
    L.put("health_kit", 60, 25)
    L.put("turret", 66, 25)
    L.put("deco_machine", 18, 25)
    L.put("deco_machine", 40, 25)

    # Hacker: the control vent. Computer 4 is by the entrance, USB 4 is across
    # the lava on a moving platform, so it is over and back, then on across the
    # spike pit to security door 4.
    L.put("computer", 13, 17, number=4)
    L.rect(22, 18, 30, 18, LAVA)
    L.put("moving_platform", 26, 17)
    L.put("usb_key", 36, 17, number=4)
    L.rect(46, 18, 54, 18, SPIKES)
    L.put("moving_platform", 50, 17)
    for y in range(11, 18):
        L.put("security_door", 60, y, number=4)
    L.put("camera", 33, 11, facing="left")
    L.put("closet", 66, 17)
    L.put("diamond", 42, 13)
    L.rect(38, 16, 39, 16, GIRDER)
    L.rect(40, 14, 44, 14, GIRDER)


def roof():
    # Control room under the sky: USB 6 at the top of the shaft, computer 6 in
    # the room, security door 6 in front of the vault holding the password.
    L.carve(44, 4, 69, 9, BG_CIRCUIT)
    L.bg_rect(70, 4, 77, 9, BG_CIRCUIT)
    L.put("usb_key", 71, 5, number=6)
    L.put("computer", 60, 9, number=6)
    for y in range(4, 10):
        L.put("security_door", 47, y, number=6)
    L.put("admin_password", 45, 9)
    L.put("exit_door", 66, 9)
    L.put("deco_machine", 54, 9)
    L.put("deco_clock", 52, 5)


def checkpoints():
    # Each flag stands on a shaft ledge that both routes of the tier below cross.
    for x, y in [(71, 45), (7, 27), (71, 19)]:
        L.put("checkpoint", x, y)


def dressing():
    for x, y in [(8, 48), (30, 48), (52, 48), (66, 48), (10, 53), (28, 52), (46, 52), (62, 52),
                 (14, 38), (30, 38), (46, 38), (62, 38), (16, 31), (36, 28), (58, 30), (14, 22),
                 (34, 22), (52, 22), (66, 22), (20, 14), (46, 14), (66, 14), (50, 6), (60, 6)]:
        L.put("light", x, y)
    L.put("letter_a", 3, 49)
    L.put("letter_g", 49, 29)
    L.put("letter_e", 24, 41)
    L.put("letter_n", 24, 25)
    L.put("letter_t", 56, 9)
    L.put("cash_stack", 60, 41)
    L.put("mobile", 58, 33)
    L.put("smartwatch", 64, 49)
    L.put("business_card", 13, 41)
    L.put("sim_card", 16, 25)
    L.put("notebook", 62, 25)
    L.put("audio_recorder", 52, 17)
    L.put("deco_clock", 20, 41)
    L.put("deco_poster", 48, 37)
    L.put("deco_table", 22, 17)
    L.put("deco_machine", 52, 33)
    L.put("deco_machine", 10, 41)
    L.put("deco_crate_wood", 61, 55)
    L.put("deco_crate_steel", 8, 55)


def build():
    frame()
    assembly_hall()
    east_shaft_low()
    sleeping_bay()
    west_shaft()
    control_floor()
    east_shaft_high()
    roof()
    checkpoints()
    dressing()


if __name__ == "__main__":
    build()
    L.cli("tokyo.json")
