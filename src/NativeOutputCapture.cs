using System;
using System.Collections.Generic;
using System.Reflection;
using Command;
using HarmonyLib;
using UnityEngine;

namespace DEDEBUG;

// Keeps only output attributable to console submissions, rather than general game logs.
internal sealed class NativeOutputCapture
{
    private readonly CommandOutput _output;
    private readonly CommandOwnership _ownership;
    private static readonly FieldInfo SubQueue = AccessTools.Field(typeof(PlayerCommandHandler), "_subCommandsQueue");
    internal OutputEntry Direct;
    internal bool Enabled;
    private readonly Action<string> _notify;
    internal NativeOutputCapture(CommandOutput output, Action<string> notify) { _output = output; _notify = notify; _ownership = new CommandOwnership(output); }
    internal void Disable()
    {
        Enabled = false;
        _ownership.Clear();
    }
    internal OutputEntry Current()
    {
        if (_output.Contains(Direct)) return Direct;
        if (!Enabled || _ownership.Count == 0) return null;
        var handler = PlayerCommandHandler.Instance;
        if (handler == null) return null;
        var sub = handler.GetCurrentSubCommand();
        return _ownership.Owner(sub.Item2) ?? _ownership.Owner(handler.GetCurrentCommand().Item2);
    }
    internal void Track(object command)
    {
        if (!Enabled) return;
        var entry = Current();
        if (entry == null || command == null) return;
        _ownership.Track(command, entry);
    }
    internal void Helper(string text) { if (Current() != null) _notify(text); }
    internal void UnityLog(string text, string trace, LogType type)
    {
        var entry = Current();
        if (entry == null) return;
        bool error = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
        if (error) _notify(text);
        else _output.Add(entry, text, native: true);
    }
    internal void Tick()
    {
        if (_ownership.Count == 0) return;
        _ownership.BeginSnapshot();
        var handler = PlayerCommandHandler.Instance;
        if (handler != null)
        {
            foreach (var command in handler.CommandQueue) _ownership.MarkLive(command);
            if (SubQueue?.GetValue(handler) is Queue<SubCommand> queue)
                foreach (var command in queue) _ownership.MarkLive(command);
            _ownership.MarkLive(handler.GetCurrentCommand().Item2);
            _ownership.MarkLive(handler.GetCurrentSubCommand().Item2);
        }
        _ownership.EndSnapshot();
    }
}

[HarmonyPatch]
internal static class ConsoleCommandOwnership
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "AddCommand", "InsertCommand", "EnqueueSubCommand" })
            yield return AccessTools.Method(typeof(PlayerCommandHandler), name);
    }
    [HarmonyPrefix] private static void Prefix(object __0) => DedebugPlugin.Instance?.Capture?.Track(__0);
}
[HarmonyPatch(typeof(World.HelperManager), "ShowHelperContent", new[] { typeof(string) })]
internal static class ConsoleNativeHelperOutput
{
    [HarmonyPostfix] private static void Postfix(string __0) => DedebugPlugin.Instance?.Capture?.Helper(__0);
}
