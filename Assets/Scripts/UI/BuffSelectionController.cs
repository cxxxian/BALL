using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public enum SlotTutorialGate
{
    /// <summary>无教学限制（正式局）。</summary>
    None = 0,
    /// <summary>全部锁死（说明拍）。</summary>
    Locked = 1,
    /// <summary>仅允许首次开转拉杆。</summary>
    SpinOnly = 2,
    /// <summary>仅允许点轮 + 重转拉杆，不可领取。</summary>
    RerollOnly = 3,
    /// <summary>仅允许领取，不可重转。</summary>
    ClaimOnly = 4
}

[RequireComponent(typeof(UIDocument))]
public class BuffSelectionController : MonoBehaviour
{
    public static BuffSelectionController Instance { get; private set; }
    public bool IsOverlayVisible => _overlayVisible;
    public SlotTutorialGate TutorialGate { get; private set; } = SlotTutorialGate.None;

    private enum SlotUiPhase
    {
        IdleEmpty,
        Spinning,
        ResolvePreview,
        Applying,
        DebuffReveal
    }

    private UIDocument _doc;
    private VisualElement _overlay;
    private VisualElement _slotPanel;
    private VisualElement _towerChoice;
    private Label _towerChoiceHint;
    private Button _towerBtnUpgrade;
    private Button _towerBtnPlace;
    private Label _comboLabel;
    private Label _comboRuleLabel;
    private Label _rerollHintLabel;
    private Label _leverHint;
    private Label _statusText;
    private Label _chipsLabel;
    private Label _outcomeBadgeStatus;
    private Button _confirmBtn;
    private Button _cashOutBtn;
    private Button _continueBtn;
    private VisualElement _stageActionRow;
    private VisualElement _leverAssembly;
    private VisualElement _leverArm;
    private VisualElement _outcomePanel;
    private VisualElement _outcomeList;
    private VisualElement _debuffModal;
    private VisualElement _debuffList;
    private Button _debuffContinueBtn;

    private readonly SlotReelView[] _reels = new SlotReelView[3];
    private SlotSpinSession _session;
    private SlotUiPhase _phase = SlotUiPhase.IdleEmpty;
    private int _selectedReel = -1;
    private bool _overlayVisible;
    private bool _allowRealtime;
    private Coroutine _flowCoroutine;

    private readonly Queue<BuffDefinition> _towerQueue = new Queue<BuffDefinition>();
    private BuffDefinition _pendingTowerBuff;
    private List<DebuffId> _lastAppliedDebuffs = new List<DebuffId>();

    private void Awake()
    {
        Instance = this;
        _doc = GetComponent<UIDocument>();
    }

    private void Start()
    {
        var root = _doc.rootVisualElement;
        _overlay = root.Q<VisualElement>("overlay");
        _slotPanel = root.Q<VisualElement>("slot-panel");
        _towerChoice = root.Q<VisualElement>("tower-choice");
        _towerChoiceHint = root.Q<Label>("tower-choice-hint");
        _towerBtnUpgrade = root.Q<Button>("tower-btn-upgrade");
        _towerBtnPlace = root.Q<Button>("tower-btn-place");
        _comboLabel = root.Q<Label>("combo-label");
        _comboRuleLabel = root.Q<Label>("combo-rule-label");
        _rerollHintLabel = root.Q<Label>("reroll-hint-label");
        _leverHint = root.Q<Label>("lever-hint");
        _statusText = root.Q<Label>("status-text");
        _chipsLabel = root.Q<Label>("chips-label");
        _outcomeBadgeStatus = root.Q<Label>("outcome-badge");
        _confirmBtn = root.Q<Button>("confirm-btn");
        _cashOutBtn = root.Q<Button>("cashout-btn");
        _continueBtn = root.Q<Button>("continue-btn");
        _stageActionRow = root.Q<VisualElement>("stage-action-row");
        _leverAssembly = root.Q<VisualElement>("lever-assembly");
        _leverArm = root.Q<VisualElement>("lever-arm");
        _outcomePanel = root.Q<VisualElement>("outcome-panel");
        _outcomeList = root.Q<VisualElement>("outcome-list");
        _debuffModal = root.Q<VisualElement>("debuff-modal");
        _debuffList = root.Q<VisualElement>("debuff-list");
        _debuffContinueBtn = root.Q<Button>("debuff-continue-btn");

        for (int i = 0; i < 3; i++)
        {
            var reelRoot = root.Q<VisualElement>($"reel-{i}");
            _reels[i] = new SlotReelView(reelRoot, i);
            int captured = i;
            reelRoot.RegisterCallback<ClickEvent>(_ => OnReelClicked(captured));
        }

        _leverAssembly?.RegisterCallback<ClickEvent>(_ => OnLeverPulled());
        _confirmBtn?.RegisterCallback<ClickEvent>(_ => OnClaimClicked());
        _cashOutBtn?.RegisterCallback<ClickEvent>(_ => OnCashOutClicked());
        _continueBtn?.RegisterCallback<ClickEvent>(_ => OnContinueClicked());
        _debuffContinueBtn?.RegisterCallback<ClickEvent>(_ => OnDebuffContinue());
        _towerBtnUpgrade?.RegisterCallback<ClickEvent>(_ => OnTowerUpgradeChosen());
        _towerBtnPlace?.RegisterCallback<ClickEvent>(_ => OnTowerPlaceChosen());
        if (_confirmBtn != null)
            NeonHoverGlow.Attach(_confirmBtn);
        if (_cashOutBtn != null)
            NeonHoverGlow.Attach(_cashOutBtn);
        if (_continueBtn != null)
            NeonHoverGlow.Attach(_continueBtn);

        _overlay.style.display = DisplayStyle.None;
        HideTowerChoice();
        _overlay?.RegisterCallback<GeometryChangedEvent>(_ => FitPanelToViewport());

        if (GameManager.Instance != null)
        {
            GameManager.Instance.onBuffSelection.AddListener(Show);
            GameManager.Instance.onGameStart.AddListener(Hide);
            GameManager.Instance.onGameOver.AddListener(Hide);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onBuffSelection.RemoveListener(Show);
            GameManager.Instance.onGameStart.RemoveListener(Hide);
            GameManager.Instance.onGameOver.RemoveListener(Hide);
        }
    }

    private void Update()
    {
        if (!_overlayVisible) return;

        if (!_allowRealtime && Time.timeScale != 0f)
            Time.timeScale = 0f;

        if (_phase == SlotUiPhase.Spinning || _phase == SlotUiPhase.Applying) return;

        // Space 仅用于开转 / 已选轮时的重转，领取请点按钮
        if (Input.GetKeyDown(KeyCode.Space))
            OnLeverPulled();
    }

    public void Show()
    {
        if (BuffManager.Instance == null) return;

        PauseMenuController.Instance?.Close();
        DebuffManager.EnsureExists();
        StopFlow();
        HideTowerChoice();
        _pendingTowerBuff = null;
        _towerQueue.Clear();
        _selectedReel = -1;
        _lastAppliedDebuffs.Clear();

        if (SkillManager.Instance != null && SkillManager.Instance.IsAiming)
            SkillManager.Instance.AbortAiming();
        else
            SkillManager.Instance?.CancelAiming();
        LaunchGuide.Instance?.Hide();
        ExecuteLockReticle.Instance?.Hide();
        SlowMoFX.Instance?.ClearVisualOverlays();

        int wave = GameManager.Instance != null ? GameManager.Instance.Wave : 1;
        _session = SlotMachineBuffRoller.CreateEmptySession(wave);
        _phase = SlotUiPhase.IdleEmpty;
        RunTelemetry.OnSlotOpened();

        ClearComboBanner();
        HideOutcomePanel();
        HideDebuffPanel();
        SetPanelMotionState(spinning: false, reveal: false);
        SetStatusText("SYS · READY");
        RefreshChipsLabel();
        SetOutcomeBadgeStatus("IDLE", live: false);

        for (int i = 0; i < 3; i++)
        {
            _reels[i].ClearOutcomeHighlight();
            _reels[i].SetOutcomeBadge(null);
            _reels[i].SetSelected(false);
            _reels[i].SetSelectableHint(false);
            _reels[i].ShowIdleEmpty();
        }

        UpdateClaimButtonVisibility();
        UpdateStageActionButtons();
        RefreshHints();
        SetLeverEnabled(true);
        TutorialGate = SlotTutorialGate.None;

        _overlay.style.display = DisplayStyle.Flex;
        _overlayVisible = true;
        _allowRealtime = false;
        Time.timeScale = 0f;
        FitPanelToViewport();
    }

    private void FitPanelToViewport()
    {
        if (!_overlayVisible || _slotPanel == null || _overlay == null) return;

        _slotPanel.schedule.Execute(() =>
        {
            float padV = _overlay.resolvedStyle.paddingTop + _overlay.resolvedStyle.paddingBottom;
            float available = _overlay.resolvedStyle.height - padV;
            float panelHeight = _slotPanel.layout.height;
            if (available <= 1f || panelHeight <= 1f)
            {
                _slotPanel.style.scale = new Scale(Vector3.one);
                return;
            }

            // 允许缩到 0.55，避免内容加高后仍卡在 0.88 导致溢出屏幕
            float scale = panelHeight > available
                ? Mathf.Clamp(available / panelHeight, 0.55f, 1f)
                : 1f;
            _slotPanel.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }).ExecuteLater(1);
    }

    private void OnLeverPulled()
    {
        if (!_overlayVisible || _flowCoroutine != null) return;

        switch (_phase)
        {
            case SlotUiPhase.IdleEmpty:
                if (TutorialGate == SlotTutorialGate.Locked
                    || TutorialGate == SlotTutorialGate.RerollOnly
                    || TutorialGate == SlotTutorialGate.ClaimOnly)
                    return;
                _flowCoroutine = StartCoroutine(
                    _session != null && _session.useProgressiveReveal
                        ? RevealNextFlow()
                        : FullSpinFlow());
                break;
            case SlotUiPhase.ResolvePreview:
                if (TutorialGate == SlotTutorialGate.Locked
                    || TutorialGate == SlotTutorialGate.SpinOnly
                    || TutorialGate == SlotTutorialGate.ClaimOnly)
                    return;
                // Continue 改由按钮扣筹码；拉杆仅用于已选轮重转
                if (_selectedReel >= 0 && SlotMachineBuffRoller.CanRerollReel(_session, _selectedReel))
                    _flowCoroutine = StartCoroutine(SingleRerollFlow(_selectedReel));
                break;
        }
    }

    private void OnReelClicked(int index)
    {
        if (_phase != SlotUiPhase.ResolvePreview) return;
        if (TutorialGate == SlotTutorialGate.Locked
            || TutorialGate == SlotTutorialGate.SpinOnly
            || TutorialGate == SlotTutorialGate.ClaimOnly)
            return;
        if (!SlotMachineBuffRoller.CanRerollReel(_session, index)) return;

        _selectedReel = _selectedReel == index ? -1 : index;
        for (int i = 0; i < 3; i++)
        {
            _reels[i].SetSelected(i == _selectedReel);
            _reels[i].SetSelectableHint(SlotMachineBuffRoller.CanRerollReel(_session, i));
        }
        bool finalReady = _session == null
                          || !_session.useProgressiveReveal
                          || SlotMachineBuffRoller.IsFinalRevealReady(_session);
        SetLeverEnabled(finalReady || _selectedReel >= 0);
        RefreshHints();
        UpdateStageActionButtons();
    }

    private void OnClaimClicked()
    {
        if (_phase != SlotUiPhase.ResolvePreview || _flowCoroutine != null) return;
        if (TutorialGate == SlotTutorialGate.Locked
            || TutorialGate == SlotTutorialGate.SpinOnly
            || TutorialGate == SlotTutorialGate.RerollOnly)
            return;
        if (_session != null
            && _session.useProgressiveReveal
            && !SlotMachineBuffRoller.IsFinalRevealReady(_session))
            return;
        _flowCoroutine = StartCoroutine(ApplyFlow(cashOut: false));
    }

    private void OnCashOutClicked()
    {
        if (_phase != SlotUiPhase.ResolvePreview || _flowCoroutine != null) return;
        if (TutorialGate != SlotTutorialGate.None) return;
        if (_session == null || !_session.useProgressiveReveal) return;
        if (SlotMachineBuffRoller.IsFinalRevealReady(_session)) return;
        if (_session.revealedReelCount <= 0) return;
        _flowCoroutine = StartCoroutine(ApplyFlow(cashOut: true));
    }

    private void OnContinueClicked()
    {
        if (_phase != SlotUiPhase.ResolvePreview || _flowCoroutine != null) return;
        if (TutorialGate != SlotTutorialGate.None) return;
        if (_session == null || !_session.useProgressiveReveal) return;
        if (!SlotMachineBuffRoller.CanAffordContinue(_session)) return;
        int fromStage = _session.revealedReelCount;
        int cost = SlotMachineBuffRoller.GetContinueChipCost(_session);
        if (!SlotMachineBuffRoller.TryPayContinue(_session)) return;
        RefreshChipsLabel();
        RunTelemetry.RecordContinue(fromStage, cost, RunSession.Chips);
        _flowCoroutine = StartCoroutine(RevealNextFlow());
    }

    private IEnumerator FullSpinFlow()
    {
        _phase = SlotUiPhase.Spinning;
        SetPanelMotionState(spinning: true, reveal: false);
        SetStatusText("SYS · SPINNING");
        SetOutcomeBadgeStatus("SPIN", live: false);
        SetLeverEnabled(false);
        yield return PullLeverAnimation();

        SlotMachineBuffRoller.SpinAllReels(_session);

        yield return AnimateReelStop(0, 1.65f);
        yield return new WaitForSecondsRealtime(0.18f);
        yield return AnimateReelStop(1, 1.65f);
        yield return new WaitForSecondsRealtime(0.18f);
        yield return AnimateReelStop(2, 1.65f);

        yield return ShowComboBannerRoutine();
        EnterPostSpinPhase();
        _flowCoroutine = null;
    }

    /// <summary>渐进揭示：每次只停 1 个 Reel。</summary>
    private IEnumerator RevealNextFlow()
    {
        _phase = SlotUiPhase.Spinning;
        SetPanelMotionState(spinning: true, reveal: false);
        SetStatusText($"SYS · REVEAL {(_session.revealedReelCount + 1):00}/03");
        SetOutcomeBadgeStatus("SPIN", live: false);
        SetLeverEnabled(false);
        HideClaimButton();
        yield return PullLeverAnimation();

        int index = SlotMachineBuffRoller.RevealNextReel(_session);
        if (index < 0)
        {
            EnterPostSpinPhase();
            _flowCoroutine = null;
            yield break;
        }

        yield return AnimateReelStop(index, 1.55f);

        if (SlotMachineBuffRoller.IsFinalRevealReady(_session))
            yield return ShowComboBannerRoutine();
        else
            ClearComboBanner();

        EnterPostSpinPhase();
        _flowCoroutine = null;
    }

    private IEnumerator SingleRerollFlow(int reelIndex)
    {
        _phase = SlotUiPhase.Spinning;
        SetPanelMotionState(spinning: true, reveal: false);
        SetStatusText("SYS · REROLL");
        SetOutcomeBadgeStatus("SPIN", live: false);
        SetLeverEnabled(false);
        yield return PullLeverAnimation();

        bool isFree = _session.freeRerollsRemaining > 0;
        if (!SlotMachineBuffRoller.TryRerollReel(_session, reelIndex, isFree))
        {
            EnterPostSpinPhase();
            _flowCoroutine = null;
            yield break;
        }

        yield return AnimateReelStop(reelIndex, 1.35f);

        // Stage1/2 重转后不展示 Final Combo；仅 Stage3 刷新 Combo banner
        if (SlotMachineBuffRoller.IsFinalRevealReady(_session))
            yield return ShowComboBannerRoutine();
        else
            ClearComboBanner();

        _selectedReel = -1;
        EnterPostSpinPhase();
        _flowCoroutine = null;
    }

    private void EnterPostSpinPhase()
    {
        _selectedReel = -1;
        bool showRerollHints = TutorialGate == SlotTutorialGate.None
                               || TutorialGate == SlotTutorialGate.RerollOnly;
        for (int i = 0; i < 3; i++)
        {
            _reels[i].SetSelected(false);
            _reels[i].SetSelectableHint(
                showRerollHints &&
                SlotMachineBuffRoller.HasRerollAvailable(_session) &&
                SlotMachineBuffRoller.CanRerollReel(_session, i));
        }

        EnterResolvePreview();
    }

    private void EnterResolvePreview()
    {
        _phase = SlotUiPhase.ResolvePreview;
        SetPanelMotionState(spinning: false, reveal: true);

        bool finalReady = _session == null
                          || !_session.useProgressiveReveal
                          || SlotMachineBuffRoller.IsFinalRevealReady(_session);
        bool stageHold = _session != null
                         && _session.useProgressiveReveal
                         && !finalReady
                         && _session.revealedReelCount > 0;

        if (finalReady)
        {
            SetStatusText("SYS · AWAIT CLAIM");
            SetOutcomeBadgeStatus("SYNCED", live: true);
            PopulateOutcomePanel(finalCombo: true);
            HighlightOutcomeReels();
        }
        else if (stageHold)
        {
            SetStatusText($"SYS · REVEAL {_session.revealedReelCount:00}/03");
            SetOutcomeBadgeStatus("HOLD", live: true);
            ShowStageCashOutBanner();
            PopulateOutcomePanel(finalCombo: false);
            HighlightCashOutReels();
        }
        else
        {
            SetStatusText("SYS · READY");
            SetOutcomeBadgeStatus("IDLE", live: false);
            HideOutcomePanel();
            ClearComboBanner();
        }

        // Stage1/2：拉杆仅重转；Continue 走按钮。Final：可重转。
        bool leverOn = false;
        if (TutorialGate == SlotTutorialGate.None || TutorialGate == SlotTutorialGate.RerollOnly)
            leverOn = finalReady || (_selectedReel >= 0);
        SetLeverEnabled(leverOn);
        UpdateClaimButtonVisibility();
        UpdateStageActionButtons();
        RefreshHints();
        RefreshChipsLabel();
    }

    private void ShowStageCashOutBanner()
    {
        if (_comboLabel != null)
            _comboLabel.text = $"CURRENT · STAGE {_session.revealedReelCount}";
        if (_comboRuleLabel != null)
        {
            int cost = SlotMachineBuffRoller.GetContinueChipCost(_session);
            _comboRuleLabel.text = cost > 0
                ? $"兑现拿当前奖励 · 或继续 -{cost} CHIPS 看下一轮"
                : "兑现拿当前奖励";
        }
    }

    private IEnumerator ApplyFlow(bool cashOut)
    {
        _phase = SlotUiPhase.Applying;
        SetPanelMotionState(spinning: false, reveal: false);
        SetStatusText(cashOut ? "SYS · CASH OUT" : "SYS · CLAIMED");
        SetOutcomeBadgeStatus("LOCKED", live: false);
        SetLeverEnabled(false);
        UpdateClaimButtonVisibility();
        UpdateStageActionButtons();

        var lines = cashOut
            ? SlotMachineBuffRoller.BuildCashOutPreview(_session)
            : SlotMachineBuffRoller.BuildOutcomePreview(_session);
        foreach (var line in lines)
        {
            if (line.reelIndex >= 0 && line.reelIndex < 3)
                _reels[line.reelIndex].SetOutcomeHighlight(line.kind);
            FlashOutcomeLine(line.text);
            yield return new WaitForSecondsRealtime(0.18f);
        }

        var actions = cashOut
            ? SlotMachineBuffRoller.BuildCashOutActions(_session)
            : SlotMachineBuffRoller.BuildApplyActions(_session, commitJackpotTax: true);
        _lastAppliedDebuffs = new List<DebuffId>(_session.pendingDebuffs);

        SlotMachineBuffRoller.ExecuteApplyActions(actions, BuffManager.Instance);
        SlotMachineBuffRoller.ApplyPendingDebuffs(_session);
        SlotMachineBuffRoller.MarkCommitted(_session);

        if (cashOut)
            RunTelemetry.RecordCashOut(_session.revealedReelCount, RunSession.Chips);
        else
            RunTelemetry.RecordFinalClaim(_session.combo, RunSession.Chips);

        EnqueueTowerBuffs(actions);

        if (_lastAppliedDebuffs.Count > 0)
        {
            ShowDebuffPanel(_lastAppliedDebuffs);
            _phase = SlotUiPhase.DebuffReveal;
            _flowCoroutine = null;
            yield break;
        }

        ProcessNextTowerOrHide();
        _flowCoroutine = null;
    }

    private void EnqueueTowerBuffs(List<ApplyAction> actions)
    {
        foreach (var action in actions)
        {
            if (action.kind != ApplyActionKind.FullBuff || action.buff == null) continue;
            if (IsTowerBuff(action.buff))
                _towerQueue.Enqueue(action.buff);
        }

        foreach (var reel in _session.reels)
        {
            if (!reel.alreadyApplied || reel.buff == null) continue;
            if (IsTowerBuff(reel.buff))
                _towerQueue.Enqueue(reel.buff);
        }
    }

    private void OnDebuffContinue()
    {
        HideDebuffPanel();
        ProcessNextTowerOrHide();
    }

    private IEnumerator PullLeverAnimation()
    {
        _leverArm?.AddToClassList("lever-arm-pulled");
        yield return new WaitForSecondsRealtime(0.22f);
        _leverArm?.RemoveFromClassList("lever-arm-pulled");
        yield return new WaitForSecondsRealtime(0.14f);
    }

    private IEnumerator AnimateReelStop(int index, float duration)
    {
        yield return _reels[index].AnimateToResult(this, _session.reels[index], duration);
    }

    private IEnumerator ShowComboBannerRoutine()
    {
        if (_comboLabel != null)
            _comboLabel.text = SlotMachineBuffRoller.GetComboDisplayName(_session.combo);
        if (_comboRuleLabel != null)
        {
            var rule = SlotMachineBuffRoller.GetComboTriggerText(_session.combo);
            rule += " · " + SlotMachineBuffRoller.GetComboRuleText(_session.combo);
            if (SlotMachineBuffRoller.HasRerollAvailable(_session))
                rule += " · 绿=生效 · 点轮+拉杆重转";
            else
                rule += " · 绿=生效 灰=不生效";
            _comboRuleLabel.text = rule;
        }
        yield return new WaitForSecondsRealtime(0.45f);
    }

    private void PopulateOutcomePanel(bool finalCombo = true)
    {
        if (_outcomePanel == null || _outcomeList == null) return;
        _outcomeList.Clear();

        var lines = finalCombo
            ? SlotMachineBuffRoller.BuildOutcomePreview(_session)
            : SlotMachineBuffRoller.BuildCashOutPreview(_session);

        foreach (var line in lines)
        {
            var entry = new VisualElement();
            entry.AddToClassList("outcome-entry");

            var title = new Label(line.text);
            title.AddToClassList("outcome-line");
            title.AddToClassList(GetOutcomeLineClass(line.kind));
            entry.Add(title);

            if (!string.IsNullOrEmpty(line.detail))
            {
                var detail = new Label(line.detail);
                detail.AddToClassList("outcome-detail");
                detail.AddToClassList(GetOutcomeDetailClass(line.kind));
                entry.Add(detail);
            }

            _outcomeList.Add(entry);
        }

        _outcomePanel.style.display = DisplayStyle.Flex;
        FitPanelToViewport();
    }

    private void HighlightOutcomeReels()
    {
        HighlightFromLines(SlotMachineBuffRoller.BuildOutcomePreview(_session));
    }

    private void HighlightCashOutReels()
    {
        HighlightFromLines(SlotMachineBuffRoller.BuildCashOutPreview(_session));
    }

    private void HighlightFromLines(List<OutcomePreviewLine> lines)
    {
        for (int i = 0; i < 3; i++)
        {
            _reels[i].ClearOutcomeHighlight();
            _reels[i].SetOutcomeBadge(null);
        }

        foreach (var line in lines)
        {
            if (line.reelIndex < 0 || line.reelIndex >= 3) continue;
            bool canReroll = SlotMachineBuffRoller.CanRerollReel(_session, line.reelIndex);
            _reels[line.reelIndex].SetOutcomeHighlight(line.kind, canReroll);
            _reels[line.reelIndex].SetOutcomeBadge(GetOutcomeBadgeText(line.kind, canReroll));
        }
    }

    private static string GetOutcomeBadgeText(OutcomeLineKind kind, bool canReroll) => kind switch
    {
        OutcomeLineKind.FullApply or OutcomeLineKind.MysteryApplied => "生效",
        OutcomeLineKind.PurpleScrap => "折现",
        OutcomeLineKind.Ignored when canReroll => "可重转",
        OutcomeLineKind.Ignored => "不生效",
        _ => string.Empty
    };

    private void FlashOutcomeLine(string text)
    {
        if (_outcomeList == null) return;
        foreach (var child in _outcomeList.Children())
        {
            var lbl = child.Q<Label>(className: "outcome-line");
            if (lbl != null && lbl.text == text)
            {
                lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                break;
            }
        }
    }

    private void ShowDebuffPanel(List<DebuffId> debuffs)
    {
        if (_debuffModal == null || _debuffList == null) return;
        _debuffList.Clear();

        foreach (var id in debuffs)
        {
            var label = new Label(
                $"{DebuffManager.GetDisplayName(id)}（{DebuffManager.GetTierLabel(id)}）· {DebuffManager.GetDescription(id)}");
            label.AddToClassList("debuff-line");
            _debuffList.Add(label);
        }

        _debuffModal.style.display = DisplayStyle.Flex;
        if (_debuffContinueBtn != null)
            _debuffContinueBtn.style.display = DisplayStyle.Flex;
        if (_confirmBtn != null)
            _confirmBtn.style.display = DisplayStyle.None;
    }

    private void HideOutcomePanel()
    {
        if (_outcomePanel != null)
            _outcomePanel.style.display = DisplayStyle.None;
        if (_outcomeList != null)
            _outcomeList.Clear();
        SetOutcomeBadgeStatus("IDLE", live: false);
    }

    private void SetStatusText(string text)
    {
        if (_statusText != null)
            _statusText.text = text;
    }

    private void RefreshChipsLabel()
    {
        if (_chipsLabel == null) return;
        _chipsLabel.text = $"CHIPS {Mathf.Max(0, RunSession.Chips):00}";
    }

    private void SetOutcomeBadgeStatus(string text, bool live)
    {
        if (_outcomeBadgeStatus == null) return;
        _outcomeBadgeStatus.text = text;
        _outcomeBadgeStatus.EnableInClassList("is-live", live);
    }

    private void SetPanelMotionState(bool spinning, bool reveal)
    {
        _slotPanel?.EnableInClassList("is-spinning", spinning);
        _slotPanel?.EnableInClassList("is-reveal", reveal);
    }

    private void HideDebuffPanel()
    {
        if (_debuffModal != null)
            _debuffModal.style.display = DisplayStyle.None;
        if (_debuffContinueBtn != null)
            _debuffContinueBtn.style.display = DisplayStyle.None;
        if (_debuffList != null)
            _debuffList.Clear();
    }

    private void ClearComboBanner()
    {
        if (_comboLabel != null) _comboLabel.text = string.Empty;
        if (_comboRuleLabel != null) _comboRuleLabel.text = string.Empty;
    }

    private void RefreshHints()
    {
        bool progressiveHold = _session != null
                               && _session.useProgressiveReveal
                               && !SlotMachineBuffRoller.IsFinalRevealReady(_session)
                               && _session.revealedReelCount > 0;
        int continueCost = SlotMachineBuffRoller.GetContinueChipCost(_session);

        string main = _phase switch
        {
            SlotUiPhase.IdleEmpty => _session != null && _session.useProgressiveReveal
                ? "拉动拉杆 · 免费揭示第 1 轮"
                : "拉动右侧拉杆 · 开始转动",
            SlotUiPhase.ResolvePreview when progressiveHold =>
                SlotMachineBuffRoller.CanAffordContinue(_session)
                    ? $"兑现当前奖励 · 或「继续 -{continueCost}」看下一轮"
                    : $"筹码不足无法继续（需 {continueCost}）· 可兑现当前奖励",
            SlotUiPhase.ResolvePreview when _selectedReel >= 0 =>
                $"已选 {SlotMachineBuffRoller.GetReelName(_selectedReel)} · 拉杆重转（或直接点领取）",
            SlotUiPhase.ResolvePreview when SlotMachineBuffRoller.HasFreeRerollAvailable(_session) =>
                $"免费重转 ×{_session.freeRerollsRemaining} · 可选：点轮后拉杆 · 或直接领取",
            SlotUiPhase.ResolvePreview when SlotMachineBuffRoller.HasPaidRerollAvailable(_session) =>
                $"可选付费重转 ×{_session.paidRerollsRemaining}（{SlotMachineBuffRoller.GetPaidRerollWarning(_session)}）· 点领取跳过",
            SlotUiPhase.ResolvePreview => "点击「领取」获得绿框 Buff · 不能改选其它轮",
            SlotUiPhase.DebuffReveal => "轮位税已生效 · 点击继续",
            _ => string.Empty
        };

        if (_rerollHintLabel != null)
            _rerollHintLabel.text = main;

        if (_leverHint != null)
        {
            _leverHint.text = _phase switch
            {
                SlotUiPhase.IdleEmpty => "开转",
                SlotUiPhase.ResolvePreview when progressiveHold => "—",
                SlotUiPhase.ResolvePreview when _selectedReel >= 0 => "重转",
                SlotUiPhase.ResolvePreview => "—",
                _ => "拉杆"
            };
        }
    }

    private void SetLeverEnabled(bool enabled)
    {
        _leverAssembly?.EnableInClassList("lever-disabled", !enabled);
    }

    private void UpdateClaimButtonVisibility()
    {
        if (_confirmBtn == null) return;
        bool finalReady = _session == null
                          || !_session.useProgressiveReveal
                          || SlotMachineBuffRoller.IsFinalRevealReady(_session);
        bool show = _phase == SlotUiPhase.ResolvePreview
                    && finalReady
                    && (TutorialGate == SlotTutorialGate.None
                        || TutorialGate == SlotTutorialGate.ClaimOnly);
        _confirmBtn.text = "领取";
        _confirmBtn.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void UpdateStageActionButtons()
    {
        bool stageHold = _phase == SlotUiPhase.ResolvePreview
                         && TutorialGate == SlotTutorialGate.None
                         && _session != null
                         && _session.useProgressiveReveal
                         && _session.revealedReelCount > 0
                         && !SlotMachineBuffRoller.IsFinalRevealReady(_session);

        if (_stageActionRow != null)
            _stageActionRow.style.display = stageHold ? DisplayStyle.Flex : DisplayStyle.None;

        if (!stageHold) return;

        if (_cashOutBtn != null)
            _cashOutBtn.SetEnabled(true);

        int cost = SlotMachineBuffRoller.GetContinueChipCost(_session);
        bool canContinue = SlotMachineBuffRoller.CanAffordContinue(_session);
        if (_continueBtn != null)
        {
            _continueBtn.text = $"继续 -{cost}";
            _continueBtn.SetEnabled(canContinue);
            _continueBtn.EnableInClassList("continue-disabled", !canContinue);
        }
    }

    private void HideClaimButton()
    {
        if (_confirmBtn != null)
            _confirmBtn.style.display = DisplayStyle.None;
        if (_stageActionRow != null)
            _stageActionRow.style.display = DisplayStyle.None;
    }

    private static string GetOutcomeLineClass(OutcomeLineKind kind) => kind switch
    {
        OutcomeLineKind.FullApply or OutcomeLineKind.MysteryApplied => "outcome-line-apply",
        OutcomeLineKind.PurpleScrap => "outcome-line-scrap",
        OutcomeLineKind.Ignored => "outcome-line-ignored",
        OutcomeLineKind.DebuffPending or OutcomeLineKind.JackpotTax => "outcome-line-debuff",
        _ => "outcome-line"
    };

    private static string GetOutcomeDetailClass(OutcomeLineKind kind) => kind switch
    {
        OutcomeLineKind.FullApply or OutcomeLineKind.MysteryApplied => "outcome-detail-apply",
        OutcomeLineKind.PurpleScrap => "outcome-detail-scrap",
        OutcomeLineKind.Ignored => "outcome-detail-ignored",
        OutcomeLineKind.DebuffPending or OutcomeLineKind.JackpotTax => "outcome-detail-debuff",
        _ => "outcome-detail"
    };

    private void ProcessNextTowerOrHide()
    {
        while (_towerQueue.Count > 0)
        {
            var def = _towerQueue.Dequeue();
            _pendingTowerBuff = def;
            var tm = TowerManager.Instance;
            if (tm != null && tm.HasTowerOfType(def.effectType))
            {
                ShowTowerChoice(def);
                return;
            }

            BeginBuildOrReplaceFlow();
            return;
        }

        Hide();
    }

    private void ShowTowerChoice(BuffDefinition def)
    {
        SetLeverEnabled(false);
        HideOutcomePanel();
        HideClaimButton();

        var towerManager = TowerManager.Instance;
        bool hasFree = towerManager != null && towerManager.HasFreeSlot;
        bool canUpgrade = towerManager != null &&
                          towerManager.HasUpgradeableTowerOfType(def.effectType);
        if (_towerChoiceHint != null)
        {
            _towerChoiceHint.text = canUpgrade
                ? (hasFree
                    ? "选择放置一座新结构，或升级已有同类型结构。"
                    : "场地已满：选择替换某座结构，或升级已有同类型结构。")
                : (hasFree
                    ? "同类型结构已满级；可放置一座新结构。"
                    : "同类型结构已满级；请选择替换结构。");
        }

        if (_towerBtnPlace != null)
            _towerBtnPlace.text = hasFree ? "放置新结构" : "替换结构";

        if (_towerBtnUpgrade != null)
            _towerBtnUpgrade.style.display = canUpgrade ? DisplayStyle.Flex : DisplayStyle.None;

        if (_towerChoice != null)
            _towerChoice.style.display = DisplayStyle.Flex;
    }

    private void HideTowerChoice()
    {
        if (_towerChoice != null)
            _towerChoice.style.display = DisplayStyle.None;
    }

    private void OnTowerUpgradeChosen()
    {
        if (_pendingTowerBuff == null || TowerManager.Instance == null)
        {
            FinishTowerFlow();
            return;
        }

        var type = _pendingTowerBuff.effectType;
        _pendingTowerBuff = null;
        _overlay.style.display = DisplayStyle.None;
        HideTowerChoice();
        _allowRealtime = true;
        Time.timeScale = 1f;
        TowerManager.Instance.BeginSelectTowerToUpgrade(type, FinishTowerFlow);
    }

    private void OnTowerPlaceChosen() => BeginBuildOrReplaceFlow();

    private void BeginBuildOrReplaceFlow()
    {
        if (_pendingTowerBuff == null || TowerManager.Instance == null)
        {
            FinishTowerFlow();
            return;
        }

        var type = _pendingTowerBuff.effectType;
        _pendingTowerBuff = null;
        _overlay.style.display = DisplayStyle.None;
        HideTowerChoice();
        _allowRealtime = true;
        Time.timeScale = 1f;
        TowerManager.Instance.BeginBuildOrReplace(type, FinishTowerFlow);
    }

    private void FinishTowerFlow()
    {
        _pendingTowerBuff = null;
        ProcessNextTowerOrHide();
    }

    private static bool IsTowerBuff(BuffDefinition def) =>
        BuffManager.IsTowerBuildEffect(def.effectType);

    private void StopFlow()
    {
        if (_flowCoroutine != null)
        {
            StopCoroutine(_flowCoroutine);
            _flowCoroutine = null;
        }
    }

    // ── 协议校准教学钩子 ───────────────────────────────────────────────

    /// <summary>停轮后、可领取/重转阶段。</summary>
    public bool IsResolvePreviewPhase => _phase == SlotUiPhase.ResolvePreview;

    /// <summary>本局已重转次数（各轮累计）。</summary>
    public int TutorialRerollCount
    {
        get
        {
            if (_session?.reelRerollCount == null) return 0;
            int n = 0;
            for (int i = 0; i < _session.reelRerollCount.Length; i++)
                n += _session.reelRerollCount[i];
            return n;
        }
    }

    /// <summary>教学用：保证至少有 N 次免费重转，并刷新可选轮高亮。</summary>
    public void TutorialEnsureFreeRerolls(int count = 1)
    {
        if (_session == null) return;
        count = Mathf.Max(1, count);
        if (_session.freeRerollsRemaining < count)
            _session.freeRerollsRemaining = count;

        if (_phase != SlotUiPhase.ResolvePreview) return;

        bool showRerollHints = TutorialGate == SlotTutorialGate.None
                               || TutorialGate == SlotTutorialGate.RerollOnly;
        for (int i = 0; i < 3; i++)
        {
            _reels[i].SetSelectableHint(
                showRerollHints && SlotMachineBuffRoller.CanRerollReel(_session, i));
        }
        HighlightOutcomeReels();
        RefreshHints();
    }

    /// <summary>协议校准：强制老虎机交互顺序。</summary>
    public void SetTutorialGate(SlotTutorialGate gate)
    {
        TutorialGate = gate;
        if (!_overlayVisible) return;

        if (gate == SlotTutorialGate.ClaimOnly || gate == SlotTutorialGate.Locked
            || gate == SlotTutorialGate.SpinOnly)
        {
            _selectedReel = -1;
            for (int i = 0; i < 3; i++)
            {
                _reels[i].SetSelected(false);
                _reels[i].SetSelectableHint(false);
            }
        }
        else if (gate == SlotTutorialGate.RerollOnly && _phase == SlotUiPhase.ResolvePreview)
        {
            for (int i = 0; i < 3; i++)
            {
                _reels[i].SetSelectableHint(SlotMachineBuffRoller.CanRerollReel(_session, i));
            }
        }

        bool leverOn = _phase == SlotUiPhase.IdleEmpty
                       && (gate == SlotTutorialGate.None || gate == SlotTutorialGate.SpinOnly);
        leverOn |= _phase == SlotUiPhase.ResolvePreview
                   && (gate == SlotTutorialGate.None || gate == SlotTutorialGate.RerollOnly);
        if (_phase == SlotUiPhase.Spinning || _phase == SlotUiPhase.Applying)
            leverOn = false;
        SetLeverEnabled(leverOn);
        UpdateClaimButtonVisibility();
        UpdateStageActionButtons();
        RefreshHints();
    }

    public void Hide()
    {
        StopFlow();
        HideTowerChoice();
        HideOutcomePanel();
        HideDebuffPanel();
        _pendingTowerBuff = null;
        _session = null;
        _towerQueue.Clear();
        _phase = SlotUiPhase.IdleEmpty;
        TutorialGate = SlotTutorialGate.None;
        _overlayVisible = false;
        _allowRealtime = false;
        if (_overlay != null)
            _overlay.style.display = DisplayStyle.None;
        Time.timeScale = 1f;
        GameManager.Instance?.OnBuffSelectionDone();
    }
}
