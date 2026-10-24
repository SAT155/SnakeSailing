# SnakeSailing

Maintained forks of a couple of [Smoothbrain](https://valheim.hexium.gg/mods/Smoothbrain/) /
[blaxxun-boop](https://github.com/blaxxun-boop) Valheim BepInEx mods, updated
to work with the Valheim 1.0 / Ashlands game update and to build
automatically, for use on a small private dedicated server.

| Mod | Source | What it does |
| --- | --- | --- |
| `Sailing/` | [blaxxun-boop/Sailing](https://github.com/blaxxun-boop/sailing) | Adds a Sailing skill: ships you build gain extra health, ships you command get faster, exploration radius while sailing increases with skill. Optional ship "nudge" hotkey. |
| `Ranching/` | [blaxxun-boop/Ranching](https://github.com/blaxxun-boop/ranching) | Adds a Ranching skill: reduces taming time and increases item yield of tamed animals as skill increases. |
| `Mining/` | [blaxxun-boop/Mining](https://github.com/blaxxun-boop/mining) | Adds a Mining skill: increases mining damage and ore yield, with an optional chance for deposits to explode, as skill increases. |

## Multiplayer / dedicated server notes

- **Install each mod on the server AND on every client.** These are
  client+server Harmony mods (they patch behavior that needs to run on both
  sides), not server-only mods — a client without them will desync or
  simply not get the effects.
- Config is synced from the server to clients automatically via
  [ServerSync](https://github.com/blaxxun-boop/ServerSync). By default
  "Lock Configuration" is **On** for both mods, so only a server admin can
  change settings, and connecting clients automatically pick up the
  server's values — you don't need to hand-edit each client's config.
- All game logic is stored on ZDOs and driven by the authoritative
  server/host, so it stays consistent across all connected players.
- Each mod is self-contained: its dependency DLLs (`SkillManager`,
  `ServerSync`, and for Ranching, `CreatureLevelControlAPI`) are merged
  into the built plugin DLL via ILRepack, so you only need to copy the one
  `.dll` file per mod to `BepInEx/plugins/` — no extra DLLs to manage.

## Requirements

- Valheim 1.0+ (Ashlands) dedicated server and clients
- [BepInEx 5](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/) installed on the server and every client

## Compatibility

- **Valheim Plus**: upstream hard-blocked all three mods from loading
  alongside any plugin with the GUID `org.bepinex.plugins.valheim_plus`
  (this includes forks that keep that GUID, e.g.
  [Grantapher's temporary V+ build](https://thunderstore.io/c/valheim/p/Grantapher/ValheimPlus_Grantapher_Temporary/)).
  That hard block has been removed here — each mod now just logs a warning
  if it detects V+ (or a fork of it) is also loaded, since these mods
  patch overlapping game behavior (Sailing: ship speed/controls;
  Ranching: taming/breeding; Mining: mining damage/yield) and could
  interact in ways neither author tested. Test your own setup after
  enabling both.

## Building automatically (CI)

`.github/workflows/build.yml` builds all three plugins (as a matrix, one
job per mod) on every push/PR with no local Valheim install required: it
anonymously downloads the Valheim dedicated server (for the game's
managed assemblies) and the BepInEx pack via SteamCMD/curl on the runner,
then builds and publicizes against those.

- Every push/PR: open the run under the **Actions** tab and download the
  `Sailing`, `Ranching`, and `Mining` artifacts (each contains its
  `.dll`) — grab these on each of your PCs for local testing.
- Every push to the default branch that changes a mod (`Sailing/`, `Ranching/`,
  `Mining/` or `VERSION`) publishes a GitHub Release by itself, tagged
  `v<MAJOR.MINOR>.<run number>` (MAJOR.MINOR from the `VERSION` file), with all
  three DLLs attached and each mod's own version listed in the notes. Docs- or
  CI-only pushes don't release; running the workflow by hand always does.

## Building locally

Each mod is a standard BepInEx/Harmony C# class library (SDK-style
project, buildable with the `dotnet` CLI). For a local build you need a
Valheim install to build against, for the game's managed DLLs.

1. Install the .NET SDK.
2. Make sure Valheim is installed via Steam. Each project auto-detects the
   game path (`~/.steam/steam/steamapps/common/Valheim` on Linux, the
   Steam registry key on Windows). Override it by setting the `GamePath`
   MSBuild property if auto-detection doesn't find it, e.g.:
   ```
   dotnet build Sailing/Sailing.csproj -c Release -p:GamePath="C:\Program Files (x86)\Steam\steamapps\common\Valheim"
   dotnet build Ranching/Ranching.csproj -c Release -p:GamePath="C:\Program Files (x86)\Steam\steamapps\common\Valheim"
   dotnet build Mining/Mining.csproj -c Release -p:GamePath="C:\Program Files (x86)\Steam\steamapps\common\Valheim"
   ```
   Alternatively, point straight at the managed/BepInEx folders with
   `-p:ManagedDir=... -p:BepInExDir=...` (this is what CI does).
3. Build in Release configuration (each mod has its own `.sln`):
   ```
   dotnet build Sailing.sln -c Release
   dotnet build Ranching.sln -c Release
   dotnet build Mining.sln -c Release
   ```
   The build automatically publicizes the game assemblies it needs
   (via `BepInEx.AssemblyPublicizer.MSBuild`), ILRepacks the plugin and
   its dependencies into a single DLL, and — only on Windows, when
   `GamePath` resolves to a real folder — copies it straight into
   `<GamePath>/BepInEx/plugins/`.

## Installing on the dedicated server

For each mod you want:

1. Build (or download) its `.dll`.
2. Copy it into `BepInEx/plugins/` on the dedicated server.
3. Copy the same `.dll` into `BepInEx/plugins/` on every client that
   connects.
4. Start the server, then adjust settings in `BepInEx/config/` on the
   server if desired (`org.bepinex.plugins.sailing.cfg` /
   `org.bepinex.plugins.ranching.cfg` / `org.bepinex.plugins.mining.cfg`)
   — connected clients will sync to these values automatically.

## Credits

Original mods by [blaxxun-boop](https://github.com/blaxxun-boop)
([Sailing](https://github.com/blaxxun-boop/sailing),
[Ranching](https://github.com/blaxxun-boop/ranching),
[Mining](https://github.com/blaxxun-boop/mining)). This fork carries
forward their upstream 1.0/Ashlands fixes for continued use on a private
dedicated server.
