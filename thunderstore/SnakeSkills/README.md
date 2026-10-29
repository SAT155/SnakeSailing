# SnakeSkills

blaxxun-boop's **Sailing**, **Ranching** and **Mining** skill mods in one plugin, kept up to date
for the SnakeSquad server.

- **Sailing**: ships you build get more health, ships you steer go faster, and you explore a wider
  radius while on a ship. Shift + E on a ladder nudges a stuck ship.
- **Ranching**: animals tame faster and tamed animals drop more. Higher levels show hunger and
  pregnancy timers, keep taming animals calm, and give offspring a chance at an extra star.
- **Mining**: more pickaxe damage and ore. From level 50, a small chance the whole deposit breaks
  at once (Ctrl + T toggles it).

## Your skill levels are safe now

Vanilla Valheim throws away any skill it doesn't know when it loads a character. So one session
without the mods (or with them disabled) used to wipe Sailing, Ranching and Mining for good.

SnakeSkills keeps a copy of those three levels in the character's custom data, which vanilla
loads and saves without touching. If you play a session unmodded, the next time you start with
SnakeSkills your levels come back (the log says `Restored level ...`).

The copy is made the first time the character is saved with SnakeSkills, so start with the mod
once before playing unmodded. Levels already lost before SnakeSkills can't be recovered.

## Installing

- Install on the **server and every modded player**. Settings sync from the server; with
  *Lock Configuration* on, only admins can change them.
- **Remove the old Sailing, Ranching and Mining mods.** SnakeSkills uses the same skill ids, so
  the levels carry straight over. While any of the old ones is installed, SnakeSkills does not
  load (the BepInEx log says which one is in the way).
- On the first start, settings are copied from the old `org.bepinex.plugins.sailing.cfg`,
  `...ranching.cfg` and `...mining.cfg` into `com.snakesquad.snakeskills.cfg`. After that only
  the new file is used.

## Config sections

1 General (lock) · 2 Sailing · 3 Sailing Ship Speed (per ship, appears once a world loads) ·
4 Ranching · 5 Mining

Works with or without Valheim Plus (a warning is logged when both are loaded, since both touch
ships, taming and mining).

Original mods by [blaxxun-boop](https://github.com/blaxxun-boop) (Smoothbrain).
