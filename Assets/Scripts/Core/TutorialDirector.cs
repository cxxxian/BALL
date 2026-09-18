using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>协议校准：10 阶段 Protocol Interface 引导。</summary>
[DefaultExecutionOrder(50)]
public class TutorialDirector : MonoBehaviour
{
    private TutorialPromptUI _ui;
    private TutorialHighlight _highlight;
    private TutorialFingerHint _finger;
    private bool _skipRequested;
    private bool _running;
    private Coroutine _routine;

    private const TutorialInputMask CombatPlay =
        TutorialInputMask.PlayfieldBasic
        | TutorialInputMask.Launch
        | TutorialInputMask.Skill0
        | TutorialInputMask.Skill1;

    private const int TotalSteps = TutorialProtocolCopy.TotalSteps;

    private void Start()
    {
        if (!RunSession.IsTutorial)
        {
            enabled = false;
            return;
        }

        _ui = TutorialPromptUI.Ensure();
        _highlight = TutorialHighlight.Ensure();
        _finger = TutorialFingerHint.Ensure();

        if (GameManager.Instance != null)
            GameManager.Instance.onGameStart.AddListener(OnGameStart);

        if (GameManager.Instance != null && GameManager.Instance.IsWaveSimActive())
            Begin();
    }

    private void OnDestroy()
    {
        TutorialTimeControl.Exit();
        TutorialInputGate.Disable();
        FlipperWeaponController.SetTutorialFireLocked(false);
        ReleaseSkillPresentation();
        if (_highlight != null) _highlight.Clear();
        if (_finger != null) _finger.Hide();
        if (GameManager.Instance != null)
            GameManager.Instance.onGameStart.RemoveListener(OnGameStart);
    }

    private void Update()
    {
        if (_running)
            TutorialTimeControl.Maintain();
    }

    private void OnGameStart()
    {
        if (!RunSession.IsTutorial) return;
        Begin();
    }

    private void Begin()
    {
        if (_routine != null)
            StopCoroutine(_routine);
        TutorialTimeControl.Exit();
        FlipperWeaponController.SetTutorialFireLocked(true);
        _skipRequested = false;
        _running = true;
        _routine = StartCoroutine(RunTutorial());
    }

    private IEnumerator RunTutorial()
    {
        yield return null;
        _ui = TutorialPromptUI.Ensure();
        yield return null;

        WaveManager.Instance?.ClearTutorialField();

        yield return T01_Flippers();
        if (_skipRequested) { Finish(true); yield break; }

        yield return T02_Launch();
        if (_skipRequested) { Finish(true); yield break; }

        yield return T03_RedirectOnly();
        if (_skipRequested) { Finish(true); yield break; }

        yield return T04_ArmE();
        if (_skipRequested) { Finish(true); yield break; }

        yield return T05_EthenQ();
        if (_skipRequested) { Finish(true); yield break; }

        yield return T06_Combo();
        if (_skipRequested) { Finish(true); yield break; }

        yield return T07_FlipperWeapon();
        if (_skipRequested) { Finish(true); yield break; }

        yield return T08_BossAndParry();
        if (_skipRequested) { Finish(true); yield break; }

        yield return T09_Slot();
        if (_skipRequested) { Finish(true); yield break; }

        yield return T10_FreePlay();
        Finish(false);
    }

    // ── T01 挡板 ─────────────────────────────────────────────────────────

    private IEnumerator T01_Flippers()
    {
        bool started = false;
        yield return ProtocolStep(1, "控制挡板", "接住球，再把它弹回去。", TutorialProtocolCopy.HintFlipper,
            TutorialTimeMode.HardPause, () => started, TutorialInputMask.UiOnly,
            TutorialTerminalMode.Brief, TutorialProtocolAnchor.Bottom, showContinue: true,
            onContinue: () => started = true);

        _finger?.ShowFlipperLeftOnce();
        _highlight?.PulseSkillSlot(0, 0.01f);

        bool left = false;
        yield return ProtocolStep(1, "控制挡板", null, TutorialProtocolCopy.HintFlipperLeft,
            TutorialTimeMode.Normal,
            () =>
            {
                if (InputManager.Instance != null && InputManager.Instance.LeftFlipperPressed)
                    left = true;
                return left;
            },
            TutorialInputMask.FlipperLeft | TutorialInputMask.Pause,
            TutorialTerminalMode.Compact, TutorialProtocolAnchor.Bottom);

        _finger?.ShowFlipperRightOnce();

        bool right = false;
        yield return ProtocolStep(1, "控制挡板", null, TutorialProtocolCopy.HintFlipperRight,
            TutorialTimeMode.Normal,
            () =>
            {
                if (InputManager.Instance != null && InputManager.Instance.RightFlipperPressed)
                    right = true;
                return right;
            },
            TutorialInputMask.FlipperRight | TutorialInputMask.Pause,
            TutorialTerminalMode.Compact, TutorialProtocolAnchor.Bottom);
    }

    private IEnumerator T02_Launch()
    {
        yield return ProtocolStep(2, "发射弹珠", "准备好了，就让球进入战场。", TutorialProtocolCopy.HintLaunch,
            TutorialTimeMode.HardPause,
            () => BallController.Instance != null && !BallController.Instance.IsWaitingForLaunch,
            TutorialInputMask.Launch,
            TutorialTerminalMode.Compact, TutorialProtocolAnchor.Bottom);
    }

    // ── T03 仅 Q 改向 ───────────────────────────────────────────────────

    private IEnumerator T03_RedirectOnly()
    {
        yield return EnsureBallInPlay();
        SkillManager.Instance?.ClearExecuteArm();

        bool brief = false;
        yield return ProtocolStep(3, "改变球路", "按 Q 瞄准，确认后改变球的方向。", TutorialProtocolCopy.HintRedirect,
            TutorialTimeMode.HardPause, () => brief, TutorialInputMask.UiOnly,
            TutorialTerminalMode.Brief, TutorialProtocolAnchor.Skills, showContinue: true,
            onContinue: () => brief = true);

        RefreshSkillReady(0);
        _highlight?.PulseSkillSlot(0);

        bool redirectDone = false;
        void OnFired(Vector2 dir)
        {
            if (dir.sqrMagnitude > 0.001f && SkillManager.Instance != null && !SkillManager.Instance.IsExecuteArmed)
                redirectDone = true;
        }

        if (SkillManager.Instance != null)
            SkillManager.Instance.onFired.AddListener(OnFired);

        yield return ProtocolStep(3, "改变球路", null, TutorialProtocolCopy.HintRedirect,
            TutorialTimeMode.SlowMo, () => redirectDone, TutorialInputMask.Skill0 | CombatPlay,
            TutorialTerminalMode.Compact, TutorialProtocolAnchor.Skills,
            timeout: 50f, whileWaiting: EnsureBallPlayable);

        if (SkillManager.Instance != null)
            SkillManager.Instance.onFired.RemoveListener(OnFired);

        ResetTutorialCombatState();
    }

    // ── T04 E ───────────────────────────────────────────────────────────

    private IEnumerator T04_ArmE()
    {
        yield return EnsureBallInPlay();
        RefreshSkillReady(1);
        _highlight?.PulseSkillSlot(1);

        bool armed = false;
        UnityAction onArm = () => armed = true;
        if (SkillManager.Instance != null)
            SkillManager.Instance.onExecuteArmStarted.AddListener(onArm);

        yield return ProtocolStep(4, "释放身份技", "标准球可以启动斩杀武装。", TutorialProtocolCopy.HintIdentityE,
            TutorialTimeMode.Normal, () => armed || (SkillManager.Instance != null && SkillManager.Instance.IsExecuteArmed),
            TutorialInputMask.Skill1 | CombatPlay,
            TutorialTerminalMode.Compact, TutorialProtocolAnchor.Skills,
            timeout: 40f, whileWaiting: EnsureBallPlayable);

        if (SkillManager.Instance != null)
            SkillManager.Instance.onExecuteArmStarted.RemoveListener(onArm);
    }

    // ── T05 E→Q ─────────────────────────────────────────────────────────

    private IEnumerator T05_EthenQ()
    {
        WaveManager.Instance?.ClearTutorialField();
        var def = WaveManager.Instance?.GetTutorialMinionDefinition();
        if (def != null && WaveManager.Instance != null)
        {
            foreach (var pos in new[] { new Vector3(-0.9f, 3.4f, 0f), new Vector3(0.6f, 3.2f, 0f), new Vector3(0f, 4f, 0f) })
            {
                var d = WaveManager.Instance.SpawnMinion(def, pos, 0);
                if (d == null) continue;
                d.moveSpeed = 0f;
                d.maxHits = 4;
                d.TutorialExecuteOnlyHits = true;
            }
        }

        yield return EnsureBallInPlay();
        RefreshSkillReady(0);
        RefreshSkillReady(1);

        if (SkillManager.Instance != null && !SkillManager.Instance.IsExecuteArmed)
        {
            SkillManager.Instance.TutorialExtendExecuteArm(60f);
        }

        bool chained = false;
        UnityAction onChain = () => chained = true;
        if (SkillManager.Instance != null)
            SkillManager.Instance.onExecuteChainStarted.AddListener(onChain);

        yield return ProtocolStep(5, "组合技能", "先启动武装，再用 Q 把球送向目标。", TutorialProtocolCopy.HintComboEthenQ,
            TutorialTimeMode.Normal, () => chained, TutorialInputMask.Skill0 | TutorialInputMask.Skill1 | CombatPlay,
            TutorialTerminalMode.Compact, TutorialProtocolAnchor.Skills,
            timeout: 55f,
            whileWaiting: () =>
            {
                EnsureBallPlayable();
                RefreshSkillReady(0);
                if (SkillManager.Instance != null && !SkillManager.Instance.IsExecuteArmed)
                    RefreshSkillReady(1);
            });

        if (SkillManager.Instance != null)
            SkillManager.Instance.onExecuteChainStarted.RemoveListener(onChain);

        yield return WaitForExecuteChainComplete();
        ResetTutorialCombatState();
        WaveManager.Instance?.ClearTutorialField();
    }

    // ── T06 Combo ───────────────────────────────────────────────────────

    private IEnumerator T06_Combo()
    {
        ResetTutorialCombatState();
        int targetCombo = 5;
        int startCombo = ComboSystem.Instance != null ? ComboSystem.Instance.CurrentCombo : 0;

        yield return ProtocolStep(6, "保持连击", "连续命中会增加 Combo，并加快技能恢复。", null,
            TutorialTimeMode.Normal,
            () => ComboSystem.Instance != null && ComboSystem.Instance.CurrentCombo >= startCombo + targetCombo,
            CombatPlay,
            TutorialTerminalMode.Compact, TutorialProtocolAnchor.Combo,
            timeout: 45f,
            whileWaiting: () =>
            {
                EnsureBallPlayable();
                _highlight?.PulseCombo(0.01f);
            });
    }

    // ── T07 挡板武器 ────────────────────────────────────────────────────

    private IEnumerator T07_FlipperWeapon()
    {
        FlipperWeaponController.SetTutorialFireLocked(false);
        FlipperWeaponController.Instance?.TutorialFillEnergyForLesson();
        _highlight?.PulseFlipperWeaponHud();

        bool fired = false;
        UnityAction onFire = () => fired = true;
        if (FlipperWeaponController.Instance != null)
            FlipperWeaponController.Instance.onWeaponFired.AddListener(onFire);

        yield return ProtocolStep(7, "挡板也是武器", "Combo 会为武器充能，满能量后按住挡板接球即可释放。", TutorialProtocolCopy.HintPerfectFlip,
            TutorialTimeMode.Normal, () => fired, CombatPlay,
            TutorialTerminalMode.Compact, TutorialProtocolAnchor.Bottom,
            timeout: 60f, whileWaiting: EnsureBallPlayable);

        if (FlipperWeaponController.Instance != null)
            FlipperWeaponController.Instance.onWeaponFired.RemoveListener(onFire);
    }

    // ── T08 Boss + 弹刀 ─────────────────────────────────────────────────

    private IEnumerator T08_BossAndParry()
    {
        WaveManager.Instance?.ClearTutorialField();
        var boss = WaveManager.Instance?.SpawnTutorialBoss(0, parryDummy: true);
        boss?.ConfigureTutorialParryDummy();

        bool parryDone = false;

        void OnParry(bool _) => parryDone = true;
        BezierMissile.OnAnyParryBossHit += OnParry;

        float nextFireAt = 0f;
        yield return ProtocolStep(8, "找到节奏", "这面靶不会掉血。看到导弹，就把它弹开。", TutorialProtocolCopy.HintParry,
            TutorialTimeMode.Normal,
            () => parryDone,
            CombatPlay | TutorialInputMask.Parry,
            TutorialTerminalMode.Compact, TutorialProtocolAnchor.Top,
            timeout: 75f,
            whileWaiting: () =>
            {
                EnsureBallPlayable();
                if (boss == null) return;
                var missile = boss.GetComponent<BossMissileAttack>();
                if (missile == null || missile.HasActiveMissile) return;
                if (Time.unscaledTime < nextFireAt) return;
                if (missile.TryFireImmediate())
                    nextFireAt = Time.unscaledTime + 4.5f;
                else
                    nextFireAt = Time.unscaledTime + 0.8f;
            });

        BezierMissile.OnAnyParryBossHit -= OnParry;
        WaveManager.Instance?.ClearTutorialField();
        TutorialTimeControl.Exit();
        yield return new WaitForSecondsRealtime(0.8f);
    }

    // ── T09 老虎机 ──────────────────────────────────────────────────────

    private IEnumerator T09_Slot()
    {
        WaveManager.Instance?.ClearTutorialField();
        TutorialTimeControl.Exit();
        TutorialInputGate.Disable();

        bool ack = false;
        yield return ProtocolStep(9, "选择强化", "清除波次后，选择一个强化继续战斗。", TutorialProtocolCopy.HintSlotSpin,
            TutorialTimeMode.HardPause, () => ack, TutorialInputMask.UiOnly,
            TutorialTerminalMode.Brief, TutorialProtocolAnchor.Center, showContinue: true,
            onContinue: () => ack = true);

        if (GameManager.Instance != null)
            GameManager.Instance.CompleteWave();

        float waitOpen = 2.5f;
        while (waitOpen > 0f && (BuffSelectionController.Instance == null
                                || !BuffSelectionController.Instance.IsOverlayVisible))
        {
            waitOpen -= Time.unscaledDeltaTime;
            yield return null;
        }

        BuffSelectionController.Instance?.SetTutorialGate(SlotTutorialGate.SpinOnly);

        yield return ProtocolStep(9, "选择强化", null, TutorialProtocolCopy.HintSlotSpin,
            TutorialTimeMode.Normal,
            () => BuffSelectionController.Instance != null && BuffSelectionController.Instance.IsResolvePreviewPhase,
            TutorialInputMask.Pause,
            TutorialTerminalMode.Compact, TutorialProtocolAnchor.SlotTop,
            timeout: 90f);

        BuffSelectionController.Instance?.TutorialEnsureFreeRerolls(1);
        BuffSelectionController.Instance?.SetTutorialGate(SlotTutorialGate.RerollOnly);

        int rerollsBefore = BuffSelectionController.Instance?.TutorialRerollCount ?? 0;
        yield return ProtocolStep(9, "选择强化", "不满意可重摇一轮。", TutorialProtocolCopy.HintSlotReroll,
            TutorialTimeMode.Normal,
            () =>
            {
                var slot = BuffSelectionController.Instance;
                return slot == null || !slot.IsOverlayVisible
                       || slot.TutorialRerollCount > rerollsBefore;
            },
            TutorialInputMask.Pause,
            TutorialTerminalMode.Compact, TutorialProtocolAnchor.SlotTop,
            timeout: 45f, allowTimeoutPass: true);

        BuffSelectionController.Instance?.SetTutorialGate(SlotTutorialGate.ClaimOnly);

        yield return ProtocolStep(9, "选择强化", null, "领取",
            TutorialTimeMode.Normal,
            () => GameManager.Instance != null && GameManager.Instance.State == GameState.Playing,
            TutorialInputMask.Pause,
            TutorialTerminalMode.Compact, TutorialProtocolAnchor.SlotTop,
            timeout: 120f);

        BuffSelectionController.Instance?.SetTutorialGate(SlotTutorialGate.None);
        if (GameManager.Instance != null && GameManager.Instance.State == GameState.BuffSelection)
            GameManager.Instance.OnBuffSelectionDone();
    }

    // ── T10 自由 ────────────────────────────────────────────────────────

    private IEnumerator T10_FreePlay()
    {
        TutorialInputGate.Disable();
        TutorialTimeControl.Exit();
        WaveManager.Instance?.ClearTutorialField();
        var boss = WaveManager.Instance?.SpawnTutorialBoss(0);

        bool ack = false;
        yield return ProtocolStep(10, "现在，交给你", "下一波已经进场。控制球、维持 Combo，用你的方式击破。", null,
            TutorialTimeMode.HardPause, () => ack, TutorialInputMask.UiOnly,
            TutorialTerminalMode.Brief, TutorialProtocolAnchor.Top, showContinue: true,
            onContinue: () => ack = true);

        yield return ProtocolStep(10, "现在，交给你", null, null,
            TutorialTimeMode.Normal,
            () => boss == null || boss.IsDead,
            CombatPlay,
            TutorialTerminalMode.Compact, TutorialProtocolAnchor.Top,
            timeout: 180f, allowTimeoutPass: true,
            whileWaiting: EnsureBallPlayable);

        bool finishAck = false;
        yield return ProtocolStep(10, "校准完成", "你已经掌握基础战斗。协议币可在商店解锁弹珠。", null,
            TutorialTimeMode.HardPause, () => finishAck, TutorialInputMask.UiOnly,
            TutorialTerminalMode.Brief, TutorialProtocolAnchor.Center, showContinue: true,
            onContinue: () => finishAck = true);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private IEnumerator EnsureBallInPlay()
    {
        if (BallController.Instance != null && !BallController.Instance.IsWaitingForLaunch)
            yield break;

        yield return ProtocolStep(2, "发射弹珠", "先重新发球。", TutorialProtocolCopy.HintLaunch,
            TutorialTimeMode.HardPause,
            () => BallController.Instance != null && !BallController.Instance.IsWaitingForLaunch,
            TutorialInputMask.Launch | TutorialInputMask.Pause,
            TutorialTerminalMode.Compact, TutorialProtocolAnchor.Bottom);
    }

    private void Finish(bool skipped)
    {
        if (!_running) return;
        _running = false;
        TutorialTimeControl.Exit();
        TutorialInputGate.Disable();
        FlipperWeaponController.SetTutorialFireLocked(false);
        ReleaseSkillPresentation();
        BuffSelectionController.Instance?.SetTutorialGate(SlotTutorialGate.None);
        if (_highlight != null) _highlight.Clear();
        if (_finger != null) _finger.Hide();
        _ui?.Hide();

        PlayerProfile.Load();
        PlayerProfile.MarkTutorialCompleted();
        WaveManager.Instance?.ClearTutorialField();

        if (GameManager.Instance != null)
            GameManager.Instance.TriggerGameOver();
    }

    private IEnumerator ProtocolStep(
        int stepIndex,
        string title,
        string description,
        string hint,
        TutorialTimeMode timeMode,
        Func<bool> gate,
        TutorialInputMask inputMask,
        TutorialTerminalMode uiMode,
        TutorialProtocolAnchor anchor,
        bool showContinue = false,
        float timeout = 0f,
        bool allowTimeoutPass = false,
        Action onContinue = null,
        Action whileWaiting = null)
    {
        TutorialTimeControl.Enter(timeMode);
        TutorialInputGate.Enable(inputMask);

        bool cont = false;
        _ui.ShowProtocol(
            stepIndex,
            TotalSteps,
            title,
            description,
            hint,
            uiMode,
            anchor,
            showContinue,
            onContinue: () =>
            {
                cont = true;
                onContinue?.Invoke();
            },
            onSkip: () => _skipRequested = true);

        yield return null;

        float left = timeout > 0f ? timeout : float.PositiveInfinity;
        Func<bool> pass = gate;
        while (!_skipRequested)
        {
            whileWaiting?.Invoke();
            TutorialTimeControl.Maintain();

            // 改向瞄准还开着时不要切下一拍：否则 timeScale 被教程态抢走，箭头和后处理会留下。
            if (IsSkillPresentationBusy())
            {
                if (timeout > 0f)
                {
                    left -= Time.unscaledDeltaTime;
                    if (left <= 0f)
                    {
                        ReleaseSkillPresentation();
                        if (allowTimeoutPass) break;
                        left = timeout;
                    }
                }

                yield return null;
                continue;
            }

            if (showContinue && cont) break;
            if (!showContinue && pass != null && pass()) break;

            if (timeout > 0f)
            {
                left -= Time.unscaledDeltaTime;
                if (left <= 0f)
                {
                    if (allowTimeoutPass) break;
                    left = timeout;
                }
            }

            yield return null;
        }

        if (_skipRequested)
        {
            ReleaseSkillPresentation();
            TutorialTimeControl.Exit();
            TutorialInputGate.Disable();
            _ui.Hide();
            yield break;
        }

        if (!showContinue)
        {
            _ui.ShowSuccess();
            yield return new WaitForSecondsRealtime(0.4f);
        }

        while (IsSkillPresentationBusy() && !_skipRequested)
            yield return null;

        if (_skipRequested)
        {
            ReleaseSkillPresentation();
            TutorialTimeControl.Exit();
            TutorialInputGate.Disable();
            _ui.Hide();
            yield break;
        }

        TutorialTimeControl.Exit();
        TutorialInputGate.Disable();
        _ui.Hide();
        yield return new WaitForSecondsRealtime(0.1f);
    }

    private static void RefreshSkillReady(int slotIndex)
    {
        var sm = SkillManager.Instance;
        if (sm?.slots == null || slotIndex < 0 || slotIndex >= sm.slots.Length) return;
        sm.slots[slotIndex].currentCD = 0f;
        sm.onSlotCooldownChanged.Invoke(slotIndex, 0f);
    }

    private static void EnsureBallPlayable() { }

    private IEnumerator WaitForExecuteChainComplete(float timeout = 14f)
    {
        while (IsSkillPresentationBusy())
            yield return null;

        TutorialTimeControl.Exit();
        var ball = BallController.Instance;
        if (ball == null || !ball.IsExecuteChainActive) yield break;

        _ui?.Hide();
        float left = timeout;
        while (ball != null && ball.IsExecuteChainActive && left > 0f)
        {
            left -= Time.unscaledDeltaTime;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.55f);
    }

    private static bool IsSkillPresentationBusy()
    {
        var sm = SkillManager.Instance;
        if (sm != null && (sm.IsAiming || sm.IsGroundAiming)) return true;
        return SlowMoFX.Instance != null && SlowMoFX.Instance.IsSkillSlowMoActive;
    }

    /// <summary>打断改向时，时间、后处理、瞄准箭头一起收掉，避免只恢复 timeScale。</summary>
    private static void ReleaseSkillPresentation()
    {
        bool busy = IsSkillPresentationBusy();
        SkillManager.Instance?.CancelAiming();
        SkillManager.Instance?.CancelGroundAim();
        var guide = LaunchGuide.Instance;
        if (guide != null) guide.Hide();
        if (busy)
            SlowMoFX.Instance?.CancelSkillAim();
    }

    private static void ResetTutorialCombatState()
    {
        ReleaseSkillPresentation();
        SkillManager.Instance?.ClearExecuteArm();

        if (BallController.Instance != null && BallController.Instance.IsExecuteChainActive)
            BallController.Instance.StopExecuteChain();

        SlowMoFX.Instance?.ForceRestore();

        var sm = SkillManager.Instance;
        if (sm?.slots == null) return;
        for (int i = 0; i < sm.slots.Length; i++)
        {
            sm.slots[i].currentCD = 0f;
            sm.onSlotCooldownChanged.Invoke(i, 0f);
        }
    }
}
