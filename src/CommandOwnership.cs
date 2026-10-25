using System.Collections.Generic;

namespace DEDEBUG;

// Reusable bookkeeping; snapshot polling keeps the same per-frame completion semantics.
internal sealed class CommandOwnership
{
    private readonly CommandOutput _output;
    private readonly Dictionary<object, OutputEntry> _owners = new Dictionary<object, OutputEntry>();
    private readonly Dictionary<OutputEntry, int> _pendingCounts = new Dictionary<OutputEntry, int>();
    private readonly HashSet<object> _live = new HashSet<object>();
    private readonly List<object> _removed = new List<object>();
    internal int Count => _owners.Count;
    internal CommandOwnership(CommandOutput output) { _output = output; }
    internal OutputEntry Owner(object command)
        => command != null && _owners.TryGetValue(command, out var entry) && _output.Contains(entry) ? entry : null;
    internal void Track(object command, OutputEntry entry)
    {
        if (command == null || !_output.Contains(entry)) return;
        if (_owners.TryGetValue(command, out var previous))
        {
            if (ReferenceEquals(previous, entry)) return;
            Release(previous);
        }
        _owners[command] = entry;
        _pendingCounts.TryGetValue(entry, out int count); _pendingCounts[entry] = count + 1;
        if (!entry.NativePending) { entry.NativePending = true; _output.Touch(); }
    }
    internal void BeginSnapshot() { _live.Clear(); }
    internal void MarkLive(object command) { if (command != null && _owners.ContainsKey(command)) _live.Add(command); }
    internal void EndSnapshot()
    {
        _removed.Clear();
        foreach (var pair in _owners)
            if (!_live.Contains(pair.Key) || !_output.Contains(pair.Value)) _removed.Add(pair.Key);
        foreach (object command in _removed) { var entry = _owners[command]; _owners.Remove(command); Release(entry); }
        _removed.Clear(); _live.Clear();
    }
    private void Release(OutputEntry entry)
    {
        int count = _pendingCounts[entry] - 1;
        if (count > 0) { _pendingCounts[entry] = count; return; }
        _pendingCounts.Remove(entry);
        if (entry.NativePending) { entry.NativePending = false; if (_output.Contains(entry)) _output.Touch(); }
    }
    internal void Clear()
    {
        bool changed = false;
        foreach (var pair in _pendingCounts)
        {
            pair.Key.NativePending = false;
            changed |= _output.Contains(pair.Key);
        }
        _owners.Clear(); _pendingCounts.Clear(); _removed.Clear(); _live.Clear();
        if (changed) _output.Touch();
    }
}
