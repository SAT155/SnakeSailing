# SnakeSailing → SnakeSkills

This repo builds **SnakeSkills**: maintained copies of three
[Smoothbrain](https://valheim.hexium.gg/mods/Smoothbrain/) / [blaxxun-boop](https://github.com/blaxxun-boop)
skill mods combined into one BepInEx plugin, updated for Valheim 1.0 / Ashlands and later, for a
small private dedicated server. Player-facing details are in
[`thunderstore/SnakeSkills/README.md`](thunderstore/SnakeSkills/README.md).

| Skill | Source | File |
| --- | --- | --- |
| Sailing | [blaxxun-boop/Sailing](https://github.com/blaxxun-boop/sailing) 1.1.10 | `SnakeSkills/Sailing.cs` |
| Ranching | [blaxxun-boop/Ranching](https://github.com/blaxxun-boop/ranching) 1.1.10 | `SnakeSkills/Ranching.cs` |
| Mining | [blaxxun-boop/Mining](https://github.com/blaxxun-boop/mining) 1.1.8 | `SnakeSkills/Mining.cs` |

Until SnakeSkills the three were separate projects here (`Sailing/`, `Ranching/`, `Mining/`, see
git history).

## What changed from upstream

- **One plugin** (`com.snakesquad.snakeskills`, `SnakeSkills.dll`) with one ServerSync config.
  Skill game logic is unchanged. Same skill names, so the same skill ids
  (`Skill.fromName(name)` = hash of the English name): existing levels carry over.
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
  cuts stamina by skill; a `TerrainOp.Awake` prefix scales the strike's radii by skill (Left Alt +
  wheel picks a size), claims ownership of every terrain zone it touches (a non-owner's strike is
  sent as just the prefab hash, so a bigger brush would be lost; same fix as SnakeSmarterHoe), and
  raises the skill. The postfix regenerates the heightmaps, then lowers lone bumps and spreads
  steps steeper than the cart slope over the points inside the brush, written into the
  `TerrainComp` level deltas like vanilla `LevelTerrain`. `UpdatePlacementGhost` resizes the
  ghost; `CheckRun` blocks sprint in hoe build mode. Off while SmarterHoe is loaded.
- `BepInIncompatibility` on the three old GUIDs, so SnakeSkills doesn't double up with them.
- Upstream's hard block on Valheim Plus is a warning instead.

## Building

- **CI** (`.github/workflows/build.yml`) builds on every push with no game install: it downloads
  the free Valheim dedicated server (SteamCMD, app 896660) for the game assemblies and the
  BepInEx pack, builds `SnakeSkills.dll`, ILRepacks `SkillManager`, `ServerSync` and
  `CreatureLevelControlAPI` into it, and packages `SnakeSkills-<version>.zip` for r2modman's
  **Import local mod**.
- **Releases** are automatic: every push to the default branch that changes `SnakeSkills/` or
  `thunderstore/` publishes `v<MAJOR.MINOR>.<run number>` (MAJOR.MINOR from
  `thunderstore/SnakeSkills/manifest.json`) with the zip attached. A manual run on another branch
  publishes a pre-release for testing.
- **Locally:** `dotnet build SnakeSkills/SnakeSkills.csproj -c Release` with Valheim and BepInEx
  installed (Steam install auto-detected, or pass `-p:GamePath=...`, or
  `-p:ManagedDir=... -p:BepInExDir=...`).

`tools/make_icon.py` redraws the package icon from the three skill icons.

## Credits

Original mods by [blaxxun-boop](https://github.com/blaxxun-boop)
([Sailing](https://github.com/blaxxun-boop/sailing),
[Ranching](https://github.com/blaxxun-boop/ranching),
[Mining](https://github.com/blaxxun-boop/mining)). This repo carries forward their 1.0/Ashlands fixes for a private dedicated server.
