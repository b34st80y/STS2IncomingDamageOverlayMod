using Godot;

namespace STS2IncomingDamageOverlayMod.Domain;

internal readonly record struct PlayerVitals(
    int Block,
    int CurrentHp,
    int OstyHp,
    bool HasDefensivePotion,
    Color CharacterColor);
