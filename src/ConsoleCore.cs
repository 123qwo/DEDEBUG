using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DEDEBUG;

internal enum ArgumentKind { None, Number, Card, Relic, Story, Enemy, Pool, Chapter, UpgradeMode, AbilityType, Toggle, Confirm }
internal sealed class CommandSpec
{
    internal readonly string Name, Zh, En, Native;
    internal readonly ArgumentKind Kind;
    internal readonly bool Required;
    internal readonly int Min, Max;
    internal readonly Action<CommandCall> Execute;
    internal readonly bool Panel;
    private string _usage, _suggestionLabel;
    internal CommandSpec(string name, string zh, string en, ArgumentKind kind = ArgumentKind.None,
        bool required = false, string native = null, int min = 0, int max = int.MaxValue, Action<CommandCall> execute = null, bool panel = false)
    { Name = name; Zh = zh; En = en; Kind = kind; Required = required; Native = native; Min = min; Max = max; Execute = execute; Panel = panel; }
    internal string Usage => _usage ??= Name + (Kind == ArgumentKind.None ? "" : Kind == ArgumentKind.Number && (Min != 0 || Max != int.MaxValue)
        ? " <" + Min + ".." + Max + ">" : Required ? " <" + Kind + ">" : " [" + Kind + "]");
    internal string SuggestionLabel => _suggestionLabel ??= Name + "[" + ArgumentHint + "] - " + Zh + " | " + En;
    private string ArgumentHint
    {
        get
        {
            switch (Kind)
            {
                case ArgumentKind.None: return "无参数|No arguments";
                case ArgumentKind.Number: return "数量|Amount " + Min + ".." + Max;
                case ArgumentKind.Card: return "卡牌ID|Card ID";
                case ArgumentKind.Relic: return "遗物ID|Relic ID";
                case ArgumentKind.Story: return "事件ID|Story ID";
                case ArgumentKind.Enemy: return "遭遇ID|Encounter ID";
                case ArgumentKind.Pool: return "卡池ID|Pool ID";
                case ArgumentKind.Chapter: return "章节ID|Chapter ID";
                case ArgumentKind.UpgradeMode: return "Basic|Upgraded";
                case ArgumentKind.AbilityType: return "PrimAbility|Buff|DeBuff|Util|Other";
                case ArgumentKind.Toggle: return "on|off";
                case ArgumentKind.Confirm: return "CONFIRM";
                default: return Kind.ToString();
            }
        }
    }
}

internal sealed class ParameterOption
{
    internal readonly string Value, Zh, En;
    private string _display;
    internal ParameterOption(string value, string zh = null, string en = null)
    { Value = value; Zh = string.IsNullOrWhiteSpace(zh) ? value : zh; En = string.IsNullOrWhiteSpace(en) ? value : en; }
    internal string Display => _display ??= Zh + " | " + En + " | " + Value;
}
internal sealed class Completion
{
    internal readonly CommandSpec Spec;
    internal readonly ParameterOption Option;
    private string _fill, _display;
    internal string Fill => _fill ??= Option == null ? Spec.Name + (Spec.Kind == ArgumentKind.None ? "" : " ") : Spec.Name + " " + CommandEngine.Quote(Option.Value);
    internal string Display => _display ??= Option == null ? Spec.SuggestionLabel : Spec.Name + "[" + Option.Display + "] - " + Spec.Zh + " | " + Spec.En;
    internal Completion(CommandSpec spec, ParameterOption option = null) { Spec = spec; Option = option; }
}
internal sealed class CommandCall
{
    internal readonly CommandSpec Spec;
    internal readonly string Argument;
    internal CommandCall(CommandSpec spec, string argument) { Spec = spec; Argument = argument; }
    internal string Canonical => Spec.Name + (string.IsNullOrEmpty(Argument) ? "" : " " + CommandEngine.Quote(Argument));
}

internal sealed class CommandEngine
{
    internal static readonly CommandSpec[] Specs = CommandDefinitions.Create(null);
    private readonly IReadOnlyList<CommandSpec> _specs;
    private readonly Dictionary<string, CommandSpec> _byName = new Dictionary<string, CommandSpec>(StringComparer.OrdinalIgnoreCase);
    private readonly Completion[] _commands;
    private readonly Dictionary<CommandSpec, OptionIndex> _indexes = new Dictionary<CommandSpec, OptionIndex>();
    internal IReadOnlyList<CommandSpec> Commands => _specs;
    private readonly Func<ArgumentKind, IReadOnlyList<ParameterOption>> _options;
    internal CommandEngine(Func<ArgumentKind, IReadOnlyList<ParameterOption>> options, IReadOnlyList<CommandSpec> specs = null)
    {
        _options = options; _specs = specs ?? Specs;
        foreach (var spec in _specs) _byName.Add(spec.Name, spec);
        _commands = _specs.OrderBy(spec => spec.Name, StringComparer.OrdinalIgnoreCase).Select(spec => new Completion(spec)).ToArray();
    }
    internal CommandSpec Find(string name) => name != null && _byName.TryGetValue(name, out var spec) ? spec : null;
    internal void InvalidateOptions() => _indexes.Clear();

    private sealed class OptionIndex
    {
        internal readonly IReadOnlyList<ParameterOption> Source;
        internal readonly int Count;
        internal readonly Completion[] Candidates;
        internal OptionIndex(CommandSpec spec, IReadOnlyList<ParameterOption> source)
        {
            Source = source; Count = source.Count;
            Candidates = source.Where(option => spec.Kind != ArgumentKind.Number ||
                    (int.TryParse(option.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number >= spec.Min && number <= spec.Max))
                .OrderBy(option => option.Value, StringComparer.OrdinalIgnoreCase).Select(option => new Completion(spec, option)).ToArray();
        }
    }

    internal static string Quote(string value)
    {
        if (value.Length > 0 && !value.Any(c => char.IsWhiteSpace(c) || c == '"' || c == '\\')) return value;
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
    internal static List<string> Tokenize(string text, bool allowIncomplete, out string error)
    {
        error = null;
        var result = new List<string>(); var token = new StringBuilder();
        bool quoted = false, started = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\\' && quoted && i + 1 < text.Length && (text[i + 1] == '"' || text[i + 1] == '\\'))
            { token.Append(text[++i]); started = true; continue; }
            if (c == '"') { quoted = !quoted; started = true; continue; }
            if (char.IsWhiteSpace(c) && !quoted)
            { if (started) { result.Add(token.ToString()); token.Clear(); started = false; } continue; }
            token.Append(c); started = true;
        }
        if (quoted && !allowIncomplete) error = "引号未闭合 | Unclosed quote";
        if (started) result.Add(token.ToString());
        return result;
    }
    // Prefixes begin at a word boundary; no fuzzy or middle-of-word matching.
    internal static bool Matches(string value, string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return true;
        if (string.IsNullOrEmpty(value)) return false;
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        int start = 0;
        while (start < value.Length)
        {
            while (start < value.Length && Separator(value[start])) start++;
            int end = start;
            while (end < value.Length && !Separator(value[end])) end++;
            if (end - start >= prefix.Length && string.Compare(value, start, prefix, 0, prefix.Length, StringComparison.OrdinalIgnoreCase) == 0) return true;
            start = end + 1;
        }
        return false;
    }
    private static bool Separator(char value) => value == ' ' || value == '\t' || value == '-' || value == '_';

    internal List<Completion> Complete(string text)
    {
        var tokens = Tokenize(text ?? "", true, out _);
        bool trailingSpace = !string.IsNullOrEmpty(text) && char.IsWhiteSpace(text[text.Length - 1]);
        if (tokens.Count == 0 || (tokens.Count == 1 && !trailingSpace))
        {
            string query = tokens.Count == 0 ? "" : tokens[0];
            var result = new List<Completion>(_commands.Length);
            var preferred = Find(string.IsNullOrEmpty(query) ? "help" : query);
            if (preferred != null)
                foreach (var candidate in _commands) if (ReferenceEquals(candidate.Spec, preferred)) { result.Add(candidate); break; }
            foreach (var candidate in _commands)
            {
                var command = candidate.Spec;
                if (!ReferenceEquals(command, preferred) && (Matches(command.Name, query) || Matches(command.Zh, query) || Matches(command.En, query))) result.Add(candidate);
            }
            return result;
        }
        var spec = Find(tokens[0]);
        if (spec == null || spec.Kind == ArgumentKind.None || tokens.Count > 2) return new List<Completion>();
        string arg = tokens.Count == 2 ? tokens[1] : "";
        var source = _options(spec.Kind);
        if (!_indexes.TryGetValue(spec, out var index) || !ReferenceEquals(index.Source, source) || index.Count != source.Count)
            _indexes[spec] = index = new OptionIndex(spec, source);
        var matches = new List<Completion>(Math.Min(300, index.Candidates.Length));
        foreach (var candidate in index.Candidates)
            if (string.Equals(candidate.Option.Value, arg, StringComparison.OrdinalIgnoreCase))
            { matches.Add(candidate); if (matches.Count == 300) return matches; }
        foreach (var candidate in index.Candidates)
        {
            var option = candidate.Option;
            if (!string.Equals(option.Value, arg, StringComparison.OrdinalIgnoreCase) &&
                (Matches(option.Value, arg) || Matches(option.Zh, arg) || Matches(option.En, arg)))
            { matches.Add(candidate); if (matches.Count == 300) break; }
        }
        return matches;
    }

    internal bool Parse(string text, out CommandCall call, out string error)
    {
        call = null;
        var tokens = Tokenize(text ?? "", false, out error);
        if (error != null) return false;
        if (tokens.Count == 0) { error = "请输入指令 | Enter a command"; return false; }
        var spec = Find(tokens[0]);
        if (spec == null) { error = "未知指令 | Unknown command: " + tokens[0]; return false; }
        if (tokens.Count > 2) { error = "参数含空格时请加双引号 | Quote arguments containing spaces"; return false; }
        string argument = tokens.Count == 2 ? tokens[1] : "";
        if (spec.Kind == ArgumentKind.None && tokens.Count > 1) { error = "此指令不接受参数 | No argument expected"; return false; }
        if (spec.Required && string.IsNullOrWhiteSpace(argument)) { error = "缺少参数 | Missing argument: " + spec.Usage; return false; }
        if (spec.Kind == ArgumentKind.Number)
        {
            if (!int.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out int number))
            { error = "需要非负整数 | Expected a nonnegative integer"; return false; }
            if (number < spec.Min || number > spec.Max)
            { error = "数量范围 | Allowed amount: " + spec.Min + ".." + spec.Max; return false; }
            argument = number.ToString(CultureInfo.InvariantCulture);
        }
        else if (spec.Kind == ArgumentKind.Confirm)
        {
            if (!string.Equals(argument, "CONFIRM", StringComparison.Ordinal))
            { error = "此操作会修改进度或结束本局，请明确输入 CONFIRM | This operation changes progression or ends the run; type CONFIRM"; return false; }
        }
        else if (spec.Kind != ArgumentKind.None)
        {
            if (string.IsNullOrEmpty(argument)) argument = spec.Kind == ArgumentKind.Toggle ? "on" : "Basic";
            var options = _options(spec.Kind);
            var exact = options.Where(x => string.Equals(x.Value, argument, StringComparison.Ordinal)).ToArray();
            if (exact.Length == 0) exact = options.Where(x => string.Equals(x.Value, argument, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (exact.Length == 1) argument = exact[0].Value;
            else if (exact.Length > 1) { error = "ID 大小写存在歧义，请选择精确 ID | Ambiguous ID casing; select an exact ID"; return false; }
            else
            {
                var aliases = options.Where(x => string.Equals(x.Zh, argument, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.En, argument, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (aliases.Length != 1)
                { error = aliases.Length > 1 ? "名称不唯一，请从提示选择内部 ID | Ambiguous name; select an ID" : "参数未找到，输入空格查看候选或执行 refresh | Argument not found; use suggestions or refresh"; return false; }
                argument = aliases[0].Value;
            }
        }
        call = new CommandCall(spec, argument);
        return true;
    }
}

