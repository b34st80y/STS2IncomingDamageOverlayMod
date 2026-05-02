using Godot;

namespace STS2IncomingDamageOverlayMod.Domain;

internal readonly record struct PlayerVitals(
    int Block,
    int CurrentHp,
    bool HasDefensivePotion,
    Color CharacterColor);
