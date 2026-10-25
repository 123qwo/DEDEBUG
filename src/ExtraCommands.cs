using System;
using System.Globalization;
using Card;
using Combat;
using Combat.BattleWorld;
using Command;
using Relic;
using UI;
using World;

namespace DEDEBUG;

internal sealed partial class NativeExecutor
{
    private static int Amount(CommandCall call) => int.Parse(call.Argument, CultureInfo.InvariantCulture);
    internal override void HealPlayer(CommandCall call) => Heal(Amount(call));
    internal override void FullHeal(CommandCall call) => Heal(int.MaxValue);
    private void Heal(int requested)
    {
        var player = RequirePlayer();
        int missing = (int)Math.Max(0L, (long)player.GetMaxHealthPoint() - player.GetHealthPoint());
        int heal = Math.Min(missing, requested);
        if (heal > 0) player.TakeHeal(heal);
        ShowHealth(player);
    }
    private void ShowHealth(CharacterPlayer player) => _plugin.Write("生命 | Health: " + player.GetHealthPoint() + " / " + player.GetMaxHealthPoint());
    internal override void SetHealth(CommandCall call)
    {
        var player = RequirePlayer();
        player.SetHealthPoint(Math.Min(Amount(call), player.GetMaxHealthPoint()));
        ShowHealth(player);
    }
    internal override void SetMaxHealth(CommandCall call)
    {
        var player = RequirePlayer(); player.SetMaxHealthPoint(Amount(call)); ShowHealth(player);
    }
    internal override void DrawCards(CommandCall call)
    {
        RequirePlayerMainPhase();
        int amount = Amount(call);
        for (int i = 0; i < amount; i++) new SubCommandDrawOneCard(null, false, null, true);
        _plugin.Write("已排队抽牌，受手牌上限和牌堆限制 | Draw queued, subject to hand/deck limits: " + amount);
    }
    internal override void UpgradeAllCards(CommandCall call)
    {
        RequirePlayer(); int upgraded = 0;
        foreach (var card in CardManager.Instance.GetAllPlayerCards())
            if (card != null && CardManager.Instance.UpgradeCard(card)) upgraded++;
        _plugin.Write("已升级已有卡牌 | Existing cards upgraded: " + upgraded);
    }
    internal override void ChargeDice(CommandCall call)
    {
        var dice = RequireDice();
        if (dice.relicChargeRNumber == null) throw new InvalidOperationException("骰子充能尚未就绪 | Dice charge is not ready");
        int current = dice.relicChargeRNumber.GetCollapsedNumber(true);
        int target = checked(current + Amount(call));
        new SubCommandChangeRNumber(dice.relicChargeRNumber, new RNumberInfo(target, false, "Standard", null), SubCommandChangeRNumber.ChangeType.Regular, true);
        _plugin.Write("骰子充能修改已排队 | Dice charge change queued: " + current + " → " + target);
    }
    internal override void UpgradeDice(CommandCall call)
    {
        RequireDice();
        if (DiceUtilities.DiceManager.Instance == null) throw new InvalidOperationException("骰子管理器尚未就绪 | Dice manager is not ready");
        new CommandUpgradeTheOneDice();
        _plugin.Write("骰子升级已排队，已达上限时遵循原生提示 | Dice upgrade queued; native maximum applies");
    }
    internal override void GainUpgradeCoin(CommandCall call)
    {
        var career = Career.CareerManager.Instance;
        if (career == null || career.currentCareerInformation == null || career.currentCareerInformation.playerCareerUpgradeInfo == null)
            throw new InvalidOperationException("生涯数据尚未就绪 | Career data is not ready");
        int amount = Amount(call);
        int target = checked(career.currentCareerInformation.playerCareerUpgradeInfo.upgradeCoinCount + amount);
        career.GainUpgradeCoin(amount);
        _plugin.Write("升级币 | Upgrade coins: " + target + "；由游戏正常存档保存 | Saved by normal game saving");
    }
    internal override void GainVoidMana(CommandCall call)
    {
        RequirePlayerMainPhase(); var mana = ManaManager.Instance;
        if (mana == null) throw new InvalidOperationException("法力管理器尚未就绪 | Mana manager is not ready");
        int available = Math.Max(0, mana.GetMaxCount() - mana.GetManaArray().Length);
        int granted = Math.Min(Amount(call), available);
        if (granted == 0) throw new InvalidOperationException("没有空余法力位置 | No free mana slots");
        new SubCommandAddManaS(ColorE.Void, granted, false, true);
        _plugin.Write("无色法力生成已排队，受法力容量限制 | Void mana queued, capacity applies: " + granted);
    }
    internal override void EndTurn(CommandCall call)
    {
        RequirePlayerMainPhase(); BattleManager.Instance.SendEndTurnCommand();
        _plugin.Write("结束回合已排队 | End turn queued"); _plugin.SetOpen(false);
    }
    internal override void WinRun(CommandCall call)
    {
        RequirePlayer(); _plugin.SetOpen(false); new CommandVictory();
        _plugin.Write("整局胜利已排队 | Full run victory queued");
    }
    private static CharacterPlayer RequirePlayer()
    {
        var player = CharacterPlayer.Instance;
        bool inBattle = WorldManager.Instance.GetCampaignState() == CampaignState.InsideBattle;
        if (player == null || (!inBattle && TopUIItemManager.Instance == null) || player.GetCurrentPlayerHpRNumber() == null ||
            player.GetCurrentPlayerMaxHpRNumber() == null || player.GetHealthPoint() <= 0)
            throw new InvalidOperationException("存活玩家的生命数据尚未就绪 | Living player health data is not ready");
        return player;
    }
    private static void RequirePlayerMainPhase()
    {
        var player = RequirePlayer(); var battle = BattleManager.Instance;
        if (WorldManager.Instance.GetCampaignState() != CampaignState.InsideBattle || battle == null ||
            battle.currentBattlePhase.character != player || battle.currentBattlePhase.battlePhase != BattlePhase.Main)
            throw new InvalidOperationException("此指令仅限玩家战斗回合的主阶段 | Requires the player's battle main phase");
        if (UIManager.Instance == null || UIManager.Instance.GetUIShowingMenuState() != UIShowingMenuState.NoUIShowing)
            throw new InvalidOperationException("请先关闭游戏中的其他选择或菜单界面 | Close other game selections or menus first");
    }
    private static RelicHolder RequireDice()
    {
        if (RelicManager.Instance != null)
        {
            var result = RelicManager.Instance.HasCertainRelic("TheOneDice");
            if (result.Item1 && result.Item2 != null) return result.Item2;
        }
        throw new InvalidOperationException("尚未拥有唯一之骰 | The One Dice is not owned");
    }
}
