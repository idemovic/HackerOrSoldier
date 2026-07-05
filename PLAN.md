# Development plan

Tech: Godot 4.7 (.NET edition), C#, targeting a Steam release (Windows + Linux/Steam Deck).
Design source of truth: [README.md](README.md). This file tracks build order and status.

The guiding principle: the level editor is the main deliverable (see "Objective of design"
in the README), so everything is built as placeable objects on top of the editor loop.
Placeholder colored rects stand in for art until the systems are done.

## Done

### Scaffold
- Godot 4.7 .NET project: 640x360 viewport, integer scaling, pixel filtering off,
  GL Compatibility renderer
- Input map from the README default controls (keyboard + gamepad), rebindable actions:
  move_left, move_right, jump, shoot, activate, pause, editor_play (P)
- Agent movement derived from the README spec: jumps 2.5 tiles high, 5 tiles sideways
  (gravity, jump velocity and run speed computed from those constraints)

### Milestone 1 - editor loop
- Level data model: tile grid + object list with a properties dictionary
  (scripts/LevelData.cs), JSON save/load in user://levels/
- Validation: exactly one agent start, at least one exit door
- Placeholder tileset generated at runtime with collision on solid tiles
  (scripts/TileSetBuilder.cs)
- Editor scene: object palette, paint/erase, object placement, camera pan,
  play/stop toggle (P or button, Esc stops), save/load dialogs with name and
  description, top-right minimap with camera view outline
- Headless smoke test of the whole loop (scenes/smoke_test.tscn):
  Godot_v4.7-stable_mono_win64_console.exe --headless --path . res://scenes/smoke_test.tscn

## Milestone 2 - core game systems (next)

First slice, "dying":
- Lives on the agent (default 3), HUD counter during play
- Killing terrain behavior: lava and spear instant kill, spikes on a 10s cycle,
  electric floor takes a life every 3 seconds
- Kill plane below the level bounds
- Losing a life respawns at the agent start; losing all lives ends the run
  (in the editor: back to edit mode)
- Water: slower horizontal movement, jump boost calculated from the surface line

Then:
- Doors and keys: color matching, which introduces the object property UI in the
  editor (the last missing piece of the original milestone 1 scope)
- Item pickup and inventory: health kit, ammo pack, bulletproof vest, briefcase,
  infra goggles (hidden platforms)
- Shooting: pistol first (wire the shoot action), bullets, ammo count
- Destructible walls

## Milestone 3 - enemies
- Line of sight: row vs all directions
- Patrol movement and speeds per the enemy table
- Soldier degradation chain (Ultimate -> Super -> Soldier -> tombstone)
- Alarm system: triggers (gunshot, explosion, camera, alarm floor), 30s timer,
  enemies at double speed, room appearance change
- Enemy drops (ammo packs)

## Milestone 4 - hacking path
- Computers + USB keys (number matching), switches, laser beams
- Security cameras
- Closets (activate action), briefcase disguise
- Movable barrels

## Milestone 5 - meta layer
- Main menu (New game, Continue, Settings, Create level, Exit)
- Shop and coin economy, collectibles with score
- Statistics screen (time, score, kills, accuracy, completeness) with the
  Hacker tag for non-violent runs
- Settings: difficulty, key rebinding, music/sound volume
- Level briefing screen showing level name and description

## Milestone 6 - Steam prep
- Export presets for Windows and Linux
- GodotSteam integration: achievements, cloud saves
- Steam Workshop publishing for user levels (the JSON level format is already
  share-ready)
- Store page assets

## Deferred on purpose
- Visual layer (VGA palette + dithering shader, real sprites, 32x32 tile art):
  after milestone 2 or 3. Placeholder rects iterate faster, and the retro
  aesthetic is an asset swap plus one shader, not a structural decision.
- Level size is fixed at 40x12 tiles for now; the format stores width/height,
  so resizing is a later editor feature, not a format change.

## Open design decisions
- Respawn semantics: current plan is respawn at level start but keep level state
  (opened doors stay open, dead enemies stay dead). Alternative: full level reset
  per death. README does not specify.
