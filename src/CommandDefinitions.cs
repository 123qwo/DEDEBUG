using System;

namespace DEDEBUG;

internal static class CommandDefinitions
{
    internal static CommandSpec[] Create(CommandActions h) => new[]
    {
        new CommandSpec("help", "查看指令帮助", "Show command help", execute: c => h?.Help(c), panel: true),
        new CommandSpec("clear", "清空指令输出", "Clear command output", execute: c => h?.Clear(c), panel: true),
        new CommandSpec("status", "查看游戏与目录状态", "Show game and catalog status", execute: c => h?.Status(c), panel: true),
        new CommandSpec("refresh", "刷新参数与双语名称", "Refresh argument catalogs", execute: c => h?.Refresh(c), panel: true),
        new CommandSpec("close", "关闭控制台", "Close console", execute: c => h?.Close(c), panel: true),
        new CommandSpec("GainMoney", "增加金币", "Gain money", ArgumentKind.Number, true, "GainMoney", execute: c => h?.InvokeNative(c)),
        new CommandSpec("GainCard", "获得指定卡牌", "Gain a card", ArgumentKind.Card, true, "GainCard", execute: c => h?.InvokeNative(c)),
        new CommandSpec("GainRelic", "获得指定遗物", "Gain a relic", ArgumentKind.Relic, true, "GainRelic", execute: c => h?.InvokeNative(c)),
        new CommandSpec("NodeStory", "进入指定原生事件", "Enter a native story", ArgumentKind.Story, true, "NodeStory", execute: c => h?.InvokeNative(c)),
        new CommandSpec("NodeEnemy", "进入指定敌人遭遇", "Enter an encounter", ArgumentKind.Enemy, true, "NodeEnemy", execute: c => h?.InvokeNative(c)),
        new CommandSpec("GainCardPool", "获得指定卡池的卡牌", "Gain a card pool", ArgumentKind.Pool, true, "GainCardPool", execute: c => h?.InvokeNative(c)),
        new CommandSpec("GainAllCardInGame", "获得全部卡牌（普通或升级）", "Gain all cards (Basic or Upgraded)", ArgumentKind.UpgradeMode, false, "GainAllCardInGame", execute: c => h?.InvokeNative(c)),
        new CommandSpec("GainAllRelic", "获得全部遗物", "Gain all relics", native: "GainAllRelic", execute: c => h?.InvokeNative(c)),
        new CommandSpec("GainAllAbilityByAbilityType", "获得该类型全部能力", "Gain all abilities of a type", ArgumentKind.AbilityType, true, "GainAllAbilityByAbilityType", execute: c => h?.InvokeNative(c)),
        new CommandSpec("SetStoreRelic", "替换商店首个遗物", "Replace the first store relic", ArgumentKind.Relic, true, "SetStoreRelic", execute: c => h?.InvokeNative(c)),
        new CommandSpec("EnableDebugCheat", "开关原生调试快捷键", "Toggle native debug keys", ArgumentKind.Toggle, execute: c => h?.EnableDebugCheat(c)),
        new CommandSpec("HealPlayer", "回复指定数量生命", "Heal the player", ArgumentKind.Number, true, min: 1, execute: c => h?.HealPlayer(c)),
        new CommandSpec("FullHeal", "回复至生命上限", "Restore full health", execute: c => h?.FullHeal(c)),
        new CommandSpec("SetHealth", "设置当前生命（不超过上限）", "Set current health (clamped to maximum)", ArgumentKind.Number, true, min: 1, execute: c => h?.SetHealth(c)),
        new CommandSpec("SetMaxHealth", "设置生命上限（不自动补血）", "Set maximum health without healing", ArgumentKind.Number, true, min: 1, execute: c => h?.SetMaxHealth(c)),
        new CommandSpec("DrawCards", "战斗主阶段抽取指定数量卡牌", "Draw cards during the player main phase", ArgumentKind.Number, true, min: 1, max: 100, execute: c => h?.DrawCards(c)),
        new CommandSpec("UpgradeAllCards", "升级所有已有可升级卡牌", "Upgrade all existing eligible cards", execute: c => h?.UpgradeAllCards(c)),
        new CommandSpec("ChargeDice", "增加唯一之骰充能", "Add The One Dice charge", ArgumentKind.Number, true, min: 1, execute: c => h?.ChargeDice(c)),
        new CommandSpec("UpgradeDice", "将唯一之骰升级一级", "Upgrade The One Dice once", execute: c => h?.UpgradeDice(c)),
        new CommandSpec("GainUpgradeCoin", "增加永久升级币", "Gain permanent upgrade coins", ArgumentKind.Number, true, min: 1, execute: c => h?.GainUpgradeCoin(c)),
        new CommandSpec("GainVoidMana", "战斗主阶段获得无色法力（Void）", "Gain Void mana during the player main phase", ArgumentKind.Number, true, min: 1, max: 100, execute: c => h?.GainVoidMana(c)),
        new CommandSpec("EndTurn", "结束当前玩家战斗回合", "End the current player battle turn", execute: c => h?.EndTurn(c)),
        new CommandSpec("WinRun", "整局胜利并结束本局（需 CONFIRM）", "Win and end the entire run (requires CONFIRM)", ArgumentKind.Confirm, true, execute: c => h?.WinRun(c)),
        new CommandSpec("AdvanceTime", "地图时间推进一步并结算原生时间效果", "Advance world time by one step with native effects", execute: c => h?.AdvanceTime(c)),
        new CommandSpec("AddCorruption", "增加预告侵蚀（百分点，遵循节点规则）", "Add forecast corruption in percentage points; native node rules apply", ArgumentKind.Number, true, min: 1, max: 100, execute: c => h?.AddCorruption(c)),
        new CommandSpec("CleanCorruption", "降低当前及预告侵蚀（百分点，保留原生下限）", "Clean current and forecast corruption; native minimum applies", ArgumentKind.Number, true, min: 1, max: 100, execute: c => h?.CleanCorruption(c)),
        new CommandSpec("PauseCorruption", "增加暂停地图侵蚀增长的时间步数", "Pause future map corruption growth for additional time steps", ArgumentKind.Number, true, min: 1, max: 1000, execute: c => h?.PauseCorruption(c)),
        new CommandSpec("MoveToChapter", "通过原生流程进入配置中的指定章节", "Move to a configured chapter through native progression", ArgumentKind.Chapter, true, execute: c => h?.MoveToChapter(c)),
        new CommandSpec("InspectWorld", "查看章节、世界时间和侵蚀状态", "Inspect chapter, world time and corruption", execute: c => h?.InspectWorld(c)),
        new CommandSpec("DamageFirstEnemy", "对首个存活敌人排队造成伤害", "Queue damage to the first living enemy", ArgumentKind.Number, true, min: 1, max: 1000000, execute: c => h?.DamageFirstEnemy(c)),
        new CommandSpec("DamageAllEnemies", "对所有存活敌人排队造成伤害", "Queue damage to all living enemies", ArgumentKind.Number, true, min: 1, max: 1000000, execute: c => h?.DamageAllEnemies(c)),
        new CommandSpec("DamagePlayer", "对玩家排队造成伤害（可能结束本局）", "Queue damage to the player; may end the run", ArgumentKind.Number, true, min: 1, max: 1000000, execute: c => h?.DamagePlayer(c)),
        new CommandSpec("WinBattle", "直接进入本场战斗胜利结算（需 CONFIRM）", "Resolve the current battle as a victory (requires CONFIRM)", ArgumentKind.Confirm, true, execute: c => h?.WinBattle(c)),
        new CommandSpec("InspectBattle", "查看当前回合、阶段和敌人生命", "Inspect current battle turn, phase and enemy health", execute: c => h?.InspectBattle(c)),
        new CommandSpec("UpgradeCard", "升级牌组中一张指定的可升级卡牌", "Upgrade one existing eligible copy of the selected card", ArgumentKind.Card, true, execute: c => h?.UpgradeCard(c)),
        new CommandSpec("RemoveCard", "从牌组删除一张指定卡牌", "Remove one existing copy of the selected card from the deck", ArgumentKind.Card, true, execute: c => h?.RemoveCard(c)),
        new CommandSpec("InspectDeck", "查看已有牌组的卡牌数量及升级数量", "Inspect owned card counts and upgraded copies", execute: c => h?.InspectDeck(c)),
        new CommandSpec("PlayAllCardAnimation", "播放全部卡牌动画（大量动画）", "Play every card animation (heavy)", native: "PlayAllCardAnimation", execute: c => h?.InvokeNative(c)),
        new CommandSpec("RemoveAllCards", "删除全部牌组卡牌（需 CONFIRM）", "Remove the entire deck (requires CONFIRM)", ArgumentKind.Confirm, true, "RemoveAllCards", execute: c => h?.InvokeNative(c)),
        new CommandSpec("CleanCareer", "清空永久生涯并保存（需 CONFIRM）", "Reset permanent career and save (requires CONFIRM)", ArgumentKind.Confirm, true, "CleanCareer", execute: c => h?.InvokeNative(c)),
        new CommandSpec("CleanAllAchievements", "重置本地及 Steam 成就（需 CONFIRM）", "Reset local and Steam achievements (requires CONFIRM)", ArgumentKind.Confirm, true, "CleanAllAchievements", execute: c => h?.InvokeNative(c))
    };
}

internal abstract class CommandActions
{
    internal abstract void Help(CommandCall call);
    internal abstract void Clear(CommandCall call);
    internal abstract void Status(CommandCall call);
    internal abstract void Refresh(CommandCall call);
    internal abstract void Close(CommandCall call);
    internal abstract void InvokeNative(CommandCall call);
    internal abstract void EnableDebugCheat(CommandCall call);
    internal abstract void HealPlayer(CommandCall call);
    internal abstract void FullHeal(CommandCall call);
    internal abstract void SetHealth(CommandCall call);
    internal abstract void SetMaxHealth(CommandCall call);
    internal abstract void DrawCards(CommandCall call);
    internal abstract void UpgradeAllCards(CommandCall call);
    internal abstract void ChargeDice(CommandCall call);
    internal abstract void UpgradeDice(CommandCall call);
    internal abstract void GainUpgradeCoin(CommandCall call);
    internal abstract void GainVoidMana(CommandCall call);
    internal abstract void EndTurn(CommandCall call);
    internal abstract void WinRun(CommandCall call);
    internal abstract void AdvanceTime(CommandCall call);
    internal abstract void AddCorruption(CommandCall call);
    internal abstract void CleanCorruption(CommandCall call);
    internal abstract void PauseCorruption(CommandCall call);
    internal abstract void MoveToChapter(CommandCall call);
    internal abstract void InspectWorld(CommandCall call);
    internal abstract void DamageFirstEnemy(CommandCall call);
    internal abstract void DamageAllEnemies(CommandCall call);
    internal abstract void DamagePlayer(CommandCall call);
    internal abstract void WinBattle(CommandCall call);
    internal abstract void InspectBattle(CommandCall call);
    internal abstract void UpgradeCard(CommandCall call);
    internal abstract void RemoveCard(CommandCall call);
    internal abstract void InspectDeck(CommandCall call);
}
