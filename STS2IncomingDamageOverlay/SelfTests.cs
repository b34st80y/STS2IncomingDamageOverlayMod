namespace STS2IncomingDamageOverlay;

internal static class SelfTests
{
    public static int Run()
    {
        foreach (var (text, expected) in new[] { ("13", 13), ("6 x 3", 18), ("[red]8[/red] × 2", 16), ("0", 0) })
            if (DamageSnapshot.ParseIntent(text) != expected) throw new Exception($"Wrong parse: {text}");
        foreach (string text in new[] { "", "12 poison 3", "4x0", "-5", "7 / 8", "9999999999999" })
        {
            try { DamageSnapshot.ParseIntent(text); }
            catch (InvalidDataException) { continue; }
            throw new Exception($"Accepted invalid intent: {text}");
        }
        var snapshot = new DamageSnapshot(30, 10, 15, 5, true, "Necrobinder", ["10x3"]);
        if (snapshot.AfterBlock != 20 || snapshot.AfterOsty != 15 || !snapshot.IsLethal || !snapshot.Text.Contains("Defensive Potion")) throw new Exception("Warning calculation failed");
        snapshot = snapshot with { Block = 40 };
        if (snapshot.AfterBlock != 0 || snapshot.AfterOsty != 0 || snapshot.IsLethal || snapshot.Text.Contains("Defensive Potion")) throw new Exception("Full block calculation failed");
        byte[] memory = new byte[1024];
        using var reader = new LiveMemory((address, count) => memory.AsSpan(checked((int)(address - 0x10000)), count).ToArray());
        BitConverter.GetBytes(123UL).CopyTo(memory, 0);
        reader.Begin();
        if (reader.U64(0x10000) != 123) throw new Exception("Raw field read failed");
        reader.Validate();
        reader.Begin(); reader.U64(0x10000);
        BitConverter.GetBytes(456UL).CopyTo(memory, 0);
        MustReject(reader.Validate, "Changed pointer was accepted");
        reader.Begin();
        MustReject(() => reader.Read(0, 8), "Null pointer was accepted");
        MustReject(() => reader.Read(0x10000, 5000), "Oversized read was accepted");
        BitConverter.GetBytes(2).CopyTo(memory, 8);
        BitConverter.GetBytes(0x20000UL).CopyTo(memory, 16);
        BitConverter.GetBytes(0x30000UL).CopyTo(memory, 24);
        reader.Begin();
        var array = reader.Array(0x10000, "Test");
        if (array.Length != 2 || array[0].Address != 0x20000 || array[1].Address != 0x30000) throw new Exception("Reference array read failed");
        MustReject(() => reader.Array(0x10000, "Test", 3), "Array overrun was accepted");
        reader.Begin(); reader.Array(0x10000, "Test");
        BitConverter.GetBytes(1).CopyTo(memory, 8);
        MustReject(reader.Validate, "Changed collection was accepted");
        Console.WriteLine("19 checks passed (including changed-pointer/collection rejection and read bounds).");
        return 0;
    }

    private static void MustReject(Action action, string message)
    {
        try { action(); } catch (InvalidDataException) { return; }
        throw new Exception(message);
    }
}
