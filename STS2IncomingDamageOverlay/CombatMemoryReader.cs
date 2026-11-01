using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
namespace STS2IncomingDamageOverlay;

internal sealed class CombatMemoryReader : IDisposable
{
    private const string EngineHash = "8602C26BFFD2937E3841835FD8360EF8E974624A543E05977229FD3D062BE231";
    private const string AssemblyHash = "0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9";
    private const string Creature = "MegaCrit.Sts2.Core.Entities.Creatures.Creature";
    private const string IntentNode = "MegaCrit.Sts2.Core.Nodes.Combat.NIntent";
    private (int Id, long Start)? _identity;
    private LiveMemory? _memory;
    private string? _setupError;
    public int SnapshotCount { get; private set; }
    public int LastBytesRead => _memory?.BytesRead ?? 0;
    private static readonly HashSet<string> DefensivePotions = new(StringComparer.Ordinal)
    { "BlockPotion", "DexterityPotion", "Fortifier", "GhostInAJar", "LiquidBronze", "ShacklingPotion", "SkillPotion", "SpeedPotion", "WeakPotion" };

    public ReadResult Read()
    {
        var watch = Stopwatch.StartNew();
        var processes = Process.GetProcessesByName("SlayTheSpire2");
        int pid = 0;
        try
        {
            if (processes.Length != 1)
            {
                Dispose(); _identity = null;
                return new(processes.Length == 0 ? "Waiting for Slay the Spire 2" : "Multiple game instances — close extras");
            }
            var process = processes[0]; pid = process.Id;
            var identity = (pid, process.StartTime.ToUniversalTime().Ticks);
            if (_identity != identity)
            {
                Dispose(); _identity = identity; _setupError = null;
                try
                {
                    string exe = process.MainModule!.FileName;
                    string data = Path.Combine(Path.GetDirectoryName(exe)!, "data_sts2_windows_x86_64");
                    if (Hash(exe) != EngineHash || Hash(Path.Combine(data, "sts2.dll")) != AssemblyHash)
                        throw new InvalidDataException("Game update detected — reader needs a compatibility update");
                    SnapshotCount++;
                    _memory = new LiveMemory(pid, Path.Combine(data, "mscordaccore.dll"));
                }
                catch (Exception ex) { _setupError = ex.Message; }
                // Never retry failed setup in a polling loop: that would reintroduce stutter.
            }
            if (_memory == null) return new(_setupError ?? "Reader unavailable", ProcessId: pid);
            var m = _memory;
            m.Begin();
            var manager = m.Root();
            var turn = manager.Obj("_turnState");
            if (turn.IsNull || !turn.Bool("IsInProgress")) return new("Waiting for combat", ProcessId: pid);
            var state = turn.Obj("State");
            // Submitting locally can disable actions while teammates are still playing.
            // Keep reading live estimates until the shared player turn actually ends.
            if (state.Int("CurrentSide") != 1) return new("Waiting for your turn", ProcessId: pid);
            ulong localData = m.U64(m.LocalIdAddress) + 8; // Static Nullable<ulong> is boxed; follow its slot after every GC.
            if (!m.Bool(localData + (ulong)m.LocalHasValueOffset)) return new("Waiting for local player", ProcessId: pid);
            ulong netId = m.U64(localData + (ulong)m.LocalValueOffset);
            var allies = state.Obj("_allies").List(Creature);
            var local = allies.Single(c => !c.Obj("Player").IsNull && c.Obj("Player").Ulong("NetId") == netId);
            var player = local.Obj("Player");
            var enemies = state.Obj("_enemies").List(Creature).Where(c => c.Int("_currentHp") > 0).ToArray();
            var expected = new HashSet<(ulong Owner, ulong Intent)>();
            foreach (var enemy in enemies)
            {
                var move = enemy.Obj("Monster").Obj("NextMove");
                var intents = m.Array(move.Obj("Intents").Address, "MegaCrit.Sts2.Core.MonsterMoves.Intents.AbstractIntent");
                foreach (var intent in intents)
                    if (m.IsAttack(intent.Address)) expected.Add((enemy.Address, intent.Address));
            }
            var labels = new Dictionary<(ulong Owner, ulong Intent), string>();
            // NIntent subscribes OnCombatStateChanged on entry and unsubscribes on exit.
            // Follow that existing event's targets, not the whole managed heap.
            var handler = manager.Obj("StateTracker").Obj("CombatStateChanged");
            foreach (var node in IntentSubscribers(m, handler))
            {
                if (!IsLive(node)) continue;
                var key = (node.Obj("_owner").Address, node.Obj("_intent").Address);
                if (!expected.Contains(key)) continue;
                var label = node.Obj("_valueLabel");
                if (!IsLive(label)) continue;
                string text = ReadNativeText(m, label.Ulong("NativePtr"));
                if (labels.TryGetValue(key, out string? old) && old != text) throw new InvalidDataException("Intent visuals are transitioning");
                labels[key] = text;
            }
            if (labels.Count != expected.Count) throw new InvalidDataException("Waiting for all attack labels");
            int incoming = labels.Values.Sum(DamageSnapshot.ParseIntent);
            int hp = local.Int("_currentHp"), block = local.Int("_block");
            int osty = allies.Where(c => c.Obj("_petOwner").Address == player.Address && c.Obj("Monster").TypeName == "MegaCrit.Sts2.Core.Models.Monsters.Osty")
                .Sum(c => Math.Max(0, c.Int("_currentHp")));
            var potions = player.Obj("_potionSlots").List("MegaCrit.Sts2.Core.Models.PotionModel");
            string[] potionNames = potions.Select(p => p.TypeName.Split('.').Last()).ToArray();
            if (potionNames.Contains("Unknown")) throw new InvalidDataException("New potion type loaded — restart overlay to refresh metadata");
            bool potion = potionNames.Any(DefensivePotions.Contains);
            string character = player.Obj("Character").TypeName.Split('.').Last();
            if (hp < 0 || hp > 100000 || block < 0 || block > 100000) throw new InvalidDataException("Invalid player vitals");
            m.Validate();
            return new("Connected", new(incoming, block, hp, osty, potion, character, labels.Values.ToArray()), pid, watch.ElapsedMilliseconds);
        }
        catch (Exception ex) { return new($"Reading unavailable: {ex.Message}", ProcessId: pid, ReadMs: watch.ElapsedMilliseconds); }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    private static IEnumerable<RemoteObject> IntentSubscribers(LiveMemory m, RemoteObject handler)
    {
        if (handler.IsNull) yield break;
        var d = handler.As("System.MulticastDelegate");
        var array = d.Obj("_invocationList");
        ulong count = d.Ulong("_invocationCount");
        if (count > 256) throw new InvalidDataException("Unexpected event subscriber count");
        var delegates = array.IsNull ? new[] { d } : m.Array(array.Address, "System.MulticastDelegate", (int)count);
        foreach (var callback in delegates)
        {
            var target = callback.Obj("_target");
            if (!target.IsNull && target.TypeName == IntentNode) yield return target.As(IntentNode);
        }
    }
    private static bool IsLive(RemoteObject o) => !o.IsNull && !o.Bool("_disposed") && o.Ulong("NativePtr") != 0;
    private static string ReadNativeText(LiveMemory m, ulong label)
    {
        ulong address = m.U64(label + 0xa78);
        if (address < 65536) throw new InvalidDataException("Label text unavailable");
        ulong count = m.U64(address - 8);
        if (count < 2 || count > 256) throw new InvalidDataException("Invalid label length");
        byte[] text = m.Read(address, checked((int)count * 4));
        if (BitConverter.ToUInt32(text, text.Length - 4) != 0) throw new InvalidDataException("Incomplete label text");
        return new UTF32Encoding(false, false, true).GetString(text, 0, text.Length - 4);
    }
    private static string Hash(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
    public void Dispose() { _memory?.Dispose(); _memory = null; }
}
