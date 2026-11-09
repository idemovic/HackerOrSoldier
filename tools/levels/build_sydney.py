"""Builds levels/sydney.json, the 80x60 harbour level.

Three tiers. First the basin: the soldier crosses on the decks of two supply
ships, the hacker swims underneath. Then the harbour authority's pier building:
a guarded hall for the soldier and an air duct for the hacker. Last the roof
and the crane: a guarded deck against the crane's gantry, with the password in
the control cab at the end. Run it, then check with tools/route_check.py.

    python tools/levels/build_sydney.py [--preview]
"""

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from levelkit import *  # noqa: E402,F403

L = Level(
    "Harbour Authority",
    "The supply ships for Antarctica leave from this port, and the harbour authority's systems "
    "hold the tenth password. Cross the basin, climb through the authority's pier building and "
    "finish on the roof, in the crane's control cab. The soldier fights over the decks of two "
    "ships, down the building's hall and along the roof. The hacker swims under the hulls, "
    "crawls through the air duct and rides the crane's gantry, hacking the gates, the electric "
    "floor and the lasers on the way.",
    "day", 80, 60)


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
    L.rect(1, 23, 78, 40, BRICK)
    L.rect(1, 49, 17, 58, CONCRETE)
    L.rect(62, 49, 78, 58, CONCRETE)
    L.rect(18, 57, 61, 58, CONCRETE)
    L.rect(18, 49, 61, 56, WATER)
    L.border(WALL)
    L.rect(0, 0, 0, 59, WALL)
    L.rect(79, 0, 79, 59, WALL)


def basin():
    L.put("agent_start", 2, 48)
    L.rect(6, 47, 7, 47, STONE)
    L.put("deco_crate_steel", 12, 48)
    L.put("closet", 14, 48)

    # Ship A and ship B: steel hulls with a deck at row 47. Soldier route.
    L.rect(19, 47, 38, 50, STEEL)
    L.rect(43, 47, 60, 50, STEEL)
    L.rect(40, 49, 41, 56, CONCRETE)
    L.rect(24, 45, 26, 46, CONTAINER)
    L.rect(33, 45, 34, 46, CONTAINER)
    L.rect(37, 45, 38, 46, CONTAINER)
    L.rect(43, 45, 44, 46, CONTAINER)
    L.rect(48, 45, 50, 46, CONTAINER)
    L.rect(59, 45, 60, 46, CONTAINER)
    L.put("soldier", 21, 46)
    L.put("ammo_pack", 27, 46)
    L.put("turret", 30, 46)
    L.put("fighter", 35, 46)
    L.put("soldier", 46, 46)
    L.put("super_soldier", 53, 46)
    L.put("health_kit", 51, 46)
    L.put("landmine", 56, 46)
    L.put("robodog", 29, 46)

    # Hacker route: dive off the dock, fetch USB 1 from the cave under the
    # west dock, swim back under ship A past the robot piranha, surface on the
    # island between the ships for computer 1,
    # which opens the gate 1 on the island between the ships.
    L.rect(9, 52, 17, 56, WATER)
    L.put("usb_key", 10, 56, number=1)
    L.put("robot_piranha", 30, 56)
    L.put("computer", 40, 48, number=1)
    for y in range(45, 49):
        L.put("security_door", 41, y, number=1)
    L.put("robot_piranha", 52, 56)
    L.put("camera", 20, 44, facing="right")
    L.put("sim_card", 33, 52)
    L.rect(66, 48, 69, 48, CONCRETE)
    L.put("deco_machine", 63, 48)
    L.put("deco_crate_steel", 74, 48)
    L.put("checkpoint", 64, 48)


def east_shaft_low():
    shaft(70, 77, 24, 46, "left", bottom=48)


def pier_hall():
    L.carve(9, 33, 69, 37, BG_PANEL)
    L.carve(9, 24, 69, 29, BG_CIRCUIT)
    L.set(69, 37, SANDBAGS)
    L.set(9, 37, SANDBAGS)

    # Soldier: the hall, walking west, two collapsed walls to shoot through.
    L.put("robodog", 64, 37)
    L.put("soldier", 57, 37)
    L.put("ammo_pack", 54, 37)
    L.rect(47, 33, 51, 37, DESTRUCTIBLE_RED)
    L.put("super_soldier", 42, 37)
    L.put("health_kit", 38, 37)
    L.put("landmine", 36, 37)
    L.rect(24, 33, 30, 37, DESTRUCTIBLE_RED)
    L.put("ultimate_soldier", 21, 37)
    L.put("fighter", 17, 37)
    L.put("soldier", 12, 37)

    # Hacker: the air duct. Goggles at the entry show the step to the shelf
    # with USB 3, computer 3 switches off the electric floor, the turret
    # watches the middle stretch so the briefcase is needed to fetch USB 4,
    # which goes back east to computer 4 for security door 4.
    L.put("infra_goggles", 67, 29)
    L.put("computer", 65, 29, number=3)
    L.rect(60, 28, 61, 28, HIDDEN)
    L.rect(54, 26, 59, 26, GIRDER)
    L.put("usb_key", 56, 25, number=3)
    L.rect(44, 30, 52, 30, ELECTRIC)
    L.put("floor_circuit", 44, 30, number=3)
    L.put("briefcase", 40, 29)
    L.rect(28, 27, 36, 27, GIRDER)
    L.put("turret", 28, 26)
    L.rect(38, 28, 39, 28, GIRDER)
    L.put("usb_key", 35, 26, number=4)
    L.put("computer", 62, 29, number=4)
    for y in range(24, 30):
        L.put("security_door", 16, y, number=4)
    L.put("camera", 50, 24, facing="left")
    L.put("deco_machine", 20, 29)
    L.put("diamond", 31, 26)


def west_shaft():
    shaft(1, 8, 22, 36, "right", bottom=37)
    L.rect(1, 22, 3, 22, GIRDER)


def roof():
    # The roof deck at row 23. Soldier route: the deck. Hacker route: the
    # elevator to the crane's gantry.
    L.put("usb_key", 2, 21, number=6)
    L.put("soldier", 14, 22)
    L.put("robodog", 19, 22)
    L.set(23, 22, SANDBAGS)
    L.put("super_soldier", 28, 22)
    L.put("ammo_pack", 31, 22)
    L.put("fighter", 40, 22)
    L.put("health_kit", 43, 22)
    L.put("ultimate_soldier", 47, 22)
    L.rect(51, 18, 55, 22, DESTRUCTIBLE_STEEL)
    L.rect(33, 18, 37, 22, DESTRUCTIBLE_STEEL)
    L.put("drone", 20, 9)
    L.put("drone", 40, 9)

    L.put("elevator", 12, 20)
    L.rect(13, 17, 38, 17, GIRDER)
    L.rect(48, 17, 56, 17, GIRDER)
    L.put("computer", 16, 16, number=5)
    L.put("infra_goggles", 19, 16)
    L.rect(24, 15, 25, 15, HIDDEN)
    L.rect(26, 14, 32, 14, GIRDER)
    L.put("usb_key", 29, 13, number=5)
    L.put("laser", 36, 16, number=5)
    L.put("moving_platform", 43, 17)
    L.put("camera", 33, 11, facing="left")



def crane():
    # The crane tower climbs from the roof deck to the jib, which runs west to
    # the control cab. Both routes cross it: two moving platforms over the gaps,
    # computer 6 outside the cab's security door, the password inside.
    shaft(70, 77, 4, 21, "left", bg=BG_MESH, bottom=22)
    L.rect(66, 8, 69, 8, GIRDER)
    L.rect(53, 8, 56, 8, GIRDER)
    L.rect(26, 8, 43, 8, GIRDER)
    L.put("moving_platform", 61, 8)
    L.put("moving_platform", 48, 8)
    L.rect(32, 3, 41, 7, STEEL)
    L.carve(33, 4, 40, 7, BG_CIRCUIT)
    L.carve(32, 4, 32, 7, BG_CIRCUIT)
    L.carve(41, 4, 41, 7, BG_CIRCUIT)
    for y in range(4, 8):
        L.put("security_door", 41, y, number=6)
    L.put("computer", 43, 7, number=6)
    L.put("admin_password", 36, 7)
    L.put("exit_door", 27, 7)
    L.put("camera", 48, 3, facing="left")
    L.put("deco_machine", 38, 7)


def checkpoints():
    for x, y in [(71, 41), (7, 31), (71, 20), (71, 8)]:
        L.put("checkpoint", x, y)


def dressing():
    for x, y in [(14, 35), (30, 35), (46, 35), (62, 35), (14, 27), (34, 27), (54, 27), (64, 27),
                 (74, 30), (4, 28), (36, 5), (48, 5), (73, 14)]:
        L.put("light", x, y)
    L.put("letter_a", 7, 46)
    L.put("letter_g", 49, 44)
    L.put("letter_e", 31, 37)
    L.put("letter_n", 22, 29)
    L.put("letter_t", 30, 7)
    L.put("cash_stack", 13, 48)
    L.put("business_card", 67, 47)
    L.put("mobile", 24, 29)
    L.put("smartwatch", 22, 16)
    L.put("notebook", 63, 22)
    L.put("audio_recorder", 45, 29)
    L.put("deco_clock", 40, 35)
    L.put("deco_poster", 18, 35)
    L.put("deco_plant", 60, 37)
    L.put("deco_copier", 26, 37)
    L.put("deco_table", 46, 29)
    L.put("deco_crate_wood", 66, 22)
    L.put("deco_crate_steel", 9, 22)
    L.put("deco_crate_wood", 4, 48)
    L.put("deco_machine", 25, 22)


def build():
    frame()
    basin()
    east_shaft_low()
    pier_hall()
    west_shaft()
    roof()
    crane()
    checkpoints()
    dressing()


if __name__ == "__main__":
    build()
    L.cli(sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else "sydney.json")
