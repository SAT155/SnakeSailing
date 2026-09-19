# SnakeSailing

A BepInEx mod for Valheim that adds a Sailing skill: ships you build gain
extra health, ships you command get faster the higher your skill, and your
map exploration radius while on a ship increases with skill level. Also adds
an optional "nudge" hotkey to unstick a grounded ship.

This is a maintained fork of [blaxxun-boop/Sailing](https://github.com/blaxxun-boop/sailing),
updated to work with the Valheim 1.0 / Ashlands game update, for use on a
small private dedicated server.

## Multiplayer / dedicated server notes

- **Install the mod on the server AND on every client.** This is a
  client+server Harmony mod (it patches ship/player behavior that needs to
  run on both sides), not a server-only mod — a client without it will
  desync or simply not get the skill/speed effects.
- Config is synced from the server to clients automatically via
  [ServerSync](https://github.com/blaxxun-boop/ServerSync). By default
  "Lock Configuration" is **On**, so only a server admin can change
  settings, and connecting clients automatically pick up the server's
  values — you don't need to hand-edit each client's config.
- All game logic (skill level, ship speed bonus, ship health) is stored on
  the ship/player ZDOs and driven by the authoritative server/host, so it
  stays consistent across all connected players.
- The mod is self-contained: `SkillManager` and `ServerSync` are merged
  into the built `Sailing.dll` via ILRepack, so you only need to copy the
  one `Sailing.dll` file to `BepInEx/plugins/` — no extra DLLs to manage.

## Requirements

- Valheim 1.0+ (Ashlands) dedicated server and clients
- [BepInEx 5](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/) installed on the server and every client

## Compatibility

- **Valheim Plus**: the original upstream mod hard-blocked loading
  alongside any plugin with the GUID `org.bepinex.plugins.valheim_plus`
  (this includes forks that keep that GUID, e.g.
  [Grantapher's temporary V+ build](https://thunderstore.io/c/valheim/p/Grantapher/ValheimPlus_Grantapher_Temporary/)).
  That hard block has been removed here — the mod now just logs a warning
  if it detects V+ (or a fork of it) is also loaded, since both mods patch
  ship speed/controls and could interact in ways neither author tested.
  Test your own ship setup after enabling both.

## Building automatically (CI)

`.github/workflows/build.yml` builds the plugin on every push/PR with no
local Valheim install required: it anonymously downloads the Valheim
dedicated server (for the game's managed assemblies) and the BepInEx pack
via SteamCMD/curl on the runner, then builds and publicizes against those.

- Every push/PR: open the run under the **Actions** tab and download the
  `Sailing` artifact (contains `Sailing.dll`) — grab this on each of your
  two PCs for local testing.
- Pushing a tag like `v1.1.10` also cuts a GitHub Release with
  `Sailing.dll` attached.

## Building locally

This is a standard BepInEx/Harmony C# class library targeting .NET
Framework 4.8 (SDK-style project, buildable with the `dotnet` CLI). For a
local build you need a Valheim install to build against, for the game's
managed DLLs.

1. Install the .NET SDK.
2. Make sure Valheim is installed via Steam. The project auto-detects the
   game path (`~/.steam/steam/steamapps/common/Valheim` on Linux, the
   Steam registry key on Windows). Override it by setting the `GamePath`
   MSBuild property if auto-detection doesn't find it, e.g.:
   ```
   dotnet build Sailing/Sailing.csproj -c Release -p:GamePath="C:\Program Files (x86)\Steam\steamapps\common\Valheim"
   ```
   Alternatively, point straight at the managed/BepInEx folders with
   `-p:ManagedDir=... -p:BepInExDir=...` (this is what CI does).
3. Build in Release configuration:
   ```
   dotnet build Sailing.sln -c Release
   ```
   The build automatically publicizes the game assemblies it needs
   (via `BepInEx.AssemblyPublicizer.MSBuild`), ILRepacks the plugin and
   its dependencies into a single `Sailing.dll`, and — only on Windows,
   when `GamePath` resolves to a real folder — copies it straight into
   `<GamePath>/BepInEx/plugins/`.

## Installing on the dedicated server

1. Build (or download) `Sailing.dll`.
2. Copy it into `BepInEx/plugins/Sailing.dll` on the dedicated server.
3. Copy the same `Sailing.dll` into `BepInEx/plugins/Sailing.dll` on every
   client that connects.
4. Start the server, then adjust settings in
   `BepInEx/config/org.bepinex.plugins.sailing.cfg` on the server if
   desired — connected clients will sync to these values automatically.

## Credits

Original mod by [blaxxun-boop](https://github.com/blaxxun-boop/sailing).
This fork carries forward their upstream 1.0/Ashlands fixes for continued
use on a private dedicated server.
