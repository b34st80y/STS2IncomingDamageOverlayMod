# AGENTS.md

## Project

This repository contains a Slay the Spire 2 Godot/.NET mod. The solution is at the repo root, while the mod project lives in `STS2IncomingDamageOverlayMod/`.

## Layout

- `STS2IncomingDamageOverlayMod.sln` - solution file.
- `STS2IncomingDamageOverlayMod/STS2IncomingDamageOverlayMod.csproj` - Godot .NET project.
- `STS2IncomingDamageOverlayMod/Source/` - C# mod source.
- `STS2IncomingDamageOverlayMod/Assets/` - Godot/mod assets.
- `STS2IncomingDamageOverlayMod/STS2IncomingDamageOverlayMod.json` - mod manifest.

## Build

Run commands from the repository root unless noted otherwise.

```powershell
dotnet restore .\STS2IncomingDamageOverlayMod.sln
dotnet build .\STS2IncomingDamageOverlayMod.sln -c Release
```

The build expects Slay the Spire 2 dependency paths to resolve through the project props. If the game is not installed at the default Steam path, configure the local props file as described in the project README.
