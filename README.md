# SnakeSailing → SnakeSkills

This repo builds two BepInEx plugins for a small private dedicated server:

- **SnakeSkills** (`SnakeSkills/`, `com.snakesquad.snakeskills`, `SnakeSkills.dll`): Sailing,
  Ranching, Mining and Landscaping skills, Fishing XP by fish and other tweaks, with one ServerSync
  config. All SAT's own code plus the free SkillManager and ServerSync libraries, so it can be
  shared on Thunderstore. Player-facing details are in
  [`thunderstore/SnakeSkills/README.md`](thunderstore/SnakeSkills/README.md).
- **SnakeSmartHammer** (`SnakeSmartHammer/`, `com.snakesquad.snakesmarthammer`): the smart hammer.
  It is a decompiled fork of codepeople's SmartBuild, which has no open licence, so it ships in
  this repo's GitHub releases only. **Never upload it to Thunderstore.**

## Sailing, Ranching and Mining (rewritten in 1.29)

Up to 1.28 these three were copies of blaxxun-boop's Sailing, Ranching and Mining mods, which
have no open licence. 1.29 replaces them with new code written from a description of how the
skills behave. The skill names are unchanged (skill ids are `Skill.fromName(name)`, a hash of the
English name), and so are the config sections and keys, so levels and settings carry over.

- **Shared (`SkillShare.cs`)**: the machine that runs a ship, animal or rock is often not the
  skilled player's, so the local player writes `SnakeSkills <Skill>` (level 0-1) and
  `SnakeSkills ExplosiveMining` to their player ZDO twice a second, and experience goes back
  through the `SnakeSkills RaiseSkill` RPC on the player's `ZNetView`.
- **Sailing**: `WearNTear.OnPlaced` saves the builder's level on the ship's ZDO as
  `Sailing Skill Level` (the 1.28 key, so older ships keep it) and `WearNTear.Awake` scales
  `m_health` by `1 + level x Ship Health Factor`. `Ship.GetSailForce` scales by the captain's
  level x the ship type's speed factor and gives the captain 0.5 XP per second under sail.
  Per-ship-type settings are added in a late `ZNetScene.Awake` postfix. `ShipControlls.Interact`
  and `Ship.Forward` enforce the helm/half/full sail levels. `Minimap.Explore` widens the radius
  while attached to a ship. `Ladder.Interact` with the modifier pushes the ship
  (`ForceMode.VelocityChange`, 1 s cooldown, only when facing it).
- **Ranching**: `Tameable.DecreaseRemainingTime` (nearest player within 10 m, 10% chance of 7 XP),
  `CharacterDrop.GenerateDropList` for tamed animals (nearest player within 50 m, 50 XP),
  `Player.GetStealthFactor` returns 0 while a partly tamed animal's `MonsterAI.UpdateAI` runs,
  `Tameable.GetStatusString` adds breeding progress, time until birth (approximate: birth happens
  at the first breeding check after it) and time until hungry, and `Procreation.Procreate`
  temporarily raises `m_minOffspringLevel`. The star cap is 3, or Creature Level & Loot Control's
  `API.GetMaxLevel()` found by reflection (no compile-time dependency any more).
- **Mining**: `SEMan.ModifyAttack` scales pickaxe damage (`Mining.DamageMultiplier`, also used by
  Ember Blast). Prefixes on `MineRock5.RPC_Damage`, `MineRock.RPC_Hit` and rock-like
  `Destructible.RPC_Damage` (`Mining.IsRock`) note the miner for the yield bonus in a low-priority
  `DropTable.GetDropList` postfix, give 1 XP per valid hit on the owner, and roll the
  whole-deposit break, which hits every remaining area with tool tier 1000. Each player's
  Ctrl+T choice travels in their ZDO, so it applies on whichever machine owns the rock.
- Icons are new (`tools/make_skill_icons.py`).

## Other features

- **Skill backup (`SkillBackup.cs`).** Vanilla `Skills.Load` drops skill ids that aren't in the
  `SkillType` enum (`IsSkillValid`), and the next save writes the list without them, so one
  unmodded login wiped the custom skills. A `Player.Save` prefix copies each custom skill to
  `Player.m_customData["SnakeSkills_<Name>"] = "level;accumulator"` (vanilla loads and saves that
  dictionary as-is). After loading, any custom skill missing from the skill list is restored from
  it; a skill that is present wins.
- **Config import (`ConfigImport.cs`).** On the first start (no SnakeSkills .cfg yet), values are
  copied from the old mods' .cfg files by section and key.
- **Fishing XP by fish (`Fishing.cs`)**, new: vanilla only raises Fishing while reeling
  (`FishingFloat.FixedUpdate`, `RaiseSkill(Fishing)` once per second of pulling, twice as often
  with a fish on), the same for every fish. A `FishingFloat.Catch` postfix adds catch XP per fish
  prefab (config list, stars add a bonus), given in steps of 1 because one `Skills.RaiseSkill`
  can only gain one level. While a fish is on the line, `Player.RaiseSkill(Fishing)` is scaled by
  `1 + fishXp x Reeling XP Per Fish XP`.
  1.3: a `Recipe.GetAmount` postfix gives more Raw fish and Fish 'n' Bread with Fishing skill, and
  adds vanilla's per-star bonus to Fish 'n' Bread (it only applies to one-ingredient recipes).
- **Landscaping (`Landscaping.cs`)**, new skill for the vanilla hoe. `GetBuildStamina` postfix
  cuts stamina by skill, an `UpdatePlacement` prefix/postfix gives back the skill's share of hoe wear; a `TerrainOp.Awake` prefix scales the strike's radii by skill (Left Alt +
  wheel picks a size), claims ownership of every terrain zone it touches (a non-owner's strike is
  sent as just the prefab hash, so a bigger brush would be lost; same fix as SnakeSmarterHoe), and
  raises the skill. The postfix regenerates the heightmaps, then lowers lone bumps (only where the ring around them is close to a plane, so the edges of cuts and fills keep their shape) and spreads
  steps steeper than the cart slope over the points inside the brush, written into the
  `TerrainComp` level deltas like vanilla `LevelTerrain`. `UpdatePlacementGhost` resizes the
  ghost; `CheckRun` blocks sprint in hoe build mode. Off while SmarterHoe is loaded.
  Slope easing runs only while Shift is held (1.4). 1.5: Ramp Mode Slope fits a plane to the
  ground before the strike (least squares) and moves the brush onto one even ramp, along the
  ground's slope or, if steeper than the cart slope, across the hill toward the camera's facing;
  Relax is the old neighbour relaxation. Alt + wheel keeps `GameCamera.m_distance` and
  `Player.m_placeRotation`; the picked brush size is an absolute, unsynced config value. **Restore terrain (`RestoreTerrain.cs`, 1.4)**:
  a copy of the level ground piece without its `TerrainOp`/`ZNetView`, added to the hoe's table and
  shown from Landscaping 25 (`PieceTable.UpdateAvailable` known recipes). `Player.PlacePiece` is
  intercepted for it (stamina, durability, animation done by hand) and it moves `TerrainComp`
  level deltas toward 0 within a per-strike volume and depth limit with soft falloff.
- **Dead Raiser skeleton choice (`DeadRaiser.cs`, 1.26)**: middle click with the Dead Raiser opens
  an IMGUI menu of skeleton kinds. The choice is queued at `SpawnAbility.Setup` (local caster,
  `Skeleton_Friendly`), written to the new skeleton's ZDO in a `Character.SetLevel` prefix (called
  right after `Instantiate`, before `Start`), and a `Humanoid.GiveDefaultItems` prefix narrows
  `m_randomWeapon`/`m_randomShield` to the chosen items on every client and reload. An
  `Attack.GetAttackEitr` postfix sets the cost: Random is vanilla, each kind lerps between its own
  multiples of the base cost at Blood Magic 0 and 100. Mechanics in SnakeWood
  `docs/game/summons-and-dead-raiser.md`.
- **Bubble shield autocast (`BubbleShield.cs`, 1.27)**: off by default, local (unsynced) config in
  *11 - Bubble Shield*. Each frame, with `StaffShield` as the current weapon, checks: still (on ground,
  no `m_moveDir`, body speed < 0.2 m/s, not in attack/attached) for > 0.5 s (1.28; was 5 s); not in combat
  (`Humanoid.m_lastCombatTimer` < 10 s, a `Player.OnDamaged` postfix for hits, or an alerted
  `BaseAI.IsEnemy` character within 30 m); the longest `SE_Shield` `GetRemaningTime()` <= 5 s (0 with
  no bubble); full health and eitr. The flag arms on the false-to-true edge and clears when
  `Humanoid.StartAttack(null, false)` succeeds (retried at most once a second while armed). While armed, a `Hud.UpdateStatusEffects` prefix appends a local `StatusEffect` with the staff's icon to the HUD list (1.28).
- `BepInIncompatibility` on blaxxun-boop's three GUIDs, so SnakeSkills doesn't double up with them.
- Works with Valheim Plus (a warning is logged, since both touch ships, taming and mining).

## Smart hammer (SnakeSmartHammer)

- 1.29: moved out of SnakeSkills into its own plugin, `SnakeSmartHammer/` (namespace
  `SnakeSmartHammer`, `HammerPlugin` is the BepInEx plugin, SmartBuild's old `Plugin` class is a
  component on it). On its first start it copies the `[Smart Hammer...]` sections from
  `com.snakesquad.snakeskills.cfg` into its own config. Blueprints stay in
  `BepInEx/config/SmartBuild/Blueprints`. No ServerSync (all its settings are local).
- History (1.19-1.28, then inside SnakeSkills as `SnakeSkills/SmartBuild/`): the SnakeHammer repo's source (a
  **private, decompiled fork of codepeople's SmartBuild 1.21.0: never publish or redistribute
  SnakeSkills builds that contain it**), namespace `SnakeSkills.SmartBuild`. Changes from
  SnakeHammer: no Jotunn. The Smart Hammer item is gone; `SmartHammer.Equipped` = vanilla
  `Hammer` in hand (1.17-1.20 also needed a smart-mode hotkey; dropped in 1.22 at the user's
  request), and every SmartBuild patch already gated on it. Blueprint stubs go into the vanilla
  hammer's table (Misc). 1.23: `BuildMenuTab.cs` adds a Blueprints tab to the game's own build
  menu (`BuildUi`, Deep North): a copy of the last tab button, plus a vanilla `RecentPieceList`
  appended to `BuildUi.m_pieceLists` and `TabHandler.m_tabs`, refilled with the stubs before
  `UpdatePieceButtons`; the stubs are filtered out of `ByUsagePieceList`/`ByMaterialPieceList`,
  and each blueprint's picture becomes its icon. 1.24: `BlueprintCostPanel.cs` (IMGUI) lists every material when there are more than
  `Hud.m_requirementItems` slots, placed from the info panel's RectTransforms' screen corners. Also 1.24: the thumbnail camera
  and directional light are created per render and destroyed right after (1.23 kept them alive
  and the user saw broken scene lighting, then a crash), and pictures are only drawn while the
  list or build menu is open.
  `GUIManager.cs` stands in for Jotunn's (DefaultControls + the game's sprites/fonts);
  `BlueprintList.cs` (IMGUI, default Y) lists, picks (`Player.SetSelectedPiece`), deletes and
  saves blueprints; 1.22 adds pictures (`BlueprintThumbnails.cs`: the stub cloned 5 km below the world on a spare layer for one frame, a disabled camera + light that only see that layer, `Camera.Render` into a RenderTexture, `ReadPixels`). SmartBuild's former plugin class is a component on SnakeSkills' object and
  patches its own classes (`SnakeSkillsPlugin` skips that namespace); with
  `com.codepeople.smartbuild` loaded none of it runs. Config: SmartBuild sections prefixed
  `Smart Hammer - `, bound unsynced, imported once from `com.codepeople.smartbuild.cfg`.

## Building

- **CI** (`.github/workflows/build.yml`) builds on every push with no game install: it downloads
  the free Valheim dedicated server (SteamCMD, app 896660) for the game assemblies and the
  BepInEx pack, builds `SnakeSkills.dll` (ILRepacks `SkillManager` and `ServerSync` into it) and
  `SnakeSmartHammer.dll`, and packages `SnakeSkills-<version>.zip` and
  `SnakeSmartHammer-<version>.zip` for r2modman's **Import local mod**.
- **Releases** are automatic: every push to the default branch that changes `SnakeSkills/`, `SnakeSmartHammer/` or
  `thunderstore/` publishes `v<MAJOR.MINOR>.<run number>` (MAJOR.MINOR from
  `thunderstore/SnakeSkills/manifest.json`) with the zip attached. A manual run on another branch
  publishes a pre-release for testing.
- **Locally:** `dotnet build SnakeSkills/SnakeSkills.csproj -c Release` with Valheim and BepInEx
  installed (Steam install auto-detected, or pass `-p:GamePath=...`, or
  `-p:ManagedDir=... -p:BepInExDir=...`).

`tools/make_skill_icons.py` draws the three skill icons; `tools/make_icon.py` redraws the
package icon from them.

## Credits

Sailing, Ranching and Mining are inspired by [blaxxun-boop](https://github.com/blaxxun-boop)'s
(Smoothbrain) mods of the same names; the code here is written from scratch. The smart hammer
derives from codepeople's SmartBuild. SkillManager and ServerSync are blaxxun-boop's libraries,
free to use.
