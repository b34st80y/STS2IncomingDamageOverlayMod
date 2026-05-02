using Godot;
using STS2IncomingDamageOverlayMod.Domain;

namespace STS2IncomingDamageOverlayMod.Application;

internal sealed class IncomingDamageOverlayService
{
    private readonly ICombatDamageReader _damageReader;

    public IncomingDamageOverlayService(ICombatDamageReader damageReader)
    {
        _damageReader = damageReader;
    }

    public IncomingDamageSnapshot GetSnapshot(Node sceneRoot)
    {
        return _damageReader.Read(sceneRoot);
    }
}
