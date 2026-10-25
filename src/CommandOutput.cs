using System;
using System.Collections.Generic;
using System.Text;

namespace DEDEBUG;

internal sealed class OutputEntry
{
    internal readonly string Command;
    internal readonly List<string> Native = new List<string>();
    internal readonly List<string> Fallback = new List<string>();
    internal int StoredCharacters;
    internal bool Submitted, NativePending, CatalogPending;
    private int _contentRevision, _renderedRevision = -1;
    private string _renderedStatus, _rendered;
    internal void ContentChanged() { _contentRevision++; }
    internal void DropRenderCache() { _rendered = null; }
    internal OutputEntry(string command) { Command = command; }
    internal string Status => !Submitted || NativePending || CatalogPending ? "Pending" : "Complete";
    internal string Render()
    {
        string status = Status;
        if (_rendered != null && _renderedRevision == _contentRevision && _renderedStatus == status) return _rendered;
        var text = new StringBuilder("> " + Command + "\n");
        var lines = Native.Count > 0 ? Native : Fallback;
        foreach (string line in lines) text.AppendLine(line);
        if (lines.Count == 0) text.AppendLine(status == "Pending" ? "Waiting for command processing." : "Command processed.");
        text.Append('[').Append(status).AppendLine("]");
        _renderedRevision = _contentRevision; _renderedStatus = status;
        return _rendered = text.ToString();
    }
}

internal sealed class CommandOutput
{
    private readonly List<OutputEntry> _entries = new List<OutputEntry>();
    private readonly HashSet<OutputEntry> _membership = new HashSet<OutputEntry>();
    private readonly StringBuilder _renderBuffer = new StringBuilder(4096);
    internal const int RenderLimit = 120000;
    private int _renderedVersion = -1;
    private string _rendered = "";
    private readonly int _capacity;
    internal int Version { get; private set; }
    internal int Count => _entries.Count;
    internal CommandOutput(int capacity = 80) { _capacity = Math.Max(1, capacity); }
    internal bool Contains(OutputEntry entry) => entry != null && _membership.Contains(entry);
    internal OutputEntry Begin(string command)
    {
        var entry = new OutputEntry(Trim(command ?? "", 2000));
        _entries.Add(entry); _membership.Add(entry);
        while (_entries.Count > _capacity) { _membership.Remove(_entries[0]); _entries.RemoveAt(0); }
        Touch(); return entry;
    }
    internal void Add(OutputEntry entry, string text, bool native = false)
    {
        if (!Contains(entry)) return;
        var lines = native ? entry.Native : entry.Fallback;
        if (string.IsNullOrWhiteSpace(text) || lines.Count >= 64 || entry.StoredCharacters >= 32000) return;
        string value = Trim(text, Math.Min(16000, 32000 - entry.StoredCharacters));
        lines.Add(value); entry.StoredCharacters += value.Length;
        if (native || entry.Native.Count == 0) { entry.ContentChanged(); Touch(); }
    }
    internal void Clear(OutputEntry keep)
    {
        _entries.Clear(); _membership.Clear();
        if (keep != null) { _entries.Add(keep); _membership.Add(keep); }
        Touch();
    }
    internal void Touch() { Version++; }
    internal string Render()
    {
        if (_renderedVersion == Version) return _rendered;
        int start = _entries.Count, skip = 0, remaining = RenderLimit;
        bool truncated = false;
        string newline = Environment.NewLine;
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            int length = _entries[i].Render().Length + newline.Length;
            start = i;
            if (length >= remaining) { skip = length - remaining; truncated = skip > 0 || i > 0; break; }
            remaining -= length;
        }
        _renderBuffer.Clear();
        if (truncated) _renderBuffer.Append("[Older output omitted]\n");
        for (int i = start; i < _entries.Count; i++)
        {
            string part = _entries[i].Render();
            int offset = i == start ? skip : 0;
            if (offset < part.Length) { _renderBuffer.Append(part, offset, part.Length - offset); _renderBuffer.Append(newline); }
            else { int newlineOffset = offset - part.Length; _renderBuffer.Append(newline, newlineOffset, newline.Length - newlineOffset); }
        }
        for (int i = 0; i < start; i++) _entries[i].DropRenderCache();
        _renderedVersion = Version;
        return _rendered = _renderBuffer.ToString();
    }
    private static string Trim(string text, int maximum) => text.Length <= maximum ? text : text.Substring(0, maximum) + "\n[Output truncated]";
}
