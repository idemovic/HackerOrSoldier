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

### Milestone 3 - enemies
- All ten README enemies as one data-driven class (scripts/EnemyDefs.cs +
  scripts/Enemy.cs): fighter, soldier, super soldier, ultimate soldier, drone,
  robodog, turret, robot, landmine, electric eel; placeable from an Enemies
  palette section
- Patrol with wall and ledge turning; drones fly, eels stay inside water,
  turrets and landmines are static; contact damage with a 1s per-enemy cooldown
- Line of sight: row (facing direction, up to 10 tiles, blocked by solid
  tiles) or all directions (8 tiles); melee enemies chase, ranged enemies
  stop and fire (pistol/minigun bullets straight, turret plasma aimed),
  drones drop dynamite
- Soldier degradation chain: Ultimate -> Super -> Soldier -> dead with a
  tombstone (decoration until the milestone 5 score collectible)
- Robot is invulnerable except during its 1s off-window every 10s
- Alarm: triggered by gunshots, explosions and alarm floor tiles; 30s from
  the last trigger, doubles enemy movement speed, tints the level red, shows
  in the HUD (camera trigger arrives with milestone 4)
- Explosions (dynamite, landmines): 5-tile radius, destroy destructible
  walls, kill non-infinite enemies outright, hurt the agent, raise the alarm
- Soldier/super soldier/drone/robodog/robot drop an ammo pack on death with
  30% chance (PlaySession.DropChance)
- Collision layers: tiles on layer 1, agent on layer 2, enemies collide with
  tiles only, so enemies and the agent pass through each other and damage is
  proximity-based

Known simplifications (tuning constants, not README rules):
- Speed unit 40 px/s per table point; sight ranges 10/8 tiles
- Enemy shot cooldowns: pistol 1.5s, minigun 0.6s, plasma 2s, dynamite 4s
- Low-difficulty rules (super soldier dies outright, minimum lives) wait for
  the milestone 5 settings screen

### Milestone 4 - hacking path
- Circuits: computers, switches and lasers carry a number property (1-4),
  chosen from a second property row in the palette; markers and computers
  show their number
- USB keys are numbered pickups; walking up to a computer while carrying the
  matching USB key hacks it automatically (README: items activate on
  proximity) and turns off its circuit
- Switches turn off their circuit when shot (README: "on hit"), trading a
  gunshot alarm for the hack
- Laser beams run from the emitter up to the first solid tile; contact costs
  a life after 3 seconds (same rule as the electric floor); turning off the
  circuit disables the beam
- Turning off any circuit also disables all electric floor tiles (README says
  computers/switches switch off "laser beam / floor"; per-tile floor linking
  would need tile properties, so floor disabling is global for now)
- Security cameras raise the alarm while they see the agent (8 tiles,
  blocked by solid tiles); an active briefcase disguise or hiding defeats them
- Closets: activate (C) next to a closet hides the agent (immobile,
  invisible to enemies, cameras, bullets and contact damage); C again leaves.
  Priority for C: leave closet, hide in closet, then use held items
- Movable barrels: pushed by walking into them (100 px/s), solid to stand
  on, treated as walls by enemies
- Smoke test covers hack-by-number, wrong-number stays locked, laser damage
  and pass-through when off, switch shooting, floor disabling, camera alarm,
  disguise, closet hiding and leaving, barrel pushing

### Milestone 5 - meta layer
- Main menu (scenes/main_menu.tscn, now the main scene): New game (with the
  "start from the beginning?" confirmation), Continue level, Settings,
  Create level, Exit game
- Campaign mode (scenes/game.tscn, scripts/GameFlow.cs): plays the levels
  saved in user://levels in file name order - the editor's levels are the
  campaign. Briefing screen with level name and description before each
  level, pause menu (Esc) with Resume / Restart level / Main menu / Exit,
  fail screen with retry
- Statistics screen after each level: time, score, kills, accuracy,
  completeness, the HACKER tag for runs with zero kills, and buttons for
  Shop / Next level / Exit game
- Persistent profile (user://profile.json): coins (default 100), campaign
  progress, difficulty, volumes, key binds, hacking tool ownership
- Run state across levels: lives and ammo carry over (README: ammo
  transfers), coins bank into the profile after each level
- Collectibles (scripts/CollectibleDefs.cs): all README entries incl. the
  AGENT letter set (40k + 5 coins when complete); tombstones dropped by dead
  soldiers are 1k collectibles; completeness = placed collectibles collected
- Shop: briefcase 20, hacking tool 30 (permanent: hacks computers without a
  USB key), extra life 50, 10 ammo 5; weapon entries wait for weapon switching
- Difficulty (Low/Medium/Hard): agent 4/3/2 lives, enemies -1/+1 life
  (min 1, applied to the final stage of the soldier chain), low-difficulty
  super soldiers die outright with a tombstone
- Settings: difficulty picker, music/sound volume 0-10 (stored; applied when
  audio lands), keyboard rebinding per action (gamepad binds preserved)
- Score, kills, shots fired/hit tracked in PlaySession; accuracy and
  completeness computed there so the editor's test mode shows them too

### Content update - remaining README objects
- Teleports: capsule pairs matched by the number property; entering one
  moves the agent to the other (a lock prevents ping-ponging until the agent
  steps away)
- Elevators (up/down) and moving platforms (left/right): AnimatableBody2D
  at 60 px/s, travelling up to 4 tiles from the start cell or until blocked
  by a solid tile; carries whoever stands on them. A movement-range property
  UI can replace the fixed range later
- Electric door: passable frame whose field engages during the last 2s of
  each 10s cycle (same phase as spikes) and takes a life on contact
- Flame trap: burns its own cell plus the cell above during seconds 4-6 of
  each cycle (offset from spikes so both can be combined)
- Rocket launcher: fires a rocket every 3 seconds, alternating left/right;
  rockets move at 1x agent speed and take a life (README ammo table)
- Weapons as pickups that equip on collection with README default ammo:
  chainsaw (melee sweep, no ammo, no alarm), assault rifle (burst of 3),
  mini gun (burst of 2), crossbow (arrows, does not trigger the alarm),
  dynamite (placed stick, 5s fuse, reuses the explosion system). Pistol,
  rifle and mini gun share the bullet pool per the README ammo table; the
  weapon resets to pistol at the start of each level
- Decorations: clock, poster, plant, toilet, pipe, sign, machine (visual
  only) plus table, wooden crate and steel crate (solid, jump-on-able;
  tables at 18px height)
- Lights: PointLight2D glow with a runtime radial gradient, strictly visual

### Sprite pass
- Runtime-generated pixel art for everything, still no binary assets:
  scripts/Sprites.cs holds hand-authored 16x16 pixel maps (upscaled 2x) for
  the agent, all 10 enemies, items, devices, weapons, collectibles,
  decorations and projectiles; door/key sprites take the pair color
- The agent is an original take on the classic pixel spy (black suit, white
  shirt, red tie, shades) per the design direction
- Tiles are drawn procedurally in TileSetBuilder with the README aesthetic:
  checkerboard dithering (water surface, lava glow), brick patterns, hazard
  stripes, VGA-style saturated palette
- Sprites are used everywhere: play mode, editor markers (90% opacity) and
  palette icons; enemy and agent sprites flip with facing
- The smoke test force-builds every sprite so a malformed pixel map fails
  the run; scenes/screenshot.tscn renders a showcase level for eyeballing

### Wall variants and Walls palette category
- New "Walls" palette category between Mission and Terrain holding Wall,
  Destructible wall and the new variants; the Light object moved from
  Terrain to Decorations
- Four new solid (walkable) wall tiles drawn procedurally in the house
  style, taking their material vocabulary from the Secret Agent reference
  screenshots in references/ (original art, nothing copied): stone blocks
  (rounded, lit from the top left), red brick, riveted steel plate, girder
  (flanges, bolts, lattice web)
- Second batch of solid walls: concrete slabs (form-tie holes, stains),
  sandbags (staggered courses, lit tops), container (corrugated green with
  riveted frame rails), pipe wall (stacked teal pipes with couplings)
- Background wall tiles: no collision, so they are pure scenery behind the
  agent; deliberately darker and flatter than the solid walls so gameplay
  reads on top of them. Non-solid also means they do not block enemy line
  of sight. Variants: mesh, rock, lit panel, shadowed brick, circuit board
  (dim traces and LEDs, fits the hacking theme), corrugated hangar, night
  sky with stars for the outdoor top of tall levels
- Smoke test: agent walks through a background wall and is stopped by a
  stone block wall (both negative-verified by flipping the Solid flags)

### Outdoor sky
- Cells with no tile are the outdoors: a per-level sky fills the whole
  level rect behind the tile layers (repeating Sprite2D under the grids in
  both the editor and the campaign scene). Painted background tiles cover
  the sky per cell, so they read as indoor walls in front of it
- The sky is a level property ("night" or "day", default night), toggled
  with a toolbar button in the editor and saved in the level JSON; old
  levels without the field load as night
- New "Day sky" paintable tile (blue with dithered cloud dashes and haze)
  next to the night one; the sky itself reuses these tile textures
- Smoke test: default is night, the toggle flips to day, and the choice
  round-trips through save/load (negative-verified with a no-op toggle)

### Level size 40x40
- Default level size raised from 40x12 to 40x40 tiles; the editor camera
  starts at the bottom-left corner where the default floor and agent start
  are, and pans in both axes within the level bounds
- Camera limits, kill plane, minimap and play camera all derive from the
  level dimensions, so they adapted without changes; saved 40x12 levels
  keep their own size and still load and play
- Editor gained SetLevel (used by the load dialog and the smoke test); the
  smoke test runs its scenarios on an injected 40x12 level so their
  hand-tuned coordinates stay valid, and a new check covers the 40x40
  default (dimensions, spawn at the bottom, standing on the floor)

### Editor palette rework
- Palette layout: vertical category list (Mission, Terrain, Devices,
  Movables, Enemies, Items, Weapons, Collectibles, Decorations, Eraser -
  following the README's palette grouping) with a horizontal, scrollable row
  of swatch buttons for the selected category; tooltips carry the names and
  the selected brush name shows in the property row next to the color and
  number pickers
- scenes/screenshot.tscn renders the editor and saves user://editor_ui.png
  for quick visual checks of UI changes

## Milestone 6 - Steam prep (next)
- Export presets for Windows and Linux
- GodotSteam integration: achievements, cloud saves
- Steam Workshop publishing for user levels (the JSON level format is already
  share-ready)
- Store page assets

## Deferred on purpose
- Visual layer (VGA palette + dithering shader, real sprites, 32x32 tile art):
  after milestone 2 or 3. Placeholder rects iterate faster, and the retro
  aesthetic is an asset swap plus one shader, not a structural decision.
- Level size is fixed at 40x40 tiles (raised from 40x12); the format stores
  width/height, so per-level resizing is a later editor feature, not a
  format change. Old 40x12 levels still load and play.
- Elevator / moving platform movement-range property (start and end tile per
  the README editor section): the current fixed 4-tile obstacle-limited range
  works without new UI; add the range picker when the property row grows up.

## Open design decisions
- Respawn semantics: implemented as respawn at the agent start keeping level
  state (opened doors stay open, picked items stay picked). The alternative,
  a full level reset per death, is still easy to switch to if playtesting
  favors it. README does not specify.
