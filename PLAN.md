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
- Killing terrain: lava instant kill, spears instant kill while out (holes,
  tips, out: 3s each), spike tiles hold two spikes
  that take turns (left up seconds 3-5, right up the last 2s of each 10s
  cycle), each hurting only over its own half, electric floor takes a life after each 3s of contact (per-region circuits, see milestone 4),
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
- Bullets pass through locked doors and security doors (they only collide with tiles so far)
- (Resolved: the briefcase disguise now defeats cameras and enemy spotting.)
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
- Circuits: computers, switches, lasers, security doors and floor circuits carry a number property (1-8),
  chosen from a second property row in the palette; markers and computers
  show their number
- USB keys are numbered pickups; walking up to a computer while carrying the
  matching USB key hacks it automatically (README: items activate on
  proximity) and turns off its circuit
- Switches turn off their circuit when shot (README: "on hit"), trading a
  gunshot alarm for the hack
- Laser beams run from the emitter up to the first solid tile; touching an
  active beam costs a life at once, then a 1s cooldown before it can hit again
  (only visible with the vest); turning off the circuit disables the beam
- Security door (`security_door`): solid 1-tile door on a circuit number;
  opens for good (collision off, faint frame left) when the circuit turns off.
  Colored keys do not open it; enemies treat it as a wall (layer 1) and bullets
  pass through it like they do the colored door
- Floor circuits: an editor-only `floor_circuit` marker with a number on an
  electric floor tile claims that tile's 4-connected electric floor region.
  PlaySession maps cell -> circuit at session start; when the circuit turns
  off only that region goes dark. Floor with no marker stays live forever,
  and a marker off the floor does nothing. Replaces the old "first hack
  disables every electric floor" rule
- Security cameras raise the alarm while they see the agent (8 tiles,
  blocked by solid tiles); an active briefcase disguise or hiding defeats them.
  Enemies share the rule: SeesAgent returns false while disguised or hiding,
  so they do not chase, fire or drop dynamite; contact damage still applies
- Security cameras carry a facing property (`right` default, or `left`), picked with
  two chips in the status bar or [ ]; left is the same sprite mirrored (FlipH) in
  the editor and in play. Sight is still all-around; levels saved without a facing stay right
- Closets: activate (C) next to a closet hides the agent (immobile,
  invisible to enemies, cameras, bullets and contact damage); C again leaves.
  Priority for C: leave closet, hide in closet, then use held items
- Movable barrels: pushed by walking into them (100 px/s), solid to stand
  on, treated as walls by enemies; wooden and steel kinds (barrel,
  barrel_steel) share the physics and differ only in art
- Smoke test covers hack-by-number, wrong-number stays locked, laser damage
  and pass-through when off, switch shooting, floor disabling, camera alarm,
  disguise, closet hiding and leaving, barrel pushing; milestone 6 mechanics
  add circuit 7, instant laser hit, security door, per-region floors and
  enemies ignoring the disguise

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
- Settings: difficulty picker, music/sound volume 0-10 and on/off toggles
  (see Sound effects), keyboard rebinding per action (gamepad binds preserved)
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
  each 10s cycle (same phase as the right spike) and takes a life on contact
- Flame trap: burns its own cell plus the cell above during seconds 4-6 of
  each cycle (offset from spikes so both can be combined)
- Rocket launcher: fires a rocket every 3 seconds, alternating left/right;
  rockets move at 1x agent speed and take a life (README ammo table)
- Weapons as pickups that equip on collection with README default ammo:
  chainsaw (melee sweep, no ammo, no alarm), assault rifle (burst of 3),
  mini gun (burst of 2), crossbow (arrows, does not trigger the alarm),
  dynamite (placed stick, 2.5s fuse, reuses the explosion system). Pistol,
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
  level rect behind the tile layers (SkyBackdrop under the grids in
  both the editor and the campaign scene). Painted background tiles cover
  the sky per cell, so they read as indoor walls in front of it
- The sky is a level property ("night" or "day", default night), toggled
  with a toolbar button in the editor and saved in the level JSON; old
  levels without the field load as night
- New "Day sky" paintable tile (blue with dithered cloud dashes and haze)
  next to the night one
- The sky is animated by a pixel-art canvas shader (SkyBackdrop): stars
  step in one palette shade at a time, twinkle, step out and respawn
  elsewhere; night wisps and two depths of day clouds drift right, swell
  in through a Bayer dither and dissolve the same way. Every cycle length
  divides Godot's 3600 s TIME rollover so the sky never pops when it wraps
- Smoke test: default is night, the toggle flips to day, and the choice
  round-trips through save/load (negative-verified with a no-op toggle)

### Level size 80x80
- Default level size raised from 40x12 to 40x40, then to 80x80 tiles; the editor camera
  starts at the bottom-left corner where the default floor and agent start
  are, and pans in both axes within the level bounds
- Camera limits, kill plane, minimap and play camera all derive from the
  level dimensions, so they adapted without changes; saved 40x12 levels
  keep their own size and still load and play
- Editor gained SetLevel (used by the load dialog and the smoke test); the
  smoke test runs its scenarios on an injected 40x12 level so their
  hand-tuned coordinates stay valid, and a new check covers the 80x80
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

### Agent animation
- Ten hand-authored agent poses in scripts/Sprites.cs, same runtime pixel
  maps as the rest of the art: idle + breath, a four-frame run cycle, jump,
  fall, aim and fire (plus the four death poses below). Frame swapping only,
  no tweening or skeletal rig - the DOS platformer idiom the README asks for
- The agent is drawn in profile facing right (hair mass at the back, shades
  band and nose bump at the front, shoe toes pointing forward), so FlipH on
  the facing direction makes the agent visibly look where it is heading.
  The original front-facing sprite was symmetric, which made FlipH a no-op
  and left facing unreadable
- The white collar and red tie sit on the same two rows in every pose, so
  the suit reads continuously instead of flickering through the cycle; the
  near leg is dark grey against the black far leg, which is what makes the
  two run contact frames read as opposite strides
- The aim/fire arm is level with the shoulders so the muzzle lines up with
  the bullet spawn (PlaySession.SpawnAgentBullet, y = -2)
- scripts/AgentAnimator.cs is the state machine: shoot pose wins, then
  swim, then jump/fall by vertical velocity, then run/idle. The run cycle
  advances on distance travelled rather than a clock, so the feet never
  slide at any speed; the pass frames sit a pixel higher than the contact
  frames for the bob. Swimming reuses the run frames on a slower clock
- PlaySession.TryShoot returns whether the weapon actually went off, so an
  empty magazine never flashes a muzzle
- Smoke test drives the real inputs through idle, run, jump, fall and the
  fire -> aim -> idle sequence, and force-builds every frame via
  AgentAnimator.AllFrames
### Agent death
- Four more poses in scripts/Sprites.cs, played once and then held: struck
  (chin snapped up, arms flung wide, the body a row taller than it stands and
  clear of the floor), buckling, down on the floor, flat. This is the one set
  that lets the collar and tie leave their fixed rows - the whole silhouette
  has to travel down the frame - and the head keeps its profile throughout,
  so the agent still reads as facing the way it was hit
- Losing a life is a sequence rather than an instant respawn: the life goes
  on the hit, the agent stops taking input and crumples where it stood, and
  the respawn (or the fail screen, when it was the last life) happens 1.2s
  later behind a fully covered screen. The agent cannot be hurt again while
  it is down, which also stops standing in lava from draining three lives in
  three frames
- The vest keeps the old instant behaviour: surviving a hit is not a death,
  so it costs the agent its ground but plays no death sequence
- scripts/DeathEffect.cs is the whole-screen part, in the same DOS idiom as
  the sprites and with no shader: a hard red palette flash, a few frames of
  whole-pixel camera shake decaying to nothing, then a chunky block dissolve
  to black on the 32px tile grid. The block order is a shuffle from a fixed
  seed - stepping through the grid by any fixed stride bands into diagonals
  instead of scattering - and it finishes before the respawn, so the level
  snaps back rather than popping the agent across the screen
- The level keeps running behind the effect; only the agent is down
- Smoke test drives a real lava death through the whole sequence: the life on
  the hit, the body staying where it fell, the poses, the held final pose,
  the immunity while down, and the respawn clearing the effect

### Enemy animation
- All ten README enemies redrawn in profile to match the agent, with 24
  frames total in scripts/Sprites.cs: a two-frame walk each, plus a firing
  pose for the soldier, super soldier, turret and robot, and a powered-down
  pose for the robot's off window
- The five bipeds (fighter, soldier, super soldier, ultimate soldier,
  robot) are one 16x16 skeleton parameterised by palette, so the soldier
  degradation chain reads as the same trooper losing armour. The robot gets
  a boxy machine head and darker steel so it never reads as another
  armoured trooper
- Non-humanoids are hand drawn: robodog (four legs, trotting), drone
  (spinning rotor plus a hover bob), turret (barrel and muzzle flash),
  landmine (blinking LED)
- The electric eel is generated rather than drawn: four phases of a sine
  wave travelling backwards along the body, so it swims by undulating. The
  amplitude is one pixel, which is the most the body can shift per column
  without tearing, and the thickness grows downward from the wave so the
  back stays one smooth curve while the belly deepens into the head. It
  runs on the clock, not on distance, so the ripple rate does not depend on
  the eel's speed
- scripts/EnemyAnimator.cs mirrors AgentAnimator with one table for all ten
  types: off pose wins, then the firing pose, then the walk cycle. Walkers
  advance on distance travelled; the drone rotor and landmine LED have
  nothing to travel so they run on the wall clock instead
- The first frame of every cycle is the plain type key, so editor markers
  and palette icons keep working with no changes
- Enemy sprites now face right like the agent (Enemy.cs flipped its FlipH
  test to match), and the turret turns to track the agent so its barrel
  points where the plasma actually goes
- Smoke test covers the patrol cycle, the turret hold-then-fire, the robot
  off pose, and that degrading swaps the whole pose set

- scenes/agent_frames.tscn renders contact sheets of every agent and enemy
  frame to user://agent_frames.png and user://enemy_frames.png - dark
  background, mid-tone background, and a mirrored band for the other
  facing - the sprite-art equivalent of screenshot.tscn

### Editor shortcuts, panning and help
- Every top-bar button has a Ctrl+letter shortcut and shows its letter in
  parentheses, for example "(N)ew". One MenuItem table in scripts/Editor.cs
  drives the buttons, the shortcuts and the help text
- Middle-mouse drag pans the camera. The camera position is clamped to the
  level limits, so panning back from an edge moves the view at once instead
  of first undoing an invisible overshoot
- Ctrl+H (and the Help button) opens a help dialog with the mouse and
  keyboard controls, the shortcuts, the palette and the README's level rules
- Smoke test covers Ctrl+K, middle-drag panning without painting, Ctrl+P
  versus Ctrl+Shift+P, and panning back from the clamped edge

### Test level Operation Night Lift and the route checker
- levels/operation-night-lift.json is an 80x80 tower generated by
  `python tools/build_night_lift.py` (`--preview` prints it as text). Edit
  the generator, not the JSON
- Each of the four floors (sewer, offices, server vault, roof) splits into
  a short guarded soldier route and a longer hacker route, and both rejoin
  at the shaft to the next floor. The hacker route goes back and forth for
  USB keys 1 and 3 to 6, computers, security doors, laser switch 2, the
  goggles and the briefcase
- `python tools/route_check.py <level.json> [--mode hacker|soldier|both]
  [--png out.png]` simulates the agent's jump physics, finds the fastest
  route in each mode, and for the hacker reruns the search with each event
  forbidden to show which ones are required. Hacker mode never enters an
  enemy's contact or sight zone. Exit status is nonzero when a mode cannot
  reach the exit. `python tools/test_route_check.py` runs its fixtures
- Current result: soldier 46.5 s through 19 enemy zones, hacker 65.1 s with
  no enemy contact, all 13 hacker events required
- Checker approximations: a connected destructible wall opens as a whole
  once shot, at one shot per wall cell in the agent's row; the electric
  door and flame trap are drawn but not modeled
- Checkpoints: a `checkpoint` object (Mission palette) is a flag post the
  agent lights by touching it; a lost life, or the vest absorbing a hit,
  respawns there instead of at the agent start. Night Lift has one in each
  shaft, at the lowest standing cell both routes cross, so a death costs
  one floor. The smoke test runs past two flags into a pit, checks only the
  second is lit and that the agent respawns on it (negative-verified with
  the respawn assignment removed)
- scenes/level_overview.tscn renders a whole level to
  user://<level>_overview.png (run it without --headless, and pass only
  the file name after `--`)

### Level format and storage
- Level files carry `formatVersion` (LevelData.CurrentFormatVersion, now 1);
  a file without it reads as 1 and a newer one is refused. A breaking
  change bumps the version and converts old files in LevelStore.Load
- The smoke test pins every tile number and object type name, and fails
  when one is renumbered, renamed, removed, or added without a pin
- Run from the project, LevelStore uses the repo's levels/ folder, so
  levels are committed and pushed like code; exported builds keep
  user://levels. The smoke test deletes the levels it saves

### Sound effects
- Sfx autoload (scripts/Sfx.cs): every sound is a row of tones (square,
  triangle, saw, noise, pitch sweeps) rendered once at startup into an
  AudioStreamWav, so there are no audio assets. `Sfx.Play` for the agent and
  UI, `Sfx.PlayAt` (700 px range) for enemies and traps; the same sound
  restarting within 40 ms is skipped
- Each action plays from the place it happens, one owner per event: the
  explosion from PlaySession.Explode, the alarm from the off-to-on step of
  TriggerAlarm, the level jingles from PlaySession.End (so editor playtest
  has them too)
- Settings: Sound and Music toggles beside the volume sliders; the profile
  fields SoundOn/MusicOn default on. The "SFX" and "Music" buses are created
  at startup, so music code only has to play on `Sfx.MusicBus`
- Audition: `godot --headless --path . res://scenes/sfx_dump.tscn` writes
  every sound to user://sfx/<name>.wav

### Swivelling turret
- The turret is a fixed platform and mast (turret_base) under a gun head
  (scripts/TurretGun.cs) that turns toward the agent at 120 degrees a
  second and fires only once the barrel is within 3 degrees of the agent.
  The plasma leaves the muzzle along the barrel, so a moving agent can
  outrun the aim
- The platform blocks a 70-degree cone straight down: the barrel covers
  145 degrees either side of straight up, swings over the top to change
  sides, and never fires at an agent directly underneath
- Aiming left flips the head vertically so its lit dome stays on top. The
  editor marker is built by overlaying the head on the base, so it matches
  the in-play turret at rest (level, facing right)
- Smoke test: an agent on the far side keeps the turret silent while it
  swings round, then the plasma takes a life

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
- Level size is fixed at 80x80 tiles (raised from 40x12 and 40x40); the format stores
  width/height, so per-level resizing is a later editor feature, not a
  format change. Old smaller levels still load and play.
- Elevator / moving platform movement-range property (start and end tile per
  the README editor section): the current fixed 4-tile obstacle-limited range
  works without new UI; add the range picker when the property row grows up.

## Open design decisions
- Respawn semantics: implemented as respawn at the agent start keeping level
  state (opened doors stay open, picked items stay picked). The alternative,
  a full level reset per death, is still easy to switch to if playtesting
  favors it. README does not specify.
