# STS2 Incoming Damage Overlay

Standalone overlays are available for Windows and macOS. Both can start during an existing run. The original in-game mod is retained for reference.

| Version | Status | Installation |
| --- | --- | --- |
| [Standalone Windows overlay](STS2IncomingDamageOverlay/README.md) | Current and actively supported | Separate Windows app; no mod installation |
| [Standalone Mac overlay](macOS/README.md) | Tested on Apple silicon with STS2 v0.113.1 | Menu-bar app that reads the visible game window |
| Original in-game mod | Legacy | Installed through the game's mod loader; counts as a modded game |

## Standalone Mac overlay

Run `./macOS/build.sh`, then open `artifacts/macOS/STS2 Damage Overlay.app`.
Allow Screen Recording when macOS asks. The click-through panel shows the sum
of visible attack labels and an after-block estimate when it can identify the
player’s health bar. Use the shield in the menu bar to move, hide, or quit the
overlay. The [Mac guide](macOS/README.md) covers build prerequisites and recognition limits.

## Standalone Windows overlay

### Download and run (no build required)

Download **[STS2-Incoming-Damage-Overlay-win-x64.zip](https://github.com/b34st80y/STS2IncomingDamageOverlayMod/releases/latest/download/STS2-Incoming-Damage-Overlay-win-x64.zip)**
from the [latest release](https://github.com/b34st80y/STS2IncomingDamageOverlayMod/releases/latest).
Extract the **entire ZIP**, then double-click `STS2IncomingDamageOverlay.exe`.
The Windows x64 package includes the .NET runtime; no SDK or separate runtime install is needed.
Keep all extracted files together. For automatic launch with the game, double-click
`Enable-AutoStart.cmd` in the extracted folder; use `Disable-AutoStart.cmd` to turn it off.

Check the release notes for the supported game build. The standalone overlay checks
game-file versions and may need an update after STS2 updates.

### Build from source (optional)

The desktop application lives in `STS2IncomingDamageOverlay/`.
It reads the game's memory and displays estimated incoming damage in its own transparent window.
It does not load through the mod loader, install game files, or require BaseLib.

Build the standalone project directly:

```powershell
dotnet build .\STS2IncomingDamageOverlay\STS2IncomingDamageOverlay.csproj -c Release
& .\STS2IncomingDamageOverlay\bin\Release\net9.0-windows\STS2IncomingDamageOverlay.exe
```

Launch the game normally to use it without mods. Hold **Ctrl + left-drag** to move
the overlay. Use the orange shield tray icon's **Exit** menu to close it.

To open and close the overlay automatically with the game, run this once after building:

```powershell
.\STS2IncomingDamageOverlay\Set-AutoStart.ps1
```

See the [standalone guide](STS2IncomingDamageOverlay/README.md) for supported game
builds, automatic startup, diagnostics, and calculation limitations.

## Legacy: original in-game mod

**Everything below applies only to the legacy mod.** These instructions are kept
for existing mod users and reference. Current overlay compatibility updates do not
imply that the legacy mod has been updated or tested on the same game build.

A small quality-of-life mod for **Slay the Spire 2** that shows estimated incoming enemy damage during combat.

```text
Incoming: 37    After block: 12
```

The overlay is read-only. It does not change cards, relics, enemies, RNG, saves, or combat behavior.

### What the legacy mod shows

- Total incoming attack damage from visible enemy intents.
- Estimated damage after your current block.
- A lethal warning when the incoming damage after block would kill the local player.
- A defensive-potion reminder when damage is getting through and a defensive or weak-related potion appears to be available.
- Character-colored overlay text when the local player character can be identified.

### Legacy status and limitations

Development and support now focus on the standalone overlay. The legacy mod prefers typed STS2 intent data when available and falls back to reflection and visible intent labels when needed.

Known limitations:

- Damage is an estimate based on enemy attack intents and current block.
- It does not fully model special rules such as block bypass, intangible, thorns, delayed damage, or other custom damage modifiers.
- Multiplayer/local-player targeting is handled best-effort.
- It should be tested against the current Early Access build after game updates.

### Legacy mod usage

During combat, the overlay appears only when incoming damage is detected.

Hold `Ctrl` to enter edit mode, then drag the overlay to reposition it. The position is saved to `config.json` next to the built mod DLL.

### Legacy mod build requirements

- Slay the Spire 2 installed through Steam.
- A compatible .NET SDK for the project target framework.
- Slay the Spire 2 mod template dependencies restored from NuGet.
- BaseLib installed in `Slay the Spire 2/mods`.

The project attempts to find the default Steam install path:

```text
C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2
```

If your install is elsewhere, configure `Sts2Path` as described by the project props/template setup.

### Legacy mod build

From the repository root:

```powershell
dotnet restore .\STS2IncomingDamageOverlayMod.sln
dotnet build .\STS2IncomingDamageOverlayMod.sln -c Release
```

Builds do not install the original mod. To explicitly reinstall it, build with
`-p:InstallMod=true`; otherwise the game’s mods folder is left untouched.

### Legacy mod manual installation

After building, copy these files into a folder named `STS2IncomingDamageOverlayMod` inside the game's `mods` folder:

- `STS2IncomingDamageOverlayMod.dll`
- `STS2IncomingDamageOverlayMod.json`

Then launch Slay the Spire 2 using the modded launch option.

### Legacy mod project layout

```text
STS2IncomingDamageOverlayMod.sln
STS2IncomingDamageOverlayMod/
  STS2IncomingDamageOverlayMod.csproj
  STS2IncomingDamageOverlayMod.json
  Source/
    Presentation/
      IncomingDamageHud.cs
```

The legacy HUD logic lives in `STS2IncomingDamageOverlayMod/Source/Presentation/IncomingDamageHud.cs`.

### Legacy mod behavior

This mod is intended to be informational only. The manifest declares `affects_gameplay: false` because it displays combat information without mutating game state.
