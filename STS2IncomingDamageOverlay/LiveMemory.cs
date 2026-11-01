using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Win32.SafeHandles;

namespace STS2IncomingDamageOverlay;

// ClrMD is used ONLY to discover metadata in one consistent startup snapshot.
// The polling path uses ReadProcessMemory; it never suspends or snapshots the game.
internal sealed class LiveMemory : IDisposable
{
    internal sealed record Field(int Offset, string Type);
    private readonly Dictionary<string, Dictionary<string, Field>> _layouts = new();
    private readonly Dictionary<ulong, string> _typeNames = new();
    private readonly HashSet<ulong> _attackTypes = new();
    private readonly SafeProcessHandle? _handle;
    private readonly Func<ulong, int, byte[]>? _testRead;
    private readonly List<(ulong Address, byte[] Bytes)> _reads = [];
    private readonly Stopwatch _budget = new();
    public ulong ManagerSlot { get; }
    public ulong LocalIdAddress { get; }
    public int LocalHasValueOffset { get; }
    public int LocalValueOffset { get; }
    public int BytesRead { get; private set; }

    internal LiveMemory(Func<ulong, int, byte[]> read) => _testRead = read;

    public LiveMemory(int pid, string dac)
    {
        _handle = OpenProcess(0x0010 | 0x1000, false, pid); // VM_READ + QUERY_LIMITED_INFORMATION
        if (_handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            using var snapshot = DataTarget.CreateSnapshotAndAttach(pid);
            using var runtime = snapshot.ClrVersions.Single().CreateRuntime(dac);
            var module = runtime.EnumerateModules().Single(m => Path.GetFileName(m.Name) == "sts2.dll");
            var core = runtime.EnumerateModules().Single(m => Path.GetFileName(m.Name) == "System.Private.CoreLib.dll");
            var domain = runtime.AppDomains.Single();
            ClrType Game(string name) => module.GetTypeByName("MegaCrit.Sts2.Core." + name)
                ?? throw new InvalidDataException("Waiting for combat…");
            void Register(ClrType type)
            {
                if (_layouts.ContainsKey(type.Name!)) return;
                _layouts[type.Name!] = type.Fields.Where(f => f.Name != null).ToDictionary(f => f.Name!, f => new Field(f.Offset + 8, f.Type?.Name ?? ""));
                // No object addresses are cached. Only immutable metadata and static slots.
            }
            var manager = Game("Combat.CombatManager");
            var local = Game("Context.LocalContext");
            ManagerSlot = manager.GetStaticFieldByName("<Instance>k__BackingField")!.GetAddress(domain);
            var localField = local.GetStaticFieldByName("<NetId>k__BackingField")!;
            LocalIdAddress = localField.GetAddress(domain);
            LocalHasValueOffset = localField.Type!.GetFieldByName("hasValue")!.Offset;
            LocalValueOffset = localField.Type.GetFieldByName("value")!.Offset;
            if (ManagerSlot == 0 || LocalIdAddress == 0) throw new InvalidDataException("Game statics are not initialized. Restart overlay after entering a run.");
            foreach (string name in new[] { "Combat.CombatManager", "Combat.CombatTurnState", "Combat.CombatState", "Combat.CombatStateTracker", "Entities.Creatures.Creature", "Entities.Players.Player", "Models.MonsterModel", "MonsterMoves.MonsterMoveStateMachine.MoveState", "Nodes.Combat.NIntent" }) Register(Game(name));
            Register(core.GetTypeByName("System.MulticastDelegate")!);
            var node = Game("Nodes.Combat.NIntent");
            Register(node.GetFieldByName("_valueLabel")!.Type!);
            // Reference-type List<T> layouts are taken from the game's actual declared fields.
            foreach (var (type, field) in new[] { (Game("Combat.CombatState"), "_allies"), (Game("Combat.CombatState"), "_enemies"), (Game("Entities.Players.Player"), "_potionSlots") }) Register(type.GetFieldByName(field)!.Type!);
            foreach (var (mt, _) in module.EnumerateTypeDefToMethodTableMap())
            {
                var type = runtime.GetTypeByMethodTable(mt);
                if (type?.Name == null) continue;
                _typeNames[mt] = type.Name;
                for (var ancestor = type; ancestor != null; ancestor = ancestor.BaseType)
                    if (ancestor.Name == "MegaCrit.Sts2.Core.MonsterMoves.Intents.AttackIntent") { _attackTypes.Add(mt); break; }
            }
        }
        catch { _handle.Dispose(); throw; }
    }

    public void Begin() { _reads.Clear(); BytesRead = 0; _budget.Restart(); }
    public byte[] Read(ulong address, int count)
    {
        if (address < 65536 || count < 1 || count > 4096 || _reads.Count > 2048 || BytesRead > 131072)
            throw new InvalidDataException("Read outside combat memory budget");
        if (_budget.ElapsedMilliseconds > 100) throw new InvalidDataException("Read exceeded time budget");
        byte[] bytes = Raw(address, count);
        _reads.Add((address, bytes)); BytesRead += count;
        return bytes;
    }
    private byte[] Raw(ulong address, int count)
    {
        if (_testRead != null) return _testRead(address, count);
        byte[] bytes = new byte[count];
        if (!ReadProcessMemory(_handle!, (nint)address, bytes, (nuint)count, out nuint read) || read != (nuint)count)
            throw new InvalidDataException("Game memory changed or became unavailable");
        return bytes;
    }
    public void Validate()
    {
        // Recheck leaves first, then roots. A GC move, changed collection, or freed native
        // label invalidates this read instead of leaving cached object pointers in use.
        for (int i = _reads.Count - 1; i >= 0; i--)
        {
            if (_budget.ElapsedMilliseconds > 100) throw new InvalidDataException("Read exceeded time budget");
            var (address, bytes) = _reads[i];
            if (!Raw(address, bytes.Length).AsSpan().SequenceEqual(bytes)) throw new InvalidDataException("Combat state changed during reading");
            BytesRead += bytes.Length;
        }
    }
    public ulong U64(ulong address) => BitConverter.ToUInt64(Read(address, 8));
    public int I32(ulong address) => BitConverter.ToInt32(Read(address, 4));
    public bool Bool(ulong address) => Read(address, 1)[0] != 0;
    public RemoteObject At(ulong address, string type) => new(this, address, type);
    public RemoteObject Root() => At(U64(ManagerSlot), "MegaCrit.Sts2.Core.Combat.CombatManager");
    public string TypeName(ulong address) => address == 0 ? "" : _typeNames.GetValueOrDefault(U64(address), "Unknown");
    public bool IsAttack(ulong address)
    {
        ulong mt = U64(address);
        if (!_typeNames.ContainsKey(mt)) throw new InvalidDataException("New intent type loaded — restart overlay to refresh metadata");
        return _attackTypes.Contains(mt);
    }
    public Field GetField(string type, string name)
    {
        if (!_layouts.TryGetValue(type, out var fields)) throw new InvalidDataException($"Unsupported layout: {type}");
        return fields.GetValueOrDefault(name) ?? fields.GetValueOrDefault($"<{name}>k__BackingField")
            ?? throw new InvalidDataException($"Unsupported field: {type}.{name}");
    }
    public RemoteObject[] Array(ulong address, string elementType, int? take = null)
    {
        int length = I32(address + 8); // .NET 9 x64 reference SZARRAY, pinned game runtime.
        int count = take ?? length;
        if (length < 0 || length > 4096 || count < 0 || count > length || count > 256) throw new InvalidDataException("Invalid reference array length");
        if (count == 0) return [];
        byte[] data = Read(address + 16, count * 8);
        return Enumerable.Range(0, count).Select(i => At(BitConverter.ToUInt64(data, i * 8), elementType)).ToArray();
    }
    public void Dispose() => _handle?.Dispose();
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(SafeProcessHandle process, nint address, [Out] byte[] buffer, nuint size, out nuint read);
}

internal readonly record struct RemoteObject(LiveMemory Memory, ulong Address, string Layout)
{
    public bool IsNull => Address == 0;
    public string TypeName => Memory.TypeName(Address);
    public RemoteObject As(string type) => Memory.At(Address, type);
    private ulong FieldAddress(string field) => IsNull ? throw new InvalidDataException("Missing combat object") : Address + (ulong)Memory.GetField(Layout, field).Offset;
    public RemoteObject Obj(string name)
    {
        if (IsNull) return default;
        var field = Memory.GetField(Layout, name);
        return Memory.At(Memory.U64(Address + (ulong)field.Offset), field.Type);
    }
    public int Int(string field) => Memory.I32(FieldAddress(field));
    public ulong Ulong(string field) => Memory.U64(FieldAddress(field));
    public bool Bool(string field) => Memory.Bool(FieldAddress(field));
    public RemoteObject[] List(string elementType) => Memory.Array(Obj("_items").Address, elementType, Int("_size")).Where(o => !o.IsNull).ToArray();
}
