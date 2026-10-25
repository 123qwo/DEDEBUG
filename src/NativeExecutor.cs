using System;
using System.Reflection;
using Combat;
using Command;
using HarmonyLib;
using UI;
using UnityEngine;
using World;

namespace DEDEBUG;

internal sealed partial class NativeExecutor : CommandActions
{
    private readonly DedebugPlugin _plugin;
    internal NativeExecutor(DedebugPlugin plugin) { _plugin = plugin; }
    internal void Execute(CommandCall call)
    {
        if (!call.Spec.Panel) RequireRun();
        if (call.Spec.Execute == null) throw new InvalidOperationException("指令未绑定执行器 | Command handler is missing");
        call.Spec.Execute(call);
    }
    private static void RequireRun()
    {
        if (LevelLoader.Instance == null || LevelLoader.Instance.GetCurrentLevelMode() != LevelLoader.CurrentLevelMode.InGame ||
            WorldManager.Instance == null || CampaignManager.Instance == null || PlayerCommandHandler.Instance == null || CardManager.Instance == null)
            throw new InvalidOperationException("请先进入一局游戏 | Enter a run first");
        if (PlayerCommandHandler.Instance.GetIsCommandBusy())
            throw new InvalidOperationException("原生命令正在执行，请稍后重试 | Native commands are busy; retry shortly");
    }
    internal override void Help(CommandCall call) => _plugin.Write(string.Join("\n", System.Linq.Enumerable.Select(_plugin.Engine.Commands, spec => spec.SuggestionLabel)));
    internal override void Clear(CommandCall call) => _plugin.ClearOutput();
    internal override void Close(CommandCall call) => _plugin.SetOpen(false);
    internal override void Refresh(CommandCall call) => _plugin.RefreshCatalog(true);
    internal override void Status(CommandCall call) => _plugin.Write("DEDEBUG " + DedebugPlugin.Version +
        "; inRun=" + (LevelLoader.Instance != null && LevelLoader.Instance.GetCurrentLevelMode() == LevelLoader.CurrentLevelMode.InGame && WorldManager.Instance != null) +
        "; busy=" + (PlayerCommandHandler.Instance != null && PlayerCommandHandler.Instance.GetIsCommandBusy()) +
        "; cards=" + _plugin.Catalog.Get(ArgumentKind.Card).Count + "; relics=" + _plugin.Catalog.Get(ArgumentKind.Relic).Count +
        "; catalogCached=" + !_plugin.Catalog.NeedsRefresh + "; inputIsolation=" + _plugin.InputReady);
    internal override void EnableDebugCheat(CommandCall call)
    {
            if (Test.DitwTest.Instance == null) throw new InvalidOperationException("原生调试组件不存在 | Native debug component unavailable");
            Test.DitwTest.Instance.isDebugCheatCommandActive = call.Argument == "on";
            _plugin.Write("原生调试快捷键 | Native debug keys: " + call.Argument);
    }
    internal override void InvokeNative(CommandCall call)
    {
        if (call.Spec.Name == "SetStoreRelic" && (Store.StoreManager.Instance == null ||
            WorldManager.Instance.GetCampaignNonBattleState() != CampaignNonBattleState.Store))
            throw new InvalidOperationException("此指令需要正在访问商店 | This command requires an active store");
        if (call.Spec.Name == "GainMoney" && MoneyManager.Instance != null &&
            (long)MoneyManager.Instance.GetMoney() + long.Parse(call.Argument) > int.MaxValue)
            throw new InvalidOperationException("金币数量会溢出 | Money amount would overflow");

        // Only the explicit command registry can select a native method. No arbitrary reflection invocation.
        if (string.IsNullOrEmpty(call.Spec.Native)) throw new InvalidOperationException("指令没有执行器 | Missing executor");
        var receiver = Utilities.LogManager.Instance != null
            ? AccessTools.Field(typeof(Utilities.LogManager), "debugRedeemCode").GetValue(Utilities.LogManager.Instance) as DebugRedeemCode : null;
        if (receiver == null) receiver = UnityEngine.Object.FindObjectOfType<DebugRedeemCode>(true);
        if (receiver == null) throw new InvalidOperationException("原生命令接收器尚未就绪 | Native command receiver unavailable");
        var method = typeof(DebugRedeemCode).GetMethod(call.Spec.Native, BindingFlags.Instance | BindingFlags.Public,
            null, new[] { typeof(string) }, null);
        if (method == null) throw new MissingMethodException("Native command signature changed: " + call.Spec.Native);
        try { method.Invoke(receiver, new object[] { call.Spec.Kind == ArgumentKind.Confirm ? "" : call.Argument }); }
        catch (TargetInvocationException e) { throw e.InnerException ?? e; }
        _plugin.Write("已调用原生命令 | Native command invoked: " + call.Canonical);
    }
}
