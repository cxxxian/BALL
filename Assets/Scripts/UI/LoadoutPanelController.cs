using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public enum LoadoutReturnTarget { MainMenu, Campaign }

[RequireComponent(typeof(UIDocument))]
public class LoadoutPanelController : MonoBehaviour
{
    public static LoadoutPanelController Instance { get; private set; }

    public event Action LoadoutChanged;

    private VisualElement _panel;
    private VisualElement _ballPreviewHost;
    private VisualElement _ballPreviewOrb;
    private VisualElement _ballPagerDots;
    private Label _ballNameLabel;
    private Label _ballDescLabel;
    private Label _ballLockHint;
    private Button _equipBtn;
    private VisualElement _skillSlot0;
    private VisualElement _skillSlot1;

    private RunCatalog _catalog;
    private LoadoutReturnTarget _returnTarget = LoadoutReturnTarget.MainMenu;
    private int _browseBallIndex;
    private readonly List<BallDefinition> _browseBalls = new List<BallDefinition>();

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;

        var root = doc.rootVisualElement;
        _panel = root.Q<VisualElement>("LoadoutPanel");
        _ballPreviewHost = root.Q<VisualElement>("BallPreviewHost");
        _ballPreviewOrb = root.Q<VisualElement>("BallPreviewOrb");
        _ballPagerDots = root.Q<VisualElement>("BallPagerDots");
        _ballNameLabel = root.Q<Label>("BallNameLabel");
        _ballDescLabel = root.Q<Label>("BallDescLabel");
        _ballLockHint = root.Q<Label>("BallLockHint");
        _equipBtn = root.Q<Button>("BtnEquipBall");
        _skillSlot0 = root.Q<VisualElement>("SkillSlot0");
        _skillSlot1 = root.Q<VisualElement>("SkillSlot1");

        root.Q<Button>("BtnLoadoutBack")?.RegisterCallback<ClickEvent>(_ => Hide());
        root.Q<Button>("BtnBallPrev")?.RegisterCallback<ClickEvent>(_ => BrowseBall(-1));
        root.Q<Button>("BtnBallNext")?.RegisterCallback<ClickEvent>(_ => BrowseBall(1));
        _equipBtn?.RegisterCallback<ClickEvent>(_ => TryEquipBrowseBall());

        if (_panel != null)
            _panel.style.display = DisplayStyle.None;

        _catalog = RunCatalog.Load();
        RunLoadout.Load();
        PlayerProfile.Load();
        if (_catalog != null)
            RunLoadout.EnsureDefaults(_catalog);
    }

    public void Show(LoadoutReturnTarget returnTarget)
    {
        _returnTarget = returnTarget;
        _catalog = RunCatalog.Load();
        RunLoadout.Load();
        PlayerProfile.Load();
        if (_catalog != null)
            RunLoadout.EnsureDefaults(_catalog);

        if (_panel != null)
            _panel.style.display = DisplayStyle.Flex;

        RefreshAll();
    }

    public void Hide()
    {
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
            string left = s0 != null ? s0.displayName : "—";
            string right = s1 != null ? s1.displayName : "—";
            skillsLabel.text = s1 != null ? $"{left} · {right}" : left;
        }
    }

    private void RefreshAll()
    {
        RebuildBrowseBallList();
        SyncBrowseIndexToSelection();
        RefreshBallColumn();
    }

    private void RebuildBrowseBallList()
    {
        _browseBalls.Clear();
        if (_catalog == null) return;
        foreach (var ball in _catalog.GetLoadoutBalls())
        {
            if (ball != null) _browseBalls.Add(ball);
        }
    }

    private void SyncBrowseIndexToSelection()
    {
        if (_browseBalls.Count == 0)
        {
            _browseBallIndex = 0;
            return;
        }

        var selected = RunLoadout.GetSelectedBall(_catalog);
        if (selected == null)
        {
            _browseBallIndex = 0;
            return;
        }

        for (int i = 0; i < _browseBalls.Count; i++)
        {
            if (_browseBalls[i].ballId == selected.ballId)
            {
                _browseBallIndex = i;
                return;
            }
        }
    }

    private BallDefinition GetBrowseBall()
    {
        if (_browseBalls.Count == 0) return RunLoadout.GetSelectedBall(_catalog);
        _browseBallIndex = Mathf.Clamp(_browseBallIndex, 0, _browseBalls.Count - 1);
        return _browseBalls[_browseBallIndex];
    }

    private void BrowseBall(int delta)
    {
        if (_browseBalls.Count == 0) return;
        _browseBallIndex = (_browseBallIndex + delta + _browseBalls.Count) % _browseBalls.Count;
        RefreshBallColumn();
    }

    private void RefreshBallColumn()
    {
        var ball = GetBrowseBall();
        if (ball == null) return;

        bool unlocked = PlayerProfile.IsBallUnlocked(ball.ballId);
        bool equipped = RunLoadout.Data.ballId == ball.ballId;

        if (_ballNameLabel != null) _ballNameLabel.text = ball.displayName;
        if (_ballDescLabel != null) _ballDescLabel.text = ball.loadoutDescription;
        if (_ballPreviewOrb != null) _ballPreviewOrb.style.backgroundColor = ball.glowColor;
        if (_ballPreviewHost != null)
            _ballPreviewHost.EnableInClassList("ball-preview-locked", !unlocked);

        if (_ballLockHint != null)
        {
            if (!unlocked)
            {
                _ballLockHint.text = "未解锁 · 去协议商店获取";
                _ballLockHint.style.display = DisplayStyle.Flex;
            }
            else
            {
                _ballLockHint.style.display = DisplayStyle.None;
            }
        }

        RefreshSkillSlot(_skillSlot0, RunLoadout.GetBoundSkillForPreview(ball, 0, _catalog), 0, locked: true);
        RefreshSkillSlot(_skillSlot1, RunLoadout.GetBoundSkillForPreview(ball, 1, _catalog), 1, locked: false);
        RefreshBallPagerDots(ball, unlocked, equipped);

        if (_equipBtn != null)
        {
            _equipBtn.SetEnabled(unlocked && !equipped);
            _equipBtn.text = equipped ? "已装备" : "装备此弹珠";
        }
    }

    private void RefreshBallPagerDots(BallDefinition ball, bool unlocked, bool equipped)
    {
        if (_ballPagerDots == null) return;
        _ballPagerDots.Clear();

        for (int i = 0; i < _browseBalls.Count; i++)
        {
            var b = _browseBalls[i];
            bool dotUnlocked = PlayerProfile.IsBallUnlocked(b.ballId);
            bool dotActive = ball != null && b.ballId == ball.ballId;

            var dot = new VisualElement();
            dot.AddToClassList("ball-pager-dot");
            if (dotActive) dot.AddToClassList("ball-pager-dot-active");
            if (!dotUnlocked) dot.AddToClassList("ball-pager-dot-locked");
            if (equipped && dotActive) dot.AddToClassList("ball-pager-dot-equipped");

            int captured = i;
            dot.RegisterCallback<ClickEvent>(_ =>
            {
                _browseBallIndex = captured;
                RefreshBallColumn();
            });

            _ballPagerDots.Add(dot);
        }
    }

    private void TryEquipBrowseBall()
    {
        var ball = GetBrowseBall();
        if (ball == null) return;
        if (!RunLoadout.TrySelectBall(ball.ballId, _catalog)) return;
        LoadoutChanged?.Invoke();
        RefreshAll();
    }

    private void RefreshSkillSlot(VisualElement slotRoot, SkillDefinition def, int index, bool locked)
    {
        if (slotRoot == null) return;

        var nameLbl = slotRoot.Q<Label>("SlotSkillName");
        var keyLbl = slotRoot.Q<Label>("SlotKeyHint");
        var cdLbl = slotRoot.Q<Label>("SlotCooldown");
        var modeLbl = slotRoot.Q<Label>("SlotMode");
        var icon = slotRoot.Q<VisualElement>("SlotIcon");

        if (def == null)
        {
            if (nameLbl != null) nameLbl.text = index == 1 ? "身份待定" : "—";
            if (keyLbl != null) keyLbl.text = index == 1 ? "E" : string.Empty;
            if (cdLbl != null) cdLbl.text = string.Empty;
            if (modeLbl != null) modeLbl.text = index == 1 ? "待定" : string.Empty;
            if (icon != null) icon.style.opacity = 0.2f;
            slotRoot.EnableInClassList("skill-slot-empty", true);
            slotRoot.EnableInClassList("skill-slot-locked", locked);
            return;
        }

        if (nameLbl != null) nameLbl.text = locked ? $"{def.displayName} · 锁" : def.displayName;
        if (keyLbl != null) keyLbl.text = def.GetSlotKeyHint(index);
        if (cdLbl != null) cdLbl.text = $"CD {def.baseCooldown:F0}s";
        if (modeLbl != null)
        {
            modeLbl.text = def.activationMode switch
            {
                SkillActivationMode.Aim => "瞄准",
                SkillActivationMode.Instant => def.implementationType == ActiveSkillType.ExecuteChain
                    ? "武装"
                    : "即时",
                _ => string.Empty
            };
        }
        if (icon != null)
        {
            icon.style.backgroundColor = GetCategoryColor(def.category);
            icon.style.opacity = 1f;
        }
        slotRoot.EnableInClassList("skill-slot-empty", false);
        slotRoot.EnableInClassList("skill-slot-locked", locked);
    }

    private static Color GetCategoryColor(SkillCategory category) => category switch
    {
        SkillCategory.Defense => new Color(0.2f, 0.85f, 1f),
        SkillCategory.Control => new Color(0.75f, 0.45f, 1f),
        _ => new Color(1f, 0.35f, 0.45f)
    };
}
