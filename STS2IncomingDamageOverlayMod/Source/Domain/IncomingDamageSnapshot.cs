using Godot;

namespace STS2IncomingDamageOverlayMod.Domain;

internal readonly record struct IncomingDamageSnapshot(
    int Incoming,
    int AfterBlock,
    int AfterOsty,
    bool IsLethal,
    bool HasDefensivePotion,
    Color CharacterColor)
{
    public bool ShouldShow => Incoming > 0;
}
