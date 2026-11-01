using System.Text.Json;
using System.Windows;
namespace STS2IncomingDamageOverlay;
internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--benchmark"))
        {
            using var reader = new CombatMemoryReader();
            var samples = new List<object>();
            for (int i = 0; i < 40; i++)
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var result = reader.Read();
                samples.Add(new { Sample = i, Milliseconds = timer.Elapsed.TotalMilliseconds, Bytes = reader.LastBytesRead, Snapshots = reader.SnapshotCount, Result = result });
                Thread.Sleep(250);
            }
            string json = JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true });
            int output = Array.IndexOf(args, "--output");
            if (output >= 0 && output + 1 < args.Length) File.WriteAllText(args[output + 1], json);
            else Console.WriteLine(json);
            return reader.SnapshotCount == 1 ? 0 : 1;
        }
        if (args.Contains("--probe"))
        {
            using var reader = new CombatMemoryReader();
            var result = reader.Read();
            string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
            int output = Array.IndexOf(args, "--output");
            if (output >= 0 && output + 1 < args.Length) File.WriteAllText(args[output + 1], json);
            return result.Damage != null ? 0 : 1;
        }
        if (args.Contains("--self-test")) return SelfTests.Run();
        using var instance = new Mutex(true, "Local\\STS2IncomingDamageExternalOverlay", out bool first);
        if (!first) return 0;
        var app = new Application();
        app.Run(new OverlayWindow());
        return 0;
    }
}
