STS2 Incoming Damage Overlay — Windows x64

This is the current standalone overlay, not the legacy in-game mod.
The .NET runtime is included. No SDK, build tools, BaseLib, or mod installation needed.

GET STARTED
1. Extract the entire ZIP to a permanent folder you can write to.
2. Run STS2IncomingDamageOverlay.exe, then launch Slay the Spire 2 normally.
   Keep all extracted files together; do not move just the EXE or run it inside the ZIP.

OPTIONAL AUTOMATIC STARTUP
Double-click Enable-AutoStart.cmd once. A hidden watcher starts now and at your
Windows sign-in. It opens the overlay when STS2 starts and closes it when STS2 exits.
Exiting the overlay manually keeps it closed until the next game session.
Double-click Disable-AutoStart.cmd before moving/removing this folder to disable it.
After moving the folder, run Enable-AutoStart.cmd again to update the saved path.

CONTROLS
- Hold Ctrl and left-drag the overlay to move it.
- Orange shield tray icon: Show/Hide overlay or Exit. Double-click toggles visibility.
- The overlay keeps updating while the game is in the background.

COMPATIBILITY
This release supports the inspected v0.113.0 game build dated 2026-11-12, including
Fool, and the earlier inspected build. Exact game-file hashes are checked.
If an update changes those files, readings stop until a compatible overlay is released.
At game startup, the reader waits for combat data and connects automatically once
it loads (checked every five seconds). If it explicitly requests refreshed metadata
or reports a connection failure, exit using the tray menu and run the EXE again.

Damage after block/Osty is an estimate; special mitigation and character mechanics
are not fully simulated. This app does not modify the game or remove existing mods.
Windowed/borderless mode is recommended. Windows x64 only.

This community build is not code-signed. Only download it from the project releases.
Documentation and updates:
https://github.com/b34st80y/STS2IncomingDamageOverlayMod
