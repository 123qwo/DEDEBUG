using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Card;
using GameData;
using HarmonyLib;
using Story;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace DEDEBUG;

internal sealed class NativeCatalog
{
    private readonly Dictionary<ArgumentKind, ParameterOption[]> _items = new Dictionary<ArgumentKind, ParameterOption[]>();
    internal bool Refreshing { get; private set; }
    internal int Revision { get; private set; }
    private readonly CatalogGeneration _generation = new CatalogGeneration();
    private CardDataManager _cardSource;
    private DataManager _dataSource;
    private World.GameChapterSo _chapterSource;
    private World.ChapterByName[] _chapterRows;
    private ParameterOption[] _chapterOptions = Array.Empty<ParameterOption>();
    internal bool NeedsRefresh => _generation.Dirty || !ReferenceEquals(_cardSource, CardDataManager.Instance) || !ReferenceEquals(_dataSource, DataManager.Instance);
    internal void Invalidate() { _generation.Invalidate(); _chapterSource = null; _chapterRows = null; }
    private static readonly Dictionary<string, StringTable> Tables = new Dictionary<string, StringTable>();
    internal NativeCatalog()
    {
        Set(ArgumentKind.Number, new[] { "1", "2", "3", "5", "10", "20", "50", "100", "1000" }.Select(x => new ParameterOption(x, "数量 " + x, "Amount " + x)));
        Set(ArgumentKind.Toggle, new[] { new ParameterOption("on", "启用原生快捷键", "Enable native hotkeys"), new ParameterOption("off", "关闭原生快捷键", "Disable native hotkeys") });
        Set(ArgumentKind.UpgradeMode, new[] { new ParameterOption("Basic", "普通版", "Basic"), new ParameterOption("Upgraded", "升级版", "Upgraded") });
        Set(ArgumentKind.Confirm, new[] { new ParameterOption("CONFIRM", "确认执行该指令", "Confirm this command") });
        Set(ArgumentKind.AbilityType, Enum.GetNames(typeof(AbilityType)).Select(x => new ParameterOption(x, AbilityLabel(x), x)));
    }
    private static string AbilityLabel(string name)
    {
        switch (name) { case "Buff": return "增益"; case "DeBuff": return "减益"; case "PrimAbility": return "基础能力"; case "Util": return "工具能力"; default: return "其他能力"; }
    }
    private void Set(ArgumentKind kind, IEnumerable<ParameterOption> values)
    {
        _items[kind] = values.Where(x => !string.IsNullOrWhiteSpace(x.Value)).GroupBy(x => x.Value, StringComparer.Ordinal)
            .Select(x => x.First()).OrderBy(x => x.Value, StringComparer.OrdinalIgnoreCase).ToArray();
        Revision++;
    }
    internal IReadOnlyList<ParameterOption> Get(ArgumentKind kind)
        => kind == ArgumentKind.Chapter ? GetChapters() : _items.TryGetValue(kind, out var result) ? result : Array.Empty<ParameterOption>();
    private IReadOnlyList<ParameterOption> GetChapters()
    {
        var source = World.WorldManager.Instance != null ? World.WorldManager.Instance.gameChapterSo : null;
        var rows = source != null ? source.allGameChapter : null;
        if (!ReferenceEquals(source, _chapterSource) || !ReferenceEquals(rows, _chapterRows))
        {
            _chapterSource = source; _chapterRows = rows;
            _chapterOptions = rows == null ? Array.Empty<ParameterOption>() : rows.Where(row => row.chapterPrefab != null)
                .Select(row => row.chapterName.ToString()).Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase).Select(id => new ParameterOption(id, ChapterLabel(id), id)).ToArray();
        }
        return _chapterOptions;
    }
    private static string ChapterLabel(string id)
    {
        switch (id)
        {
            case "Prologue": return "序章";
            case "TheWorldBetween": return "世界之间";
            case "ChapterPlain": return "平原章节";
            case "ChapterForest": return "森林章节";
            case "ChapterCity": return "城市章节";
            case "ChapterFishing": return "钓鱼章节";
            case "Test": return "测试章节（仅在游戏配置存在时提供）";
            default: return id;
        }
    }

    private static string Local(string table, string key, string code)
    {
        try
        {
            var locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(code)) ??
                LocalizationSettings.AvailableLocales.Locales.FirstOrDefault(x => x.Identifier.Code.StartsWith(code == "en" ? "en" : "zh", StringComparison.OrdinalIgnoreCase));
            if (locale == null) return key;
            string cacheKey = table + "|" + locale.Identifier.Code;
            if (!Tables.TryGetValue(cacheKey, out var loaded) || loaded == null)
            { loaded = LocalizationSettings.StringDatabase.GetTable(table, locale); Tables[cacheKey] = loaded; }
            var value = loaded != null ? loaded.GetEntry(key)?.LocalizedValue : null;
            return string.IsNullOrWhiteSpace(value) ? key : value;
        }
        catch { return key; }
    }
    private static ParameterOption Named(string table, string id)
        => new ParameterOption(id, Local(table, id, "zh-Hans"), Local(table, id, "en"));

    internal IEnumerator Refresh(Action<string> report)
    {
        if (Refreshing) yield break;
        Refreshing = true;
        _chapterSource = null; _chapterRows = null;
        int generation = _generation.Begin();
        var dataManager = DataManager.Instance;
        Tables.Clear();
        try
        {
            var cards = CardDataManager.Instance;
            if (cards == null)
            {
                throw new InvalidOperationException("游戏数据尚未就绪；进入游戏后执行 refresh | Game data not ready; refresh later");
            }
            var cardOptions = new List<ParameterOption>();
            var cardData = cards.GetAllCardsCardDateCollectionsInGame();
            if (cardData == null || cardData.Length == 0) throw new InvalidOperationException("卡牌数据尚未就绪 | Card data is not ready");
            for (int i = 0; i < cardData.Length; i++)
            {
                if (cardData[i] != null && !string.IsNullOrWhiteSpace(cardData[i].cardName)) cardOptions.Add(Named("CardName", cardData[i].cardName));
                if (i % 16 == 15) yield return null;
            }
            var pools = AccessTools.Field(typeof(CardDataManager), "_cardDateCollectionDictionary").GetValue(cards) as IDictionary;
            var poolOptions = pools == null ? Array.Empty<ParameterOption>() : pools.Keys.Cast<string>().Select(x => new ParameterOption(x, x, x)).ToArray();
            yield return null;
            var relics = dataManager?.GetRelicDataManager();
            if (relics == null) throw new InvalidOperationException("遗物数据尚未就绪 | Relic data is not ready");
            var relicOptions = new List<ParameterOption>();
            if (relics != null)
            {
                var data = relics.GetAllInGameRelicSo();
                for (int i = 0; i < data.Length; i++)
                {
                    if (data[i] != null) relicOptions.Add(Named("Relic", data[i].relicInfo.relicName));
                    if (i % 16 == 15) yield return null;
                }
            }
            yield return null;
            var stories = Resources.LoadAll<StorySo>("ScriptableObjects/Stories");
            var storyOptions = new List<ParameterOption>();
            for (int i = 0; i < stories.Length; i++)
            {
                if (stories[i] != null) storyOptions.Add(Named("Story", stories[i].storyTitle));
                if (i % 16 == 15) yield return null;
            }
            yield return null;
            var encounters = Resources.LoadAll<Combat.EncounterEnemySO>("ScriptableObjects/EnemyEncounter");
            var enemies = new List<ParameterOption>();
            for (int i = 0; i < encounters.Length; i++)
            {
                var encounter = encounters[i];
                if (encounter == null) continue;
                var names = encounter.genEnemyInfos == null ? new ParameterOption[0] : encounter.genEnemyInfos
                    .Where(x => !string.IsNullOrEmpty(x.enemyName)).Select(x => Named("EnemyInfo", x.enemyName)).ToArray();
                enemies.Add(new ParameterOption(encounter.encounterName,
                    string.Join(" + ", names.Select(x => x.Zh)), string.Join(" + ", names.Select(x => x.En))));
                if (i % 16 == 15) yield return null;
            }
            if (!ReferenceEquals(cards, CardDataManager.Instance) || !ReferenceEquals(dataManager, DataManager.Instance))
                throw new InvalidOperationException("游戏数据已切换，请重试刷新 | Game data changed; refresh again");
            Set(ArgumentKind.Card, cardOptions); Set(ArgumentKind.Pool, poolOptions); Set(ArgumentKind.Relic, relicOptions);
            Set(ArgumentKind.Story, storyOptions); Set(ArgumentKind.Enemy, enemies);
            _cardSource = cards; _dataSource = dataManager; _generation.Commit(generation);
            report("参数目录已刷新 | Catalog ready: cards=" + Get(ArgumentKind.Card).Count +
                ", relics=" + Get(ArgumentKind.Relic).Count + ", stories=" + Get(ArgumentKind.Story).Count + ", enemies=" + Get(ArgumentKind.Enemy).Count);
        }
        finally { Refreshing = false; }
    }
}
