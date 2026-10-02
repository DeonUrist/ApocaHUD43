# ApocaHUD43

Keeps Apocalypter's HUD on the screen edges at **any aspect ratio** (BepInEx 5 plugin).

The game lays its HUD out for 16:9 with every element anchored to the screen centre. On a 4:3 or 16:10 monitor the canvas
grows taller than 1080, so the bottom row (weapon card, survival card, quick items, ammo / nuts / armour, compass, hints) ends
up hanging above the bottom of the screen; on 21:9 the side groups drift towards the middle.

ApocaHUD43 re-anchors each HUD element to the edge or corner it was designed next to, keeping its exact distance from that edge:

- bottom and top rows stick to the bottom / top edge, corner groups to their corner;
- the middle of the screen (crosshair, item names, use prompts, scope / binocular overlays) is unchanged;
- the survival rows (health, hunger, thirst, fatigue, sanity, radiation) stay in the bottom-left corner;
- the loading screen picture covers the whole screen (aspect kept, edges cropped) instead of leaving the game world
  visible above and below it;
- at 16:9 nothing moves;
- works with [Apocapocket](https://github.com/DeonUrist/Apocapocket)'s extra slot card (4 / 5 / 6) and its moved hints.

## Install

BepInEx 5 (5.4.x) in the game folder. Copy `ApocaHUD43.dll` into `Apocalypter\BepInEx\plugins\` and start the game
(`Loading [ApocaHUD43 1.0.2]` in `BepInEx\LogOutput.log`). Remove it to go back to the game's layout.

## Config

`BepInEx\config\com.denis.apocalypter.apocahud43.cfg`: `[General] Enabled` (live toggle; also in the
[Apocasetter](https://github.com/DeonUrist/Apocasetter) Mods menu).

## Build

`./build.sh` (mono `mcs`, against the game's own `Managed` DLLs + `BepInEx\core`), or `dotnet build` (deploys to `BepInEx\plugins`).
