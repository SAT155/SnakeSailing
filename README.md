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

## Building

This is a standard BepInEx/Harmony C# class library targeting .NET
Framework 4.8. It needs a local Valheim install to build against (for the
game's managed DLLs), so building has to happen on a machine (or Wine
prefix) with Valheim installed via Steam.

1. Install the .NET SDK (with MSBuild) and Visual Studio / Rider or the
   `dotnet` CLI.
2. Make sure Valheim is installed via Steam. The project auto-detects the
   game path (`~/.steam/steam/steamapps/common/Valheim` on Linux, the
   Steam registry key on Windows). Override it by setting the `GamePath`
   MSBuild property if auto-detection doesn't find it, e.g.:
   ```
   dotnet build Sailing/Sailing.csproj -p:GamePath="C:\Program Files (x86)\Steam\steamapps\common\Valheim"
   ```
3. Build in Release configuration:
   ```
   dotnet build Sailing.sln -c Release
   ```
   The build automatically ILRepacks the plugin and its dependencies into
   a single `Sailing.dll`, and (per `ILRepack.targets`) copies it into
   `<GamePath>/BepInEx/plugins/` when building on Windows.

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
