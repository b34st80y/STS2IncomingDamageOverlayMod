using STS2IncomingDamageOverlayMod.Domain;

namespace STS2IncomingDamageOverlayMod.Application;

internal static class OverlayTextFormatter
{
    public static string Format(IncomingDamageSnapshot snapshot)
    {
        string reminder = snapshot.AfterBlock > 0 && snapshot.HasDefensivePotion
            ? "    Defensive Potion Available"
            : "";

        return snapshot.IsLethal
            ? $"Incoming: {snapshot.Incoming}    After block: {snapshot.AfterBlock}{reminder}    LETHAL"
            : $"Incoming: {snapshot.Incoming}    After block: {snapshot.AfterBlock}{reminder}";
    }
}
