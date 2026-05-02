using Godot;
using STS2IncomingDamageOverlayMod.Domain;

namespace STS2IncomingDamageOverlayMod.Application;

internal interface ICombatDamageReader
{
    IncomingDamageSnapshot Read(Node sceneRoot);
}
