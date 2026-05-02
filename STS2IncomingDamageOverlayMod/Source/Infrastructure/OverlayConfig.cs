using System.Text.Json;
using Godot;

namespace STS2IncomingDamageOverlayMod.Infrastructure;

internal sealed class OverlayConfig
{
    public float X { get; set; } = 34;
    public float Y { get; set; } = 160;

    public static string ConfigPath
    {
        get
        {
            string? assemblyDir = Path.GetDirectoryName(typeof(OverlayConfig).Assembly.Location);
            return Path.Combine(assemblyDir ?? AppContext.BaseDirectory, "config.json");
        }
    }

    public static OverlayConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                OverlayConfig created = new();
                created.Save();
                return created;
            }

            string json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<OverlayConfig>(json) ?? new OverlayConfig();
        }
        catch
        {
            return new OverlayConfig();
        }
    }

    public Vector2 Position => new(X, Y);

    public void SetPosition(Vector2 position)
    {
        X = MathF.Round(position.X);
        Y = MathF.Round(position.Y);
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // Position persistence is nice to have; overlay should still work if saving fails.
        }
    }
}
