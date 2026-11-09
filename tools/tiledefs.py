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


def _object_types():
    """Every object type the game knows: the string consts of ObjectDefs,
    CollectibleDefs and EnemyDefs plus the deco_* table. An unknown type
    would load as a pickup, so levelkit rejects it."""
    scripts = SOURCE.parent
    types = set()
    for name in ("TileDefs.cs", "CollectibleDefs.cs", "EnemyDefs.cs"):
        types |= set(re.findall(r'public const string \w+ = "([a-z_]+)";', (scripts / name).read_text(encoding="utf-8")))
    types |= set(re.findall(r'new\("(deco_\w+)"', (scripts / "Decoration.cs").read_text(encoding="utf-8")))
    return types


OBJECT_TYPES = _object_types()
