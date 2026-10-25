# DEDEBUG

DICEOMANCER 游戏内调试控制台，当前版本 **0.1.6**。独立 BepInEx 6 Mono 插件，提供 46 条控制台与调试指令。

**AI 生成说明：本 Mod 由作者提出功能需求，使用 ChatGPT（AI）生成代码并迭代开发。本文亦由 AI 生成。**

## 功能

- 获取金币、卡牌和遗物，升级或删除卡牌，调整生命、骰子充能和永久升级币。
- 查看或调整战斗、章节、世界时间及侵蚀状态。
- 中英双语指令说明与参数补全，兼容大小写；支持内部 ID 和唯一的完整本地化名称。
- 指令历史、草稿恢复、滚动输出及独立错误提示。
- 面板打开时请求暂停并隔离游戏本体输入，关闭时恢复。

完整指令见 [COMMANDS.md](docs/COMMANDS.md)。

## 运行与使用

运行需要 Windows 版 DICEOMANCER 和 **BepInEx 6 Mono**。将编译后的 `DEDEBUG.dll` 放入游戏目录的 `BepInEx/plugins/DEDEBUG/`，不要保留多个插件副本。

游戏菜单加载完成后按键盘左上角的 **` / ~ 键**打开控制台，无需按住 Shift。Esc 或 × 关闭。

| 按键 | 操作 |
|---|---|
| Enter | 提交完整指令；输入不完整且存在候选时先补全 |
| Tab / 点击候选 | 填入补全，不立即执行 |
| ↑ / ↓ | 选择补全；空输入或历史浏览状态下浏览历史 |
| Ctrl + ↑ / ↓ | 直接浏览历史，返回末尾恢复草稿 |
| 滚轮 | 在输出区阅读历史，或在提示区翻页 |

指令与参数用空格分隔，含空格的参数用英文双引号包围。每条指令接受零个或一个参数。历史最多 100 条，退出游戏后清除。

```text
help
status
InspectDeck
InspectWorld
InspectBattle
GainMoney 100
HealPlayer 10
DrawCards 2
ChargeDice 10
refresh
clear
```

`[Pending]` 表示仍在处理，`[Complete]` 仅表示处理结束，不判断游戏效果是否成功。参数或执行错误通过独立提示显示；普通游戏日志不汇入控制台。

部分操作要求处于空闲地图或玩家战斗主阶段。`WinRun`、`WinBattle`、`RemoveAllCards`、`CleanCareer` 和 `CleanAllAchievements` 需要手动输入大写 `CONFIRM`，不会自动补全确认词。

调试指令会实际修改游戏数据，部分操作涉及永久进度或成就，建议先备份存档。卸载 Mod 不会撤销已执行的修改。

配置在 `BepInEx/config/diceomancer.dedebug.cfg`，`ToggleKey` 默认 `BackQuote`，修改后重启生效。卸载时退出游戏并删除插件 DLL；配置可按需另行删除。

## 从源码构建

需要：

- 支持 C# 文件范围命名空间和 netstandard2.1 的 .NET SDK（已使用 .NET 10 SDK 验证）。
- 自行安装的 DICEOMANCER 及 BepInEx 6 Mono，用于提供游戏和运行时程序集引用。
- PowerShell。

仓库不包含游戏 DLL、BepInEx DLL、编译成品、私有配置或本机调试资料。所有程序集引用均从构建时指定的游戏目录读取，不复制到输出目录。

在仓库根目录执行：

```powershell
.\Build.ps1 -GameDir '<游戏安装目录>' -Verify
```

也可以先设置环境变量，再构建：

```powershell
$env:DICEOMANCER_GAME_DIR = '<游戏安装目录>'
.\Build.ps1 -Verify
```

默认从 NuGet.org 还原引用包；离线环境可通过 `-PackageSource '<本地包源目录>'` 指定已准备好的 `NETStandard.Library.Ref 2.1.0` 包源。可用 `-DotnetPath '<dotnet 可执行文件路径>'` 指定 SDK。

构建先读取游戏程序集，生成输入隔离入口清单，再编译插件。清单和诊断结果写入忽略目录 `.build/`，DLL 输出到 `src/bin/Release/DEDEBUG.dll`。游戏更新后应重新构建以更新入口清单。

`-Verify` 执行原生接口、指令注册、输入包装签名及完整输入读取覆盖检查，不会启动游戏或执行游戏指令。静态检查不能代替游戏内效果与交互验证。

退出游戏后可安装本次构建：

```powershell
.\Install.ps1 -GameDir '<游戏安装目录>'
```

安装器只处理 DEDEBUG DLL，检查插件 GUID、重复副本和文件哈希。安装回执与旧版备份写入忽略目录 `.local/`。脚本不会关闭游戏。

## 源码结构

```text
src/                       插件源码和项目文件
docs/                      指令清单
Build.ps1                  构建入口
Generate-InputManifest.ps1  游戏输入入口生成器
Verify-NativeApi.ps1       静态兼容性检查
Install.ps1                本地安装工具
```

`CommandDefinitions.cs` 统一注册指令，`NativeExecutor.cs`、`ExtraCommands.cs` 和 `WorldAndBattleCommands.cs` 处理游戏接口。`ConsoleCore.cs`、`ConsoleState.cs`、`ConsoleView.cs` 实现解析、历史和界面。输入隔离由 `GameInputGate.cs`、`GameInputIsolation.cs` 及构建时生成的清单共同实现。

参数目录按需缓存，切换场景、语言或游戏数据管理器时失效；`refresh` 可手动刷新其他 Mod 动态加入的数据。输入隔离覆盖游戏本体的旧输入 API，不接管 Windows、Steam 快捷键或其他 Mod 的私有输入。

## English

DEDEBUG is an independent in-game debug console for DICEOMANCER, built for BepInEx 6 Mono. It includes 46 commands, Chinese/English suggestions, argument completion, command history and game input isolation while the console is open.

Press Backquote (` / ~) after the game menu loads. Type `help` for commands. Back up your save before changing progress; uninstalling does not undo changes.

Build with `Build.ps1 -GameDir '<game directory>' -Verify`. Supply your own game and BepInEx assemblies. Generated metadata and build artifacts are excluded from Git. This mod's code and documentation were generated and iteratively refined with ChatGPT (AI).
"# DEDEBUG" 
