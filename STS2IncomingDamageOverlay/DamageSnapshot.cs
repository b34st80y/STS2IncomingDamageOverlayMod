using System.Text.RegularExpressions;

namespace STS2IncomingDamageOverlay;

internal sealed record DamageSnapshot(int Incoming, int Block, int Hp, int OstyHp,
    bool HasDefensivePotion, string Character, string[] Intents)
{
    public int AfterBlock => Math.Max(0, Incoming - Block);
    public int AfterOsty => Math.Max(0, AfterBlock - OstyHp);
    public bool IsLethal => Hp > 0 && AfterOsty >= Hp;
    public string Text => $"Incoming: {Incoming}    After block: {AfterBlock}"
        + (AfterOsty < AfterBlock ? $"    After Osty: {AfterOsty}" : "")
        + (AfterOsty > 0 && HasDefensivePotion ? "    Defensive Potion Available" : "")
        + (IsLethal ? "    LETHAL" : "");

    public static int ParseIntent(string text)
    {
        // Strip BBCode only; never concatenate arbitrary digits from unknown text.
        text = Regex.Replace(text, @"\[[^\]]*\]", "");
        var match = Regex.Match(text, @"^\s*(\d{1,5})(?:\s*[x×*]\s*(\d{1,3}))?\s*$");
        if (!match.Success) throw new InvalidDataException($"Unrecognized attack label: {text}");
        int damage = int.Parse(match.Groups[1].Value);
        int hits = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 1;
        if (hits < 1) throw new InvalidDataException("Invalid attack count.");
        return checked(damage * hits);
    }
}

internal sealed record ReadResult(string Status, DamageSnapshot? Damage = null, int ProcessId = 0, long ReadMs = 0);
