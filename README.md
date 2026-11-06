# Hacker or Soldier
2D platformer from 2030, about an agent who fights through levels full of robots, drones and enemy soldiers, using one of two strategies: either hacking through the levels without using violence or fighting through the hordes of enemies to the end.
The agent is sent by the Agency across the globe to execute impossible missions and accomplish the main objective.

## Graphic aesthetics
Original maps can be found as png images here:
http://www.vgmaps.com/Atlas/PC/index.htm#SecretAgent
e.g. http://www.vgmaps.com/Atlas/PC/SecretAgent-MissionOne-HuntForRedRockRover-Area01.png

### 256-color VGA limitation
This limited the palette to 256 stark, highly saturated colors. There are no smooth gradients or subtle shading values. Shading is entirely achieved through dithering—checkerboard-like pixel patterns alternating two solid colors (such as dithering cyan and dark blue to create a mid-tone blue).The palette heavily relies on high-contrast primaries: intense reds for dynamite, bright cyans and magentas for high-tech base structures, and deep blacks or dark greys for underground rocks.

### Rigid, Block-Based Level Design
The game uses a hyper-segmented grid system, meaning the environment is constructed entirely out of square tiles. Floors, ceilings, and walls are completely flat, rectilinear, and snap together like virtual Lego blocks.This gives the industrial fortresses, radar rooms, and underground caves a clean, hyper-ordered, geometric look typical of early DOS puzzle-platformers.

### The "Modern Retro" standard (32×32 tiles on a 640×360 canvas)
Grants enough pixel real estate for expressive animations, distinct sub-pixel sub-lighting, and atmospheric debris. The downside is that asset production time increases by ~300% per sprite sheet compared to 16×16.

## Objective of design
The main objective of this design document is to help to create a level editor with an engine, where the game can be created.
The user should have at his disposal a level editor similar to Mario Maker, where he can create levels from objects given in the design,
play the level at any time and save the level once satisfied with a specific name and description that will be shown in the 
level briefing. The game has edit and play mode.

## Character
The main character, the agent, has the following statistics:
- Lives (default 3)
- Ammo (for the current weapon, if it uses ammo)
- Coins (to use in the shop, default 100)
- Skin (agent can use briefcase / disguise to deceive enemies)
- Score
- Time to finish level

#### Movement
- The agent is 1 tile (square) wide / tall
- The agent jumps 2.5 tiles high (or hits his head sooner)
- The agent jumps up to 5 tiles sideways

##### Jump from water
The player swims up toward the surface. As long as they are submerged, their vertical velocity is capped by water resistance. When they press the jump button right at the surface boundary, the engine grants a standard jump velocity boost, but it is calculated from the water's surface line.

## Level
The main objective of the level is to get to the exit door, which ends the level and transitions to the statistics screen. No level object can't be destroyed, unless it's stated that it's destroyable.

### Checkpoints
- A checkpoint is a flag post, red until the agent touches it and green while it is the respawn point
- Losing a life respawns the agent at the last checkpoint touched, or at the agent start before any; only the latest flag stays green
- Level state stays as it was: opened doors stay open, hacked computers stay hacked, picked items stay picked

### Terrain
Terrain is used to create the level, the rooms. It consists of:
- Doors
- Walls
- Lights
- Decorations
- Elevators and platforms
- Killing terrains
- Teleports
- Floors

### Floors and platforms
- Alarm floor - triggers alarm on step
- Hidden platforms - platforms that are visible only when wearing infra goggles

#### Walls
Standard or destructible walls of various textures. Walls can also be floors.

#### Teleport
Two joined teleport capsules scattered around the level. Once the agent enters one capsule, he appears in the other.

#### Elevators and moving platforms
An elevator is an up/down moving platform. Moving platforms in general move left/right. 

#### Doors
Doors require keys to be opened. Doors and keys can be recognized by their matching color. When the agent has the correct key,
he can open the appropriate door instantly.

#### Lights
Lights decorate the levels with shadows; they're strictly a visual thing.

#### Decorations
Fulfill strictly decorative / visual aspect of the game. 
- Digital clocks
- Machines (various kinds)
- Posters
- Plants
- Toilet
- Pipes
- Signs (Beware the dogs, Server room, WC, Biohazard, Fire danger, Pointing arrows)
- Tables and stools
- Crates (wooden / steel)

#### Killing terrain
- Water floor - allows slower movement horizontally, contains electric eels
- Spike trap - two spikes take turns rising within a 10s cycle; a raised spike takes a life on its half of the tile
- Electric floor - takes a life every 3 seconds; a connected region marked with a floor circuit number switches off when that circuit goes off, unmarked floors stay live
- Spear - cycles through three 3s states: bare holes, spear tips showing in the holes (a harmless warning), spears out; while out they instantly kill the agent
- Electric door - cyclically (10s) engages an electric field that takes a life
- Lava floor - instantly kills the agent
- Flame traps - cyclically fires flame (10s) from a hole, which takes a life

### Rooms
Rooms are areas enclosed with terrain populated with enemies, machines and items.

#### Alarm
Alarm is triggered upon explosion, gun shot, when a camera spots the agent or when agent steps on a certain floor.
The alarm is on for 30 seconds from last trigger time, when on, the room changes its appearance and enemies start to move
twice as quickly. 

#### Closet
The agent can hide in the closet on activate action, e.g. in case of an alarm.

Whenever the activate action does something where the agent stands (hide in or leave a closet), a prompt such as "Press C to hide" shows in the middle of the screen.

### Machines / Devices
- Computer - can be hacked via USB key with same number, switches off the laser beams, security doors and floor circuits with that number (8 circuits)
- Security camera - triggers the alarm
- Laser beam - a beam between two ends, takes a life the moment the agent touches it (then a 1 second cooldown before it can hit again)
- Rocket launcher - launches rockets
- Switch - switches off the laser beams, security doors and floor circuits with its number on hit (a shot counts even when the agent stands on the switch); the activate action does not flip it
- Security door - a solid door wired to a circuit number; opens for good when the circuit goes off, colored keys do not open it
- Floor circuit - editor-only marker with a number, placed on an electric floor tile; it assigns that tile's connected electric floor region to the circuit

### Movables
In the rooms the agent may find barrels that can be moved (properly emphasized) in both directions to make a way to originally inaccessible places.

## Enemies
The agent may encounter the following enemies:
- Fighter - Lives: 2, Weapon: fists, LoS: row, Speed: 2
- Soldier - Lives: 1, Weapon: pistol, LoS: row, Speed: 1
- Super soldier - Lives: 2, Weapon: mini gun, LoS: row, Speed: 2
- Ultimate soldier - Lives: 3, Weapon: chainsaw, LoS: row, Speed: 3
- Drone - Lives: 1, Weapon: dynamite, LoS: all directions, Speed: 1
- Robodog - Lives: 2, Weapon: teeth, LoS: row, Speed: 3
- Turret - Lives: 1, Weapon: plasma shot, LoS: all directions, Speed: 0
- Robot - Lives: infinite, Weapon: plasma shot, LoS: row, Speed: 1
- Landmine - Lives: infinite, Weapon: mine, LoS: none (impact), Speed: 0
- Electric eel - Lives: 1, Weapon electric shock, LoS: row, Speed 1
Legend:
LoS - line of sight, where can the enemy spot the agent
Soldier, Super soldier, Drone, Robodog and Robot sometimes drop a free ammo pack

### Soldier (Super soldier)
Ultimate soldier once hit, changes to Super soldier, who when hit, changes to soldier, who when hit dies with RIP tombstone. 

### Robot
Robot can be killed only when stopped and turned off for a second. Robot stops and turns off automatically every 10 seconds.

### On low difficulty
On low difficulty super soldier doesn't change to
soldier but dies with tombstone. Minimum lives for all enemies is 1.

## Items
The agent collects items usually to inventory, and uses them automatically (or manually in some cases) usually upon proximity of machine / device / door, e.g. uses the key to open the door or uses the usb key to disable laser beam.
The following items available across levels activate automatically:
- Door key - opens the door of given color
- USB key - hacks a computer of given number
- Health kit - adds a life
- Ammo pack - adds 10 ammo
- Bulletproof vest - saves 1 life, then is destroyed

These items activate manually:
- Briefcase (disguise), when used, agent looks like a soldier for 30 seconds, and can't be detected by cameras or spotted by enemies (walking into one still hurts)
- Infra goggles - changes visual to infra red and reveals hidden platforms, when activated they are active for 30 seconds

Items can't be destroyed nor shot.

### Collectibles
Collectibles are items scattered across the level to be picked and converted instantly to score and coins, e.g. the agent picks enemy notebook and gains X score and Y coins. These collectibles are available in levels:
- A, G, E, N, T characters (each 1k score, 0 coins - if all collected in level 40k score, 5 coins)
- Admin password (30k score, 10 coins)
- Diamond (25k score, 7 coins)
- Notebook (20k score, 6 coins)
- Mobile (15k score, 5 coins)
- Smartwatch (10k score, 5 coins)
- Audio recorder (7k score, 2 coins)
- SIM card (5k score, 2 coins)
- Cash stack (3k score, 1 coin)
- Business card (2k score, 1 coin) 
- Tombstone (1k score)

## Weapons
The agent uses weapons to kill enemies. The following weapons are available to find in the levels:
- Pistol (1 bullet shot at once, default 10 ammo, default weapon)
- Chainsaw (melee range, interchangeable with pistol as default)
- Assault rifle (3 bullets shot at once, default 10 ammo)
- Mini gun (2 bullets shot at once, default 20 ammo)
- Crossbow (doesn't trigger alarm, default 20 ammo)
- Dynamite (used to destruct obstacles, kills everything in 5 square radius, explodes in 2.5 seconds; doesn't apply to objects with infinite lives)

## Ammo
- Bullet - (goes to pistol, assault rifle, mini gun) takes a life, moves 2x speed of agent
- Plasma shot - takes a life, moves 2x speed of agent
- Rocket - takes a life, moves 1x speed of agent
- Arrow - takes a life, moves 2x speed of agent
Ammo is transferred across levels.

## Economics
The player gains coins during the game, keeps them across levels and spends them on items. 

### Shop
In the shop the agent finds items to buy and use in the levels:
- Briefcase (20 coins)
- Hacking tool (30 coins) - replaces USB key to hack computers
- Extra life (50 coins)
- Chainsaw (20 coins, default weapon)
- Pistol (20 coins, default weapon, 20 ammo)
- 10 ammo pack (5 coins)

## Hacking
Player can finish the level without killing anyone just using hacking, switching off machines, using disguise, going
through harder passages without enemies. Hackers are then tagged as Hackers at the end of level at statistics for choosing non-violent path.

## UI

### Main menu
- New game - starts a new game, asks 'Are you sure you want to start from the beginning?'
- Continue level - continue playing from last level start
- Restart level - available during play, restart the level
- Settings
- Create level - opens level editor
- Exit game

### Settings

#### Difficulty (Low, Medium - default, Hard)
Low: +1 life for agent, -1 life for enemies
Medium: Default settings of lives, enemies (lives and speed)
Hard: -1 life for agent, +1 life for enemies

#### Change controls
Default controls can be changed to any keyboard keys or gamepad.

##### Default controls
Left - arrow left / A
Right - arrow right / D
Jump - Space / Enter
Shoot - E
Activate - C
Pause - Esc
Help - hold Tab (pauses and shows the controls beside what each thing in the level does)

#### Music / Sound volume
Music and sounds have separate volume controls with 0-10 values (0 means no sound / music).

### Pause
Triggers main menu with Restart level option

### Statistics
Displays statistics of the agent at the end of the level:
- Level time 0:00 - x:xx
- Level score 0-x
- Number of kills 0-x
- Accuracy 0-100%
- Completeness 0-100%
Contains buttons:
- Shop
- Next level
- Exit game

## Level editor
The level editor opens from the main menu (Create level) and lets the user build levels from the objects defined in this
design. The editor works on a tile grid; the agent is 1 tile, so all objects snap to tiles. The editor switches between
edit mode and play mode at any time.

### Edit mode
- Object palette grouped by category: Terrain, Machines / Devices, Movables, Enemies, Items, Collectibles, Weapons
- Objects are placed on the grid and can be moved or deleted afterwards
- Some objects have properties set on placement:
  - Door / Door key - color (matching pairs)
  - Computer / USB key - number (matching pairs)
  - Teleport - capsules are placed as a joined pair
  - Elevator / moving platform - movement range (start and end tile)
  - Elevator / moving platform - paint color (steel, hazard, rust, teal, olive, maroon), drawn the same in the editor and in play
  - Switch / Computer / Laser beam / Security door / Floor circuit - number 1-8, the circuit they share
- Every level must contain exactly one agent start position and at least one exit door

### Play mode
- Runs the level from the agent start position to test it
- Returning to edit mode restores the level to its state before playing (enemies, items and the agent reset)

### Saving
- The level is saved with a name and description, which are shown in the level briefing
- Saved levels can be reopened for editing
- Run from the project, levels are saved in the repo's `levels/` folder; commit and push them to edit them on another machine. An exported build saves them in the player's own levels folder
- Every level file carries a `formatVersion`; a file without one is version 1, and a file from a newer version than the game knows is refused

### Minimap
To orient in a bigger level while editing, there should be top right minimap of the whole level.

