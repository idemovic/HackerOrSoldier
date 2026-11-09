#!/usr/bin/env python3
"""Checks every campaign level against the rules a finished city must meet.

    python tools/check_campaign.py [--strict] [--png-dir DIR]

Reads the LevelFile of each stop from scripts/Campaign.cs. For each file that
exists in levels/ it requires: formatVersion 3, exactly one agent_start, at
least one exit_door, exactly one admin_password, the exit reachable in soldier
and in hacker mode, and the password REQUIRED in the hacker necessity report.
Prints one row per city. A missing file is reported as MISSING and is a
failure only with --strict. Exit status is nonzero on any failure.
"""
import argparse
import os
import pathlib
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import route_check as rc  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parent.parent
CAMPAIGN = ROOT / "scripts" / "Campaign.cs"
LEVELS = ROOT / "levels"
ENEMY_NAMES = rc.ENEMY_TYPES - {"landmine"}


def campaign_stops():
    """(city, level file) for each stop; the Origin has no level."""
    text = CAMPAIGN.read_text(encoding="utf-8")
    block = text[text.index("Stops ="):]
    stops = re.findall(r'new\("([^"]+)",[^"]*?"(?:[^"\\]|\\.)*",\s*"([^"]+\.json)"\)', block)
    if not stops:
        raise SystemExit("no stops with a LevelFile found in %s" % CAMPAIGN)
    return stops


def validate(level):
    problems = []
    if level.get("formatVersion") != 3:
        problems.append("formatVersion %s, want 3" % level.get("formatVersion"))
    counts = {}
    for o in level["objects"]:
        counts[o["type"]] = counts.get(o["type"], 0) + 1
    if counts.get("agent_start", 0) != 1:
        problems.append("agent_start x%d, want 1" % counts.get("agent_start", 0))
    if counts.get("exit_door", 0) < 1:
        problems.append("no exit_door")
    if counts.get("admin_password", 0) != 1:
        problems.append("admin_password x%d, want 1" % counts.get("admin_password", 0))
    return problems


def check_level(path, png_dir):
    """Returns (row fields, problems)."""
    level = rc.load_level(path)
    problems = validate(level)
    row = {"size": "%dx%d" % (level["width"], level["height"]), "soldier": None, "hacker": None,
           "enemies": sum(o["type"] in ENEMY_NAMES for o in level["objects"])}
    if any(p.startswith(("agent_start", "no exit")) for p in problems):
        return row, problems
    world = rc.World(level)
    routes = {}
    for mode in ("soldier", "hacker"):
        routes[mode] = rc.search(world, mode)
        if routes[mode].reachable:
            row[mode] = routes[mode].time
        else:
            problems.append("%s cannot reach the exit" % mode)
    hacker = routes["hacker"]
    if hacker.reachable and any("admin password" in text for _, text, _ in hacker.events):
        for text, alt in rc.necessity(world, hacker):
            if "admin password" in text and alt.reachable:
                problems.append("hacker mode reaches the exit without the password (%s)" % text)
    elif hacker.reachable:
        problems.append("the hacker route does not pick up the password")
    if png_dir:
        png_dir.mkdir(parents=True, exist_ok=True)
        rc.render(world, routes, str(png_dir / (path.stem + ".png")))
    return row, problems


def seconds(value):
    return "%.1f" % value if value is not None else "-"


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--strict", action="store_true", help="count a missing level file as a failure")
    ap.add_argument("--png-dir", type=pathlib.Path, help="write a route PNG per level here")
    args = ap.parse_args(argv)

    fmt = "%-15s %-26s %-7s %8s %8s %8s  %s"
    print(fmt % ("city", "file", "size", "soldier", "hacker", "enemies", "status"))
    failed = missing = 0
    for city, file in campaign_stops():
        path = LEVELS / file
        if not path.exists():
            missing += 1
            print(fmt % (city, file, "-", "-", "-", "-", "MISSING"))
            continue
        row, problems = check_level(path, args.png_dir)
        failed += bool(problems)
        print(fmt % (city, file, row["size"], seconds(row["soldier"]), seconds(row["hacker"]), row["enemies"],
                     "FAIL: " + "; ".join(problems) if problems else "ok"))
    total = len(campaign_stops())
    print("%d of %d levels built, %d failing, %d missing" % (total - missing, total, failed, missing))
    return 1 if failed or (args.strict and missing) else 0


if __name__ == "__main__":
    sys.exit(main())
