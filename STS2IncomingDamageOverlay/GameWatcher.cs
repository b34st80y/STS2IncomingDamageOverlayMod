using System.Diagnostics;

namespace STS2IncomingDamageOverlay;

// This idle process only checks process names. It never attaches to game memory.
internal static class GameWatcher
{
    public static int Run()
    {
        using var instance = new Mutex(true, "Local\\STS2IncomingDamageOverlayWatcher", out bool first);
        if (!first) return 0;
        (int Id, long Start)? launched = null;
        while (true)
        {
            var games = Process.GetProcessesByName("SlayTheSpire2");
            try
            {
                if (games.Length == 0) launched = null;
                if (games.Length == 1)
                {
                    var game = games[0];
                    var identity = (game.Id, game.StartTime.ToUniversalTime().Ticks);
                    if (launched != identity)
                    {
                        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
                        start.ArgumentList.Add("--game-pid");
                        start.ArgumentList.Add(identity.Id.ToString());
                        start.ArgumentList.Add(identity.Ticks.ToString());
                        using var overlay = Process.Start(start);
                        if (overlay != null) launched = identity;
                    }
                }
            }
            catch (InvalidOperationException) { /* Game exited during discovery. */ }
            catch (System.ComponentModel.Win32Exception) { /* Retry discovery next tick. */ }
            finally { foreach (var game in games) game.Dispose(); }
            // Exiting the overlay manually is respected until the next game session.
            Thread.Sleep(2000);
        }
    }

    internal static bool IsRunning(int pid, long start)
    {
        try
        {
            using var game = Process.GetProcessById(pid);
            return !game.HasExited && game.StartTime.ToUniversalTime().Ticks == start;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return true; }
    }
}
