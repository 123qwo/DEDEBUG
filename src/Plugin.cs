using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UI;
using Utilities;

namespace DEDEBUG;

[BepInPlugin(Guid, "DICEOMANCER DEDEBUG", Version)]
[DefaultExecutionOrder(-32000)]
public sealed class DedebugPlugin : BepInEx.Unity.Mono.BaseUnityPlugin
{
    public const string Guid = "diceomancer.dedebug";
    public const string Version = "0.1.6";
    internal static DedebugPlugin Instance;
    internal static bool InputCaptured => Instance != null && (Instance.IsOpen || Time.frameCount == Instance._lastCloseFrame);
    internal bool IsOpen { get; private set; }
    internal bool InputReady { get; private set; }
    internal string FeatureStatus => InputReady ? (Capture.Enabled ? "输入隔离就绪 | Input isolated" : "输出捕获已降级 | Output capture limited") : "输入隔离不可用 | Input isolation unavailable";
    internal CommandOutput Output { get; private set; }
    internal NativeOutputCapture Capture { get; private set; }
    internal NativeCatalog Catalog { get; private set; }
    internal CommandEngine Engine { get; private set; }
    private ConfigEntry<KeyCode> _key;
    private ConsoleView _view;
    private NativeExecutor _executor;
    private Harmony _inputHarmony, _outputHarmony;
    private readonly List<string> _patchIssues = new List<string>();
    private static readonly MethodInfo NativeHelper = AccessTools.Method(typeof(World.HelperManager), "ShowHelperContent", new[] { typeof(string) });
    private int _lastCloseFrame = -1;
    private GameManager _pauseOwner;
    private bool _wasPaused, _destroying, _sendingNotice;
    private UIShowingMenuState _oldUi;

    private void Awake()
    {
        Instance = this;
        _key = Config.Bind("Console", "ToggleKey", KeyCode.BackQuote, "Toggle console; BackQuote is the ` / ~ physical key.");
        Output = new CommandOutput();
        Capture = new NativeOutputCapture(Output, text => { if (!_sendingNotice) Notify(text, false); });
        Catalog = new NativeCatalog(); _executor = new NativeExecutor(this);
        Engine = new CommandEngine(Catalog.Get, CommandDefinitions.Create(_executor));
        _inputHarmony = new Harmony(Guid + ".input");
        _outputHarmony = new Harmony(Guid + ".output");
        InputReady = PatchGroup(_inputHarmony, "输入隔离 | Input isolation", typeof(ConsoleGameInputIsolation), typeof(ConsolePauseGuard), typeof(ConsoleMapInputGuard));
        Capture.Enabled = PatchGroup(_outputHarmony, "原生输出捕获 | Native output capture", typeof(ConsoleCommandOwnership), typeof(ConsoleNativeHelperOutput));
        Application.logMessageReceived += OnCommandLog;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        Logger.LogInfo("DEDEBUG " + Version + " loaded; input isolation=" + InputReady + "; press ` / ~.");
    }
    private bool PatchGroup(Harmony harmony, string feature, params Type[] patches)
    {
        try { foreach (var patch in patches) harmony.CreateClassProcessor(patch).Patch(); return true; }
        catch (Exception e)
        {
            harmony.UnpatchSelf();
            string message = feature + "加载失败 | Failed to load: " + e.GetBaseException().Message;
            _patchIssues.Add(message); Logger.LogError(message); return false;
        }
    }
    private void Update()
    {
        try { Capture.Tick(); }
        catch (Exception e)
        {
            Capture.Disable();
            Notify("输出捕获已降级 | Output capture limited: " + e.Message, sticky: true);
        }
        if (Input.GetKeyDown(_key.Value)) SetOpen(!IsOpen);
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) SetOpen(false);
        _view?.TickNotice();
        if (IsOpen) _view?.Tick();
    }
    private void LateUpdate() { if (IsOpen) _view?.FlushOutput(); }
    internal void SetOpen(bool open)
    {
        if (open == IsOpen || _destroying) return;
        if (open)
        {
            if (!InputReady)
            {
                Notify("输入隔离不可用，无法打开控制台。请查看插件检查结果。 | Console disabled because input isolation failed.\n" + string.Join("\n", _patchIssues), sticky: true);
                return;
            }
            if (_view == null) _view = ConsoleView.Create(this);
            if (_view == null) { Notify("游戏字体尚未加载，请稍后重试 | Game font is not ready; retry shortly"); return; }
            IsOpen = true; _pauseOwner = GameManager.Instance;
            _wasPaused = _pauseOwner != null && _pauseOwner.GetGameIsPaused();
            _oldUi = UIManager.Instance != null ? UIManager.Instance.GetUIShowingMenuState() : UIShowingMenuState.NoUIShowing;
            if (_pauseOwner != null) _pauseOwner.PauseGame();
            _view.Show(); RefreshCatalog();
            if (_patchIssues.Count > 0) Notify(string.Join("\n", _patchIssues), sticky: true);
        }
        else
        {
            IsOpen = false; _lastCloseFrame = Time.frameCount; _view?.Hide();
            if (_pauseOwner != null && ReferenceEquals(GameManager.Instance, _pauseOwner) &&
                (!_wasPaused || (UIManager.Instance != null && UIManager.Instance.GetUIShowingMenuState() != _oldUi)))
                _pauseOwner.TryToUnpauseGame();
            _pauseOwner = null;
        }
    }
    internal void RefreshCatalog(bool force = false)
    {
        if (!force && !Catalog.NeedsRefresh) return;
        if (!Catalog.Refreshing) StartCoroutine(RefreshSafely(Capture.Direct));
        else Write("目录正在刷新 | Catalog refresh already in progress");
    }
    private IEnumerator RefreshSafely(OutputEntry entry)
    {
        if (entry != null) { entry.CatalogPending = true; Output.Touch(); }
        int revisionBefore = Catalog.Revision;
        var routine = Catalog.Refresh(text => Output.Add(entry, text));
        try
        {
            while (true)
            {
                object current;
                try { if (!routine.MoveNext()) break; current = routine.Current; }
                catch (Exception e) { if (entry != null) Notify("刷新目录失败 | Catalog refresh error: " + e.Message); break; }
                yield return current;
            }
        }
        finally
        {
            try { (routine as IDisposable)?.Dispose(); }
            finally
            {
                if (Catalog.Revision != revisionBefore) Engine.InvalidateOptions();
                if (entry != null) { entry.CatalogPending = false; Output.Touch(); }
            }
        }
    }
    internal void Submit(string text)
    {
        if (!InputReady) { Notify("输入隔离未就绪，无法执行指令 | Input isolation is not ready"); return; }
        bool valid = Engine.Parse(text, out var call, out var error);
        var entry = Output.Begin(valid ? call.Canonical : text);
        if (!valid) { entry.Submitted = true; Output.Touch(); Notify(error); return; }
        Capture.Direct = entry;
        try { _executor.Execute(call); }
        catch (Exception e) { Notify(e.Message); }
        finally { entry.Submitted = true; Capture.Direct = null; Output.Touch(); }
    }
    internal void Write(string text) => Output.Add(Capture.Direct, text);
    internal void ClearOutput() { Output.Clear(Capture.Direct); Write("Output cleared."); }
    internal void Notify(string text, bool preferNative = true, bool sticky = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (_view == null) _view = ConsoleView.Create(this);
        _view?.ShowNotice(text, sticky);
        if (preferNative && !_sendingNotice && NativeHelper != null && World.HelperManager.Instance != null)
        {
            _sendingNotice = true;
            try { NativeHelper.Invoke(World.HelperManager.Instance, new object[] { text }); }
            catch { /* The separate notice stays visible if the native helper is unavailable. */ }
            finally { _sendingNotice = false; }
        }
    }
    private void OnCommandLog(string text, string trace, LogType type)
    { try { Capture.UnityLog(text, trace, type); } catch { /* Display observers must not interrupt game logging. */ } }
    private void InvalidateCatalog() { Catalog.Invalidate(); Engine.InvalidateOptions(); }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => InvalidateCatalog();
    private void OnActiveSceneChanged(Scene previous, Scene current) { InvalidateCatalog(); if (IsOpen) SetOpen(false); }
    private void OnLocaleChanged(UnityEngine.Localization.Locale locale) { InvalidateCatalog(); if (IsOpen) RefreshCatalog(); }
    private void OnDestroy()
    {
        try { if (IsOpen) SetOpen(false); } catch { }
        _destroying = true;
        Application.logMessageReceived -= OnCommandLog;
        SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        _inputHarmony?.UnpatchSelf(); _outputHarmony?.UnpatchSelf(); _view?.Destroy();
        if (Instance == this) Instance = null;
    }
}

[HarmonyPatch(typeof(GameManager), "TryToUnpauseGame")]
internal static class ConsolePauseGuard
{
    [HarmonyPrefix] private static bool Prefix() => DedebugPlugin.Instance == null || !DedebugPlugin.Instance.IsOpen;
}
[HarmonyPatch(typeof(World.MapPlayerTracker), "SelectNode")]
internal static class ConsoleMapInputGuard
{
    [HarmonyPrefix] private static bool Prefix() => !DedebugPlugin.InputCaptured;
}
