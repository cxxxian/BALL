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
    private VisualElement _ballThumbList;
    private Label _ballNameLabel;
    private Label _ballRoleLabel;
    private Label _ballDescLabel;
    private Label _ballLockHint;
    private Button _equipBtn;
    private VisualElement _skillSlot0;
    private VisualElement _skillSlot1;
    private VisualElement _flipperWeaponList;

    private RunCatalog _catalog;
    private LoadoutReturnTarget _returnTarget = LoadoutReturnTarget.MainMenu;
    private int _browseBallIndex;
    private readonly List<BallDefinition> _browseBalls = new List<BallDefinition>();

    private Vector2 _swipeStart;
    private bool _swipeTracking;
    private const float SwipeThresholdPx = 56f;

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
        _ballThumbList = root.Q<VisualElement>("BallThumbList");
        _ballNameLabel = root.Q<Label>("BallNameLabel");
        _ballRoleLabel = root.Q<Label>("BallRoleLabel");
        _ballDescLabel = root.Q<Label>("BallDescLabel");
        _ballLockHint = root.Q<Label>("BallLockHint");
        _equipBtn = root.Q<Button>("BtnEquipBall");
        _skillSlot0 = root.Q<VisualElement>("SkillSlot0");
        _skillSlot1 = root.Q<VisualElement>("SkillSlot1");
        _flipperWeaponList = root.Q<VisualElement>("FlipperWeaponList");

        root.Q<Button>("BtnLoadoutBack")?.RegisterCallback<ClickEvent>(_ => Hide());
        root.Q<Button>("BtnBallPrev")?.RegisterCallback<ClickEvent>(_ => BrowseBall(-1));
        root.Q<Button>("BtnBallNext")?.RegisterCallback<ClickEvent>(_ => BrowseBall(1));
        _equipBtn?.RegisterCallback<ClickEvent>(_ => TryEquipBrowseBall());
        BindBallSwipe(_ballPreviewHost);

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

        if (_equipBtn != null)
            NeonHoverGlow.Attach(_equipBtn);

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
            var weapon = RunLoadout.GetSelectedFlipperWeapon();
            string left = s0 != null ? s0.displayName : "—";
            string right = s1 != null ? s1.displayName : "—";
            string flip = weapon != null ? weapon.displayName : "—";
            skillsLabel.text = s1 != null
                ? $"{left} · {right} · FLIP:{flip}"
                : $"{left} · FLIP:{flip}";
        }
    }

    private void RefreshAll()
    {
        RebuildBrowseBallList();
        SyncBrowseIndexToSelection();
        RefreshBallColumn();
        RefreshFlipperWeapons();
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

    private void BindBallSwipe(VisualElement host)
    {
        if (host == null) return;

        host.RegisterCallback<PointerDownEvent>(evt =>
        {
            _swipeTracking = true;
            _swipeStart = evt.position;
            host.CapturePointer(evt.pointerId);
        });

        host.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!_swipeTracking) return;
            _swipeTracking = false;
            if (host.HasPointerCapture(evt.pointerId))
                host.ReleasePointer(evt.pointerId);

            float dx = evt.position.x - _swipeStart.x;
            if (Mathf.Abs(dx) < SwipeThresholdPx) return;
            BrowseBall(dx < 0 ? 1 : -1);
        });

        host.RegisterCallback<PointerCaptureOutEvent>(_ => { _swipeTracking = false; });
    }

    private void RefreshBallColumn()
    {
        var ball = GetBrowseBall();
        if (ball == null) return;

        bool unlocked = PlayerProfile.IsBallUnlocked(ball.ballId);
        bool equipped = RunLoadout.Data.ballId == ball.ballId;

        if (_ballNameLabel != null) _ballNameLabel.text = ball.displayName;
        if (_ballRoleLabel != null) _ballRoleLabel.text = FormatBallRole(ball);
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
        RefreshBallThumbs(ball);

        if (_equipBtn != null)
        {
            _equipBtn.EnableInClassList("btn-outline-cyan", unlocked && !equipped);
            _equipBtn.EnableInClassList("btn-outline-yellow", equipped || !unlocked);

            if (!unlocked)
            {
                _equipBtn.SetEnabled(false);
                _equipBtn.text = "未解锁";
            }
            else if (equipped)
            {
                _equipBtn.SetEnabled(false);
                _equipBtn.text = "已装备";
            }
            else
            {
                _equipBtn.SetEnabled(true);
                _equipBtn.text = "装备";
            }
        }
    }

    private void RefreshBallThumbs(BallDefinition activeBall)
    {
        if (_ballThumbList == null) return;
        _ballThumbList.Clear();

        string selectedId = RunLoadout.Data.ballId;
        for (int i = 0; i < _browseBalls.Count; i++)
        {
            var b = _browseBalls[i];
            bool unlocked = PlayerProfile.IsBallUnlocked(b.ballId);
            bool isActive = activeBall != null && b.ballId == activeBall.ballId;
            bool isEquipped = b.ballId == selectedId;

            var thumb = new VisualElement();
            thumb.AddToClassList("ball-thumb");
            if (isActive) thumb.AddToClassList("ball-thumb-active");
            if (isEquipped) thumb.AddToClassList("ball-thumb-equipped");
            if (!unlocked) thumb.AddToClassList("ball-thumb-locked");

            var orb = new VisualElement();
            orb.AddToClassList("ball-thumb-orb");
            orb.style.backgroundColor = b.glowColor;
            thumb.Add(orb);

            var name = new Label(b.displayName);
            name.AddToClassList("ball-thumb-name");
            thumb.Add(name);

            if (isEquipped)
            {
                var mark = new Label("EQ");
                mark.AddToClassList("ball-thumb-mark");
                thumb.Add(mark);
            }

            int captured = i;
            thumb.RegisterCallback<ClickEvent>(_ =>
            {
                _browseBallIndex = captured;
                RefreshBallColumn();
            });

            _ballThumbList.Add(thumb);
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

            var core = new VisualElement();
            core.AddToClassList("ball-pager-dot-core");
            core.pickingMode = PickingMode.Ignore;
            dot.Add(core);

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
            if (keyLbl != null) keyLbl.text = index == 0 ? "SLOT.01 · 主技能" : "SLOT.02 · 副技能";
            if (cdLbl != null) cdLbl.text = string.Empty;
            if (modeLbl != null) modeLbl.text = index == 1 ? "待定" : string.Empty;
            if (icon != null) icon.style.opacity = 0.35f;
            slotRoot.EnableInClassList("skill-slot-empty", true);
            slotRoot.EnableInClassList("skill-slot-locked", locked);
            return;
        }

        if (nameLbl != null) nameLbl.text = def.displayName;
        if (keyLbl != null) keyLbl.text = index == 0 ? "SLOT.01 · 主技能" : "SLOT.02 · 副技能";
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

    private void RefreshFlipperWeapons()
    {
        if (_flipperWeaponList == null) return;
        _flipperWeaponList.Clear();

        var weapons = FlipperWeaponCatalog.GetLoadoutWeapons();
        string selectedId = RunLoadout.Data.flipperWeaponId;
        int count = 0;
        foreach (var weapon in weapons)
        {
            if (weapon == null) continue;
            count++;
        }

        int index = 0;
        foreach (var weapon in weapons)
        {
            if (weapon == null) continue;
            bool selected = weapon.weaponId == selectedId;
            bool isLast = index == count - 1;

            var card = new VisualElement();
            card.AddToClassList("flipper-weapon-card");
            if (selected) card.AddToClassList("flipper-weapon-card-selected");
            if (isLast) card.AddToClassList("flipper-weapon-card-last");

            var swatch = new VisualElement();
            swatch.AddToClassList("flipper-weapon-swatch");
            swatch.style.backgroundColor = weapon.effectColor;
            card.Add(swatch);

            var copy = new VisualElement();
            copy.AddToClassList("flipper-weapon-copy");
            var name = new Label(weapon.displayName);
            name.AddToClassList("flipper-weapon-name");
            copy.Add(name);
            var desc = new Label(ResolveWeaponShortDesc(weapon));
            desc.AddToClassList("flipper-weapon-desc");
            copy.Add(desc);
            card.Add(copy);

            var state = new Label(selected ? "已装备" : "装备");
            state.AddToClassList("flipper-weapon-state");
            if (!selected) state.style.color = new StyleColor(new Color(0.55f, 0.7f, 0.78f));
            card.Add(state);

            string capturedId = weapon.weaponId;
            card.RegisterCallback<ClickEvent>(_ => TryEquipFlipperWeapon(capturedId));
            _flipperWeaponList.Add(card);
            index++;
        }
    }

    private void TryEquipFlipperWeapon(string weaponId)
    {
        if (!RunLoadout.TrySelectFlipperWeapon(weaponId)) return;
        LoadoutChanged?.Invoke();
        RefreshFlipperWeapons();
    }

    private static string FormatBallRole(BallDefinition ball)
    {
        if (ball == null || string.IsNullOrEmpty(ball.ballId)) return "CORE";
        return ball.ballId.Replace('_', ' ').ToUpperInvariant();
    }

    private static string ResolveWeaponShortDesc(FlipperWeaponDefinition weapon)
    {
        if (weapon == null) return string.Empty;
        if (!string.IsNullOrEmpty(weapon.loadoutDescription))
        {
            string d = weapon.loadoutDescription;
            int cut = d.IndexOf('·');
            if (cut > 0 && cut < 18) return d.Substring(0, cut).Trim();
            if (d.Length > 16) return d.Substring(0, 16) + "…";
            return d;
        }

        return weapon.weaponType switch
        {
            FlipperWeaponType.Cannon => "单体爆发",
            FlipperWeaponType.Bomb => "范围清场",
            FlipperWeaponType.Laser => "持续输出",
            _ => "挡板武器"
        };
    }

    private static Color GetCategoryColor(SkillCategory category) => category switch
    {
        SkillCategory.Defense => new Color(0.2f, 0.85f, 1f),
        SkillCategory.Control => new Color(0.75f, 0.45f, 1f),
        _ => new Color(1f, 0.35f, 0.45f)
    };
}
