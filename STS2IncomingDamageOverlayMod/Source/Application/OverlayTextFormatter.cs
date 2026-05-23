using STS2IncomingDamageOverlayMod.Domain;

namespace STS2IncomingDamageOverlayMod.Application;

internal static class OverlayTextFormatter
{
    public static string Format(IncomingDamageSnapshot snapshot)
    {
        int playerDamage = snapshot.AfterOsty;
        string ostyText = snapshot.AfterOsty < snapshot.AfterBlock
            ? $"    After Osty: {snapshot.AfterOsty}"
            : "";
        string reminder = playerDamage > 0 && snapshot.HasDefensivePotion
            ? "    Defensive Potion Available"
            : "";

        return snapshot.IsLethal
            ? $"Incoming: {snapshot.Incoming}    After block: {snapshot.AfterBlock}{ostyText}{reminder}    LETHAL"
            : $"Incoming: {snapshot.Incoming}    After block: {snapshot.AfterBlock}{ostyText}{reminder}";
    }
}
