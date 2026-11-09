#!/usr/bin/env python3
"""Route checker for Hacker or Soldier levels.

    python tools/route_check.py level.json [--png out.png] [--mode hacker|soldier|both]

Builds a movement graph by simulating the agent's physics (Agent.cs) over the
level's tiles and objects, then searches (cell, inventory) states for the exit
in hacker mode (no kills) and soldier mode. Prints the route, its events, a
necessity report for every hacker event, and optionally renders a PNG.
Exit status is nonzero when a requested mode cannot reach the exit.
"""
import argparse
import heapq
import json
import math
import re
import struct
import sys
import zlib
from collections import defaultdict

import tiledefs

TILE = 32
DT = 1 / 60
HALF_X = 8                      # the agent's 16x30 box (Agent.HalfSize)
HALF_Y = 15
JUMP_HEIGHT = 2.5 * TILE
JUMP_DISTANCE = 5 * TILE
TIME_TO_APEX = 0.35
GRAVITY = 2 * JUMP_HEIGHT / (TIME_TO_APEX * TIME_TO_APEX)
JUMP_VELOCITY = -GRAVITY * TIME_TO_APEX
RUN_SPEED = JUMP_DISTANCE / (2 * TIME_TO_APEX)
WATER_GRAVITY = 0.4
SWIM_SPEED = 120.0
SINK_SPEED = 80.0
MAX_TICKS = 900

PLATFORM_SPEED = 60.0
PLATFORM_RANGE = 4 * TILE
RIDE_STEP = 16                  # px between sampled ride positions

EMPTY, WALL, DWALL, ALARM, ELECTRIC, WATER, LAVA, SPIKES, SPEAR, HIDDEN = range(10)
SOLID_TILES = tiledefs.SOLID                # from scripts/TileDefs.cs
DESTRUCTIBLE_TILES = tiledefs.DESTRUCTIBLE  # DWALL and its colored variants
BACKGROUND_TILES = tiledefs.BACKGROUND

PICKUP_TOKENS = {
    "door_key": lambda p: "key:" + p.get("color", "red"),
    "usb_key": lambda p: "usb:" + p.get("number", "1"),
    "infra_goggles": lambda p: "goggles",
    "briefcase": lambda p: "briefcase",
    "weapon_dynamite": lambda p: "dynamite",
    "admin_password": lambda p: "password",
}
GROUND_ENEMIES = {"fighter", "soldier", "super_soldier", "ultimate_soldier", "robodog", "robot"}
ROW_SHOOTERS = {"soldier", "super_soldier", "robot"}
ENEMY_TYPES = GROUND_ENEMIES | {"drone", "turret", "electric_eel", "landmine", "robot_piranha"}

ROW_SIGHT = 10 * TILE
ALL_SIGHT = 8 * TILE
EXPLOSION_RADIUS = 5 * TILE

JUMP_HOLDS = (3, 6, 9, 12, 15, 18, 21, 24, 27, 30, 34, 38, 42, 48, 55, 65, 80, 100, 130)
FALL_HOLDS = (6, 9, 12, 16, 20, 25, 30, 40, 55, 75, 100)
WALK = 10 ** 6
EXIT = ("exit",)


def cell_of(px, py):
    return (math.floor(px / TILE), math.floor(py / TILE))


def load_level(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def split_layers(level):
    """(tiles, background) for a v3 level, or a v2 one whose background walls
    still sit in the tile array (LevelStore.Parse does the same move)."""
    tiles = list(level["tiles"])
    background = level.get("background")
    if background is not None and level.get("formatVersion", 1) >= 3:
        return tiles, list(background)
    background = [0] * len(tiles)
    for i, t in enumerate(tiles):
        if t in BACKGROUND_TILES:
            background[i], tiles[i] = t, EMPTY
    return tiles, background


class Platform:
    """An elevator (vertical) or moving platform, sampled along its sweep.

    MovingPlatform.cs: 60 px/s, up to 4 tiles from the origin, reversing when
    the probe 16 px ahead of the body enters a solid tile. The sweep is
    simulated tick by tick so it stops where the game's platform stops.
    """

    def __init__(self, pid, cell, vertical, is_solid_tile):
        self.pid = pid
        self.vertical = vertical
        ox, oy = cell[0] * TILE + TILE / 2, cell[1] * TILE + TILE / 2
        axis = (0, 1) if vertical else (1, 0)
        extremes = []
        for direction in (-1, 1):
            pos = 0.0
            while True:
                nxt = pos + direction * PLATFORM_SPEED * DT
                probe = (ox + axis[0] * (nxt + direction * 16),
                         oy - 4 + axis[1] * (nxt + direction * 16))
                if abs(nxt) > PLATFORM_RANGE or is_solid_tile(*cell_of(*probe)):
                    break
                pos = nxt
            extremes.append(pos)
        lo, hi = extremes
        self.samples = []
        offset = lo
        while offset <= hi + 1e-6:
            self.samples.append((ox + axis[0] * offset, oy + axis[1] * offset))
            offset += RIDE_STEP
        if self.samples[-1] != (ox + axis[0] * hi, oy + axis[1] * hi):
            self.samples.append((ox + axis[0] * hi, oy + axis[1] * hi))
        xs = [s[0] for s in self.samples]
        ys = [s[1] for s in self.samples]
        self.bbox = (min(xs) - 16 - HALF_X - 1, min(ys) - 10 - 2 * HALF_Y - 1,
                     max(xs) + 16 + HALF_X + 1, max(ys) + 2)

    @staticmethod
    def box(sample):
        sx, sy = sample
        return (sx - 16, sy - 10, sx + 16, sy + 2)

    @staticmethod
    def rider(sample):
        return (sample[0], sample[1] - 10 - HALF_Y)


class EnemyZone:
    def __init__(self, obj, cell, contact, sight):
        self.type = obj["type"]
        self.cell = cell
        self.contact = frozenset(contact)
        self.sight = frozenset(sight)

    @property
    def label(self):
        return "%s at (%d,%d)" % (self.type, self.cell[0], self.cell[1])


class World:
    def __init__(self, level):
        self.width = level["width"]
        self.height = level["height"]
        self.tiles, self.background = split_layers(level)
        self.name = level.get("name", "")
        w, h = self.width, self.height
        self.solid = set()
        self.dyn = {}            # cell -> (token, solid when token held)
        self.water = set()
        self.electric = {}       # electric floor cell -> token that turns it off, or None
        self.deadly = defaultdict(list)   # cell -> [(x0, y0, x1, y1, token_off)]
        self.pickups = {}        # cell -> (token, label)
        self.computers = []      # (cell, number)
        self.switches = []
        self.security_doors = []
        self.doors = {}          # cell -> color
        self.teleports = {}      # id -> (center, partner id)
        self.platforms = []
        self.enemies = []
        self.lasers = []         # (cell, top row, number)
        self.start = None
        self.exit = None
        self.dwalls = set()
        self.needs_password = False   # the exit door stays locked until the password is taken
        self.objects = level["objects"]

        for y in range(h):
            for x in range(w):
                t = self.tiles[y * w + x]
                if t == HIDDEN:
                    self.dyn[(x, y)] = ("goggles", True)
                elif t in DESTRUCTIBLE_TILES:
                    self.dwalls.add((x, y))
                elif t in SOLID_TILES:
                    self.solid.add((x, y))
                elif t == WATER:
                    self.water.add((x, y))
                elif t in (LAVA, SPEAR):
                    # Agent.CheckHazards samples 1 px inside the box.
                    self._add_deadly(x * TILE - (HALF_X - 1), y * TILE - (HALF_Y - 1),
                                     (x + 1) * TILE + (HALF_X - 1), (y + 1) * TILE + (HALF_Y - 1), None)
                if t == ELECTRIC:
                    self.electric[(x, y)] = None

        markers = {}
        # A connected destructible wall is one token, named after its first
        # cell: per-cell tokens multiply the soldier's inventories past what
        # the search can finish. Shooting through it costs one shot per cell
        # of the wall in the agent's row, since the agent is one tile tall.
        self.wall_group = {}     # cell -> (token, cells in the wall)
        for cell in sorted(self.dwalls, key=lambda c: (c[1], c[0])):
            if cell in self.wall_group:
                continue
            group, todo = set(), [cell]
            while todo:
                c = todo.pop()
                if c in group:
                    continue
                group.add(c)
                todo.extend(n for n in ((c[0] + 1, c[1]), (c[0] - 1, c[1]), (c[0], c[1] + 1), (c[0], c[1] - 1))
                            if n in self.dwalls)
            entry = ("wall:%d,%d" % cell, frozenset(group))
            for c in group:
                self.wall_group[c] = entry
                self.dyn[c] = (entry[0], False)

        capsules = defaultdict(list)
        for obj in self.objects:
            typ, x, y = obj["type"], obj["x"], obj["y"]
            props = obj.get("properties") or {}
            cell = (x, y)
            cx, cy = x * TILE + TILE / 2, y * TILE + TILE / 2
            if typ == "agent_start":
                self.start = cell
            elif typ == "exit_door":
                self.exit = cell
            elif typ in ("barrel", "barrel_steel"):
                self.solid.add(cell)
            elif typ == "door":
                color = props.get("color", "red")
                self.dyn[cell] = ("key:" + color, False)
                self.doors[cell] = color
            elif typ == "security_door":
                number = props.get("number", "1")
                self.dyn[cell] = ("off:" + number, False)
                self.security_doors.append((cell, number))
            elif typ == "laser":
                number = props.get("number", "1")
                top = y
                while top > 0 and self.tile(x, top - 1) not in SOLID_TILES - {HIDDEN}:
                    top -= 1
                self.lasers.append((cell, top, number))
                # LaserBeam.cs contact test: |dx| < HALF_X + 2, agent.y + HALF_Y > topY, agent.y - HALF_Y < emitter.y + 10
                self._add_deadly(cx - HALF_X - 2, top * TILE - HALF_Y, cx + HALF_X + 2, cy + 10 + HALF_Y, "off:" + number)
            elif typ == "landmine":
                # Enemy.CheckContact: the agent box meets the mine body (-13, 7, 26, 8) shrunk by 1 px.
                self._add_deadly(cx - 12 - HALF_X, cy + 8 - HALF_Y, cx + 12 + HALF_X, cy + 14 + HALF_Y, None)
            elif typ == "teleport":
                capsules[props.get("number", "1")].append((cx, cy))
            elif typ in ("elevator", "moving_platform"):
                self.platforms.append(Platform(len(self.platforms), cell, typ == "elevator", self.is_solid_tile))
            elif typ == "computer":
                self.computers.append((cell, props.get("number", "1")))
            elif typ == "switch":
                self.switches.append((cell, props.get("number", "1")))
            elif typ == "floor_circuit":
                markers[cell] = props.get("number", "1")
            elif typ in PICKUP_TOKENS:
                token = PICKUP_TOKENS[typ](props)
                self.needs_password |= token == "password"
                label = typ.replace("weapon_", "").replace("_", " ")
                if "color" in props:
                    label = props["color"] + " " + label
                if "number" in props:
                    label += " " + props["number"]
                self.pickups[cell] = (token, label)

        for number, pair in capsules.items():
            if len(pair) != 2:
                continue
            a, b = len(self.teleports), len(self.teleports) + 1
            self.teleports[a] = (pair[0], b)
            self.teleports[b] = (pair[1], a)

        for cell, number in markers.items():
            for member in self.flood(cell, lambda c: c in self.electric):
                self.electric[member] = "off:" + number

        for obj in self.objects:
            if obj["type"] in ENEMY_TYPES and obj["type"] != "landmine":
                self.enemies.append(self.enemy_zone(obj))

        self.exit_zone = None
        if self.exit:
            ex, ey = self.exit
            self.exit_zone = (ex * TILE, ey * TILE, (ex + 1) * TILE, (ey + 1) * TILE)

        self.node_cache = {}
        self.program_cache = {}

    def _add_deadly(self, x0, y0, x1, y1, token):
        for cy in range(math.floor(y0 / TILE), math.floor(y1 / TILE) + 1):
            for cx in range(math.floor(x0 / TILE), math.floor(x1 / TILE) + 1):
                self.deadly[(cx, cy)].append((x0, y0, x1, y1, token))

    def tile(self, x, y):
        if 0 <= x < self.width and 0 <= y < self.height:
            return self.tiles[y * self.width + x]
        return EMPTY

    def is_solid_tile(self, x, y):
        return self.tile(x, y) in SOLID_TILES - {HIDDEN}

    def flood(self, cell, inside):
        seen = {cell}
        stack = [cell]
        while stack:
            x, y = stack.pop()
            for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if n not in seen and inside(n):
                    seen.add(n)
                    stack.append(n)
        return seen

    def static_solid(self, cell):
        """Solid as the level starts: tiles, barrels, closed doors, intact walls."""
        if cell in self.solid:
            return True
        d = self.dyn.get(cell)
        return d is not None and not d[1]

    def is_solid(self, cell, inv, consulted=None):
        if cell in self.solid:
            return True
        d = self.dyn.get(cell)
        if d is None:
            return False
        if consulted is not None:
            consulted.add(d[0])
        return (d[0] in inv) == d[1]

    def standable(self, cell, inv, consulted=None):
        x, y = cell
        return self.is_solid((x, y + 1), inv, consulted) and not self.is_solid(cell, inv, consulted)

    def live_electric(self, cell, inv):
        token = self.electric.get(cell, "")
        return token is None or (token != "" and token not in inv)

    def node_pos(self, node):
        kind = node[0]
        if kind == "s":
            return (node[1] * TILE + TILE / 2, node[2] * TILE + TILE / 2 + 1)
        if kind == "w":
            return (node[1] * TILE + TILE / 2, node[2] * TILE + TILE / 2)
        if kind == "r":
            return Platform.rider(self.platforms[node[1]].samples[node[2]])
        if kind == "t":
            return self.teleports[node[1]][0]
        return None

    def node_cell(self, node):
        return cell_of(*self.node_pos(node))

    def node_ok(self, node, inv):
        kind = node[0]
        if kind == "s":
            cell = (node[1], node[2])
            if not self.standable(cell, inv):
                return False
            if (node[1], node[2] + 1) in self.electric and self.live_electric((node[1], node[2] + 1), inv):
                return False
        elif kind == "w":
            if (node[1], node[2]) not in self.water:
                return False
        elif kind == "r":
            if not self.ride_ok(node):
                return False
        px, py = self.node_pos(node)
        return not self.deadly_at(px, py, inv)

    def ride_ok(self, node):
        rx, ry = Platform.rider(self.platforms[node[1]].samples[node[2]])
        for cy in range(math.floor((ry - HALF_Y) / TILE), math.ceil((ry + HALF_Y) / TILE)):
            for cx in range(math.floor((rx - HALF_X) / TILE), math.ceil((rx + HALF_X) / TILE)):
                if self.static_solid((cx, cy)):
                    return False
        return True

    def deadly_at(self, px, py, inv, consulted=None):
        for x0, y0, x1, y1, token in self.deadly.get(cell_of(px, py), ()):
            if token is not None:
                if consulted is not None:
                    consulted.add(token)
                if token in inv:
                    continue
            if x0 < px < x1 and y0 < py < y1:
                return True
        return False

    def line_clear(self, a, b):
        """PlaySession.LineClear: samples every 16 px, solid tiles block."""
        dist = math.hypot(b[0] - a[0], b[1] - a[1])
        steps = math.ceil(dist / 16)
        for i in range(1, steps):
            t = i / steps
            if self.is_solid_tile(*cell_of(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)):
                return False
        return True

    def enemy_zone(self, obj):
        typ, x, y = obj["type"], obj["x"], obj["y"]
        contact, sight = set(), set()
        if typ in GROUND_ENEMIES:
            row = y
            while row < self.height and not self.static_solid((x, row + 1)):
                row += 1
            segment = {(x, row)}
            for d in (-1, 1):
                cx = x + d
                while not self.static_solid((cx, row)) and self.static_solid((cx, row + 1)):
                    segment.add((cx, row))
                    cx += d
            contact = segment
            if typ in ROW_SHOOTERS:
                sight = set(segment)
                lo = min(c[0] for c in segment)
                hi = max(c[0] for c in segment)
                for d, end in ((-1, lo), (1, hi)):
                    cx = end + d
                    while abs(cx - end) <= 10 and not self.static_solid((cx, row)):
                        sight.add((cx, row))
                        cx += d
        elif typ == "drone":
            contact = {(x, y)}
            for d in (-1, 1):
                cx = x + d
                while 0 <= cx < self.width and not self.static_solid((cx, y)):
                    contact.add((cx, y))
                    cx += d
        elif typ == "turret":
            contact = {(x, y)}
            tc = (x * TILE + TILE / 2, y * TILE + TILE / 2)
            r = math.ceil(ALL_SIGHT / TILE)
            for cy in range(y - r, y + r + 1):
                for cx in range(x - r, x + r + 1):
                    c = (cx * TILE + TILE / 2, cy * TILE + TILE / 2)
                    if math.hypot(c[0] - tc[0], c[1] - tc[1]) <= ALL_SIGHT and self.line_clear(tc, c):
                        sight.add((cx, cy))
        elif typ == "electric_eel":
            contact = self.flood((x, y), lambda c: c in self.water) if (x, y) in self.water else {(x, y)}
        elif typ == "robot_piranha":
            contact = {(x, y)}
            for k in range(1, 4):
                if self.static_solid((x, y - k)):
                    break
                contact.add((x, y - k))
        return EnemyZone(obj, (x, y), contact, sight)

    def blocked_cells(self, mode, disguise):
        cells = set()
        for e in self.enemies:
            if mode == "hacker" or e.type == "robot":
                cells |= e.contact
                if not disguise:
                    cells |= e.sight
        return frozenset(cells)

    # ---- physics simulation -------------------------------------------------

    def simulate(self, start, on_floor, jump, d, hold, inv, consulted, extra_box=None, self_node=None):
        """Run one input program from a position. Returns (dest, ticks, trace).

        dest is a node, EXIT, or None (died, got stuck, or never arrived).
        trace is the ordered list of cells the agent's center passed through.
        """
        cx, cy = start
        vx = vy = 0.0
        start_cell = cell_of(cx, cy)
        start_in_water = start_cell in self.water
        left_floor = not on_floor
        hold_from = 0 if jump else None
        trace = [start_cell]
        locked = {tid for tid, (c, _) in self.teleports.items() if abs(cx - c[0]) < 20 and abs(cy - c[1]) < 26}
        bottom_limit = self.height * TILE + 2 * TILE
        is_solid = self.is_solid
        water = self.water
        self_pid = self.platforms[self_node[1]].pid if self_node and self_node[0] == "r" else None

        def box_hits(nx, ny):
            # The box is the half-open [c - half, c + half): resting exactly on
            # a floor does not overlap it, sinking into it by any amount does.
            x0 = math.floor((nx - HALF_X) / TILE)
            x1 = math.ceil((nx + HALF_X) / TILE) - 1
            y0 = math.floor((ny - HALF_Y) / TILE)
            y1 = math.ceil((ny + HALF_Y) / TILE) - 1
            for yy in range(y0, y1 + 1):
                for xx in range(x0, x1 + 1):
                    if is_solid((xx, yy), inv, consulted):
                        return True
            return False

        def extra_hit(nx, ny):
            return extra_box is not None and nx + HALF_X > extra_box[0] and nx - HALF_X < extra_box[2] \
                and ny + HALF_Y > extra_box[1] and ny - HALF_Y < extra_box[3]

        for tick in range(MAX_TICKS):
            tx, ty = cell_of(cx, cy)
            submerged = (tx, ty) in water
            at_surface = submerged and cell_of(cx, cy - TILE) not in water
            vy += GRAVITY * DT * (WATER_GRAVITY if submerged else 1.0)
            if jump and tick == 0:
                if submerged:
                    vy = JUMP_VELOCITY if at_surface else -SWIM_SPEED
                elif on_floor:
                    vy = JUMP_VELOCITY
            if submerged:
                vy = min(vy, SINK_SPEED) if at_surface else max(-SWIM_SPEED, min(vy, SINK_SPEED))
            # A jump's hold counts from takeoff; a walk-off's from leaving the ledge.
            held = hold_from is None or tick - hold_from < hold
            axis = d if held else 0
            vx = axis * RUN_SPEED * (0.5 if submerged else 1.0)

            prev_cx = cx
            if vx:
                nx = cx + vx * DT
                if box_hits(nx, cy):
                    lead = math.ceil((nx + HALF_X) / TILE) - 1 if vx > 0 else math.floor((nx - HALF_X) / TILE)
                    nx = lead * TILE - HALF_X if vx > 0 else (lead + 1) * TILE + HALF_X
                if extra_hit(nx, cy):
                    nx = extra_box[0] - HALF_X if vx > 0 else extra_box[2] + HALF_X
                cx = nx

            landed_on = None
            dy = vy * DT
            steps = max(1, math.ceil(abs(dy) / 16))
            on_floor = False
            for _ in range(steps):
                ny = cy + dy / steps
                if vy > 0:
                    prev_bottom, new_bottom = cy + HALF_Y, ny + HALF_Y
                    best_top = None
                    for p in self.platforms:
                        if p.pid == self_pid:
                            continue
                        bx0, by0, bx1, by1 = p.bbox
                        if not (bx0 <= cx <= bx1 and by0 <= ny <= by1):
                            continue
                        for k, s in enumerate(p.samples):
                            top = s[1] - 10
                            if prev_bottom <= top < new_bottom and abs(cx - s[0]) < 16 + HALF_X:
                                if best_top is None or top < best_top:
                                    best_top, landed_on = top, ("r", p.pid, k)
                    if box_hits(cx, ny):
                        row = math.ceil((ny + HALF_Y) / TILE) - 1
                        if best_top is None or row * TILE < best_top:
                            best_top, landed_on = row * TILE, None
                    if extra_box is not None and extra_hit(cx, ny) and prev_bottom <= extra_box[1]:
                        if best_top is None or extra_box[1] < best_top:
                            best_top, landed_on = extra_box[1], self_node
                    if best_top is not None:
                        cy = best_top - HALF_Y
                        vy = 0.0
                        on_floor = True
                        break
                    cy = ny
                else:
                    if box_hits(cx, ny):
                        row = math.floor((ny - HALF_Y) / TILE)
                        cy = (row + 1) * TILE + HALF_Y
                        vy = 0.0
                        break
                    if extra_box is not None and extra_hit(cx, ny):
                        cy = extra_box[3] + HALF_Y
                        vy = 0.0
                        break
                    cy = ny

            cell = cell_of(cx, cy)
            if cell != trace[-1]:
                trace.append(cell)
            ticks = tick + 1

            if cy > bottom_limit or self.deadly_at(cx, cy, inv, consulted):
                return None, ticks, trace
            if self.exit_zone and self.exit_zone[0] < cx < self.exit_zone[2] and self.exit_zone[1] < cy < self.exit_zone[3]:
                return EXIT, ticks, trace
            for tid, (c, _) in self.teleports.items():
                inside = abs(cx - c[0]) < 20 and abs(cy - c[1]) < 26
                if not inside:
                    locked.discard(tid)
                elif tid not in locked:
                    return ("t", tid), ticks, trace
            if cell in water:
                if not (start_in_water and cell == start_cell):
                    return ("w", cell[0], cell[1]), ticks, trace
            if landed_on is not None:
                return landed_on, ticks, trace
            if on_floor:
                if left_floor:
                    if self.standable(cell, inv, consulted):
                        return ("s", cell[0], cell[1]), ticks, trace
                    # The box caught a ledge beside the center's cell: the player
                    # steps onto it.
                    for side in (-1, 1):
                        n = (cell[0] + side, cell[1])
                        overlaps = (cx - HALF_X < cell[0] * TILE) if side < 0 else (cx + HALF_X > (cell[0] + 1) * TILE)
                        if overlaps and self.standable(n, inv, consulted):
                            return ("s", n[0], n[1]), ticks, trace
                    return None, ticks, trace
                if cell != start_cell and self.standable(cell, inv, consulted):
                    return ("s", cell[0], cell[1]), ticks, trace
                if cx == prev_cx:
                    return None, ticks, trace   # walked into a wall
            else:
                if not left_floor and hold_from is None:
                    hold_from = tick
                left_floor = True
        return None, MAX_TICKS, trace

    def programs(self, node):
        kind = node[0]
        if kind == "t":
            return [("drop",)]
        if kind == "w":
            x, y = node[1], node[2]
            if (x, y - 1) in self.water:
                return []
            return [(True, d, h) for d in (-1, 1) for h in JUMP_HOLDS] + [(True, 0, 0)]
        progs = [(True, d, h) for d in (-1, 1) for h in JUMP_HOLDS] + [(True, 0, 0)]
        progs += [(False, d, WALK) for d in (-1, 1)]
        if kind == "s":
            x, y = node[1], node[2]
            for d in (-1, 1):
                if (x + d, y + 1) not in self.solid:
                    progs += [(False, d, h) for h in FALL_HOLDS]
        else:
            progs += [(False, d, h) for d in (-1, 1) for h in FALL_HOLDS]
        return progs

    def run_program(self, node, prog, inv):
        """Returns ((dest, ticks, trace), consulted tokens), cached per inventory slice."""
        key = (node, prog)
        for consulted, present, result in self.program_cache.get(key, ()):
            if inv & consulted == present:
                return result, consulted
        consulted = set()
        if prog[0] == "drop":
            partner = self.teleports[self.teleports[node[1]][1]][0]
            dest, ticks, trace = self.simulate(partner, False, False, 0, 0, inv, consulted)
        else:
            jump, d, hold = prog
            extra = None
            start = self.node_pos(node)
            if node[0] == "r":
                extra = Platform.box(self.platforms[node[1]].samples[node[2]])
            elif node[0] == "w":
                # README: a surface jump is calculated from the water's surface
                # line. The player swims up to it; a quarter tile below leaves
                # a few ticks of slack for the jump press.
                start = (start[0], node[2] * TILE + 8)
            dest, ticks, trace = self.simulate(start, True, jump, d, hold, inv, consulted,
                                               extra_box=extra, self_node=node)
        if dest == node:
            dest = None
        result = (dest, ticks, trace)
        consulted = frozenset(consulted)
        self.program_cache.setdefault(key, []).append((consulted, inv & consulted, result))
        return result, consulted

    def edges(self, node, inv):
        """Outgoing (dest, ticks, trace cells, trace frozenset) for this inventory."""
        for union, key, edges in self.node_cache.get(node, ()):
            if inv & union == key:
                return edges
        best = {}
        union = set()
        for prog in self.programs(node):
            (dest, ticks, trace), consulted = self.run_program(node, prog, inv)
            union |= consulted
            if dest is None:
                continue
            fs = frozenset(trace)
            k = (dest, fs)
            if k not in best or ticks < best[k][1]:
                best[k] = (dest, ticks, trace, fs)
        edges = list(best.values())
        if node[0] == "w":
            x, y = node[1], node[2]
            for n, ticks in (((x + 1, y), 17), ((x - 1, y), 17), ((x, y + 1), 24), ((x, y - 1), 35)):
                if n in self.water:
                    edges.append((("w", n[0], n[1]), ticks, [(x, y), n], frozenset([(x, y), n])))
        elif node[0] == "r":
            p = self.platforms[node[1]]
            for k in (node[2] - 1, node[2] + 1):
                if 0 <= k < len(p.samples):
                    a, b = Platform.rider(p.samples[node[2]]), Platform.rider(p.samples[k])
                    cells = [cell_of(*a), cell_of(*b)]
                    edges.append((("r", p.pid, k), RIDE_STEP, cells, frozenset(cells)))
        union = frozenset(union)
        self.node_cache.setdefault(node, []).append((union, inv & union, edges))
        return edges


# ---- search -----------------------------------------------------------------

ACTION_TIME = 0.5
FUSE_TIME = 5.0                 # Dynamite.cs: the stick explodes 5 s after it is placed


def center(cell):
    return (cell[0] * TILE + TILE / 2, cell[1] * TILE + TILE / 2)


def hacks_at(world, node, inv, forbid):
    px, py = world.node_pos(node)
    gained = []
    for cell, number in world.computers:
        token = "off:" + number
        if token in inv or "usb:" + number not in inv or ("hack", cell) in forbid:
            continue
        cx, cy = center(cell)
        if abs(cx - px) < 36 and abs(cy - py) < 36:
            gained.append((token, ("hack", cell, number)))
    return gained


def row_clear(world, y, x0, x1, inv):
    lo, hi = sorted((x0, x1))
    return all(not world.is_solid((x, y), inv) for x in range(lo + 1, hi))


def actions_at(world, node, inv, mode, forbid):
    """Explicit things the player does while standing: shoot a switch, shoot or blast a wall."""
    acts = []
    if node[0] not in ("s", "w"):
        return acts
    x, y = node[1], node[2]
    for cell, number in world.switches:
        token = "off:" + number
        if cell[1] != y or token in inv or ("switch", cell) in forbid:
            continue
        if row_clear(world, y, x, cell[0], inv):
            acts.append((frozenset([token]), ("switch", cell, number), ACTION_TIME))
    if mode != "soldier":
        return acts
    for d in (-1, 1):
        cx = x + d
        while 0 <= cx < world.width:
            c = (cx, y)
            if world.is_solid(c, inv):
                if c in world.wall_group and ("wall", c) not in forbid:
                    token, group = world.wall_group[c]
                    shots = sum(1 for g in group if g[1] == y)
                    acts.append((frozenset([token]), ("wall", c), ACTION_TIME * shots))
                break
            cx += d
    if "dynamite" in inv and ("blast", (x, y)) not in forbid:
        px, py = world.node_pos(node)
        hit = sorted(c for c in world.dwalls
                     if world.wall_group[c][0] not in inv and math.dist(center(c), (px, py)) <= EXPLOSION_RADIUS)
        if hit:
            acts.append((frozenset(world.wall_group[c][0] for c in hit), ("blast", (x, y), hit), FUSE_TIME))
    return acts


def find_start(world):
    sx, sy = world.start
    dest, _, _ = world.simulate(center((sx, sy)), False, False, 0, 0, frozenset(), set())
    return dest


class Route:
    def __init__(self, mode, reachable, time=0.0, steps=(), events=(), cells=frozenset(), polyline=(), reached=()):
        self.mode = mode
        self.reachable = reachable
        self.time = time
        self.steps = list(steps)
        self.events = list(events)
        self.cells = cells
        self.polyline = list(polyline)
        self.reached = frozenset(reached)   # cells of every node the search expanded


def search(world, mode, forbid=frozenset()):
    start = find_start(world)
    if start is None or start == EXIT:
        return Route(mode, start == EXIT)
    inv0 = frozenset()
    dist = {(start, inv0): 0.0}
    parent = {}
    heap = [(0.0, 0, start, inv0)]
    seen = defaultdict(list)
    blocked = {}
    counter = 1
    goal = None
    while heap:
        t, _, node, inv = heapq.heappop(heap)
        if dist.get((node, inv), math.inf) < t:
            continue
        if node == EXIT:
            goal = (node, inv)
            break
        # A superset inventory reached no later dominates: every token only opens geometry.
        if any(inv <= other for other in seen[node]):
            continue
        seen[node].append(inv)
        disguise = "briefcase" in inv
        if disguise not in blocked:
            blocked[disguise] = world.blocked_cells(mode, disguise)
        bl = blocked[disguise]

        def push(state, nt, info):
            nonlocal counter
            if nt < dist.get(state, math.inf):
                dist[state] = nt
                parent[state] = ((node, inv), info)
                counter += 1
                heapq.heappush(heap, (nt, counter, state[0], state[1]))

        for dest, ticks, trace, cells in world.edges(node, inv):
            if not cells.isdisjoint(bl):
                continue
            inv2 = inv
            events = []
            for c in trace:
                p = world.pickups.get(c)
                if p and p[0] not in inv2 and ("pickup", c) not in forbid:
                    inv2 = inv2 | {p[0]}
                    events.append(("pickup", c, p[1]))
            if dest == EXIT and world.needs_password and "password" not in inv2:
                continue
            if dest != EXIT:
                if not world.node_ok(dest, inv2):
                    continue
                for token, ev in hacks_at(world, dest, inv2, forbid):
                    inv2 = inv2 | {token}
                    events.append(ev)
            push((dest, inv2), t + ticks / 60, ("move", trace, events))
        for tokens, ev, cost in actions_at(world, node, inv, mode, forbid):
            push((node, inv | tokens), t + cost, ("action", [world.node_cell(node)], [ev]))
    reached = {world.node_cell(n) for n in seen}
    if goal is None:
        return Route(mode, False, reached=reached)

    chain = []
    state = goal
    while state in parent:
        prev, info = parent[state]
        chain.append((prev, state, info))
        state = prev
    chain.reverse()

    events, cells, polyline = [], set(), [world.node_pos(start)]
    opened = set()
    t = 0.0
    for (node, inv), (dest, inv2), (kind, trace, evs) in chain:
        t = dist[(dest, inv2)]
        cells |= set(trace)
        if kind == "move":
            polyline.extend(center(c) for c in trace[1:])
            if dest != EXIT:
                polyline.append(world.node_pos(dest))
            for c in trace:
                color = world.doors.get(c)
                if color and "key:" + color in inv2 and c not in opened:
                    opened.add(c)
                    events.append((t, "opened %s door at (%d,%d)" % (color, c[0], c[1]), None))
        for ev in evs:
            if ev[0] == "pickup":
                events.append((t, "picked %s at (%d,%d)" % (ev[2], ev[1][0], ev[1][1]), ("pickup", ev[1])))
            elif ev[0] == "hack":
                events.append((t, "hacked computer %s at (%d,%d)" % (ev[2], ev[1][0], ev[1][1]), ("hack", ev[1])))
            elif ev[0] == "switch":
                events.append((t, "shot switch %s at (%d,%d)" % (ev[2], ev[1][0], ev[1][1]), ("switch", ev[1])))
            elif ev[0] == "wall":
                events.append((t, "shot destructible wall at (%d,%d)" % ev[1], ("wall", ev[1])))
            elif ev[0] == "blast":
                walls = ", ".join("(%d,%d)" % c for c in ev[2])
                events.append((t, "dynamite at (%d,%d) destroyed %s" % (ev[1][0], ev[1][1], walls), ("blast", ev[1])))
            if ev[0] in ("hack", "switch"):
                for cell, number in world.security_doors:
                    if number == ev[2]:
                        events.append((t, "security door %s at (%d,%d) opened" % (number, cell[0], cell[1]), None))
    if goal[0] == EXIT:
        polyline.append(center(world.exit))
    return Route(mode, True, dist[goal], chain, events, frozenset(cells), polyline, reached)


def kills_proxy(world, route):
    return [e for e in world.enemies if not route.cells.isdisjoint(e.contact | e.sight)]


def necessity(world, route):
    """Rerun the search with each event's item or hack forbidden."""
    report = []
    for t, text, key in route.events:
        if key is None:
            continue
        report.append((text, search(world, route.mode, forbid=frozenset([key]))))
    return report


# ---- report -----------------------------------------------------------------

def describe(world, mode, route, show_necessity):
    lines = []
    if not route.reachable:
        lines.append("[%s] exit NOT reachable" % mode)
        if route.reached:
            xs = [c[0] for c in route.reached]
            ys = [c[1] for c in route.reached]
            nearest = min(route.reached, key=lambda c: math.dist(c, world.exit))
            lines.append("  reached %d cells, x %d..%d, y %d..%d; nearest to the exit: (%d,%d)"
                         % (len(route.reached), min(xs), max(xs), min(ys), max(ys), nearest[0], nearest[1]))
        return lines
    lines.append("[%s] exit reachable in %.1f s (%d moves)" % (mode, route.time, len(route.steps)))
    for i, (t, text, _) in enumerate(route.events, 1):
        lines.append("  %2d. %6.1fs  %s" % (i, t, text))
    if mode == "soldier":
        kills = kills_proxy(world, route)
        names = (": " + ", ".join(k.label for k in kills)) if kills else ""
        lines.append("  kill proxy: %d enemy zones crossed%s" % (len(kills), names))
    if show_necessity and any(key for _, _, key in route.events):
        lines.append("  necessity (rerun with the event forbidden):")
        for text, alt in necessity(world, route):
            if alt.reachable:
                verdict = "not required, exit still reachable in %.1f s" % alt.time
            else:
                verdict = "REQUIRED, exit unreachable without it"
            lines.append("    %-48s %s" % (text + ":", verdict))
    return lines


# ---- PNG --------------------------------------------------------------------

TILE_COLORS = {
    EMPTY: (24, 24, 30), WALL: (150, 150, 165), DWALL: (170, 120, 60), ALARM: (200, 150, 30),
    ELECTRIC: (230, 210, 40), WATER: (40, 90, 200), LAVA: (240, 70, 20), SPIKES: (200, 200, 210),
    SPEAR: (150, 40, 60), HIDDEN: (90, 170, 190),
}
OBJECT_COLORS = {
    "agent_start": (255, 60, 150), "exit_door": (60, 240, 100), "checkpoint": (64, 174, 78),
    "computer": (60, 220, 220),
    "switch": (200, 120, 220), "teleport": (140, 90, 240), "barrel": (160, 110, 60), "barrel_steel": (120, 135, 165),
    "security_door": (255, 160, 40), "closet": (120, 90, 60), "camera": (170, 170, 190),
    "flame_trap": (240, 130, 40), "electric_door": (230, 230, 100), "rocket_launcher": (150, 80, 80),
    "floor_circuit": (255, 255, 255),
}
KEY_COLORS = {"red": (230, 50, 50), "blue": (60, 130, 240), "yellow": (240, 220, 50), "magenta": (220, 60, 220)}
DIGITS = {
    "0": ("111", "101", "101", "101", "111"), "1": ("010", "110", "010", "010", "111"),
    "2": ("111", "001", "111", "100", "111"), "3": ("111", "001", "111", "001", "111"),
    "4": ("101", "101", "111", "001", "001"), "5": ("111", "100", "111", "001", "111"),
    "6": ("111", "100", "111", "101", "111"), "7": ("111", "001", "001", "001", "001"),
    "8": ("111", "101", "111", "101", "111"), "9": ("111", "101", "111", "001", "111"),
}


class Canvas:
    def __init__(self, w, h, fill=(0, 0, 0)):
        self.w, self.h = w, h
        self.px = bytearray(bytes(fill) * (w * h))

    def put(self, x, y, color):
        if 0 <= x < self.w and 0 <= y < self.h:
            i = 3 * (y * self.w + x)
            self.px[i:i + 3] = bytes(color)

    def rect(self, x0, y0, x1, y1, color):
        for y in range(max(0, y0), min(self.h, y1)):
            for x in range(max(0, x0), min(self.w, x1)):
                self.put(x, y, color)

    def line(self, a, b, color, thick=1):
        x0, y0 = int(round(a[0])), int(round(a[1]))
        x1, y1 = int(round(b[0])), int(round(b[1]))
        dx, dy = abs(x1 - x0), -abs(y1 - y0)
        sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
        err = dx + dy
        while True:
            for ox in range(thick):
                for oy in range(thick):
                    self.put(x0 + ox, y0 + oy, color)
            if x0 == x1 and y0 == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x0 += sx
            if e2 <= dx:
                err += dx
                y0 += sy

    def text(self, x, y, s, color, scale=2):
        for ch in s:
            glyph = DIGITS.get(ch)
            if glyph:
                for gy, row in enumerate(glyph):
                    for gx, bit in enumerate(row):
                        if bit == "1":
                            self.rect(x + gx * scale, y + gy * scale, x + (gx + 1) * scale, y + (gy + 1) * scale, color)
            x += 4 * scale

    def save(self, path):
        stride = 3 * self.w
        raw = b"".join(b"\x00" + bytes(self.px[y * stride:(y + 1) * stride]) for y in range(self.h))

        def chunk(tag, data):
            body = tag + data
            return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

        with open(path, "wb") as f:
            f.write(b"\x89PNG\r\n\x1a\n")
            f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", self.w, self.h, 8, 2, 0, 0, 0)))
            f.write(chunk(b"IDAT", zlib.compress(raw, 9)))
            f.write(chunk(b"IEND", b""))


def render(world, routes, path, scale=8):
    S = scale
    cv = Canvas(world.width * S, world.height * S)
    for y in range(world.height):
        for x in range(world.width):
            t = world.tile(x, y)
            color = TILE_COLORS.get(t)
            if color is None:
                color = (110, 110, 125) if t in SOLID_TILES else (34, 36, 44)
            if t == EMPTY and world.background[y * world.width + x]:
                color = (40, 44, 58)
            cv.rect(x * S, y * S, (x + 1) * S, (y + 1) * S, color)
            if t in SOLID_TILES:
                cv.rect(x * S, y * S, (x + 1) * S, y * S + 1, tuple(min(255, c + 40) for c in color))
    for cell, token in world.electric.items():
        if token is None:
            cv.rect(cell[0] * S + 2, cell[1] * S + 2, cell[0] * S + S - 2, cell[1] * S + S - 2, (200, 60, 40))
    for p in world.platforms:
        a, b = p.samples[0], p.samples[-1]
        cv.line((a[0] * S / TILE, a[1] * S / TILE), (b[0] * S / TILE, b[1] * S / TILE), (90, 90, 110), 2)
        for s in (a, b):
            cv.rect(int((s[0] - 16) * S / TILE), int((s[1] - 10) * S / TILE),
                    int((s[0] + 16) * S / TILE), int((s[1] + 2) * S / TILE), (180, 190, 210))
    for (cell, top, number) in world.lasers:
        x = int(cell[0] * S + S / 2)
        cv.line((x, top * S), (x, cell[1] * S + S / 2), (255, 40, 40), 1)
    for obj in world.objects:
        typ, x, y = obj["type"], obj["x"], obj["y"]
        props = obj.get("properties") or {}
        color = OBJECT_COLORS.get(typ)
        if typ in ("door", "door_key"):
            color = KEY_COLORS.get(props.get("color", "red"), KEY_COLORS["red"])
        elif typ in PICKUP_TOKENS:
            color = (250, 230, 90)
        elif typ == "laser":
            color = (255, 40, 40)
        elif typ == "landmine":
            color = (255, 0, 255)
        elif typ in ENEMY_TYPES:
            color = (235, 50, 50)
        elif typ in ("elevator", "moving_platform", "light") or typ.startswith("deco_") or color is None:
            continue
        inset = 1 if typ in ("door", "security_door", "exit_door") else 2
        cv.rect(x * S + inset, y * S + inset, (x + 1) * S - inset, (y + 1) * S - inset, color)
        if typ in ("door", "security_door"):
            cv.rect(x * S + 3, y * S + 3, (x + 1) * S - 3, (y + 1) * S - 3, (20, 20, 20))
    route_colors = {"hacker": (40, 230, 90), "soldier": (255, 150, 30)}
    offset = {"hacker": -1, "soldier": 1}
    for mode, route in routes.items():
        if route.reachable:
            continue
        # No route: dot every cell the search reached so the dead end shows.
        for x, y in route.reached:
            d = 2 + offset[mode]
            cv.rect(x * S + d, y * S + d, x * S + d + 2, y * S + d + 2, route_colors[mode])
    for mode, route in routes.items():
        if not route.reachable:
            continue
        pts = [(px * S / TILE + offset[mode], py * S / TILE + offset[mode]) for px, py in route.polyline]
        for a, b in zip(pts, pts[1:]):
            cv.line(a, b, route_colors[mode], 2)
    for mode, route in routes.items():
        if not route.reachable:
            continue
        label_cells = {}
        for i, (t, text, key) in enumerate(route.events, 1):
            where = tuple(int(v) for v in re.search(r"\((\d+),(\d+)\)", text).groups())
            label_cells.setdefault(where, []).append(str(i))
        for (x, y), labels in label_cells.items():
            s = labels[0]
            w = 4 * len(s) * 2
            px = x * S + S // 2 - w // 2
            py = y * S - 12 if mode == "hacker" else y * S + S + 1
            cv.rect(px - 2, py - 1, px + w + 1, py + 11, (0, 0, 0))
            cv.rect(px - 1, py, px + w, py + 10, route_colors[mode])
            cv.text(px, py, s, (0, 0, 0))
    cv.save(path)


# ---- CLI --------------------------------------------------------------------

def check(level, modes, png=None, scale=8, out=sys.stdout):
    world = World(level)
    lines = ["Level %r %dx%d, start %s, exit %s" % (world.name, world.width, world.height, world.start, world.exit)]
    routes = {}
    ok = True
    if world.start is None or world.exit is None:
        lines.append("level needs an agent start and an exit door")
        ok = False
    else:
        for mode in modes:
            route = search(world, mode)
            routes[mode] = route
            lines.extend(describe(world, mode, route, show_necessity=(mode == "hacker")))
            ok = ok and route.reachable
        if png:
            render(world, routes, png, scale)
            lines.append("PNG: %s" % png)
    out.write("\n".join(lines) + "\n")
    return ok, routes


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("level")
    ap.add_argument("--png")
    ap.add_argument("--mode", choices=("hacker", "soldier", "both"), default="both")
    ap.add_argument("--scale", type=int, default=8, help="PNG pixels per tile")
    args = ap.parse_args(argv)
    modes = ("hacker", "soldier") if args.mode == "both" else (args.mode,)
    ok, _ = check(load_level(args.level), modes, args.png, args.scale)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
