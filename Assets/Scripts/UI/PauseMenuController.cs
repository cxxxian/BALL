using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }

    public bool IsOpen { get; private set; }

    private UIDocument _doc;
    private VisualElement _overlay;
    private VisualElement _confirmBar;
    private Label _confirmText;

    private readonly Dictionary<string, VisualElement> _tabViews = new Dictionary<string, VisualElement>();
    private readonly Dictionary<string, Button> _tabButtons = new Dictionary<string, Button>();
    private string _activeTab = "run-status";

    private VisualElement _buffList;
    private VisualElement _debuffList;
    private VisualElement _statsSummary;
    private VisualElement _skillCards;
    private VisualElement _comboCards;
    private Label _slotFlowText;
    private Label _slotDocHint;

    private enum PendingConfirm { None, MainMenu, Quit }
    private PendingConfirm _pendingConfirm = PendingConfirm.None;

    private static readonly SlotCombo[] RuleCombos =
    {
        SlotCombo.Jackpot,
        SlotCombo.TripleRare,
        SlotCombo.DoubleEpic,
        SlotCombo.DoubleRare,
        SlotCombo.TripleCommon,
        SlotCombo.Mixed,
        SlotCombo.Smooth,
        SlotCombo.Omen
    };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _doc = GetComponent<UIDocument>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (GameManager.Instance != null)
            GameManager.Instance.onGameOver.RemoveListener(Close);
    }

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onGameOver.AddListener(Close);

        var root = _doc.rootVisualElement;
        _overlay = root.Q<VisualElement>("overlay");
        _confirmBar = root.Q<VisualElement>("confirm-bar");
        _confirmText = root.Q<Label>("confirm-text");

        _buffList = root.Q<VisualElement>("buff-list");
        _debuffList = root.Q<VisualElement>("debuff-list");
        _statsSummary = root.Q<VisualElement>("stats-summary");
        _skillCards = root.Q<VisualElement>("skill-cards");
        _comboCards = root.Q<VisualElement>("combo-cards");
        _slotFlowText = root.Q<Label>("slot-flow-text");
        _slotDocHint = root.Q<Label>("slot-doc-hint");

        BindTab("run-status", root.Q<Button>("tab-run-status"), root.Q<VisualElement>("run-status-view"));
        BindTab("skills", root.Q<Button>("tab-skills"), root.Q<VisualElement>("skills-view"));
        BindTab("slot-rules", root.Q<Button>("tab-slot-rules"), root.Q<VisualElement>("slot-rules-view"));
        BindTab("settings", root.Q<Button>("tab-settings"), root.Q<VisualElement>("settings-view"));

        HideTabScrollbars(root);

        root.Q<Button>("btn-resume")?.RegisterCallback<ClickEvent>(_ => Close());
        root.Q<Button>("btn-main-menu")?.RegisterCallback<ClickEvent>(_ => ShowConfirm(PendingConfirm.MainMenu));
        root.Q<Button>("btn-quit")?.RegisterCallback<ClickEvent>(_ => ShowConfirm(PendingConfirm.Quit));
        root.Q<Button>("btn-confirm-yes")?.RegisterCallback<ClickEvent>(_ => OnConfirmYes());
        root.Q<Button>("btn-confirm-no")?.RegisterCallback<ClickEvent>(_ => HideConfirm());

        SetupSettingsStub(root);
        PopulateSlotRulesStatic();

        _overlay.style.display = DisplayStyle.None;
        IsOpen = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Toggle();

        if (IsOpen)
            RefreshActiveTab();
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (IsOpen) return;
        if (!CanOpen()) return;

        ForceExitAiming();
        HideConfirm();
        SelectTab("run-status");
        RefreshAllTabs();

        _overlay.style.display = DisplayStyle.Flex;
        IsOpen = true;
        Time.timeScale = 0f;
    }

    public void Close()
    {
        if (!IsOpen) return;

        HideConfirm();
        _overlay.style.display = DisplayStyle.None;
        IsOpen = false;
        RestoreTimeScaleIfAllowed();
    }

    private bool CanOpen()
    {
        if (BuffSelectionController.Instance != null && BuffSelectionController.Instance.IsOverlayVisible)
            return false;

        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.State == GameState.GameOver) return false;
            if (GameManager.Instance.State == GameState.BuffSelection) return false;
        }

        if (HUDController.Instance != null && HUDController.Instance.IsGameOverVisible)
            return false;

        if (TowerManager.Instance != null && TowerManager.Instance.IsTowerInteractionPending)
            return false;

        return GameManager.Instance != null && GameManager.Instance.IsWaveSimActive();
    }

    /// <summary>
    /// 关闭暂停菜单时恢复 timeScale；Buff 界面仍打开时不强行设为 1。
    /// </summary>
    private void RestoreTimeScaleIfAllowed()
    {
        if (BuffSelectionController.Instance != null && BuffSelectionController.Instance.IsOverlayVisible)
            return;

        if (GameManager.Instance != null && GameManager.Instance.State == GameState.GameOver)
            return;

        Time.timeScale = 1f;
    }

    private static void ForceExitAiming()
    {
        if (SkillManager.Instance != null && SkillManager.Instance.IsAiming)
            SkillManager.Instance.AbortAiming();

        LaunchGuide.Instance?.Hide();
        ExecuteLockReticle.Instance?.Hide();
        SlowMoFX.Instance?.CancelSkillAim();
    }

    private void BindTab(string id, Button btn, VisualElement view)
    {
        if (btn == null || view == null) return;
        _tabButtons[id] = btn;
        _tabViews[id] = view;
        btn.RegisterCallback<ClickEvent>(_ => SelectTab(id));
    }

    private void SelectTab(string id)
    {
        _activeTab = id;
        foreach (var kv in _tabButtons)
            kv.Value.EnableInClassList("tab-active", kv.Key == id);
        foreach (var kv in _tabViews)
            kv.Value.EnableInClassList("tab-view-active", kv.Key == id);
        RefreshActiveTab();
    }

    private void RefreshActiveTab()
    {
        switch (_activeTab)
        {
            case "run-status": RefreshRunStatus(); break;
            case "skills": RefreshSkills(); break;
        }
    }

    private void RefreshAllTabs()
    {
        RefreshRunStatus();
        RefreshSkills();
    }

    private void RefreshRunStatus()
    {
        if (_buffList == null) return;

        _buffList.Clear();
        var bm = BuffManager.Instance;
        if (bm == null || bm.buffPool == null)
        {
            AddEmptyHint(_buffList, "暂无被动 Buff");
        }
        else
        {
            bool any = false;
            foreach (var def in bm.buffPool)
            {
                if (def == null) continue;
                int stacks = bm.GetStacks(def.effectType);
                if (stacks <= 0) continue;
                any = true;
                _buffList.Add(BuildBuffCard(def, stacks));
            }
            if (!any)
                AddEmptyHint(_buffList, "暂无被动 Buff");
        }

        _debuffList.Clear();
        DebuffManager.EnsureExists();
        var dm = DebuffManager.Instance;
        if (dm == null || dm.ActiveDebuffs.Count == 0)
        {
            AddEmptyHint(_debuffList, "本局暂无轮位税");
        }
        else
        {
            foreach (var id in dm.ActiveDebuffs)
            {
                var card = new VisualElement();
                card.AddToClassList("entry-card");
                var header = new VisualElement();
                header.AddToClassList("entry-header");
                var name = new Label($"{DebuffManager.GetDisplayName(id)}（{DebuffManager.GetTierLabel(id)}）");
                name.AddToClassList("entry-name");
                header.Add(name);
                card.Add(header);
                var desc = new Label(DebuffManager.GetDescription(id));
                desc.AddToClassList("entry-desc");
                card.Add(desc);
                _debuffList.Add(card);
            }
        }

        _statsSummary.Clear();
        var catalog = RunCatalog.Load();
        var ball = RunLoadout.GetSelectedBall(catalog);
        if (ball != null)
            AddStatLine($"弹珠 {ball.displayName}");

        if (bm != null)
        {
            AddStatLine($"弹珠伤害 +{bm.BallDamageBonus}");
            AddStatLine($"最大生命 +{bm.MaxHPBonus}");
            AddStatLine($"Combo 阈值 −{bm.ComboThresholdReduction}");
            AddStatLine($"击杀得分 +{bm.ScoreOnKillBonus:P0}");
            AddStatLine($"护心 {bm.HeartGuardCharges}/{bm.MaxHeartGuardCharges}");
            AddStatLine($"Epic 权重垫刀 {bm.EpicWeightPadding:P0}");
            if (GameManager.Instance != null)
                AddStatLine($"当前生命 {GameManager.Instance.Lives}/{GameManager.Instance.MaxLives}");
        }
    }

    private VisualElement BuildBuffCard(BuffDefinition def, int stacks)
    {
        var card = new VisualElement();
        card.AddToClassList("entry-card");

        var header = new VisualElement();
        header.AddToClassList("entry-header");

        var dot = new VisualElement();
        dot.AddToClassList("rarity-dot");
        dot.AddToClassList(GetRarityClass(def.rarity));
        header.Add(dot);

        var name = new Label(def.buffName);
        name.AddToClassList("entry-name");
        header.Add(name);

        var stackLbl = new Label($"{stacks}/{def.maxStacks}");
        stackLbl.AddToClassList("entry-stacks");
        header.Add(stackLbl);

        card.Add(header);

        var desc = new Label(def.GetBriefDescription());
        desc.AddToClassList("entry-desc");
        card.Add(desc);

        return card;
    }

    private void AddStatLine(string text)
    {
        var line = new Label(text);
        line.AddToClassList("stat-line");
        _statsSummary.Add(line);
    }

    private void RefreshSkills()
    {
        if (_skillCards == null) return;
        _skillCards.Clear();

        var sm = SkillManager.Instance;
        if (sm == null) return;

        for (int i = 0; i < sm.slots.Length; i++)
            _skillCards.Add(BuildSkillCard(sm.slots[i], i));
    }

    private VisualElement BuildSkillCard(SkillSlot slot, int index)
    {
        var card = new VisualElement();
        card.AddToClassList("skill-card");

        var def = slot.definition;
        var name = new Label(def != null ? def.displayName : "空槽");
        name.AddToClassList("skill-name");
        card.Add(name);

        float ratio = slot.CooldownRatio;
        bool ready = slot.IsReady;
        var cd = new Label(ready
            ? "就绪"
            : $"冷却 {slot.currentCD:F1}s / {slot.MaxCooldown:F0}s（{(1f - ratio) * 100f:F0}%）");
        cd.AddToClassList("skill-cd");
        card.Add(cd);

        var barBg = new VisualElement();
        barBg.AddToClassList("cd-bar-bg");
        var barFill = new VisualElement();
        barFill.AddToClassList("cd-bar-fill");
        if (ready) barFill.AddToClassList("ready");
        barFill.style.width = new StyleLength(new Length((1f - ratio) * 100f, LengthUnit.Percent));
        barBg.Add(barFill);
        card.Add(barBg);

        var desc = new Label(def != null ? def.GetBriefDescription() : string.Empty);
        desc.AddToClassList("skill-desc");
        card.Add(desc);

        if (def != null)
        {
            var key = new Label(def.GetSlotKeyHint(index));
            key.AddToClassList("skill-key-hint");
            card.Add(key);
        }

        return card;
    }

    private void PopulateSlotRulesStatic()
    {
        if (_slotFlowText != null)
        {
            _slotFlowText.text =
                "流程：Boss 击杀 → 三轮回转 → 按组合定轮生效 → 可选重转（可能挂轮位税）→ 领取。" +
                " 头奖需左中右三轮均为史诗。";
        }

        if (_comboCards != null)
        {
            _comboCards.Clear();
            foreach (var combo in RuleCombos)
            {
                var card = new VisualElement();
                card.AddToClassList("combo-card");
                card.AddToClassList(SlotMachineBuffRoller.GetComboBorderClass(combo));

                var title = new Label(SlotMachineBuffRoller.GetComboDisplayName(combo));
                title.AddToClassList("combo-name");
                card.Add(title);

                var trigger = new Label(SlotMachineBuffRoller.GetComboTriggerText(combo));
                trigger.AddToClassList("combo-trigger");
                card.Add(trigger);

                var rule = new Label(SlotMachineBuffRoller.GetComboRuleText(combo));
                rule.AddToClassList("combo-rule");
                card.Add(rule);
                _comboCards.Add(card);
            }
        }

        // 完整规则见 Notes/02_Systems/SlotMachine_Buff_Design.md
        if (_slotDocHint != null)
        {
            _slotDocHint.text =
                "完整规则见设计文档（Notes/02_Systems/SlotMachine_Buff_Design.md）。";
        }
    }

    private static void HideTabScrollbars(VisualElement root)
    {
        string[] scrollIds = { "run-status-view", "skills-view", "slot-rules-view", "settings-view" };
        foreach (var id in scrollIds)
        {
            var scroll = root.Q<ScrollView>(id);
            if (scroll == null) continue;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        }
    }

    private static void SetupSettingsStub(VisualElement root)
    {
        root.Q<Slider>("slider-master")?.SetEnabled(false);
        root.Q<Slider>("slider-sfx")?.SetEnabled(false);
        root.Q<Slider>("slider-music")?.SetEnabled(false);

        foreach (var row in root.Query(className: "settings-row").ToList())
            row.AddToClassList("settings-stub-disabled");

        var hintPc = root.Q<Label>("hint-pc");
        if (hintPc != null)
        {
            hintPc.text =
                "PC：Esc 暂停 · 右键斩击瞄准（左键确认）· Q/E 装备技能";
        }

        var hintMobile = root.Q<Label>("hint-mobile");
        if (hintMobile != null)
        {
            hintMobile.text =
                "移动端：右上角暂停 · 技能按钮松手确认瞄准 · 双槽装备技能";
        }
    }

    private void ShowConfirm(PendingConfirm action)
    {
        _pendingConfirm = action;
        if (_confirmText != null)
        {
            _confirmText.text = action == PendingConfirm.MainMenu
                ? "确认返回主菜单？本局进度将丢失。"
                : "确认退出游戏？";
        }
        _confirmBar?.AddToClassList("confirm-visible");
    }

    private void HideConfirm()
    {
        _pendingConfirm = PendingConfirm.None;
        _confirmBar?.RemoveFromClassList("confirm-visible");
    }

    private void OnConfirmYes()
    {
        var action = _pendingConfirm;
        HideConfirm();
        Time.timeScale = 1f;

        if (action == PendingConfirm.MainMenu)
            SceneManager.LoadScene("MainMenu");
        else if (action == PendingConfirm.Quit)
            Application.Quit();
    }

    private static void AddEmptyHint(VisualElement parent, string text)
    {
        var hint = new Label(text);
        hint.AddToClassList("empty-hint");
        parent.Add(hint);
    }

    private static string GetRarityClass(BuffRarity rarity) => rarity switch
    {
        BuffRarity.Common => "rarity-common",
        BuffRarity.Rare   => "rarity-rare",
        BuffRarity.Epic   => "rarity-epic",
        _ => "rarity-common"
    };
}
