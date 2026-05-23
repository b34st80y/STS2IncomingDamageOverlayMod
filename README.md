# STS2 Incoming Damage Overlay Mod

A small quality-of-life mod for **Slay the Spire 2** that shows estimated incoming enemy damage during combat.

```text
Incoming: 37    After block: 12
```

The overlay is read-only. It does not change cards, relics, enemies, RNG, saves, or combat behavior.

## What It Shows

- Total incoming attack damage from visible enemy intents.
- Estimated damage after your current block.
- A lethal warning when the incoming damage after block would kill the local player.
- A defensive-potion reminder when damage is getting through and a defensive or weak-related potion appears to be available.
- Character-colored overlay text when the local player character can be identified.

## Current Status

This is an early Slay the Spire 2 mod and the game internals are still moving. The mod prefers typed STS2 intent data when available and falls back to reflection and visible intent labels when needed.

Known limitations:

- Damage is an estimate based on enemy attack intents and current block.
- It does not fully model special rules such as block bypass, intangible, thorns, delayed damage, or other custom damage modifiers.
- Multiplayer/local-player targeting is handled best-effort.
- It should be tested against the current Early Access build after game updates.

## Usage

During combat, the overlay appears only when incoming damage is detected.

Hold `Ctrl` to enter edit mode, then drag the overlay to reposition it. The position is saved to `config.json` next to the built mod DLL.

## Build Requirements

- Slay the Spire 2 installed through Steam.
- A compatible .NET SDK for the project target framework.
- Slay the Spire 2 mod template dependencies restored from NuGet.
- BaseLib installed in `Slay the Spire 2/mods`.

The project attempts to find the default Steam install path:

```text
C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2
```

If your install is elsewhere, configure `Sts2Path` as described by the project props/template setup.

## Build

From the repository root:

```powershell
dotnet restore .\STS2IncomingDamageOverlayMod.sln
dotnet build .\STS2IncomingDamageOverlayMod.sln -c Release
```

The build copies the mod DLL and manifest into the Slay the Spire 2 mods folder when the game paths resolve correctly.

## Manual Install

After building, copy these files into a folder named `STS2IncomingDamageOverlayMod` inside the game's `mods` folder:

- `STS2IncomingDamageOverlayMod.dll`
- `STS2IncomingDamageOverlayMod.json`

Then launch Slay the Spire 2 using the modded launch option.

## Project Layout

```text
STS2IncomingDamageOverlayMod.sln
STS2IncomingDamageOverlayMod/
  STS2IncomingDamageOverlayMod.csproj
  STS2IncomingDamageOverlayMod.json
  Source/
    MainFile.cs
    IncomingDamageHud.cs
    OverlayConfig.cs
```

The main HUD logic lives in `STS2IncomingDamageOverlayMod/Source/IncomingDamageHud.cs`.

## Safety

This mod is intended to be informational only. The manifest declares `affects_gameplay: false` because it displays combat information without mutating game state.
