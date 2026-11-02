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
  costs less stamina and wears the hoe less (each up to 60% less at 100) and the brush can get bigger (up to 2.5x the vanilla
  radius at 100). Hold Left Alt and turn the mouse wheel in hoe build mode to pick a size up to
  what your skill allows. Level ground, path and paved road strikes also knock down small lumps (up to three
  points wide, also on sloped roads) in
  the brush. Otherwise a click and a Shift-click are the game's own strikes. **Hold Ctrl** to
  see an incline that starts at your feet and runs the way you look; the mouse wheel sets its
  angle (15 degrees to start, negative goes down) and a click shapes the brush onto it, blending
  the sides into the ground around it. It follows you until your first Ctrl click, then stays put
  while you hold Ctrl, so you can walk up it striking ahead for one straight ramp (Ctrl doesn't
  crouch with the hoe out). **Hold Alt** to plan a longer ramp: Alt+click lays points (each gets
  a marker), Alt+click the last point again saves it as a blueprint that everyone with SnakeSkills
  sees, and Alt+click on the blueprint builds the part under your brush, at your brush size, paying
  stamina like any strike. Built parts turn green, the rest stays amber, and the blueprint clears
  itself once it's all built. Backspace undoes a point; Alt+Backspace removes the blueprint under
  the brush (its maker or an admin). **Ctrl+Z** undoes your last hoe strike (strikes made while
  holding the button count as one, up to 30 back) and Ctrl+Y or Ctrl+Shift+Z redoes it.
  **Shift-click** with level ground or a path tool bends the ground under the brush into a smooth
  curve along its slope, level side to side: on flat ground it flattens, and at the top or bottom of
  a ramp it rounds off the kink (vanilla's flatten digs divots into slopes). Alt ramp plans ease in
  and out over their first and last few metres. The hoe menu also lists the game's **trees and
  decorative bushes** (none that give items other than wood or resin): placing one plants the real
  thing, which everyone sees and can chop down. Trees cost 5 wood (*Tree Wood Cost*); Ctrl+Z
  takes a plant back, and plants you planted count as building pieces for players with the mod: the
  hammer removes them (giving the wood back) and Valheim Plus's Advanced Editing Mode can move them.
  The brush size you pick stays put as the skill rises; middle click resets it. Each hoe
  tool's description in the build menu lists these controls. The skill also limits how big a bump
  you can knock down (0.5 m at 0, 3 m at 100) and how far one level ground strike can dig or fill
  (1.5 m at 0, 8 m at 100); strike again to go further. When a strike can't change something
  (a skill limit, the game's 8 m limit on digging or raising, or a rock that isn't terrain) the
  brush flashes a dim red and a message says why. Older behaviour is in the config: *Click* LevelToFeet and *Shift Click* FromFeet give
  1.11-1.13 (Switchback 1.5-1.9, Relax 1.2-1.4), *Raise Click* Ramp makes Raise ground build a
  ramp up from your feet. Shift doesn't sprint while the hoe is out, so stamina recovers
  between strikes. **Raise ground** needs no workbench from Landscaping 50, and costs less stone with skill
  (half at 100). From Landscaping 25 the hoe also gets **Restore terrain**: it moves the
  ground in the brush back toward how the world generated it (fills holes, takes down raised
  ground), with no stone or workbench. One strike moves up to 2 m³ at the unlock level and 20 m³ at
  100, less toward the brush edge so a partial restore stays smooth. Once the ground is fully back,
  path and paved paint goes too. Not compatible with SmarterHoe: if it is installed the hoe is left
  alone.
- **Pulsating Earrings** (vanilla trinket): while worn they give a permanent **+25 max eitr** on
  top of your food (works with no eitr food too), plus an optional always-on eitr regen bonus
  (off by default). Their adrenaline effect is unchanged, so ViableTrinkets still works. Amounts
  are in the *8 - Trinkets* config section; the bonus shows in the item description.
- **Ember Blast** (vanilla Staff of Embers): middle click fires a blasting charge. The fireball
  breaks rocks and ore deposits within 4 m of where it lands, like dynamite, with 150 pickaxe
  damage (raised by Mining like a pickaxe) at Black metal pickaxe tier. It does nothing to
  creatures, players or buildings. Costs half again the eitr of a fireball. Blasted rock trains
  Mining and gets Mining's ore bonus. Settings in *9 - Ember Blast*.

- **Smart hammer** (vanilla hammer, SnakeHammer built in): with the hammer out in build mode,
  press **N** to switch smart mode on or off. Off, the hammer is completely vanilla. On, you get
  SnakeHammer's (SmartBuild's) grids, Hold, nudging and the **K** panel, with the same keys as
  before. Press **Y** for the **blueprint list** (with a small picture of each one): click a name or picture to build it, type to search,
  delete with a second click to confirm. To make a blueprint, aim at built pieces and press **F**
  (Shift+F: the whole group they were built with, Alt+F: everything connected) or use the **J**
  box, then press **Save selection** in the list (or Ctrl+S) and name it. Saved blueprints also
  sit in the hammer's Misc tab while smart mode is on. Blueprints and settings carry over from
  SmartBuild/SnakeHammer (same `BepInEx/config/SmartBuild/Blueprints` folder; settings are
  copied once into the *Smart Hammer - ...* sections). **Remove SnakeHammer/SmartBuild**: while
  either is installed the smart hammer here stays off. Client side only; works with Valheim
  Plus's grid alignment on or off.

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
4 Ranching · 5 Mining · 6 Fishing · 7 Landscaping · 8 Trinkets · 9 Ember Blast ·
Smart Hammer (and *Smart Hammer - Size/Gap/...*, local to each player, not synced)

Works with or without Valheim Plus (a warning is logged when both are loaded, since both touch
ships, taming and mining).

Original mods by [blaxxun-boop](https://github.com/blaxxun-boop) (Smoothbrain).
