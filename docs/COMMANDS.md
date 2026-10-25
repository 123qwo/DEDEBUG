# DEDEBUG 指令表

完整指令定义见 `src/CommandDefinitions.cs`，变更指令时请同步更新本表。指令及参数用空格分隔，参数含空格时使用英文双引号。

| 指令 | 中文 | English | 格式 |
|---|---|---|---|
| `help` | 查看指令帮助 | Show command help | `help` |
| `clear` | 清空指令输出 | Clear command output | `clear` |
| `status` | 查看游戏与目录状态 | Show game and catalog status | `status` |
| `refresh` | 刷新参数与双语名称 | Refresh argument catalogs | `refresh` |
| `close` | 关闭控制台 | Close console | `close` |
| `GainMoney` | 增加金币 | Gain money | `GainMoney <Number>` |
| `GainCard` | 获得指定卡牌 | Gain a card | `GainCard <Card>` |
| `GainRelic` | 获得指定遗物 | Gain a relic | `GainRelic <Relic>` |
| `NodeStory` | 进入指定原生事件 | Enter a native story | `NodeStory <Story>` |
| `NodeEnemy` | 进入指定敌人遭遇 | Enter an encounter | `NodeEnemy <Enemy>` |
| `GainCardPool` | 获得指定卡池的卡牌 | Gain a card pool | `GainCardPool <Pool>` |
| `GainAllCardInGame` | 获得全部卡牌（普通或升级） | Gain all cards (Basic or Upgraded) | `GainAllCardInGame [UpgradeMode]` |
| `GainAllRelic` | 获得全部遗物 | Gain all relics | `GainAllRelic` |
| `GainAllAbilityByAbilityType` | 获得该类型全部能力 | Gain all abilities of a type | `GainAllAbilityByAbilityType <AbilityType>` |
| `SetStoreRelic` | 替换商店首个遗物 | Replace the first store relic | `SetStoreRelic <Relic>` |
| `EnableDebugCheat` | 开关原生调试快捷键 | Toggle native debug keys | `EnableDebugCheat [Toggle]` |
| `HealPlayer` | 回复指定数量生命 | Heal the player | `HealPlayer <1..2147483647>` |
| `FullHeal` | 回复至生命上限 | Restore full health | `FullHeal` |
| `SetHealth` | 设置当前生命（不超过上限） | Set current health (clamped to maximum) | `SetHealth <1..2147483647>` |
| `SetMaxHealth` | 设置生命上限（不自动补血） | Set maximum health without healing | `SetMaxHealth <1..2147483647>` |
| `DrawCards` | 战斗主阶段抽取指定数量卡牌 | Draw cards during the player main phase | `DrawCards <1..100>` |
| `UpgradeAllCards` | 升级所有已有可升级卡牌 | Upgrade all existing eligible cards | `UpgradeAllCards` |
| `ChargeDice` | 增加唯一之骰充能 | Add The One Dice charge | `ChargeDice <1..2147483647>` |
| `UpgradeDice` | 将唯一之骰升级一级 | Upgrade The One Dice once | `UpgradeDice` |
| `GainUpgradeCoin` | 增加永久升级币 | Gain permanent upgrade coins | `GainUpgradeCoin <1..2147483647>` |
| `GainVoidMana` | 战斗主阶段获得无色法力（Void） | Gain Void mana during the player main phase | `GainVoidMana <1..100>` |
| `EndTurn` | 结束当前玩家战斗回合 | End the current player battle turn | `EndTurn` |
| `WinRun` | 整局胜利并结束本局（需 CONFIRM） | Win and end the entire run (requires CONFIRM) | `WinRun <Confirm>` |
| `AdvanceTime` | 地图时间推进一步并结算原生时间效果 | Advance world time by one step with native effects | `AdvanceTime` |
| `AddCorruption` | 增加预告侵蚀（百分点，遵循节点规则） | Add forecast corruption in percentage points; native node rules apply | `AddCorruption <1..100>` |
| `CleanCorruption` | 降低当前及预告侵蚀（百分点，保留原生下限） | Clean current and forecast corruption; native minimum applies | `CleanCorruption <1..100>` |
| `PauseCorruption` | 增加暂停地图侵蚀增长的时间步数 | Pause future map corruption growth for additional time steps | `PauseCorruption <1..1000>` |
| `MoveToChapter` | 通过原生流程进入配置中的指定章节 | Move to a configured chapter through native progression | `MoveToChapter <Chapter>` |
| `InspectWorld` | 查看章节、世界时间和侵蚀状态 | Inspect chapter, world time and corruption | `InspectWorld` |
| `DamageFirstEnemy` | 对首个存活敌人排队造成伤害 | Queue damage to the first living enemy | `DamageFirstEnemy <1..1000000>` |
| `DamageAllEnemies` | 对所有存活敌人排队造成伤害 | Queue damage to all living enemies | `DamageAllEnemies <1..1000000>` |
| `DamagePlayer` | 对玩家排队造成伤害（可能结束本局） | Queue damage to the player; may end the run | `DamagePlayer <1..1000000>` |
| `WinBattle` | 直接进入本场战斗胜利结算（需 CONFIRM） | Resolve the current battle as a victory (requires CONFIRM) | `WinBattle <Confirm>` |
| `InspectBattle` | 查看当前回合、阶段和敌人生命 | Inspect current battle turn, phase and enemy health | `InspectBattle` |
| `UpgradeCard` | 升级牌组中一张指定的可升级卡牌 | Upgrade one existing eligible copy of the selected card | `UpgradeCard <Card>` |
| `RemoveCard` | 从牌组删除一张指定卡牌 | Remove one existing copy of the selected card from the deck | `RemoveCard <Card>` |
| `InspectDeck` | 查看已有牌组的卡牌数量及升级数量 | Inspect owned card counts and upgraded copies | `InspectDeck` |
| `PlayAllCardAnimation` | 播放全部卡牌动画（大量动画） | Play every card animation (heavy) | `PlayAllCardAnimation` |
| `RemoveAllCards` | 删除全部牌组卡牌（需 CONFIRM） | Remove the entire deck (requires CONFIRM) | `RemoveAllCards <Confirm>` |
| `CleanCareer` | 清空永久生涯并保存（需 CONFIRM） | Reset permanent career and save (requires CONFIRM) | `CleanCareer <Confirm>` |
| `CleanAllAchievements` | 重置本地及 Steam 成就（需 CONFIRM） | Reset local and Steam achievements (requires CONFIRM) | `CleanAllAchievements <Confirm>` |
