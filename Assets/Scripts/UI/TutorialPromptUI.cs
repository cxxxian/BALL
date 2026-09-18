using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public enum TutorialTerminalMode
{
    Brief = 0,
    Compact = 1
}

public enum TutorialProtocolAnchor
{
    Bottom,
    Top,
    Skills,
    Combo,
    Center,
    SlotTop
}

/// <summary>Protocol Interface — 游戏系统校准 UI。</summary>
public class TutorialPromptUI : MonoBehaviour
{
    public static TutorialPromptUI Instance { get; private set; }

    private UIDocument _doc;
    private VisualElement _root;
    private VisualElement _card;
    private Label _progressLabel;
    private Label _titleLabel;
    private Label _descLabel;
    private Label _hintLabel;
    private Label _confirmLabel;
    private Button _startBtn;
    private Button _skipTopBtn;
    private VisualElement _skipOverlay;
    private Button _skipCancelBtn;
    private Button _skipConfirmBtn;

    private Action _onContinue;
    private Action _onSkip;
    private IVisualElementScheduledItem _hintPulse;
    private bool _bound;
    private bool _pendingSkipConfirm;

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

    private void OnDestroy()
    {
        _hintPulse?.Pause();
        if (Instance == this) Instance = null;
    }

    private void EnsureBuilt()
    {
        if (_card != null) return;
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
        if (tree == null)
        {
            Debug.LogError("[TutorialPromptUI] Missing Resources/UI/TutorialTerminal");
            return;
        }

        _doc.visualTreeAsset = tree;
        if (_doc.rootVisualElement != null)
            BindFromRoot(_doc.rootVisualElement);
        else
            StartCoroutine(BindWhenReady());
    }

    private IEnumerator BindWhenReady()
    {
        for (int i = 0; i < 40; i++)
        {
            yield return null;
            if (_doc?.rootVisualElement != null)
            {
                BindFromRoot(_doc.rootVisualElement);
                if (_card != null) yield break;
            }
        }
    }

    private void BindFromRoot(VisualElement rootVe)
    {
        _root = rootVe.Q<VisualElement>("TutorialRoot") ?? rootVe;
        _card = rootVe.Q<VisualElement>("ProtocolCard");
        _progressLabel = rootVe.Q<Label>("ProtocolProgress");
        _titleLabel = rootVe.Q<Label>("ProtocolTitle");
        _descLabel = rootVe.Q<Label>("ProtocolDescription");
        _hintLabel = rootVe.Q<Label>("ProtocolHint");
        _confirmLabel = rootVe.Q<Label>("ProtocolConfirm");
        _startBtn = rootVe.Q<Button>("BtnProtocolStart");
        _skipTopBtn = rootVe.Q<Button>("BtnSkipTop");
        _skipOverlay = rootVe.Q<VisualElement>("SkipConfirmOverlay");
        _skipCancelBtn = rootVe.Q<Button>("BtnSkipCancel");
        _skipConfirmBtn = rootVe.Q<Button>("BtnSkipConfirm");

        if (_card == null) return;

        if (!_bound)
        {
            _bound = true;
            _startBtn.clicked += OnStartClicked;
            _skipTopBtn.clicked += OnSkipTopClicked;
            _skipCancelBtn.clicked += OnSkipCancel;
            _skipConfirmBtn.clicked += OnSkipConfirm;
        }

        _root.pickingMode = PickingMode.Ignore;
        _card.pickingMode = PickingMode.Position;
        _skipTopBtn.pickingMode = PickingMode.Position;
    }

    private void OnStartClicked() => _onContinue?.Invoke();

    private void OnSkipTopClicked()
    {
        _pendingSkipConfirm = true;
        if (_skipOverlay != null)
            _skipOverlay.AddToClassList("skip-overlay-visible");
    }

    private void OnSkipCancel()
    {
        _pendingSkipConfirm = false;
        _skipOverlay?.RemoveFromClassList("skip-overlay-visible");
    }

    private void OnSkipConfirm()
    {
        _pendingSkipConfirm = false;
        _skipOverlay?.RemoveFromClassList("skip-overlay-visible");
        _onSkip?.Invoke();
    }

    public void ShowProtocol(
        int stepIndex,
        int stepTotal,
        string title,
        string description,
        string inputHint,
        TutorialTerminalMode mode,
        TutorialProtocolAnchor anchor,
        bool showContinue,
        Action onContinue = null,
        Action onSkip = null)
    {
        EnsureBuilt();
        if (_card == null) return;

        _onContinue = onContinue;
        _onSkip = onSkip;

        if (_progressLabel != null)
            _progressLabel.text = $"PROTOCOL {stepIndex:D2} / {stepTotal:D2}";
        if (_titleLabel != null) _titleLabel.text = title ?? string.Empty;
        if (_descLabel != null)
        {
            _descLabel.text = description ?? string.Empty;
            _descLabel.style.display = string.IsNullOrEmpty(description)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        bool hasHint = !string.IsNullOrEmpty(inputHint);
        if (_hintLabel != null)
        {
            _hintLabel.text = hasHint ? inputHint : string.Empty;
            _hintLabel.style.display = hasHint ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (_startBtn != null)
            _startBtn.style.display = showContinue ? DisplayStyle.Flex : DisplayStyle.None;

        if (_confirmLabel != null)
        {
            _confirmLabel.text = string.Empty;
            _confirmLabel.RemoveFromClassList("protocol-confirm-show");
        }

        ApplyMode(mode);
        ApplyAnchor(anchor);

        _card.style.display = DisplayStyle.Flex;
        _root.style.display = DisplayStyle.Flex;

        StartHintPulse(hasHint && !showContinue);
    }

    /// <summary>兼容旧 Narrate 调用。</summary>
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
        int idx = 1;
        int total = TutorialProtocolCopy.TotalSteps;
        if (!string.IsNullOrEmpty(step))
        {
            var parts = step.Split('/');
            foreach (var p in parts)
            {
                var t = p.Trim();
                if (t.Length >= 2 && char.IsDigit(t[t.Length - 2]) && char.IsDigit(t[t.Length - 1]))
                {
                    int.TryParse(t.Substring(t.Length - 2), out idx);
                    break;
                }
            }
        }

        string title = line;
        string desc = string.Empty;
        if (!string.IsNullOrEmpty(line) && line.StartsWith("> "))
        {
            var nl = line.IndexOf('\n');
            if (nl > 0)
            {
                title = line.Substring(2, nl - 2).Trim();
                desc = line.Substring(nl + 1).Replace("> ", "").Trim();
            }
            else
                title = line.TrimStart('>', ' ');
        }

        ShowProtocol(
            idx,
            total,
            title,
            desc,
            keyHint,
            terminalMode,
            TutorialProtocolAnchor.Bottom,
            showContinue,
            onContinue,
            onSkip);
    }

    private void ApplyMode(TutorialTerminalMode mode)
    {
        if (_card == null) return;

        bool action = mode == TutorialTerminalMode.Compact;
        _card.EnableInClassList("protocol-card-action", action);
        _card.pickingMode = action ? PickingMode.Ignore : PickingMode.Position;
    }

    private void ApplyAnchor(TutorialProtocolAnchor anchor)
    {
        if (_card == null) return;

        _card.RemoveFromClassList("protocol-anchor-bottom");
        _card.RemoveFromClassList("protocol-anchor-top");
        _card.RemoveFromClassList("protocol-anchor-skills");
        _card.RemoveFromClassList("protocol-anchor-combo");
        _card.RemoveFromClassList("protocol-anchor-center");
        _card.RemoveFromClassList("protocol-anchor-slot-top");

        switch (anchor)
        {
            case TutorialProtocolAnchor.Top:
                _card.AddToClassList("protocol-anchor-top");
                break;
            case TutorialProtocolAnchor.Skills:
                _card.AddToClassList("protocol-anchor-skills");
                break;
            case TutorialProtocolAnchor.Combo:
                _card.AddToClassList("protocol-anchor-combo");
                break;
            case TutorialProtocolAnchor.SlotTop:
                _card.AddToClassList("protocol-anchor-slot-top");
                break;
            case TutorialProtocolAnchor.Center:
                _card.AddToClassList("protocol-anchor-center");
                break;
            default:
                _card.AddToClassList("protocol-anchor-bottom");
                break;
        }
    }

    public void ShowSuccess(string message = "确认")
    {
        StopHintPulse();
        if (_confirmLabel != null)
        {
            _confirmLabel.text = message;
            _confirmLabel.AddToClassList("protocol-confirm-show");
        }

        if (_startBtn != null)
            _startBtn.style.display = DisplayStyle.None;
    }

    public void Hide()
    {
        StopHintPulse();
        if (_root != null) _root.style.display = DisplayStyle.None;
        if (_card != null) _card.style.display = DisplayStyle.None;
        _skipOverlay?.RemoveFromClassList("skip-overlay-visible");
        _onContinue = null;
        _onSkip = null;
    }

    private void StartHintPulse(bool enable)
    {
        StopHintPulse();
        if (!enable || _hintLabel == null) return;

        float t = 0f;
        _hintPulse = _hintLabel.schedule.Execute(() =>
        {
            t += 0.04f;
            _hintLabel.EnableInClassList("protocol-hint-pulse", Mathf.Sin(t * 8f) > 0f);
        }).Every(40);
    }

    private void StopHintPulse()
    {
        _hintPulse?.Pause();
        _hintPulse = null;
        _hintLabel?.RemoveFromClassList("protocol-hint-pulse");
    }
}
