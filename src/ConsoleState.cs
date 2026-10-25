using System;
using System.Collections.Generic;

namespace DEDEBUG;

internal sealed class ConsoleHistory
{
    private readonly List<string> _items = new List<string>();
    private readonly int _capacity;
    private int _index;
    private string _draft = "";
    internal bool HasEntries => _items.Count > 0;
    internal ConsoleHistory(int capacity = 100) { _capacity = Math.Max(1, capacity); }
    internal void Record(string command)
    {
        if (_items.Count == 0 || _items[_items.Count - 1] != command) _items.Add(command);
        while (_items.Count > _capacity) _items.RemoveAt(0);
        _index = _items.Count; _draft = "";
    }
    internal string Move(int direction, string current)
    {
        if (_items.Count == 0) return current;
        if (_index == _items.Count) _draft = current;
        _index = Math.Max(0, Math.Min(_items.Count, _index + direction));
        return _index < _items.Count ? _items[_index] : _draft;
    }
}

internal sealed class CatalogGeneration
{
    private int _generation, _loaded = -1;
    internal bool Dirty => _generation != _loaded;
    internal void Invalidate() { _generation++; }
    internal int Begin() => _generation;
    internal void Commit(int generation) { _loaded = generation; }
}
