using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DEDEBUG;

// Replaces game-assembly input reads only. Unity UI and DEDEBUG retain real input.
[HarmonyPatch]
internal static class ConsoleGameInputIsolation
{
    internal static IEnumerable<MethodBase> Targets()
    {
        var module = typeof(Command.PlayerCommandHandler).Module;
        if (!string.Equals(module.ModuleVersionId.ToString(), GameInputManifest.ModuleId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("游戏程序集已变化，请重新构建 DEDEBUG | Game assembly changed; rebuild DEDEBUG");
        foreach (int token in GameInputManifest.Tokens) yield return module.ResolveMethod(token);
    }
    private static IEnumerable<MethodBase> TargetMethods() => Targets();
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.operand is MethodInfo original)
            {
                var replacement = GameInputGate.Replacement(original);
                if (replacement != null) instruction.operand = replacement;
            }
            yield return instruction;
        }
    }
}
