# Standalone incoming-damage overlay

**This is the newest, actively supported version of the project.** Use this app
for current compatibility updates, fixes, and new features. The original in-game
mod in `STS2IncomingDamageOverlayMod/` is legacy and retained for reference.

Windows x64 desktop overlay using WPF and Microsoft's ClrMD. The downloadable
release includes the .NET runtime; a normal source build requires the .NET 9
Desktop Runtime. No game assembly
references, mod loader, Harmony, BaseLib, injected DLL, or game-file writes.

## Download and run

Get the Windows x64 ZIP from the [latest release](https://github.com/b34st80y/STS2IncomingDamageOverlayMod/releases/latest).
Extract the entire ZIP and run `STS2IncomingDamageOverlay.exe`. No building or
separate .NET installation is needed. Keep the EXE and all accompanying files together.
The included `RELEASE-README.txt` covers controls, compatibility, and automatic startup.

## Build and run from source

From the repository root:

```powershell
dotnet build .\STS2IncomingDamageOverlay\STS2IncomingDamageOverlay.csproj -c Release
& .\STS2IncomingDamageOverlay\bin\Release\net9.0-windows\STS2IncomingDamageOverlay.exe
```

Keep the build output folder together; the EXE needs its adjacent dependencies.

### Automatic launch and close

After building Release, run `./STS2IncomingDamageOverlay/Set-AutoStart.ps1`.
For a downloaded release, double-click `Enable-AutoStart.cmd` instead.
This starts a hidden watcher now and registers it for your Windows sign-in.
The watcher checks for STS2 every two seconds without reading game memory. It
opens one overlay per game session; that overlay closes (including its tray icon)
when the game exits. The watcher remains idle so it can detect the next launch.
Exiting the overlay manually keeps it closed until the next game session.
Run the same script with `-Disable` to remove automatic startup and stop the
watcher. Keep the build folder in place, or rerun the script after moving it.
Downloaded releases also include `Disable-AutoStart.cmd` for this purpose.
Launching the overlay EXE directly still works independently of the watcher.

Launch STS2 normally for an unmodded session. Running this beside an already-modded
game does not remove that game's modded status. The original mod has been uninstalled;
restart the game to unload it from any session that was already running.
Windowed/borderless display is recommended; exclusive fullscreen is unverified.

- The window is click-through during normal play and stays visible above other apps
  while the game is in the background. Readings keep updating regardless of focus.
- Hold **Ctrl** to show edit mode and drag the window.
- To exit, right-click the orange shield tray icon and choose **Exit**.
- An **orange shield icon** in the Windows system tray means the app is running,
  even when no damage is displayed. Hover for status; right-click for status,
  **Show/Hide overlay**, or **Exit**. Double-click toggles visibility. Windows may
  initially place the icon under the tray's overflow arrow. Hiding the overlay
  keeps its reader running; Exit stops the application and removes the icon.
- Position is saved in `%LOCALAPPDATA%/STS2IncomingDamageOverlay/position.json`.
- No incoming attacks displays `Incoming: 0    After block: 0`. Missing/unsupported data shows a status
  instead of retaining an old damage prediction. Hold Ctrl to see status off-game.
- Submitting your turn keeps live calculations visible while the shared player
  turn continues. The waiting status starts when the game switches to the enemy turn.

## How data is read

1. Verify the game EXE and `sts2.dll` SHA-256 against the inspected build.
2. Create **one** Windows PSS snapshot per game process to discover static storage
   slots, field offsets, and loaded type identities. Dispose the snapshot and ClrMD
   runtime immediately after setup. A failed setup is not retried automatically.
3. Read the active CombatManager state and LocalContext net ID. Match the local
   player rather than assuming the first player is yours.
4. Follow `CombatManager.StateTracker.CombatStateChanged` event subscribers to
   find the current `NIntent` nodes. Match them to living enemies and current move
   intents by owner and intent identity. No heap scan is performed.
5. Read the existing native Godot RichTextLabel UTF-32 text at the verified offset
   `0xa78`. This is the game's calculated display value, including attack modifiers;
   the app does not invoke methods in the game. Parse single and multi-hit labels.
6. Read local HP, block, Osty HP, potion slots and character. Calculate the same
   basic after-block/after-Osty estimate and reminders as the mod.

After setup, small `ReadProcessMemory` calls follow the static slots afresh on
every refresh, including the boxed nullable local-player ID. Managed object
addresses are not cached across refreshes. Every read is rechecked before returning
a prediction; changed pointers, collections, or values discard that refresh. This
is best-effort consistency, not an atomic snapshot of a running game.

The polling path never snapshots, suspends, or walks the managed heap. Reads have
byte/count/time limits and a 250 ms delay between updates, including when the game
is unfocused or in the background. One overlay
instance is allowed at a time. The initial setup snapshot can still cause a brief
pause when connecting; steady-state reads do not use it. Readings older than four
seconds are hidden. No memory snapshots are written to disk.

## Compatibility and limitations

- Added the inspected v0.113.0 build (2026-11-12), including Fool identification
  and gold character text. The engine hash and native text layout are unchanged;
  combat fields, local-player lookup, intent events, and label generation were
  checked against the updated assembly. Unknown builds still stop at the hash check.
- The native text offset is build-specific. An EXE or game assembly update disables
  readings until the layout and field mappings are revalidated. Do not bypass the
  hash check merely to make an updated game attach.
- Validated live in a modded multiplayer session with local Regent identification,
  real intent text, HP and block. A normal-launch unmodded session still needs a
  live acceptance test. The reader itself never consumes our mod's data.
- Post-block damage is an estimate: special per-hit mitigation, block bypass,
  delayed damage, and other rules beyond simple subtraction are not simulated.
  Osty subtraction retains the original mod's approximation.
- Damage uses the local client's displayed enemy intents. Complex multiplayer
  targeting has the same display/estimation limitations; it is not a combat simulator.
- Runtime compatibility failures and partial/transitioning labels suppress the
  prediction. Fullscreen behavior, all characters and all enemy intent formats
  have not been exhaustively tested.
- Type metadata is captured at connection time. If a previously unloaded intent
  or potion type appears, the reader requests an overlay restart instead of
  automatically taking another snapshot during play. Connect after entering a run.
- Run at the same privilege level as the game. No administrator requirement is
  built into the application.

## Verification

Maintainers can create a self-contained Windows x64 ZIP and checksum with
`./Publish-Overlay.ps1 -Version 1.0.0` from the repository root. Output goes under
`artifacts/overlay-1.0.0/` and is excluded from Git.

```powershell
dotnet .\STS2IncomingDamageOverlay\bin\Release\net9.0-windows\STS2IncomingDamageOverlay.dll --self-test
dotnet .\STS2IncomingDamageOverlay\bin\Release\net9.0-windows\STS2IncomingDamageOverlay.dll --probe --output .\combat-reading.json
dotnet .\STS2IncomingDamageOverlay\bin\Release\net9.0-windows\STS2IncomingDamageOverlay.dll --benchmark --output .\reader-benchmark.json
```

The probe performs one read, prints JSON, optionally saves that JSON, and exits 0
only if a combat reading is available. The self-test checks multi-hit/BBCode parsing,
rejection of malformed labels, lethal boundaries, Osty subtraction, full block,
changed-pointer/collection rejection, and memory-read bounds. The benchmark takes
40 samples and records elapsed time, bytes read, snapshot count and returned state.
Exclude sample zero (connection setup) when comparing steady-state performance;
check returned states as well as timings. Snapshot count must remain one.

Live validation on 2026-11-01: 39 warm samples averaged 5.1 ms (15.3 ms maximum).
Combat reads transferred about 2 KB including consistency checks, and snapshot
count stayed at one. The test included three attacking enemies, changing block,
and turn transitions; transient reads were suppressed. The user confirmed smooth
gameplay with the replacement overlay running. The Release solution build and all
19 self-test checks passed.
Builds no longer install the original mod automatically. Reinstalling it requires
explicitly passing `-p:InstallMod=true` when building the mod project.

Live validation on 2026-11-14 with Fool on v0.113.0: all 40 readings connected;
visible attacks `1x3 + 5 + 7` matched incoming 15, with local HP 40. Warm reads
averaged 4.6 ms (7.9 ms maximum), with one setup snapshot. The standalone Release
build and all 19 self-test checks passed. This validates the existing damage
estimate for Fool; it does not add simulation of character-specific mechanics.
