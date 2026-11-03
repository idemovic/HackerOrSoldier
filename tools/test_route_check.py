"""Controls for route_check: synthetic levels whose answer is known from the physics spec.

    python tools/test_route_check.py        prints every control's report
    python -m pytest tools/test_route_check.py
"""
import io
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import route_check as rc  # noqa: E402

CHARS = {".": rc.EMPTY, "#": rc.WALL, "~": rc.WATER, "L": rc.LAVA, "|": rc.SPEAR,
         "E": rc.ELECTRIC, "D": rc.DWALL, "H": rc.HIDDEN}


def level(rows, *objects):
    w, h = len(rows[0]), len(rows)
    assert all(len(r) == w for r in rows)
    return {"name": "fixture", "width": w, "height": h,
            "tiles": [CHARS[c] for row in rows for c in row],
            "objects": [{"type": t, "x": x, "y": y, "properties": props} for t, x, y, props in objects]}


def obj(typ, x, y, **props):
    return (typ, x, y, props)


def run(title, lvl, modes=("hacker",)):
    out = io.StringIO()
    ok, routes = rc.check(lvl, modes, out=out)
    print("--- %s" % title)
    print(out.getvalue().rstrip())
    return ok, routes


def corridor(width, floor_row, extra_rows=()):
    rows = ["." * width for _ in range(floor_row)] + ["#" * width]
    for y, row in extra_rows:
        rows[y] = row
    return rows


def test_step_height():
    # Standing row 5 on the lower floor; the upper floor's top is at row 4
    # (a 2-tile step) or row 3 (a 3-tile step). The agent jumps 2.5 tiles.
    two = level([
        "............",
        "............",
        "............",
        "............",
        ".......#####",
        ".......#####",
        "############",
    ], obj("agent_start", 1, 5), obj("exit_door", 10, 3))
    three = level([
        "............",
        "............",
        "............",
        ".......#####",
        ".......#####",
        ".......#####",
        "############",
    ], obj("agent_start", 1, 5), obj("exit_door", 10, 2))
    ok2, _ = run("2-tile step (climbable)", two)
    ok3, _ = run("3-tile step (not climbable)", three)
    assert ok2 and not ok3


def test_gap_width():
    four = level(corridor(16, 3, [(3, "######....######")]), obj("agent_start", 1, 2), obj("exit_door", 14, 2))
    six = level(corridor(18, 3, [(3, "######......######")]), obj("agent_start", 1, 2), obj("exit_door", 16, 2))
    ok4, _ = run("4-tile gap (jumpable)", four)
    ok6, _ = run("6-tile gap (not jumpable)", six)
    assert ok4 and not ok6


def test_tunnel_ceiling():
    # The same 3-tile gap in open air and under a ceiling two tiles above the
    # floor: the head bump caps the rise at 34 px and the arc falls short.
    open_air = level([
        "..............",
        "..............",
        "..............",
        "..............",
        "######...#####",
    ], obj("agent_start", 1, 3), obj("exit_door", 12, 3))
    tunnel = level([
        "##############",
        "..............",
        "..............",
        "######...#####",
    ], obj("agent_start", 1, 2), obj("exit_door", 12, 2))
    ok_open, _ = run("3-tile gap, open air (jumpable)", open_air)
    ok_tunnel, _ = run("3-tile gap under a 2-tile-high ceiling (not jumpable)", tunnel)
    assert ok_open and not ok_tunnel


def gated_corridor(with_laser=True):
    objects = [obj("agent_start", 1, 2), obj("usb_key", 4, 2, number="1"), obj("computer", 7, 2, number="1"),
               obj("exit_door", 18, 2)]
    if with_laser:
        objects.append(obj("laser", 12, 2, number="1"))
    return level(corridor(20, 3, [(0, "#" * 20)]), *objects)


def test_laser_blocks_until_hacked():
    no_key = gated_corridor()
    no_key["objects"] = [o for o in no_key["objects"] if o["type"] != "usb_key"]
    ok_blocked, _ = run("active laser, no USB key (blocked)", no_key)
    ok_hacked, routes = run("laser with USB key + computer on the way (hacked)", gated_corridor())
    assert not ok_blocked and ok_hacked
    texts = [e[1] for e in routes["hacker"].events]
    assert any(t.startswith("picked usb key 1") for t in texts), texts
    assert any(t.startswith("hacked computer 1") for t in texts), texts


def test_security_door_blocks_until_hacked():
    # A 1-tile-high corridor: under a 2-tile-high ceiling the agent hops onto
    # a lone door's top (32 px of space for a 30 px box), which is correct.
    def lvl(with_key):
        objects = [obj("agent_start", 1, 2), obj("computer", 6, 2, number="3"),
                   obj("security_door", 11, 2, number="3"), obj("exit_door", 16, 2)]
        if with_key:
            objects.append(obj("usb_key", 3, 2, number="3"))
        return level(corridor(18, 3, [(0, "#" * 18), (1, "#" * 18)]), *objects)
    ok_closed, _ = run("security door 3, no USB key (closed)", lvl(False))
    ok_open, routes = run("security door 3 after hacking computer 3 (open)", lvl(True))
    assert not ok_closed and ok_open
    texts = [e[1] for e in routes["hacker"].events]
    assert any("security door 3" in t and "opened" in t for t in texts), texts


def test_necessity_flips_without_the_gate():
    _, gated = run("necessity with a real laser gate", gated_corridor(True))
    _, fake = run("necessity when the laser is removed (fake gate)", gated_corridor(False))
    world = rc.World(gated_corridor(True))
    required = {text: alt.reachable for text, alt in rc.necessity(world, gated["hacker"])}
    assert required == {t: False for t in required}, required
    world = rc.World(gated_corridor(False))
    not_required = {text: alt.reachable for text, alt in rc.necessity(world, fake["hacker"])}
    assert not_required and all(not_required.values()), not_required


def soldier_corridor(exit_x, with_briefcase):
    # A ledge at the left keeps the start out of the soldier's row; pits at
    # x=5 and x=13 bound its patrol to 6..12 (wider than a jump), and its row
    # of fire covers the corridor.
    rows = [
        "........................",
        "........................",
        "##......................",
        "#####.#######.##########",
        "########################",
    ]
    objects = [obj("agent_start", 0, 1), obj("soldier", 9, 2), obj("exit_door", exit_x, 2)]
    if with_briefcase:
        objects.append(obj("briefcase", 1, 1))
    return level(rows, *objects)


def test_soldier_blocks_hacker_not_soldier():
    ok_h, _ = run("soldier corridor, hacker mode (blocked by the row of fire)", soldier_corridor(3, False))
    ok_s, routes = run("soldier corridor, soldier mode (passable)", soldier_corridor(17, False), ("soldier",))
    assert not ok_h and ok_s
    assert len(rc.kills_proxy(rc.World(soldier_corridor(17, False)), routes["soldier"])) == 1


def test_briefcase_unblocks_fire_not_patrol():
    ok_fire, _ = run("briefcase held, exit inside the row of fire (passable)", soldier_corridor(3, True))
    ok_patrol, _ = run("briefcase held, exit beyond the patrol cells (blocked)", soldier_corridor(17, True))
    assert ok_fire and not ok_patrol


def test_elevator_and_teleport():
    # Exit on a ledge 7 tiles above a sealed pit: only the elevator (4 tiles
    # of travel) reaches it. A second pit is left only by its teleport.
    rows = [
        "..............#....",
        "..............#....",
        "..........#####....",
        "..............#....",
        "..............#....",
        "..............#....",
        "..............#....",
        "..............#....",
        "..............#....",
        "###################",
    ]
    shaft = level(rows, obj("agent_start", 1, 8), obj("elevator", 8, 8), obj("exit_door", 12, 1))
    no_lift = level(rows, obj("agent_start", 1, 8), obj("exit_door", 12, 1))
    ok_lift, routes = run("ledge 7 tiles up, elevator in the pit (reachable)", shaft)
    ok_none, _ = run("ledge 7 tiles up, no elevator (unreachable)", no_lift)
    assert ok_lift and not ok_none
    assert any(n[0] == "r" for (n, _), _, _ in routes["hacker"].steps), "the route rides the elevator"
    sealed = level(rows, obj("agent_start", 1, 8), obj("teleport", 4, 8, number="1"),
                   obj("teleport", 16, 8, number="1"), obj("exit_door", 17, 8))
    ok_tele, routes = run("exit behind a full-height wall, teleport pair (reachable)", sealed)
    assert ok_tele
    assert any(n[0] == "t" for (n, _), _, _ in routes["hacker"].steps), "the route uses the capsule"


def test_destructible_wall_soldier_only():
    rows = corridor(16, 3, [(0, "#" * 16), (1, "#" * 16), (2, "........D......."), (3, "#" * 16)])
    wall = level(rows, obj("agent_start", 1, 2), obj("exit_door", 14, 2))
    ok_h, _ = run("destructible wall, hacker mode (solid)", wall)
    ok_s, routes = run("destructible wall, soldier mode (shot from the row)", wall, ("soldier",))
    assert not ok_h and ok_s
    assert any(t.startswith("shot destructible wall at (8,2)") for _, t, _ in routes["soldier"].events)


def test_electric_floor_region():
    rows = corridor(14, 3, [(3, "####EEEEE#####")])
    live = level(rows, obj("agent_start", 1, 2), obj("exit_door", 12, 2))
    marked = level(rows, obj("agent_start", 1, 2), obj("exit_door", 12, 2), obj("floor_circuit", 6, 3, number="2"),
                   obj("usb_key", 2, 2, number="2"), obj("computer", 3, 2, number="2"))
    ok_live, _ = run("unmarked electric floor (live forever, blocked)", live)
    ok_off, _ = run("electric floor on circuit 2, hacked off (passable)", marked)
    assert not ok_live and ok_off


if __name__ == "__main__":
    failures = 0
    for name, fn in sorted(globals().items()):
        if name.startswith("test_") and callable(fn):
            try:
                fn()
                print("PASS %s\n" % name)
            except AssertionError as e:
                failures += 1
                print("FAIL %s: %s\n" % (name, e))
    sys.exit(1 if failures else 0)
