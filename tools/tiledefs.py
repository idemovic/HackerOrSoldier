"""Tile ids and their properties, read from scripts/TileDefs.cs so the Python
tools cannot drift from the game. Parses the `public const int Name = N;` ids,
the `new(Id, "...", Color, Solid: bool)` table and the id arrays."""
import pathlib
import re

SOURCE = pathlib.Path(__file__).resolve().parent.parent / "scripts" / "TileDefs.cs"


def _parse(text):
    ids = {m[0]: int(m[1]) for m in re.findall(r"public const int (\w+) = (\d+);", text)}
    solid = {ids[m[0]] for m in re.findall(r'new\((\w+), "[^"]*", new Color\([^)]*\), Solid: (true)\)', text)}

    def id_array(name):
        body = re.search(name + r"\s*=\s*\{([^}]*)\}", text)[1]
        return {ids[n.strip()] for n in body.split(",") if n.strip()}

    return ids, solid, id_array("DestructibleWalls"), id_array("BackgroundWalls")


IDS, SOLID, DESTRUCTIBLE, BACKGROUND = _parse(SOURCE.read_text(encoding="utf-8"))
HIDDEN = IDS["HiddenPlatform"]
