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
- **More from your fish** (Fishing skill): turning fish into Raw fish fillets and Uncooked Fish 'n'
  Bread gives more as Fishing levels up, up to twice as much at 100. Fish 'n' Bread now also counts
  the Anglerfish's stars like fillets do (a 2-star Anglerfish makes 2, a 3-star 3, before the
  skill bonus). The craft uses the first Anglerfish in your inventory.
- **Landscaping** (new skill, vanilla hoe): every hoe strike trains it. With skill each strike
  costs less stamina (up to 60% less at 100) and the brush can get bigger (up to 2.5x the vanilla
  radius at 100). Hold Left Alt and turn the mouse wheel in hoe build mode to pick a size up to
  what your skill allows. Level ground, path and paved road strikes also knock down lone bumps in
  the brush. Hold Shift while striking to shape the brush into an even ramp a cart can take
  (25 degrees). If the ground is gentle it follows the ground's slope; if it's too steep the ramp
  runs across the hill, in the direction closest to where you look, like a road winding up a
  hillside. The brush size you pick stays put as the skill rises, and Alt + wheel no longer zooms
  the camera. Older behaviour: *Ramp Mode* Relax, *Ramp Only With Shift* Off and *Shift Ramp Style*
  Spread give the 1.2-1.3 hoe; Relax + Fill gives 1.4. Shift doesn't sprint while the hoe is out, so stamina recovers
  between strikes. From Landscaping 25 the hoe also gets **Restore terrain**: it moves the
  ground in the brush back toward how the world generated it (fills holes, takes down raised
  ground), with no stone or workbench. One strike moves up to 2 m³ at the unlock level and 20 m³ at
  100, less toward the brush edge so a partial restore stays smooth. Once the ground is fully back,
  path and paved paint goes too. Not compatible with SmarterHoe: if it is installed the hoe is left
  alone.
- **Pulsating Earrings** (vanilla trinket): while worn they give a permanent **+25 max eitr** on
  top of your food (works with no eitr food too), plus an optional always-on eitr regen bonus
  (off by default). Their adrenaline effect is unchanged, so ViableTrinkets still works. Amounts
  are in the *8 - Trinkets* config section; the bonus shows in the item description.

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
4 Ranching · 5 Mining · 6 Fishing · 7 Landscaping · 8 Trinkets

Works with or without Valheim Plus (a warning is logged when both are loaded, since both touch
ships, taming and mining).

Original mods by [blaxxun-boop](https://github.com/blaxxun-boop) (Smoothbrain).
