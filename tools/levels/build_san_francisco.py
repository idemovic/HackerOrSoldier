"""Builds levels/san-francisco.json, the 80x60 chip maker campus.

The agent starts on the campus plaza and goes down, never up: plaza and
utility trench, the fab floor and its air duct, then the vault floor, ending
in the key vault at the bottom of a shaft, from which the exit is a climb
away. Every tier splits into a guarded soldier lane and a quiet hacker lane.

    python tools/levels/build_san_francisco.py [--preview]
"""

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from levelkit import *  # noqa: E402,F403

L = Level(
    "Silicon Vault",
    "The chip maker that sold White Silence its hardware keeps a backup of every key, and its "
    "campus is less friendly than it looks. Cross the plaza, go down through the fab and the vault "
    "floor, take the password from the key vault at the bottom and climb back out. The soldier "
    "fights down the plaza, the fab hall and the vault hall past the turrets. The hacker drops "
    "into the utility trench, creeps through the fab's duct and the vault's service corridor, "
    "switching off lasers and floors, wearing the briefcase past the camera turret and "
    "teleporting over the lava.",
    "day", 80, 60)


def shaft(x0, x1, top, first_row, first_side, bg=BG_CIRCUIT, bottom=None, width=3):
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
    L.rect(1, 14, 78, 58, STEEL)
    L.rect(1, 14, 78, 14, CONCRETE)
    L.border(WALL)
    L.rect(0, 0, 0, 59, WALL)
    L.rect(79, 0, 79, 59, WALL)


def plaza():
    # Campus buildings stand behind the plaza.
    L.bg_rect(12, 4, 34, 13, BG_CIRCUIT)
    L.bg_rect(40, 6, 60, 13, BG_MESH)
    L.bg_rect(62, 3, 69, 13, BG_CIRCUIT)
    L.put("agent_start", 2, 13)
    L.put("closet", 4, 13)
    L.carve(6, 14, 7, 15, BG_PANEL)
    L.set(10, 13, SANDBAGS)

    # Soldier: the plaza, under the turrets, through three collapsed walls.
    L.put("soldier", 16, 13)
    L.put("turret", 21, 13)
    L.put("ammo_pack", 24, 13)
    L.put("robodog", 26, 13)
    L.rect(29, 9, 33, 13, DESTRUCTIBLE_STEEL)
    L.put("health_kit", 36, 13)
    L.put("super_soldier", 40, 13)
    L.put("landmine", 42, 13)
    L.rect(45, 9, 49, 13, DESTRUCTIBLE_STEEL)
    L.put("ultimate_soldier", 53, 13)
    L.put("ammo_pack", 56, 13)
    L.rect(59, 9, 63, 13, DESTRUCTIBLE_STEEL)
    L.put("turret", 66, 13)
    L.set(69, 13, SANDBAGS)

    # Hacker: the utility trench under the plaza. Goggles, then east to a
    # hidden step and the shelf with USB 1 and the briefcase, back west to
    # computer 1, east past laser 1 to USB 2 at the far end, back west to
    # computer 2 and east again to door 2. A camera turret on a ledge wants the
    # briefcase.
    L.carve(1, 16, 69, 21, BG_PANEL)
    L.put("infra_goggles", 10, 21)
    L.put("computer", 12, 21, number=1)
    L.put("computer", 16, 21, number=2)
    L.rect(38, 20, 39, 20, HIDDEN)
    L.rect(40, 19, 46, 19, GIRDER)
    L.put("usb_key", 42, 18, number=1)
    L.put("briefcase", 45, 18)
    L.put("laser", 52, 21, number=1)
    L.rect(54, 19, 58, 19, GIRDER)
    L.put("turret", 56, 18)
    L.put("usb_key", 63, 21, number=2)
    for y in range(16, 22):
        L.put("security_door", 67, y, number=2)
    L.put("camera", 30, 16, facing="right")
    L.put("deco_machine", 20, 21)
    L.put("cash_stack", 65, 21)


def east_shaft():
    shaft(70, 77, 14, 36, "left", bottom=37)


def fab():
    L.carve(9, 25, 69, 29, BG_CIRCUIT)
    L.carve(9, 31, 69, 37, BG_PANEL)
    L.set(69, 29, SANDBAGS)
    L.set(9, 29, SANDBAGS)

    # Soldier: the fab hall, walking west.
    L.put("ultimate_soldier", 65, 29)
    L.put("turret", 59, 29)
    L.rect(51, 25, 55, 29, DESTRUCTIBLE_STEEL)
    L.put("soldier", 47, 29)
    L.put("ammo_pack", 43, 29)
    L.put("super_soldier", 31, 29)
    L.put("health_kit", 28, 29)
    L.put("landmine", 25, 29)
    L.rect(19, 25, 23, 29, DESTRUCTIBLE_STEEL)
    L.put("fighter", 15, 29)
    L.put("turret", 11, 29)

    # Hacker: the air duct under the hall. Computers 3 and 4 are by the entry,
    # USB 3 on a shelf in the middle and USB 4 past the lava, so it is back and
    # forth twice: laser 3 comes down, a teleport crosses the lava, and door 4
    # opens only after the trip back east to computer 4.
    L.put("computer", 66, 37, number=3)
    L.rect(43, 36, 44, 36, GIRDER)
    L.rect(34, 34, 42, 34, GIRDER)
    L.put("usb_key", 37, 33, number=3)
    L.put("computer", 63, 37, number=4)
    L.put("laser", 30, 37, number=3)
    L.put("teleport", 28, 37, number=1)
    L.rect(18, 38, 26, 38, LAVA)
    L.put("teleport", 16, 37, number=1)
    L.put("usb_key", 13, 37, number=4)
    for y in range(31, 38):
        L.put("security_door", 10, y, number=4)
    L.put("camera", 56, 31, facing="left")
    L.put("deco_machine", 50, 37)
    L.put("diamond", 36, 33)


def west_shaft():
    shaft(1, 8, 25, 52, "right", bottom=53)


def vault():
    L.carve(9, 41, 69, 45, BG_CIRCUIT)
    L.carve(9, 48, 69, 53, BG_PANEL)
    L.set(69, 45, SANDBAGS)
    L.set(9, 45, SANDBAGS)

    # Soldier: the vault hall, walking east.
    L.put("soldier", 12, 45)
    L.put("turret", 15, 45)
    L.rect(18, 41, 22, 45, DESTRUCTIBLE_STEEL)
    L.put("robodog", 27, 45)
    L.put("ammo_pack", 30, 45)
    L.rect(34, 41, 38, 45, DESTRUCTIBLE_STEEL)
    L.put("super_soldier", 43, 45)
    L.put("health_kit", 46, 45)
    L.put("landmine", 48, 45)
    L.rect(50, 41, 54, 45, DESTRUCTIBLE_STEEL)
    L.put("ultimate_soldier", 59, 45)
    L.put("fighter", 62, 45)
    L.put("turret", 66, 45)

    # Hacker: the service corridor. Computers 5 and 6 are by the entry. USB 5
    # lies across the spike pit, over a moving platform, and USB 6 past laser 5
    # and the electric floor, so it is back and forth twice before lava, a
    # second platform and door 6 lead to the vault.
    L.put("computer", 11, 53, number=5)
    L.put("computer", 14, 53, number=6)
    L.rect(20, 54, 28, 54, SPIKES)
    L.put("moving_platform", 24, 53)
    L.put("usb_key", 32, 53, number=5)
    L.rect(36, 54, 42, 54, ELECTRIC)
    L.put("floor_circuit", 36, 54, number=5)
    L.put("laser", 45, 53, number=5)
    L.put("usb_key", 48, 53, number=6)
    L.rect(52, 54, 60, 54, LAVA)
    L.put("moving_platform", 56, 53)
    for y in range(48, 54):
        L.put("security_door", 64, y, number=6)
    L.put("camera", 38, 48, facing="left")
    L.put("closet", 67, 53)
    L.put("deco_machine", 17, 53)
    L.put("sim_card", 50, 53)


def key_vault():
    shaft(70, 77, 40, 52, "left", bottom=53)
    L.put("admin_password", 76, 53)
    L.put("exit_door", 76, 41)
    L.put("deco_machine", 74, 53)
    L.put("deco_clock", 74, 45)


def checkpoints():
    for x, y in [(71, 23), (7, 39), (74, 53)]:
        L.put("checkpoint", x, y)


def dressing():
    for x, y in [(14, 19), (34, 19), (50, 19), (64, 19), (14, 27), (34, 27), (56, 27), (14, 34),
                 (30, 34), (50, 34), (66, 34), (14, 43), (34, 43), (56, 43), (14, 50), (34, 50),
                 (56, 50), (66, 50), (74, 42)]:
        L.put("light", x, y)
    L.put("letter_a", 8, 13)
    L.put("letter_g", 44, 18)
    L.put("letter_e", 40, 29)
    L.put("letter_n", 39, 33)
    L.put("letter_t", 62, 53)
    L.put("mobile", 47, 21)
    L.put("smartwatch", 30, 45)
    L.put("business_card", 6, 21)
    L.put("notebook", 17, 53)
    L.put("audio_recorder", 36, 37)
    L.put("deco_poster", 48, 13)
    L.put("deco_plant", 66, 13)
    L.put("deco_copier", 30, 29)
    L.put("deco_table", 52, 45)
    L.put("deco_crate_steel", 4, 21)


def build():
    frame()
    plaza()
    east_shaft()
    fab()
    west_shaft()
    vault()
    key_vault()
    checkpoints()
    dressing()


if __name__ == "__main__":
    build()
    L.cli(sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else "san-francisco.json")
