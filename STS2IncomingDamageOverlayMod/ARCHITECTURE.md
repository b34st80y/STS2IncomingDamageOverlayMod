# Architecture

The mod is organized as a small clean architecture stack. Game behavior is unchanged; the split is intended to make future STS2 API changes and HUD changes easier to isolate.

## Folders

- `Source/Application/` contains use-case orchestration and presentation-independent formatting.
- `Source/Domain/` contains immutable data models used by the overlay.
- `Source/Infrastructure/` contains Godot scene traversal, STS2 combat-state reading, reflection helpers, and config persistence.
- `Source/Presentation/` contains the Godot `CanvasLayer` HUD, input handling, drag behavior, and visual styling.
- `Source/MainFile.cs` is the mod composition root. It patches Harmony and attaches the HUD to the scene tree.

## Dependency Direction

Presentation depends on Application and Infrastructure for composition. Application depends only on Domain abstractions. Infrastructure implements Application contracts and maps STS2/Godot state into Domain snapshots.

```text
MainFile
  -> Presentation
      -> Application
          -> Domain
      -> Infrastructure
          -> Application contracts
          -> Domain
```

## Responsibilities

- `IncomingDamageHud` renders the overlay and handles edit-mode dragging.
- `IncomingDamageOverlayService` exposes the current incoming-damage snapshot to the HUD.
- `CombatDamageReader` reads typed STS2 intents first, then keeps the previous reflection and visible-label fallbacks.
- `OverlayTextFormatter` owns the display string for incoming damage, after-block damage, defensive potion reminders, and lethal state.
- `OverlayConfig` persists only the overlay position.

The reflection fallback remains in infrastructure because STS2 Early Access internals are expected to move.
