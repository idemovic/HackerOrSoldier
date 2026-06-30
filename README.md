# Hacker or Soldier
2D platformer about an agent who fights through levels full of robots, drones, enemy soldiers, using one of two strategies,
either hack through the levels without using violence or fight through the hordes of enemies to the end.
The agent is sent by the Agency across the globe to execute impossible missions and accomplish the main objective.

## Character
The main character, the agent, has following statistics:
- Lives (default 3)
- Ammo (if it's a gun)
- Coins (to use in the shop, default 100)
- Skin (agent can use disguise to deceive enemies)
- Score
- Time to finish level

## Level

### Terrain

#### Destructible walls

#### Teleport

#### Elevators and moving platforms

#### Doors
- Automatic door

#### Lights

#### Killing terrain
- Spike trap - cyclically rises a spike, which takes a life 
- Laser floor - takes a life each 3 seconds
- Spear - stationary spear, instantly kills the agent
- Electric door - cyclically engages electric field, that takes a life
- Lava floor - instantly kills the agent
- Flame traps - cyclically fires flame from a hole, which takes a life

### Rooms

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
- Soldier - Lives: 2, Weapon: pistol, LoS: row, Speed: 1
- Super soldier - Lives: 3, Weapon: mini gun, LoS: row, Speed: 2
- Drone - Lives: 1, Weapon: dynamite, LoS: all directions, Speed: 1
- Robodog - Lives: 2, Weapon: teeth, LoS: row, Speed: 3
- Turret - Lives: 1, Weapon: plasma shot, LoS: all, Speed: 0
- Robot - Lives: infinite, Weapon: plasma shot, LoS: row, Speed: 1
- Landmines - Lives: infinite, Weapon: mine, LoS: none (impact), Speed: 0
Legend:
LoS - line of sight

### Soldier (Super soldier)

### Robot

## Items
The agent collects items usually to inventory, and uses them automatically (or manually in case of disguise) upon proximity
of machine / device, e.g. uses the key to open the door or uses the usb key to disable laser beam.
There are following items available across levels that activates automatically:
- Door key - opens the door of given color
- USB key - hacks a computer of given number
- Health kit - adds a life
- Bullet - adds 10 ammo
- Infra goggles - changes visual to infra red and reveals hidden platforms

These items activate manually:
- Briefcase (disguise)

### Collectibles
Collectibles are items scattered across the level to be picked and converted instantly to coins, e.g. the agent picks
enemy notebook and gains X coins. These collectibles are available in levels:
- A, G, E, N, T characters (each 1k score, 0 coins - if all together 250k score, 5 coins)
- Admin password (100k score, 10 coins)
- Diamond (75k score, 7 coins)
- Notebook (50k score, 6 coins)
- Mobile (30k score, 5 coins)
- Smartwatch (20k score, 5 coins)
- Audio recorder (10k score, 2 coins)
- SIM card (5k score, 2 coins)
- Cash stack (3k score, 1 coins)
- Business card (2k score, 1 coin) 

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

## Economics

### Shop
In the shop the agent finds items to buy and use in the levels. 
- Briefcase (20 coins)
- Hacking tool (30 coins)
- Extra life (50 coins)
- Chainsaw (20 coins, default weapon)
- Pistol (20 coins, default weapon, 20 ammo)

## Hacking

## Controls

## Settings
- Difficulty
- Change controls