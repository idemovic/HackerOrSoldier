# Hacker or Soldier
2D platformer about an agent who fights through levels full of robots, drones, enemy soldiers, using one of two strategies,
either hack through the levels without using violence or fight through the hordes of enemies to the end.
The agent is sent by the Agency across the globe to execute impossible missions and accomplish the main objective.

## Objective of design
The main objective of this design document is to help to create a level editor with engine, where the game can be created.
User should have in disposal level editor similar to Mario maker, where he can create levels from objects given in the design,
play the level at any time and save the level once satisfied with a specific name and description that will be shown in the 
level briefing. The game has edit and play mode.

## Character
The main character, the agent, has following statistics:
- Lives (default 3)
- Ammo (if it's a gun)
- Coins (to use in the shop, default 100)
- Skin (agent can use disguise to deceive enemies)
- Score
- Time to finish level

#### Movement
- The agent is 1 tile (square) wide / tall
- The agent jumps 2.5 tiles above (or hits his head sooner)
- Agent jumps up to 5 tiles to sides

## Level
The main objective of the level is get to exit door, which ends the level transitioning to statistics screen.

### Terrain
Terrain is used to create the level, the rooms. It consists of:
- Doors
- Walls
- Lights
- Decorations
- Elevators and platforms
- Killing terrain

### Floors and platforms
- Alarm floor - triggers alarm on touch

#### Walls
Standard or destructible walls of various texture. Walls can also be floors.

#### Teleport
Two joined teleport capsules scattered around the level. Once the agent enters one capsule, he appears in the other.

#### Elevators and moving platforms
Elevator is a up/down moving platform. Moving platfroms in general move left/right. 

#### Doors
Doors required keys to be opened. Doors and keys can be recognized with same color. When the agent has the correct key,
he can open the appropriate door instantly.

#### Lights
Lights decorate the levels with shadows, they're strictly a visual thing.

#### Decorations
Fulfill strictly decorative / visual aspect of the game. 
- Digital clocks
- Machines (various kinds)
- Posters
- Plants
- Toilet
- Pipes
- Signs (Beware the dogs, Server room, WC, Biohazard, Fire danger, Pointing arrows)
- Tables and stools - can be jumped on
- Crates (wooden / steel) - can be jumped on

#### Killing terrain
- Water floor - instantly kills the agent
- Spike trap - cyclically rises a spike, which takes a life 
- Electric floor - takes a life each 3 seconds
- Spear - stationary spear, instantly kills the agent
- Electric door - cyclically engages electric field, that takes a life
- Lava floor - instantly kills the agent
- Flame traps - cyclically fires flame from a hole, which takes a life

### Rooms
Rooms are areas enclosed with terrain populated with enemies, machines and items.

#### Alarm
Alarm is triggered upon explosion, gun shot, when a camera spots the agent or when agent steps on a certain floor.
The alarm is on for 30 seconds from last trigger time, when on, rooms changes its appearance and enemies start to move
twice as quickly. 

#### Closet
The agent can hide in the closet, e.g. in case of an alarm.

### Machines / Devices
- Computer - can be hacked via USB key with same number, switches off laser beam / floor
- Security camera - triggers the alarm
- Laser beam - a beam between two ends, takes a life each 3 seconds
- Rocket launcher - launches rockets
- Switch - switches off laser beam / floor on hit

### Movables
In the rooms the agent may find barrels that can be moved both directions to make a way to originally inaccessible places.

## Enemies
The following enemies may the agent encounter:
- Fighter - Lives: 2, Weapon: fists, LoS: row, Speed: 2
- Soldier - Lives: 1, Weapon: pistol, LoS: row, Speed: 1
- Super soldier - Lives: 2, Weapon: mini gun, LoS: row, Speed: 2
- Ultimate soldier - Lives: 3, Weapon: chainsaw, LoS: row, Speed: 3
- Drone - Lives: 1, Weapon: dynamite, LoS: all directions, Speed: 1
- Robodog - Lives: 2, Weapon: teeth, LoS: row, Speed: 3
- Turret - Lives: 1, Weapon: plasma shot, LoS: all, Speed: 0
- Robot - Lives: infinite, Weapon: plasma shot, LoS: row, Speed: 1
- Landmines - Lives: infinite, Weapon: mine, LoS: none (impact), Speed: 0
Legend:
LoS - line of sight, where can the enemy spot the agent
Soldier, Super soldier, Drone, Robodog and Robot sometimes drop free ammo pack

### Soldier (Super soldier)
Ultimate soldier once hit, changes to Super soldier, who when hit, changes to soldier, who when hit dies with RIP tombstone.

### Robot
Robot can be killed only when stopped and turned off for a second.

## Items
The agent collects items usually to inventory, and uses them automatically (or manually in case of disguise) upon proximity
of machine / device, e.g. uses the key to open the door or uses the usb key to disable laser beam.
There are following items available across levels that activates automatically:
- Door key - opens the door of given color
- USB key - hacks a computer of given number
- Health kit - adds a life
- Bullet - adds 10 ammo
- Infra goggles - changes visual to infra red and reveals hidden platforms
- Bulletproof vest - saves 1 life, then is destroyed

These items activate manually:
- Briefcase (disguise), when used, agent looks like a soldier for 30 seconds, and can't be detected by cameras

### Collectibles
Collectibles are items scattered across the level to be picked and converted instantly to coins, e.g. the agent picks
enemy notebook and gains X coins. These collectibles are available in levels:
- A, G, E, N, T characters (each 1k score, 0 coins - if all together 40k score, 5 coins)
- Admin password (30k score, 10 coins)
- Diamond (25k score, 7 coins)
- Notebook (20k score, 6 coins)
- Mobile (15k score, 5 coins)
- Smartwatch (10k score, 5 coins)
- Audio recorder (7k score, 2 coins)
- SIM card (5k score, 2 coins)
- Cash stack (3k score, 1 coins)
- Business card (2k score, 1 coin) 
- Tombstone (1k score)

## Weapons
The agent uses weapons to kill enemies, following weapons are available to find in the levels (the agent starts usually
with a pistol):
- Assault rifle (3x pistol power, default 10 ammo)
- Mini gun (2x pistol power, default 20 ammo)
- Cross bow (1x pistol power, doesn't trigger alarm, default 20 ammo)
- Dynamite (used to destruct obstacles, kills everything in 5 square radius, explodes in 5 seconds)

## Ammo
- Bullet - (goes to pistol, assault rifle, mini gun) takes a life, moves 2x speed of agent
- Plasma shot - (turret) takes a life, moves 2x speed of agent
- Rocket - takes a life, moves 1x speed of agent
- Arrow - takes a life, moves 2x speed of agent
Ammo is transfered across levels.

## Economics
Player gains coins during the game, he keeps them across levels and spends them on items. 

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
through harder passages without enemies. Hackers are then tagged as Hackers at the end of level at statistics.

## UI

### Main menu
- New game - starts a new game, ask whether 'Are you sure to start from beginning?'
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
Pause - Esc

#### Music / Sound volume
Music and sounds has separate volume controls with 0-10 values (0 means no sound / music).

### Pause
Triggers main menu with Restart level option

### Statistics
Display statistics of agent at the end of level:
- Level time 0:00 - x:xx
- Level score 0-x
- Number of kills 0-x
- Accuracy 0-100%
- Completeness 0-100%
Contains button:
- Shop
- Continue level
- Exit game

## Level editor
