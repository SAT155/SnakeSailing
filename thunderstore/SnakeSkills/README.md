# SnakeSkills

blaxxun-boop's **Sailing**, **Ranching** and **Mining** skill mods in one plugin, kept up to date
for the SnakeSquad server.

- **Sailing**: ships you build get more health, ships you steer go faster, and you explore a wider
  radius while on a ship. Shift + E on a ladder nudges a stuck ship.
- **Ranching**: animals tame faster and tamed animals drop more. Higher levels show hunger and
  pregnancy timers, keep taming animals calm, and give offspring a chance at an extra star.
- **Mining**: more pickaxe damage and ore. From level 50, a small chance the whole deposit breaks
  at once (Ctrl + T toggles it).
- **Fishing XP by fish** (vanilla Fishing skill): every catch gives Fishing XP by the kind of
  fish, from 10 for a Perch up to 150 for a Magmafish, more for starred fish. Reeling in a hard
  fish also trains faster (up to 4x for a Magmafish). Vanilla gives nothing for the catch and the
  same reeling XP for every fish. All amounts are in the *6 - Fishing* config section.
- **Landscaping** (new skill, vanilla hoe): every hoe strike trains it. With skill each strike
  costs less stamina (up to 60% less at 100) and the brush can get bigger (up to 2.5x the vanilla
  radius at 100). Hold Left Alt and turn the mouse wheel in hoe build mode to pick a size up to
  what your skill allows. Level ground, path and paved road strikes also knock down lone bumps in
  the brush, and path/paved road strikes even out steps too steep for a cart (25 degrees) into a
  ramp, only inside the brush. Shift doesn't sprint while the hoe is out, so stamina recovers
  between strikes. Not compatible with SmarterHoe: if it is installed the hoe is left alone.

## Your skill levels are safe now

Vanilla Valheim throws away any skill it doesn't know when it loads a character. So one session
without the mods (or with them disabled) used to wipe Sailing, Ranching, Mining and Landscaping for good.

SnakeSkills keeps a copy of those levels in the character's custom data, which vanilla
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
4 Ranching · 5 Mining · 6 Fishing · 7 Landscaping

Works with or without Valheim Plus (a warning is logged when both are loaded, since both touch
ships, taming and mining).

Original mods by [blaxxun-boop](https://github.com/blaxxun-boop) (Smoothbrain).
