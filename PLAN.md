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

### Milestone 2 - core game systems
- PlaySession (scripts/PlaySession.cs) owns one play-through: agent, lives,
  ammo, inventory, hazard clock, HUD; freeing it restores the level
- Lives (default 3) with HUD during play; losing a life respawns at the agent
  start but keeps level state (opened doors stay open, picked items stay
  picked); losing all lives returns to edit mode
- Killing terrain: lava and spear instant kill, spikes raised during the last
  2s of each 10s cycle, electric floor takes a life after each 3s of contact,
  kill plane below the level bounds
- Water: half horizontal speed, capped sink/swim speed while submerged, full
  jump velocity when jumping from the top water tile (surface line)
- Doors and keys with color matching (red/blue/yellow/magenta); the palette
  shows a color picker when a door or key brush is selected (first object
  property UI); a key is not consumed and opens every door of its color
- Item pickups: health kit (+1 life), ammo pack (+10), bulletproof vest
  (absorbs one hit), briefcase and infra goggles held until activated with C
  (goggles take priority when both are held; 30s each)
- Hidden platform tile on its own tilemap layer: semi-transparent in edit
  mode, invisible but solid in play mode, revealed while goggles are active
- Shooting: pistol (default 10 ammo), bullets at 2x agent speed, destructible
  walls destroyed per cell by a bullet and restored on returning to edit mode
- Smoke test covers all of the above (lives/fail path, vest, kill plane and
  respawn, key + door + shooting course, water caps and surface jump, spike
  cycle, electric floor timing, goggles activation)

Known simplifications (revisit with milestone 3):
- Bullets pass through locked doors (they only collide with tiles so far)
- Briefcase disguise is a timer + HUD entry only until enemies and cameras
  exist
- Alarm floor is a plain solid tile until the alarm system lands

## Milestone 3 - enemies (next)
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
- Respawn semantics: implemented as respawn at the agent start keeping level
  state (opened doors stay open, picked items stay picked). The alternative,
  a full level reset per death, is still easy to switch to if playtesting
  favors it. README does not specify.
