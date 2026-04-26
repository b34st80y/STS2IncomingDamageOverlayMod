# Incoming Damage Overlay

Slay the Spire 2 quality-of-life mod that displays a small combat HUD:

```text
Incoming: 37    After block: 12
```

The overlay is read-only and does not modify cards, relics, enemies, RNG, saves, or combat behavior.

## Status

This is a first-pass mod scaffold for Slay the Spire 2 Early Access. STS2 internals are still moving, so the HUD reads combat state through reflection instead of depending on one exact private field layout. That makes it more tolerant of patches, but it still needs an in-game test pass against the current build.

## Build Requirements

- Slay the Spire 2 installed through Steam.
- .NET SDK compatible with the template target.
- Slay the Spire 2 mod template dependencies restored from NuGet.
- BaseLib installed in `Slay the Spire 2/mods`.

The template auto-detects the default Steam path:

```text
C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2
```

If your install is elsewhere, set `Sts2Path` in `Directory.Build.props`.

## Build

```powershell
dotnet restore
dotnet build -c Release
```

The template copies `STS2IncomingDamageOverlayMod.dll` and `STS2IncomingDamageOverlayMod.json` into:

```text
Slay the Spire 2/mods/STS2IncomingDamageOverlayMod/
```

## Install Manually

After building, copy these files into a folder named `STS2IncomingDamageOverlayMod` inside the game's `mods` folder:

- `STS2IncomingDamageOverlayMod.dll`
- `STS2IncomingDamageOverlayMod.json`

Launch Slay the Spire 2 and choose the modded launch option.

## Notes

- It currently sums visible enemy attack intents only.
- It estimates damage after current block as `incoming - block`.
- It does not yet model special damage rules that bypass block, thorns, intangible, delayed damage, or per-player multiplayer targeting.
