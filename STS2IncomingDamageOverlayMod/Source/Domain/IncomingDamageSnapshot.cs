using Godot;

namespace STS2IncomingDamageOverlayMod.Domain;

internal readonly record struct IncomingDamageSnapshot(
    int Incoming,
    int AfterBlock,
    bool IsLethal,
    bool HasDefensivePotion,
    Color CharacterColor)
{
    public bool ShouldShow => Incoming > 0;
}
