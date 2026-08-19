using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public enum LoadoutReturnTarget { MainMenu, Campaign }

[RequireComponent(typeof(UIDocument))]
public class LoadoutPanelController : MonoBehaviour
{
    public static LoadoutPanelController Instance { get; private set; }

    public event Action LoadoutChanged;

    private UIDocument _doc;
    private VisualElement _panel;
    private VisualElement _dragOverlay;
    private VisualElement _statusHost;
    private VisualElement _statusSpinner;
    private Label _statusLabel;
    private Label _ballNameLabel;
    private Label _ballDescLabel;
    private VisualElement _ballPreviewOrb;
    private VisualElement _ballPagerDots;
    private VisualElement _skillDetailCard;
    private Label _skillDetailTitle;
    private Label _skillDetailBody;
    private VisualElement _skillSlot0;
    private VisualElement _skillSlot1;
    private VisualElement _skillLibrary;

    private RunCatalog _catalog;
    private LoadoutSkillDrag _drag;
    private LoadoutReturnTarget _returnTarget = LoadoutReturnTarget.MainMenu;
    private bool _slotsDragBound;

    private const float SaveSpinnerMinSeconds = 0.5f;
    private const float SavedMessageSeconds = 2.2f;

    private Coroutine _saveFeedbackCoroutine;
    private IVisualElementScheduledItem _spinnerSchedule;
    private float _spinnerAngle;

    private void Awake()
    {
        Instance = this;
        _doc = GetComponent<UIDocument>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (_doc == null || _doc.rootVisualElement == null) return;

        var root = _doc.rootVisualElement;
        _panel = root.Q<VisualElement>("LoadoutPanel");
        _dragOverlay = root.Q<VisualElement>("LoadoutDragOverlay");
        _statusHost = root.Q<VisualElement>("LoadoutStatusHost");
        _statusSpinner = root.Q<VisualElement>("LoadoutStatusSpinner");
        _statusLabel = root.Q<Label>("LoadoutStatusLabel");
        _ballNameLabel = root.Q<Label>("BallNameLabel");
        _ballDescLabel = root.Q<Label>("BallDescLabel");
        _ballPreviewOrb = root.Q<VisualElement>("BallPreviewOrb");
        _ballPagerDots = root.Q<VisualElement>("BallPagerDots");
        _skillDetailCard = root.Q<VisualElement>("SkillDetailCard");
        _skillDetailTitle = root.Q<Label>("SkillDetailTitle");
        _skillDetailBody = root.Q<Label>("SkillDetailBody");
        _skillSlot0 = root.Q<VisualElement>("SkillSlot0");
        _skillSlot1 = root.Q<VisualElement>("SkillSlot1");
        _skillLibrary = root.Q<VisualElement>("SkillLibrary");

        if (_dragOverlay != null)
            _dragOverlay.pickingMode = PickingMode.Ignore;

        root.Q<Button>("BtnLoadoutBack")?.RegisterCallback<ClickEvent>(_ => Hide());

        if (_dragOverlay == null && _panel != null)
        {
            _dragOverlay = new VisualElement { name = "LoadoutDragOverlay" };
            _dragOverlay.AddToClassList("loadout-drag-overlay");
            _dragOverlay.pickingMode = PickingMode.Ignore;
            _panel.Add(_dragOverlay);
        }

        _drag = new LoadoutSkillDrag(_dragOverlay ?? _panel, _skillSlot0, _skillSlot1);
        _drag.OnDropApplied += OnDragDropApplied;
        BindSlotDragsOnce();

        if (_panel != null)
            _panel.style.display = DisplayStyle.None;

        _catalog = RunCatalog.Load();
        RunLoadout.Load();
        if (_catalog != null)
            RunLoadout.EnsureDefaults(_catalog);
    }

    private void BindSlotDragsOnce()
    {
        if (_slotsDragBound || _drag == null) return;
        _drag.BindSlot(_skillSlot0, 0, () => RunLoadout.GetSkillInSlot(0, _catalog));
        _drag.BindSlot(_skillSlot1, 1, () => RunLoadout.GetSkillInSlot(1, _catalog));
        _slotsDragBound = true;
    }

    public void Show(LoadoutReturnTarget returnTarget)
    {
        _returnTarget = returnTarget;
        _catalog = RunCatalog.Load();
        RunLoadout.Load();
        if (_catalog != null)
            RunLoadout.EnsureDefaults(_catalog);

        if (_panel != null)
            _panel.style.display = DisplayStyle.Flex;

        RefreshAll();
    }

    public void Hide()
    {
        _drag?.Cancel();
        StopSaveFeedback();
        if (_panel != null)
            _panel.style.display = DisplayStyle.None;

        if (MainMenuController.Instance != null)
            MainMenuController.Instance.OnLoadoutClosed(_returnTarget);
    }

    public void RefreshSummaryLabels(Label ballLabel, Label skillsLabel)
    {
        if (_catalog == null) _catalog = RunCatalog.Load();
        RunLoadout.Load();
        if (_catalog != null)
            RunLoadout.EnsureDefaults(_catalog);

        var ball = RunLoadout.GetSelectedBall(_catalog);
        if (ballLabel != null)
            ballLabel.text = ball != null ? ball.displayName : "—";

        if (skillsLabel != null)
        {
            var s0 = RunLoadout.GetSkillInSlot(0, _catalog);
            var s1 = RunLoadout.GetSkillInSlot(1, _catalog);
            skillsLabel.text = $"{(s0 != null ? s0.displayName : "—")} · {(s1 != null ? s1.displayName : "—")}";
        }
    }

    private void OnDragDropApplied()
    {
        RefreshAll();
        LoadoutChanged?.Invoke();
        PlaySaveFeedback();
    }

    /// <summary>战前配置变更后的保存反馈。当前为本地 PlayerPrefs；之后可在此处 await 网络/数据库。</summary>
    public void PlaySaveFeedback()
    {
        if (!isActiveAndEnabled) return;
        StopSaveFeedback();
        _saveFeedbackCoroutine = StartCoroutine(SaveFeedbackRoutine());
    }

    private void RefreshAll()
    {
        _drag?.SetCatalog(_catalog);
        RefreshBallColumn();
        RefreshSkillSlots();
        RefreshSkillLibrary();
        RefreshStatusIdle();
        RefreshSkillDetail(null);
    }

    private void RefreshBallColumn()
    {
        var ball = RunLoadout.GetSelectedBall(_catalog);
        if (_ballNameLabel != null)
            _ballNameLabel.text = ball != null ? ball.displayName : "标准弹珠";
        if (_ballDescLabel != null)
            _ballDescLabel.text = ball != null ? ball.loadoutDescription : string.Empty;

        if (_ballPreviewOrb != null && ball != null)
            _ballPreviewOrb.style.backgroundColor = ball.glowColor;

        if (_ballPagerDots != null)
        {
            _ballPagerDots.Clear();
            var dot = new VisualElement();
            dot.AddToClassList("ball-pager-dot");
            dot.AddToClassList("ball-pager-dot-active");
            _ballPagerDots.Add(dot);
        }
    }

    private void RefreshSkillSlots()
    {
        RefreshSkillSlot(_skillSlot0, 0);
        RefreshSkillSlot(_skillSlot1, 1);
    }

    private void RefreshSkillSlot(VisualElement slotRoot, int index)
    {
        if (slotRoot == null) return;

        var def = RunLoadout.GetSkillInSlot(index, _catalog);

        var nameLbl = slotRoot.Q<Label>("SlotSkillName");
        var keyLbl = slotRoot.Q<Label>("SlotKeyHint");
        var cdLbl = slotRoot.Q<Label>("SlotCooldown");
        var modeLbl = slotRoot.Q<Label>("SlotMode");
        var icon = slotRoot.Q<VisualElement>("SlotIcon");

        if (nameLbl != null) nameLbl.text = def != null ? def.displayName : "拖拽技能到此";
        if (keyLbl != null) keyLbl.text = def != null ? def.GetSlotKeyHint(index) : string.Empty;
        if (cdLbl != null) cdLbl.text = def != null ? $"CD {def.baseCooldown:F0}s" : string.Empty;
        if (modeLbl != null)
        {
            modeLbl.text = def == null ? string.Empty : def.activationMode switch
            {
                SkillActivationMode.Aim => "瞄准",
                SkillActivationMode.Instant => "即时",
                _ => "占位"
            };
        }

        if (icon != null)
        {
            if (def != null)
                icon.style.backgroundColor = GetCategoryColor(def.category);
            icon.style.opacity = def != null ? 1f : 0.3f;
        }

        slotRoot.EnableInClassList("skill-slot-empty", def == null);
    }

    private void RefreshSkillLibrary()
    {
        if (_skillLibrary == null || _catalog == null) return;
        _skillLibrary.Clear();

        foreach (var def in _catalog.skills)
        {
            if (def == null) continue;
            _skillLibrary.Add(BuildLibraryCard(def));
        }
    }

    private VisualElement BuildLibraryCard(SkillDefinition def)
    {
        bool available = def.isAvailable;
        bool equipped = RunLoadout.IsSkillEquipped(def.skillId);

        var card = new VisualElement();
        card.AddToClassList("skill-library-card");
        card.AddToClassList("skill-library-draggable");
        if (!available) card.AddToClassList("skill-library-locked");
        if (equipped) card.AddToClassList("skill-library-equipped");

        var stripe = new VisualElement();
        stripe.AddToClassList("skill-library-stripe");
        stripe.style.backgroundColor = GetCategoryColor(def.category);
        card.Add(stripe);

        var name = new Label(available ? def.displayName : "待解锁");
        name.AddToClassList("skill-library-name");
        card.Add(name);

        if (equipped)
        {
            var badge = new Label("已装备");
            badge.AddToClassList("skill-library-badge");
            card.Add(badge);
        }

        if (available)
        {
            _drag.BindLibraryCard(card, def);
            card.RegisterCallback<ClickEvent>(evt =>
            {
                if (_drag != null && _drag.IsActive) return;
                RefreshSkillDetail(def);
            });
        }

        return card;
    }

    private void RefreshStatusIdle()
    {
        if (_statusLabel == null) return;

        bool valid = _catalog != null && RunLoadout.IsValid(_catalog);
        if (!valid)
        {
            _statusLabel.text = "请装备 2 个不同技能";
            SetStatusStyle(valid: false, saving: false);
            return;
        }

        _statusLabel.text = string.Empty;
        SetStatusStyle(valid: true, saving: false);
    }

    private IEnumerator SaveFeedbackRoutine()
    {
        SetStatusSaving(true);
        float started = Time.unscaledTime;

        // 本地保存已在 RunLoadout.TryEquipSkill / MoveSkillBetweenSlots 内完成。
        // 之后接云端时：yield return RunLoadout.SaveRemoteAsync();

        float elapsed = Time.unscaledTime - started;
        if (elapsed < SaveSpinnerMinSeconds)
            yield return new WaitForSecondsRealtime(SaveSpinnerMinSeconds - elapsed);

        bool valid = _catalog != null && RunLoadout.IsValid(_catalog);
        SetStatusSaving(false);

        if (valid)
        {
            _statusLabel.text = "配置已保存";
            SetStatusStyle(valid: true, saving: false);
        }
        else
        {
            _statusLabel.text = "请装备 2 个不同技能";
            SetStatusStyle(valid: false, saving: false);
            yield break;
        }

        yield return new WaitForSecondsRealtime(SavedMessageSeconds);
        RefreshStatusIdle();
        _saveFeedbackCoroutine = null;
    }

    private void SetStatusSaving(bool saving)
    {
        if (saving)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = "保存中…";
                SetStatusStyle(valid: true, saving: true);
            }

            StartSpinner();
            _statusHost?.AddToClassList("loadout-status-saving");
            return;
        }

        StopSpinner();
        _statusHost?.RemoveFromClassList("loadout-status-saving");
    }

    private void SetStatusStyle(bool valid, bool saving)
    {
        if (_statusLabel == null) return;
        _statusLabel.EnableInClassList("loadout-status-ok", valid && !saving);
        _statusLabel.EnableInClassList("loadout-status-saving-text", saving);
        _statusLabel.EnableInClassList("loadout-status-warn", !valid && !saving);
    }

    private void StartSpinner()
    {
        StopSpinner();
        if (_statusSpinner == null) return;

        _spinnerAngle = 0f;
        _spinnerSchedule = _statusSpinner.schedule.Execute(() =>
        {
            _spinnerAngle = (_spinnerAngle + 280f * 0.016f) % 360f;
            _statusSpinner.style.rotate = new Rotate(new Angle(_spinnerAngle));
        }).Every(16);
    }

    private void StopSpinner()
    {
        _spinnerSchedule?.Pause();
        _spinnerSchedule = null;
        if (_statusSpinner != null)
            _statusSpinner.style.rotate = new Rotate(new Angle(0f));
    }

    private void StopSaveFeedback()
    {
        if (_saveFeedbackCoroutine != null)
        {
            StopCoroutine(_saveFeedbackCoroutine);
            _saveFeedbackCoroutine = null;
        }

        SetStatusSaving(false);
    }

    private void RefreshSkillDetail(SkillDefinition def)
    {
        if (_skillDetailCard == null) return;

        if (def == null)
        {
            _skillDetailCard.style.display = DisplayStyle.None;
            return;
        }

        _skillDetailCard.style.display = DisplayStyle.Flex;
        if (_skillDetailTitle != null) _skillDetailTitle.text = def.displayName;
        if (_skillDetailBody != null)
        {
            _skillDetailBody.text = string.IsNullOrEmpty(def.GetBriefDescription())
                ? $"CD {def.baseCooldown:F0}s · {def.activationMode}"
                : def.GetBriefDescription();
        }
    }

    private static Color GetCategoryColor(SkillCategory category) => category switch
    {
        SkillCategory.Defense => new Color(0.2f, 0.85f, 1f),
        SkillCategory.Control => new Color(0.75f, 0.45f, 1f),
        _ => new Color(1f, 0.35f, 0.45f)
    };
}
