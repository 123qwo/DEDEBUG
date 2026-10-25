using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Card;
using Combat;
using Combat.BattleWorld;
using Combat.Enemy;
using Command;
using HarmonyLib;
using UI;
using World;
using World.ChapterMapGenerator;

namespace DEDEBUG;

internal sealed partial class NativeExecutor
{
    private static readonly FieldInfo WorldTurns = AccessTools.Field(typeof(WorldManager), "_worldTurnCount");
    private static readonly FieldInfo CorruptionPause = AccessTools.Field(typeof(WorldManager), "_corruptionPauseCounter");

    private static WorldManager RequireWorldMap()
    {
        RequirePlayer();
        var world = WorldManager.Instance;
        if (world.GetCampaignState() != CampaignState.Traveling || world.currentGeneratedChapterMap == null || world.currentMapNode == null)
            throw new InvalidOperationException("此指令需要返回章节地图并结束当前事件或战斗 | Return to the chapter map and finish the current encounter first");
        if (UIManager.Instance == null || UIManager.Instance.GetUIShowingMenuState() != UIShowingMenuState.NoUIShowing)
            throw new InvalidOperationException("请先关闭游戏中的其他菜单或选择 | Close other game menus or selections first");
        var handler = PlayerCommandHandler.Instance;
        if (handler.CommandQueue.Count > 0 || handler.GetCurrentCommand().Item1 || handler.GetCurrentSubCommand().Item1)
            throw new InvalidOperationException("地图指令仍在处理中，请稍后重试 | Map commands are still processing; retry shortly");
        return world;
    }
    private static CorruptionManager RequireCorruption(WorldManager world)
    {
        if (world.corruptionManager == null) throw new InvalidOperationException("地图侵蚀数据尚未就绪 | Map corruption data is not ready");
        return world.corruptionManager;
    }
    internal override void AdvanceTime(CommandCall call)
    {
        var world = RequireWorldMap(); RequireCorruption(world);
        int turns = (int)WorldTurns.GetValue(world);
        if (turns == int.MaxValue) throw new InvalidOperationException("世界时间已达到整数上限 | World time has reached the integer limit");
        new SubCommandTimeMove(true);
        _plugin.Write("世界时间推进一步已排队，将结算原生卡牌、状态和侵蚀效果 | One world time step queued with native card, state and corruption effects");
    }
    internal override void AddCorruption(CommandCall call)
    {
        RequireCorruption(RequireWorldMap());
        new CommandAddCorruption(Amount(call), true, false);
        _plugin.Write("侵蚀增加已排队；参数为百分点，实际结算遵循当前节点规则 | Corruption addition queued in percentage points; native node rules apply: " + call.Argument);
    }
    internal override void CleanCorruption(CommandCall call)
    {
        RequireCorruption(RequireWorldMap());
        // The native clean API accepts a fraction, whereas AddCorruption accepts percentage points.
        new CommandCleanCorruption(Amount(call) / 100f);
        _plugin.Write("当前及预告侵蚀清理已排队，保留原生最低侵蚀值 | Current and forecast corruption cleaning queued; native minimum applies: " + call.Argument + " pp");
    }
    internal override void PauseCorruption(CommandCall call)
    {
        var world = RequireWorldMap(); RequireCorruption(world);
        int amount = Amount(call), current = (int)CorruptionPause.GetValue(world);
        int total = checked(current + amount);
        world.PauseCorruption(amount);
        _plugin.Write("侵蚀暂停剩余时间步 | Remaining corruption-growth pause steps: " + total +
            "；已有侵蚀和预告仍按原生规则结算 | Existing corruption and forecasts still follow native resolution");
    }
    internal override void MoveToChapter(CommandCall call)
    {
        var world = RequireWorldMap();
        if (!Enum.TryParse(call.Argument, out ChapterMapName chapter) || world.gameChapterSo == null || world.gameChapterSo.allGameChapter == null ||
            !world.gameChapterSo.allGameChapter.Any(row => row.chapterName == chapter && row.chapterPrefab != null))
            throw new InvalidOperationException("该章节没有可用的原生配置，请刷新参数目录 | This chapter has no configured native map; refresh the catalog");
        new CommandMoveToNewChapter(chapter);
        _plugin.Write("原生章节切换已排队 | Native chapter transition queued: " + call.Argument);
        _plugin.SetOpen(false);
    }
    internal override void InspectWorld(CommandCall call)
    {
        var world = WorldManager.Instance;
        var text = new StringBuilder();
        text.AppendLine("地图状态 | World state: " + world.GetCampaignState());
        text.AppendLine("当前章节 | Current chapter: " + (world.currentGeneratedChapterMap != null ? world.currentGeneratedChapterMap.chapterMapName.ToString() : "Not ready"));
        text.AppendLine("世界时间步 | World time steps: " + WorldTurns.GetValue(world));
        text.AppendLine("侵蚀暂停步数 | Corruption-growth pause steps: " + CorruptionPause.GetValue(world));
        if (world.corruptionManager != null)
        {
            text.AppendLine("当前侵蚀 | Current corruption: " + world.corruptionManager.GetCurrentCorruptionPercent().ToString("P1", CultureInfo.InvariantCulture));
            text.AppendLine("预告侵蚀 | Forecast corruption: " + world.corruptionManager.GetNextCorruptionPercent().ToString("P1", CultureInfo.InvariantCulture));
        }
        _plugin.Write(text.ToString());
    }
    private static CharacterEnemy[] LivingEnemies()
    {
        if (EnemyManager.Instance == null) throw new InvalidOperationException("敌人数据尚未就绪 | Enemy data is not ready");
        return EnemyManager.Instance.GetAllEnemy().Where(enemy => enemy != null && enemy.GetHealthPoint() > 0).ToArray();
    }
    private static void QueueTestDamage(Character target, int amount)
        => new SubCommandDealDamage(new Damage(amount, WorldManager.Instance, target, DamageSourceType.IndirectDamage, DamageSpecialType.Standard), true);
    internal override void DamageFirstEnemy(CommandCall call)
    {
        RequirePlayerMainPhase(); var enemies = LivingEnemies();
        if (enemies.Length == 0) throw new InvalidOperationException("当前没有存活敌人 | No living enemies are available");
        QueueTestDamage(enemies[0], Amount(call));
        _plugin.Write("首个存活敌人的伤害已排队，遵循原生防御和伤害规则 | Damage queued to the first living enemy; native damage rules apply: " + call.Argument);
    }
    internal override void DamageAllEnemies(CommandCall call)
    {
        RequirePlayerMainPhase(); var enemies = LivingEnemies();
        if (enemies.Length == 0) throw new InvalidOperationException("当前没有存活敌人 | No living enemies are available");
        foreach (var enemy in enemies) QueueTestDamage(enemy, Amount(call));
        _plugin.Write("全体存活敌人的伤害已排队 | Damage queued to living enemies: " + enemies.Length + ", amount each=" + call.Argument);
    }
    internal override void DamagePlayer(CommandCall call)
    {
        RequirePlayerMainPhase(); QueueTestDamage(RequirePlayer(), Amount(call));
        _plugin.Write("玩家伤害已排队，将遵循原生防御与死亡规则 | Player damage queued with native defence and death rules: " + call.Argument);
    }
    internal override void WinBattle(CommandCall call)
    {
        RequirePlayerMainPhase(); new CommandBattleOver(true);
        _plugin.Write("本场战斗胜利结算已排队 | Current battle victory resolution queued");
        _plugin.SetOpen(false);
    }
    internal override void InspectBattle(CommandCall call)
    {
        var world = WorldManager.Instance; var battle = BattleManager.Instance;
        if (world.GetCampaignState() != CampaignState.InsideBattle || battle == null)
            throw new InvalidOperationException("当前不在战斗中 | No battle is active");
        var text = new StringBuilder(); var phase = battle.currentBattlePhase;
        text.AppendLine("战斗回合 | Battle turn: " + battle.GetTurnCount());
        text.AppendLine("阶段 | Phase: " + phase.battlePhase + "; actor=" + (phase.character != null ? phase.character.GetDamageSourceName() : "None"));
        var player = RequirePlayer(); text.AppendLine("玩家生命 | Player health: " + player.GetHealthPoint() + "/" + player.GetMaxHealthPoint());
        var enemies = LivingEnemies();
        foreach (var enemy in enemies) text.AppendLine("Enemy #" + enemy.GetEnemyBattleUniqueID() + " | " + enemy.GetDamageSourceName() + " | HP=" + enemy.GetHealthPoint());
        text.AppendLine("存活敌人数量 | Living enemies: " + enemies.Length);
        _plugin.Write(text.ToString());
    }
    internal override void UpgradeCard(CommandCall call)
    {
        RequirePlayer();
        var card = CardManager.Instance.GetAllPlayerCards().FirstOrDefault(candidate => candidate != null &&
            candidate.GetCardCoreInfo().CardName == call.Argument && candidate.cardUpgradeInfo.GetHasCardUpgradeInfo() && !candidate.cardUpgradeInfo.isUpgraded);
        if (card == null) throw new InvalidOperationException("牌组中没有可升级的该卡牌 | No eligible owned copy can be upgraded: " + call.Argument);
        new SubCommandUpgradeCards(new[] { card });
        _plugin.Write("一张已有卡牌的升级已排队 | One owned card upgrade queued: " + call.Argument);
    }
    internal override void RemoveCard(CommandCall call)
    {
        RequirePlayer();
        var card = CardManager.Instance.GetAllPlayerCards().FirstOrDefault(candidate => candidate != null && candidate.GetCardCoreInfo().CardName == call.Argument);
        if (card == null) throw new InvalidOperationException("牌组中没有该卡牌 | No owned copy is present: " + call.Argument);
        new SubCommandRemoveCardFromGame(card);
        _plugin.Write("从牌组删除一张卡牌已排队 | Removal of one owned card from the deck queued: " + call.Argument);
    }
    internal override void InspectDeck(CommandCall call)
    {
        RequirePlayer(); var cards = CardManager.Instance.GetAllPlayerCards().Where(card => card != null).ToArray();
        var labels = _plugin.Catalog.Get(ArgumentKind.Card).ToDictionary(option => option.Value, StringComparer.Ordinal);
        var text = new StringBuilder("已有卡牌总数 | Owned cards: " + cards.Length + "\n");
        foreach (var group in cards.GroupBy(card => card.GetCardCoreInfo().CardName).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            string label = labels.TryGetValue(group.Key, out var option) ? option.Display : group.Key;
            text.AppendLine(label + " | Count=" + group.Count() + ", Upgraded=" + group.Count(card => card.cardUpgradeInfo.isUpgraded));
        }
        _plugin.Write(text.ToString());
    }
}
