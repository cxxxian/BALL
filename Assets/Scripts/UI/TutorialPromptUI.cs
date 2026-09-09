using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public enum TutorialTerminalMode
{
    /// <summary>说明拍：右侧侧栏 + 右侧窄遮罩防误点。</summary>
    Brief = 0,
    /// <summary>实战拍：右侧侧栏，不挡台面，遮罩关闭。</summary>
    Compact = 1
}

/// <summary>赛博终端旁白：右侧空白区侧栏，门控后消失。</summary>
public class TutorialPromptUI : MonoBehaviour
{
    public static TutorialPromptUI Instance { get; private set; }

    private UIDocument _doc;
    private VisualElement _root;
    private VisualElement _dim;
    private VisualElement _frame;
    private Label _titleLabel;
    private Label _stepLabel;
    private Label _lineLabel;
    private Label _keyLabel;
    private Label _statusLabel;
    private Button _continueBtn;
    private Button _skipBtn;
    private Action _onContinue;
    private Action _onSkip;
    private IVisualElementScheduledItem _pulse;
    private Coroutine _typeCo;
    private string _fullLine = string.Empty;
    private bool _bound;
    private bool _typewriterDone = true;
    private bool _showContinue;
    private TutorialTerminalMode _mode = TutorialTerminalMode.Brief;

    public bool IsTypewriterDone => _typewriterDone;

    public static TutorialPromptUI Ensure()
    {
        if (Instance != null)
        {
            Instance.EnsureBuilt();
            return Instance;
        }

        var go = new GameObject("TutorialPromptUI");
        var ui = go.AddComponent<TutorialPromptUI>();
        ui.EnsureBuilt();
        return ui;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start() => EnsureBuilt();

    private void OnDestroy()
    {
        StopTypewriter();
        _pulse?.Pause();
        if (Instance == this) Instance = null;
    }

    private void EnsureBuilt()
    {
        if (_frame != null) return;
        BuildUi();
        Hide();
    }

    private void BuildUi()
    {
        _doc = gameObject.GetComponent<UIDocument>();
        if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();

        var settings = Resources.Load<PanelSettings>("UI/TutorialPanelSettings");
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(720, 1280);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
        }

        var runtimeSettings = Instantiate(settings);
        runtimeSettings.sortingOrder = 520;
        _doc.panelSettings = runtimeSettings;

        var tree = Resources.Load<VisualTreeAsset>("UI/TutorialTerminal");
        if (tree != null)
        {
            _doc.visualTreeAsset = tree;
            if (_doc.rootVisualElement != null)
                BindFromRoot(_doc.rootVisualElement);
            else
                StartCoroutine(BindWhenReady());
        }
        else
        {
            Debug.LogWarning("[TutorialPromptUI] Resources/UI/TutorialTerminal missing — using fallback terminal.");
            BuildFallback();
        }
    }

    private IEnumerator BindWhenReady()
    {
        for (int i = 0; i < 30; i++)
        {
            yield return null;
            if (_doc != null && _doc.rootVisualElement != null)
            {
                BindFromRoot(_doc.rootVisualElement);
                if (_frame != null) yield break;
            }
        }

        Debug.LogWarning("[TutorialPromptUI] Panel root missing — fallback build.");
        BuildFallback();
    }

    private void BindFromRoot(VisualElement rootVe)
    {
        _root = rootVe.Q<VisualElement>("TutorialRoot") ?? rootVe;
        _dim = rootVe.Q<VisualElement>("TutorialDim");
        _frame = rootVe.Q<VisualElement>("TerminalFrame");
        _titleLabel = rootVe.Q<Label>("TerminalTitle");
        _stepLabel = rootVe.Q<Label>("TerminalStep");
        _lineLabel = rootVe.Q<Label>("TerminalLine");
        _keyLabel = rootVe.Q<Label>("TerminalKey");
        _statusLabel = rootVe.Q<Label>("TerminalStatus");
        _continueBtn = rootVe.Q<Button>("BtnTerminalContinue");
        _skipBtn = rootVe.Q<Button>("BtnTerminalSkip");

        if (_frame == null)
        {
            BuildFallback();
            return;
        }

        if (!_bound)
        {
            _bound = true;
            if (_continueBtn != null)
                _continueBtn.clicked += OnContinueClicked;
            if (_skipBtn != null)
                _skipBtn.clicked += OnSkipClicked;
        }

        if (_root != null) _root.pickingMode = PickingMode.Ignore;
        if (_frame != null) _frame.pickingMode = PickingMode.Position;
    }

    private void OnContinueClicked()
    {
        // 打字未完：只加速出全文，不推进节拍
        if (!_typewriterDone)
        {
            FinishTypewriterNow();
            RefreshContinueEnabled();
            return;
        }

        _onContinue?.Invoke();
    }

    private void OnSkipClicked()
    {
        FinishTypewriterNow();
        _onSkip?.Invoke();
    }

    private void BuildFallback()
    {
        if (_doc == null) return;
        var root = _doc.rootVisualElement;
        if (root == null) return;
        root.Clear();
        _bound = false;

        _root = new VisualElement { name = "TutorialRoot" };
        _root.AddToClassList("tutorial-root");
        _root.style.position = Position.Absolute;
        _root.style.left = 0;
        _root.style.right = 0;
        _root.style.top = 0;
        _root.style.bottom = 0;
        _root.style.alignItems = Align.FlexEnd;
        _root.style.justifyContent = Justify.FlexStart;
        _root.style.paddingTop = Length.Percent(10);
        _root.style.paddingRight = 8;
        _root.pickingMode = PickingMode.Ignore;

        _dim = new VisualElement { name = "TutorialDim" };
        _dim.AddToClassList("tutorial-dim");
        _dim.style.position = Position.Absolute;
        _dim.style.left = StyleKeyword.Auto;
        _dim.style.right = 0;
        _dim.style.top = 0;
        _dim.style.bottom = 0;
        _dim.style.width = Length.Percent(34);
        _dim.style.maxWidth = 320;
        _dim.style.backgroundColor = new Color(10 / 255f, 10 / 255f, 15 / 255f, 0.35f);

        _frame = new VisualElement { name = "TerminalFrame" };
        _frame.AddToClassList("terminal-frame");
        _frame.style.width = Length.Percent(30);
        _frame.style.minWidth = 220;
        _frame.style.maxWidth = 300;
        _frame.style.backgroundColor = new Color(3 / 255f, 7 / 255f, 18 / 255f, 0.94f);
        _frame.style.borderTopWidth = 1;
        _frame.style.borderBottomWidth = 1;
        _frame.style.borderLeftWidth = 1;
        _frame.style.borderRightWidth = 1;
        _frame.style.borderTopColor = Color.cyan;
        _frame.style.borderBottomColor = new Color(1f, 0f, 1f, 0.55f);
        _frame.style.borderLeftColor = Color.cyan;
        _frame.style.borderRightColor = Color.cyan;
        _frame.style.paddingTop = 8;
        _frame.style.paddingBottom = 8;
        _frame.style.paddingLeft = 12;
        _frame.style.paddingRight = 12;
        _frame.style.flexDirection = FlexDirection.Column;
        _frame.pickingMode = PickingMode.Position;

        _titleLabel = MakeLabel(11, new Color(1f, 1f, 0f), true);
        _stepLabel = MakeLabel(11, Color.cyan, true);
        _lineLabel = MakeLabel(15, Color.white, true);
        _lineLabel.style.whiteSpace = WhiteSpace.Normal;
        _keyLabel = MakeLabel(13, Color.cyan, true);
        _statusLabel = MakeLabel(11, new Color(0.6f, 0.64f, 0.69f), false);

        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.Add(_titleLabel);
        header.Add(_stepLabel);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.FlexEnd;
        row.style.marginTop = 8;

        _skipBtn = new Button { text = "ABORT_SKIP" };
        _continueBtn = new Button { text = "ACK_CONTINUE" };
        StylePrimary(_continueBtn);
        StyleGhost(_skipBtn);
        _continueBtn.clicked += OnContinueClicked;
        _skipBtn.clicked += OnSkipClicked;
        _bound = true;
        row.Add(_skipBtn);
        row.Add(_continueBtn);

        _frame.Add(header);
        _frame.Add(_lineLabel);
        _frame.Add(_keyLabel);
        _frame.Add(_statusLabel);
        _frame.Add(row);
        _root.Add(_dim);
        _root.Add(_frame);
        root.Add(_root);
    }

    private static Label MakeLabel(float size, Color c, bool bold)
    {
        var l = new Label();
        l.style.fontSize = size;
        l.style.color = c;
        if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
        return l;
    }

    private static void StylePrimary(Button btn)
    {
        btn.style.height = 30;
        btn.style.marginLeft = 8;
        btn.style.backgroundColor = Color.cyan;
        btn.style.color = Color.black;
        btn.style.unityFontStyleAndWeight = FontStyle.Bold;
    }

    private static void StyleGhost(Button btn)
    {
        btn.style.height = 30;
        btn.style.backgroundColor = Color.clear;
        btn.style.color = new Color(0.6f, 0.64f, 0.69f);
        btn.style.borderTopWidth = 1;
        btn.style.borderBottomWidth = 1;
        btn.style.borderLeftWidth = 1;
        btn.style.borderRightWidth = 1;
        btn.style.borderTopColor = new Color(0.6f, 0.64f, 0.69f, 0.5f);
        btn.style.borderBottomColor = new Color(0.6f, 0.64f, 0.69f, 0.5f);
        btn.style.borderLeftColor = new Color(0.6f, 0.64f, 0.69f, 0.5f);
        btn.style.borderRightColor = new Color(0.6f, 0.64f, 0.69f, 0.5f);
    }

    public void ShowNarration(
        string step,
        string line,
        string keyHint,
        bool waitingForInput,
        bool showContinue,
        bool showSkip,
        TutorialTerminalMode terminalMode = TutorialTerminalMode.Brief,
        Action onContinue = null,
        Action onSkip = null)
    {
        EnsureBuilt();
        if (_frame == null)
        {
            StartCoroutine(ShowWhenReady(step, line, keyHint, waitingForInput, showContinue, showSkip, terminalMode, onContinue, onSkip));
            return;
        }

        ApplyNarration(step, line, keyHint, waitingForInput, showContinue, showSkip, terminalMode, onContinue, onSkip);
    }

    private IEnumerator ShowWhenReady(
        string step, string line, string keyHint, bool waitingForInput,
        bool showContinue, bool showSkip, TutorialTerminalMode terminalMode,
        Action onContinue, Action onSkip)
    {
        for (int i = 0; i < 45; i++)
        {
            yield return null;
            if (_frame != null) break;
            if (_doc != null && _doc.rootVisualElement != null && _frame == null)
                BindFromRoot(_doc.rootVisualElement);
        }

        if (_frame == null) BuildFallback();
        ApplyNarration(step, line, keyHint, waitingForInput, showContinue, showSkip, terminalMode, onContinue, onSkip);
    }

    private void ApplyNarration(
        string step,
        string line,
        string keyHint,
        bool waitingForInput,
        bool showContinue,
        bool showSkip,
        TutorialTerminalMode terminalMode,
        Action onContinue,
        Action onSkip)
    {
        _onContinue = onContinue;
        _onSkip = onSkip;
        _showContinue = showContinue;
        _mode = terminalMode;

        if (_titleLabel != null) _titleLabel.text = "NODE://CALIBRATION";
        if (_stepLabel != null) _stepLabel.text = string.IsNullOrEmpty(step) ? "SEQ" : step;

        ApplyTerminalMode(terminalMode);

        _fullLine = line ?? string.Empty;
        StopTypewriter();
        _typewriterDone = string.IsNullOrEmpty(_fullLine);
        if (_lineLabel != null && !_typewriterDone)
            _typeCo = StartCoroutine(Typewriter(_fullLine));
        else if (_lineLabel != null)
            _lineLabel.text = _fullLine;

        bool hasKey = !string.IsNullOrEmpty(keyHint);
        if (_keyLabel != null)
        {
            _keyLabel.text = hasKey ? $"▸ INPUT  {keyHint}" : string.Empty;
            _keyLabel.style.display = hasKey ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (_statusLabel != null)
        {
            bool hideStatus = terminalMode == TutorialTerminalMode.Compact;
            _statusLabel.style.display = hideStatus ? DisplayStyle.None : DisplayStyle.Flex;
            if (!hideStatus)
            {
                _statusLabel.text = waitingForInput
                    ? "STATUS: WAITING_FOR_INPUT_"
                    : (showContinue ? "STATUS: AWAIT_ACK" : "STATUS: OK");
                _statusLabel.style.color = waitingForInput
                    ? new Color(1f, 1f, 0f, 0.95f)
                    : new Color(0.61f, 0.64f, 0.69f);
            }
        }

        if (_continueBtn != null)
            _continueBtn.style.display = showContinue ? DisplayStyle.Flex : DisplayStyle.None;
        if (_skipBtn != null)
            _skipBtn.style.display = showSkip ? DisplayStyle.Flex : DisplayStyle.None;

        RefreshContinueEnabled();

        if (_frame != null) _frame.style.display = DisplayStyle.Flex;
        if (_root != null) _root.style.display = DisplayStyle.Flex;

        StartKeyPulse(waitingForInput && hasKey);
    }

    private void ApplyTerminalMode(TutorialTerminalMode mode)
    {
        if (_root != null)
        {
            _root.EnableInClassList("tutorial-root-compact", mode == TutorialTerminalMode.Compact);
            _root.EnableInClassList("tutorial-root-brief", mode == TutorialTerminalMode.Brief);
        }

        if (_frame != null)
        {
            _frame.EnableInClassList("terminal-frame-compact", mode == TutorialTerminalMode.Compact);
            _frame.EnableInClassList("terminal-frame-brief", mode == TutorialTerminalMode.Brief);
            // 实战窄条：除跳过外点击穿透，不挡瞄准/挡板
            _frame.pickingMode = mode == TutorialTerminalMode.Compact
                ? PickingMode.Ignore
                : PickingMode.Position;
        }

        if (_skipBtn != null)
            _skipBtn.pickingMode = PickingMode.Position;

        if (_continueBtn != null)
            _continueBtn.pickingMode = PickingMode.Position;

        if (_dim == null) return;

        if (mode == TutorialTerminalMode.Compact)
        {
            _dim.style.display = DisplayStyle.None;
            _dim.pickingMode = PickingMode.Ignore;
        }
        else
        {
            _dim.style.display = DisplayStyle.Flex;
            _dim.pickingMode = PickingMode.Position;
            _dim.EnableInClassList("tutorial-dim-full", false);
        }
    }

    private void RefreshContinueEnabled()
    {
        if (_continueBtn == null) return;
        bool ready = !_showContinue || _typewriterDone;
        _continueBtn.SetEnabled(ready);
        if (_showContinue && !_typewriterDone && _statusLabel != null)
            _statusLabel.text = "STATUS: PRINTING_… (CLICK TO SKIP TYPE)";
    }

    public void ShowSuccess(string message = "STATUS: SYNC_OK ✓")
    {
        FinishTypewriterNow();
        StopKeyPulse();
        if (_statusLabel != null)
        {
            _statusLabel.text = message;
            _statusLabel.style.color = new Color(0f, 1f, 0.45f);
        }
        if (_continueBtn != null)
            _continueBtn.style.display = DisplayStyle.None;
    }

    public void Hide()
    {
        StopTypewriter();
        StopKeyPulse();
        if (_root != null) _root.style.display = DisplayStyle.None;
        if (_frame != null) _frame.style.display = DisplayStyle.None;
        if (_dim != null)
        {
            _dim.style.display = DisplayStyle.None;
            _dim.pickingMode = PickingMode.Ignore;
        }
        _onContinue = null;
        _onSkip = null;
    }

    private IEnumerator Typewriter(string text)
    {
        if (_lineLabel == null)
        {
            _typewriterDone = true;
            yield break;
        }

        _typewriterDone = false;
        RefreshContinueEnabled();
        _lineLabel.text = string.Empty;
        for (int i = 0; i < text.Length; i++)
        {
            _lineLabel.text = text.Substring(0, i + 1) + (i < text.Length - 1 ? "▌" : string.Empty);
            yield return new WaitForSecondsRealtime(0.01f);
        }

        _lineLabel.text = text;
        _typeCo = null;
        _typewriterDone = true;
        RefreshContinueEnabled();
        if (_showContinue && _statusLabel != null)
            _statusLabel.text = "STATUS: AWAIT_ACK";
    }

    private void FinishTypewriterNow()
    {
        StopTypewriter();
        _typewriterDone = true;
        if (_lineLabel != null) _lineLabel.text = _fullLine;
    }

    private void StopTypewriter()
    {
        if (_typeCo != null)
        {
            StopCoroutine(_typeCo);
            _typeCo = null;
        }
    }

    private void StartKeyPulse(bool enable)
    {
        StopKeyPulse();
        if (!enable || _keyLabel == null) return;
        float t = 0f;
        _pulse = _keyLabel.schedule.Execute(() =>
        {
            t += 0.1f;
            float a = 0.5f + 0.5f * (0.5f + 0.5f * Mathf.Sin(t * 7f));
            _keyLabel.style.opacity = a;
            _keyLabel.EnableInClassList("terminal-key-pulse", Mathf.Sin(t * 7f) > 0f);
        }).Every(40);
    }

    private void StopKeyPulse()
    {
        _pulse?.Pause();
        _pulse = null;
        if (_keyLabel != null)
        {
            _keyLabel.style.opacity = 1f;
            _keyLabel.RemoveFromClassList("terminal-key-pulse");
        }
    }
}
