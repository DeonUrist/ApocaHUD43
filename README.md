# ApocaHUD

Keeps Apocalypter's HUD on the screen edges at **any aspect ratio** (BepInEx 5 plugin).

The game lays its HUD out for 16:9 with every element anchored to the screen centre. On a 4:3 or 16:10 monitor the canvas
grows taller than 1080, so the bottom row (weapon card, survival card, quick items, ammo / nuts / armour, compass, hints) ends
up hanging above the bottom of the screen; on 21:9 the side groups drift towards the middle.

ApocaHUD re-anchors each HUD element to the edge or corner it was designed next to, keeping its exact distance from that edge:

- bottom and top rows stick to the bottom / top edge, corner groups to their corner;
- the middle of the screen (crosshair, item names, use prompts, scope / binocular overlays) is unchanged;
- at 16:9 nothing moves;
- works with [Apocapocket](../Apocapocket)'s extra slot card (4 / 5 / 6) and its moved hints.

Config `BepInEx\config\com.denis.apocalypter.apocahud.cfg`: `[General] Enabled` (live toggle; also in the
[Apocasetter](../Apocasetter) Mods menu).

## Build

`./build.sh` (mono `mcs`, against the game's own `Managed` DLLs + `BepInEx\core`), or `dotnet build` (deploys to `BepInEx\plugins`).
