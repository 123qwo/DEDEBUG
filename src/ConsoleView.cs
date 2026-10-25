using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DEDEBUG;

internal sealed class ConsoleView
{
    private readonly DedebugPlugin _plugin;
    private readonly TMP_FontAsset _font;
    private GameObject _root, _suggestionPanel, _previousSelection, _noticeRoot;
    private TMP_InputField _input;
    private TMP_Text _outputText, _suggestionHeader, _featureStatus, _noticeText;
    private RectTransform _outputArea, _viewport, _content;
    private ScrollRect _scroll;
    private Button[] _rows;
    private TMP_Text[] _rowTexts;
    private Image[] _rowImages;
    private readonly ConsoleHistory _history = new ConsoleHistory();
    private List<Completion> _suggestions = new List<Completion>();
    private int _selected, _pageStart, _catalogRevision, _openedFrame, _outputVersion = -1;
    private float _suggestionHeight, _noticeExpires;
    private bool _layoutDirty, _forceBottom, _browsingHistory, _programmaticFill, _catalogRefreshing;
    private Vector2 _lastViewportSize;
    private float _measuredWidth = -1, _measuredFontSize = -1, _preferredTextHeight;
    private TMP_FontAsset _measuredFont;

    private ConsoleView(DedebugPlugin plugin, TMP_FontAsset font) { _plugin = plugin; _font = font; }
    internal static ConsoleView Create(DedebugPlugin plugin)
    {
        var fonts = UnityEngine.Object.FindObjectsOfType<TMP_Text>(true).Select(x => x.font).Where(x => x != null).Distinct().ToArray();
        var font = fonts.FirstOrDefault(x => x.HasCharacter('控', true, true)) ?? fonts.FirstOrDefault() ?? TMP_Settings.defaultFontAsset;
        if (font == null) return null;
        var view = new ConsoleView(plugin, font);
        view.Build(); view._root.SetActive(false);
        return view;
    }
    internal void Show()
    {
        _openedFrame = Time.frameCount;
        if (EventSystem.current == null)
        {
            var events = new GameObject("DEDEBUG_EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            events.transform.SetParent(_root.transform, false);
        }
        _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        _root.SetActive(true);
        _outputVersion = -1;
        _forceBottom = true;
        UpdateSuggestions();
        _plugin.StartCoroutine(FocusNextFrame());
    }
    internal void Hide()
    {
        _input.DeactivateInputField(); _root.SetActive(false);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_previousSelection != null && _previousSelection.activeInHierarchy ? _previousSelection : null);
    }
    internal void Destroy() { if (_root != null) UnityEngine.Object.Destroy(_root); if (_noticeRoot != null) UnityEngine.Object.Destroy(_noticeRoot); }
    private IEnumerator FocusNextFrame()
    {
        yield return null;
        if (_plugin.IsOpen && _input != null)
        {
            EventSystem.current?.SetSelectedGameObject(_input.gameObject);
            _input.ActivateInputField(); _input.caretPosition = _input.text.Length;
        }
    }
    internal void Tick()
    {
        if (_catalogRevision != _plugin.Catalog.Revision || _catalogRefreshing != _plugin.Catalog.Refreshing) UpdateSuggestions();
        if (_suggestionPanel.activeSelf && Input.mouseScrollDelta.y != 0 &&
            RectTransformUtility.RectangleContainsScreenPoint((RectTransform)_suggestionPanel.transform, Input.mousePosition))
            SelectSuggestion(Input.mouseScrollDelta.y > 0 ? -_rows.Length : _rows.Length);
        if (_input.isFocused)
        {
            bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (Input.GetKeyDown(KeyCode.Tab) && _suggestions.Count > 0) Fill(_suggestions[_selected].Fill);
            if (Input.GetKeyDown(KeyCode.UpArrow)) { if (control || _browsingHistory || _suggestions.Count == 0) History(-1); else SelectSuggestion(-1); }
            if (Input.GetKeyDown(KeyCode.DownArrow)) { if (control || _browsingHistory || _suggestions.Count == 0) History(1); else SelectSuggestion(1); }
        }
        if (_featureStatus.text != _plugin.FeatureStatus) _featureStatus.text = _plugin.FeatureStatus;
    }
    internal void FlushOutput()
    {
        if (_lastViewportSize != _viewport.rect.size) _layoutDirty = true;
        if (_outputVersion != _plugin.Output.Version || _layoutDirty || _forceBottom) RenderOutput();
    }
    private void History(int direction)
    {
        if (!_history.HasEntries) return;
        Fill(_history.Move(direction, _input.text), true);
    }
    private void Fill(string value, bool history = false)
    {
        _programmaticFill = true; _browsingHistory = history;
        try { _input.text = value; _input.caretPosition = value.Length; }
        finally { _programmaticFill = false; }
        _plugin.StartCoroutine(FocusNextFrame());
    }
    private void Submit()
    {
        string text = _input.text;
        if (string.IsNullOrWhiteSpace(text)) { _plugin.StartCoroutine(FocusNextFrame()); return; }
        if (!_plugin.Engine.Parse(text, out var call, out _) && _suggestions.Count > 0)
        {
            var tokens = CommandEngine.Tokenize(text, true, out _);
            var spec = tokens.Count > 0 ? _plugin.Engine.Find(tokens[0]) : null;
            // Enter must not silently add a destructive confirmation token.
            if (spec == null || spec.Kind != ArgumentKind.Confirm) { Fill(_suggestions[_selected].Fill); return; }
        }
        string historyText = call != null ? call.Canonical : text;
        _history.Record(historyText); _browsingHistory = false; _forceBottom = true;
        _plugin.Submit(text);
        _input.text = "";
        if (_plugin.IsOpen) _plugin.StartCoroutine(FocusNextFrame());
    }
    private void UpdateSuggestions()
    {
        _catalogRevision = _plugin.Catalog.Revision;
        _catalogRefreshing = _plugin.Catalog.Refreshing;
        _suggestions = string.IsNullOrWhiteSpace(_input.text) ? new List<Completion>() : _plugin.Engine.Complete(_input.text);
        _selected = Math.Max(0, Math.Min(_selected, _suggestions.Count - 1));
        RenderSuggestions();
    }
    private void SelectSuggestion(int delta)
    {
        if (_suggestions.Count == 0) return;
        _selected = (_selected + delta % _suggestions.Count + _suggestions.Count) % _suggestions.Count;
        RenderSuggestions();
    }
    private void RenderSuggestions()
    {
        _pageStart = (_selected / _rows.Length) * _rows.Length;
        bool visible = !string.IsNullOrWhiteSpace(_input.text);
        _suggestionPanel.SetActive(visible);
        float height = visible ? 34 + Math.Min(_rows.Length, _suggestions.Count) * 38 : 0;
        if (_suggestionHeight != height)
        {
            _suggestionHeight = height; Bottom(_suggestionPanel, 56, height, 0, 0);
            Stretch(_outputArea, 0, 44, 0, 56 + height); _layoutDirty = true;
        }
        _suggestionHeader.text = _suggestions.Count == 0 ? (_plugin.Catalog.Refreshing ? "参数目录加载中 | Loading argument catalog" : "无匹配指令或参数 | No matching command or argument") :
            "补全 | Completion  " + (_selected + 1) + " / " + _suggestions.Count + "    ↑↓ 选择 · Tab / 点击填入 · Enter 执行";
        for (int row = 0; row < _rows.Length; row++)
        {
            int index = _pageStart + row;
            _rows[row].gameObject.SetActive(index < _suggestions.Count);
            if (index >= _suggestions.Count) continue;
            _rowTexts[row].text = _suggestions[index].Display;
            _rowImages[row].color = index == _selected
                ? new Color(0.18f, 0.28f, 0.34f) : new Color(0.075f, 0.085f, 0.1f);
        }
    }
    private void RenderOutput()
    {
        _outputArea.ForceUpdateRectTransforms(); _viewport.ForceUpdateRectTransforms();
        float previousOffset = _content.anchoredPosition.y;
        float previousMaximum = Math.Max(0, _content.rect.height - _viewport.rect.height);
        bool follow = _forceBottom || previousMaximum <= 0 || previousOffset >= previousMaximum * 0.98f;
        string rendered = _plugin.Output.Render();
        bool textChanged = _outputText.text != rendered;
        if (textChanged) _outputText.text = rendered;
        _lastViewportSize = _viewport.rect.size;
        float width = Math.Max(100, _viewport.rect.width - 32);
        if (textChanged || width != _measuredWidth || _measuredFont != _outputText.font || _measuredFontSize != _outputText.fontSize)
        {
            _preferredTextHeight = _outputText.GetPreferredValues(rendered, width, 0).y + 28;
            _measuredWidth = width; _measuredFont = _outputText.font; _measuredFontSize = _outputText.fontSize;
        }
        float height = Math.Max(_viewport.rect.height, _preferredTextHeight);
        _content.sizeDelta = new Vector2(0, height);
        _content.ForceUpdateRectTransforms();
        if (follow) _content.anchoredPosition = new Vector2(0, Math.Max(0, height - _viewport.rect.height));
        else _content.anchoredPosition = new Vector2(0, Mathf.Clamp(previousOffset, 0, Math.Max(0, height - _viewport.rect.height)));
        _outputVersion = _plugin.Output.Version; _layoutDirty = false; _forceBottom = false;
    }
    internal void ShowNotice(string message, bool sticky)
    {
        if (_noticeRoot == null)
        {
            _noticeRoot = new GameObject("DEDEBUG_Notice", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(_noticeRoot);
            var canvas = _noticeRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32761;
            var scaler = _noticeRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            var panel = Box(_noticeRoot.transform, "Notice", new Color(0.18f, 0.11f, 0.09f, 0.98f));
            Fixed(panel, Vector2.one, Vector2.one, new Vector2(-44, -100), new Vector2(820, 210));
            var heading = Text(panel.transform, "DEDEBUG · 提示 | Notice", 21); Band(heading.gameObject, 8, 30, 16, 50);
            _noticeText = Text(panel.transform, "", 20); Stretch(_noticeText.rectTransform, 16, 46, 16, 14);
            _noticeText.textWrappingMode = TextWrappingModes.Normal; _noticeText.overflowMode = TextOverflowModes.Ellipsis;
            var close = Button(panel.transform, "×", () => _noticeRoot.SetActive(false), 22);
            Fixed(close.gameObject, Vector2.one, Vector2.one, new Vector2(-8, -6), new Vector2(32, 30));
        }
        _noticeText.text = message; _noticeRoot.SetActive(true);
        _noticeExpires = sticky ? float.PositiveInfinity : Time.unscaledTime + 8;
    }
    internal void TickNotice()
    { if (_noticeRoot != null && _noticeRoot.activeSelf && Time.unscaledTime >= _noticeExpires) _noticeRoot.SetActive(false); }
    private void Build()
    {
        _root = new GameObject("DEDEBUG_Console", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        UnityEngine.Object.DontDestroyOnLoad(_root);
        var canvas = _root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
        var scaler = _root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        var dim = Box(_root.transform, "Dim", new Color(0, 0, 0, 0.42f)); Stretch((RectTransform)dim.transform, 0, 0, 0, 0);
        var frame = Box(_root.transform, "Frame", new Color(0.35f, 0.4f, 0.43f)); Stretch((RectTransform)frame.transform, 24, 48, 24, 24);
        var panel = Box(frame.transform, "Panel", new Color(0.035f, 0.045f, 0.055f, 0.98f)); Stretch((RectTransform)panel.transform, 3, 3, 3, 3);
        var title = Text(panel.transform, "DEDEBUG · 输出 | Output", 21); Band(title.gameObject, 4, 34, 16, 1300);
        _featureStatus = Text(panel.transform, "", 16); Band(_featureStatus.gameObject, 4, 34, 580, 800);
        var controls = Text(panel.transform, "~ / Esc · ↑↓ 补全/历史 · Ctrl+↑↓ 历史", 17); Band(controls.gameObject, 4, 34, 1120, 60);
        var close = Button(panel.transform, "×", () => _plugin.SetOpen(false), 24); Fixed(close.gameObject, Vector2.one, Vector2.one, new Vector2(-8, -4), new Vector2(40, 34));

        var output = Box(panel.transform, "Output", new Color(0.02f, 0.027f, 0.035f)); _outputArea = (RectTransform)output.transform;
        Stretch(_outputArea, 0, 44, 0, 56);
        _scroll = output.AddComponent<ScrollRect>(); _scroll.horizontal = false; _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped; _scroll.scrollSensitivity = 40;
        var viewport = Box(output.transform, "Viewport", Color.clear); Stretch((RectTransform)viewport.transform, 0, 0, 0, 0);
        viewport.AddComponent<RectMask2D>(); _viewport = (RectTransform)viewport.transform;
        var content = new GameObject("Content", typeof(RectTransform)); content.transform.SetParent(viewport.transform, false);
        _content = (RectTransform)content.transform; _content.anchorMin = new Vector2(0, 1); _content.anchorMax = Vector2.one;
        _content.pivot = new Vector2(0.5f, 1); _content.sizeDelta = Vector2.zero;
        _outputText = Text(content.transform, "", 20); Stretch(_outputText.rectTransform, 16, 12, 16, 12);
        _outputText.alignment = TextAlignmentOptions.TopLeft; _outputText.textWrappingMode = TextWrappingModes.Normal;
        _scroll.viewport = _viewport; _scroll.content = _content;

        _input = InputBox(panel.transform, "> 输入指令 | Enter command"); Bottom(_input.gameObject, 0, 52, 0, 0);
        _input.onValueChanged.AddListener(value =>
        {
            if (Time.frameCount <= _openedFrame + 1 && (value.Contains("`") || value.Contains("~")))
            { _input.text = value.Replace("`", "").Replace("~", ""); return; }
            if (!_programmaticFill) _browsingHistory = false;
            _selected = 0; UpdateSuggestions();
        });
        _input.onSubmit.AddListener(_ => Submit());
        _input.onValidateInput += (text, index, c) => c == '\t' || c == '`' || c == '~' ? '\0' : c;

        _suggestionPanel = Box(panel.transform, "Suggestions", new Color(0.11f, 0.13f, 0.15f));
        _suggestionHeader = Text(_suggestionPanel.transform, "", 17); Band(_suggestionHeader.gameObject, 2, 28, 12, 12);
        _rows = new Button[5];
        _rowTexts = new TMP_Text[_rows.Length]; _rowImages = new Image[_rows.Length];
        for (int i = 0; i < _rows.Length; i++)
        {
            int row = i;
            _rows[i] = Button(_suggestionPanel.transform, "", () =>
            {
                int index = _pageStart + row;
                if (index < _suggestions.Count) { _selected = index; Fill(_suggestions[index].Fill); }
            }, 18);
            Band(_rows[i].gameObject, 32 + i * 38, 36, 3, 3);
            _rowTexts[i] = _rows[i].GetComponentInChildren<TMP_Text>();
            _rowImages[i] = _rows[i].GetComponent<Image>();
            _rowTexts[i].alignment = TextAlignmentOptions.MidlineLeft;
        }
    }
    private GameObject Box(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color; return go;
    }
    private TMP_Text Text(Transform parent, string value, float size)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>(); text.font = _font; text.fontSize = size; text.text = value;
        text.color = new Color(0.9f, 0.94f, 0.95f); text.raycastTarget = false; text.richText = false;
        text.alignment = TextAlignmentOptions.MidlineLeft; text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }
    private Button Button(Transform parent, string label, Action action, float size = 18)
    {
        var go = Box(parent, "Button", new Color(0.075f, 0.085f, 0.1f));
        var button = go.AddComponent<Button>(); button.targetGraphic = go.GetComponent<Image>(); button.onClick.AddListener(() => action());
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var text = Text(go.transform, label, size); text.alignment = TextAlignmentOptions.Center;
        text.overflowMode = TextOverflowModes.Ellipsis; Stretch(text.rectTransform, 10, 1, 10, 1);
        return button;
    }
    private TMP_InputField InputBox(Transform parent, string placeholder)
    {
        var go = Box(parent, "Input", new Color(0.075f, 0.085f, 0.1f));
        var field = go.AddComponent<TMP_InputField>(); field.targetGraphic = go.GetComponent<Image>();
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)); viewport.transform.SetParent(go.transform, false);
        Stretch((RectTransform)viewport.transform, 16, 3, 16, 3);
        var text = Text(viewport.transform, "", 22); Stretch(text.rectTransform, 0, 0, 0, 0); text.textWrappingMode = TextWrappingModes.NoWrap;
        var hint = Text(viewport.transform, placeholder, 21); Stretch(hint.rectTransform, 0, 0, 0, 0); hint.color = new Color(0.55f, 0.62f, 0.65f);
        field.textViewport = (RectTransform)viewport.transform; field.textComponent = (TextMeshProUGUI)text; field.placeholder = hint;
        field.lineType = TMP_InputField.LineType.SingleLine; field.pointSize = 22; field.customCaretColor = true; field.caretColor = Color.white;
        field.navigation = new Navigation { mode = Navigation.Mode.None };
        return field;
    }
    private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }
    private static void Band(GameObject go, float top, float height, float left, float right)
    { var r = (RectTransform)go.transform; r.anchorMin = new Vector2(0, 1); r.anchorMax = Vector2.one; r.offsetMin = new Vector2(left, -top - height); r.offsetMax = new Vector2(-right, -top); }
    private static void Bottom(GameObject go, float bottom, float height, float left, float right)
    { var r = (RectTransform)go.transform; r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(1, 0); r.offsetMin = new Vector2(left, bottom); r.offsetMax = new Vector2(-right, bottom + height); }
    private static void Fixed(GameObject go, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    { var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = anchor; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size; }
}
