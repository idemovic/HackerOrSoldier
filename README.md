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

## Level

### Terrain

#### Lights

### Rooms

#### Alarm
Alarm is triggered upon explosion, gun shot, when a camera spots the agent or when agent steps on a certain floor.
The alarm is on for 30 seconds from last trigger time, when on, rooms changes its appearance and enemies start to move
twice as quickly. 

#### Closet
The agent can hide in the closet, e.g. in case of an alarm.

### Machines / Devices
- Computer - can be hacked via USB key with same number, switches off Laser beam / floor
- Security camera - triggers the alarm
- Laser beam - a beam between two ends, takes a live each 3 seconds
- Laser floor - takes a live each 3 seconds

### Movables
In the rooms the agent may find barrels that can be moved both directions to make a way to originally inaccessible places.

## Enemies
The following enemies may the agent encounter:
- Soldier - Lives: 3, Weapon: pistol, LoS: row, Speed: 1
- Super solder - Lives: 4, Weapon: mini gun, LoS: row, Speed: 2
- Drone - Lives: 1, Weapon: dynamite, LoS: all directions, Speed: 1
- Robodog - Lives: 2, Weapon: teeth, LoS: row, Speed: 3
- Machine turret - Lives: 5, Weapon: laser shot, LoS: all, Speed: 0
- Landmines - Lives: infinite, Weapon: mine, LoS: none (impact), Speed: 0
Legend:
LoS - line of sight

## Items
The agent collects items usually to inventory, and uses them automatically (or manually in case of disguise) upon proximity
of machine / device, e.g. uses the key to open the door or uses the usb key to disable laser beam.
There are following items available across levels that activates automatically:
- Door key - opens the door of given color
- USB key - hacks a computer of given number
- Health kit - adds a life
- Bullet - adds 10 ammo

These items activate manually:
- Briefcase (disguise)

### Collectibles
Collectibles are items scattered across the level to be picked and converted instantly to coins, e.g. the agent picks
enemy notebook and gains X coins. These collectibles are available in levels:
- Admin password (2M score, 50 coins)
- Diamond (1M score, 10 coins)
- Notebook (250k score, 25 coins)
- Mobile (100k score, 20 coins)
- Smartwatch (50k score, 15 coins)
- Audio recorder (10k score, 10 coins)
- SIM card (5k score, 5 coins)
- Cash stack (3k score, 4 coins)
- Business card (1k score, 2 coins) 

## Weapons
The agent uses weapons to kill enemies, following weapons are available to find in the levels (the agent starts usually
with a pistol, or a weapon bought from the shop):
- Assault rifle (3x pistol power, default 10 ammo)
- Mini gun (2x pistol power, default 20 ammo)
- Cross bow (1x pistol power, doesn't trigger alarm, default 20 ammo)
- Dynamite (used to destruct obstacles, kills everything in 5 square radius)

## Economics

### Shop
In the shop the agent finds items to buy and use in the levels. 
- Briefcase (200 coins)
- Hacking tool (100 coins)
- Extra life (500 coins)
- Chainsaw (100 coins, default weapon)
- Pistol (100 coins, default weapon, 20 ammo)

## Hacking

## Controls

## Settings
