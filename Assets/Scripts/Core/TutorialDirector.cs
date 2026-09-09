using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 协议校准导演（旁白门控初版）：
/// Narrate → 时间态 → 等正确输入/结果 → 旁白消失 → 下一拍。
/// </summary>
[DefaultExecutionOrder(50)]
public class TutorialDirector : MonoBehaviour
{
    private TutorialPromptUI _ui;
    private bool _skipRequested;
    private bool _running;
    private Coroutine _routine;

    /// <summary>实战拍：挡板 + 发球 + 技能均可。</summary>
    private const TutorialInputMask CombatPlay =
        TutorialInputMask.PlayfieldBasic
        | TutorialInputMask.Launch
        | TutorialInputMask.Skill0
        | TutorialInputMask.Skill1;

    private void Start()
    {
        if (!RunSession.IsTutorial)
        {
            enabled = false;
            return;
        }

        _ui = TutorialPromptUI.Ensure();
        if (GameManager.Instance != null)
            GameManager.Instance.onGameStart.AddListener(OnGameStart);

        if (GameManager.Instance != null && GameManager.Instance.IsWaveSimActive())
            Begin();
    }

    private void OnDestroy()
    {
        TutorialTimeControl.Exit();
        TutorialInputGate.Disable();
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
        _skipRequested = false;
        _running = true;
        _routine = StartCoroutine(RunTutorial());
    }

    private IEnumerator RunTutorial()
    {
        yield return null;
        // 确保终端 UI 已挂好
        _ui = TutorialPromptUI.Ensure();
        yield return null;
        yield return null;

        WaveManager.Instance?.ClearTutorialField();

        // 1) 挡板 → 2) 发球 → 3) 技能 → 4) 机制
        // 球还在发射槽时练 Z/X，无需冻结台面，挡板不会卡顿
        yield return IntroBeat();
        if (_skipRequested) { Finish(skipped: true); yield break; }

        yield return FlipperExplainBeat();
        if (_skipRequested) { Finish(skipped: true); yield break; }

        yield return FlipperBeat();
        if (_skipRequested) { Finish(skipped: true); yield break; }

        yield return LaunchExplainBeat();
        if (_skipRequested) { Finish(skipped: true); yield break; }

        yield return LaunchBeat();
        if (_skipRequested) { Finish(skipped: true); yield break; }

        yield return SkillOnDummyBeat();
        if (_skipRequested) { Finish(skipped: true); yield break; }

        yield return BumperBeat();
        if (_skipRequested) { Finish(skipped: true); yield break; }

        yield return ThreatBeat();
        if (_skipRequested) { Finish(skipped: true); yield break; }

        yield return BossBeat();
        if (_skipRequested) { Finish(skipped: true); yield break; }

        yield return ParryBeat();
        if (_skipRequested) { Finish(skipped: true); yield break; }

        yield return BuffBeat();
        if (_skipRequested) { Finish(skipped: true); yield break; }

        Finish(skipped: false);
    }

    // ── Beats ─────────────────────────────────────────────────────────────

    private IEnumerator IntroBeat()
    {
        bool done = false;
        yield return Narrate(
            step: "SEQ 01 / BOOT",
            line: "> 协议校准启动。\n> 顺序：挡板 → 发球 → 技能 → 战场机制 → 老虎机重摇。",
            keyHint: null,
            mode: TutorialTimeMode.HardPause,
            gate: () => done,
            inputMask: TutorialInputMask.UiOnly,
            terminalMode: TutorialTerminalMode.Brief,
            showContinue: true,
            onContinue: () => done = true);
    }

    private IEnumerator FlipperExplainBeat()
    {
        bool done = false;
        yield return Narrate(
            step: "SEQ 02 / FLIP",
            line: "> 弹珠还在发射槽。\n> 先熟悉左右挡板——球飞起来前就把手感练好。",
#if UNITY_ANDROID || UNITY_IOS
            keyHint: "下一步：练习底栏左右挡板",
#else
            keyHint: "下一步：练习 Z / X 挡板",
#endif
            mode: TutorialTimeMode.HardPause,
            gate: () => done,
            inputMask: TutorialInputMask.UiOnly,
            terminalMode: TutorialTerminalMode.Brief,
            showContinue: true,
            onContinue: () => done = true);
    }

    private IEnumerator FlipperBeat()
    {
        // 球在槽内：Normal 流速即可，挡板 FixedUpdate 正常，无需 SoftFreeze
        bool left = false;
        yield return Narrate(
            step: "SEQ 03 / FLIP_L",
            line: "> 左挡板：按住试一下手感。发球后用它托住弹珠。",
#if UNITY_ANDROID || UNITY_IOS
            keyHint: "[ 按住 · 屏幕底栏左侧 ]",
#else
            keyHint: "[ 按住 Z 或 ← ]",
#endif
            mode: TutorialTimeMode.Normal,
            gate: () =>
            {
                if (InputManager.Instance != null && InputManager.Instance.LeftFlipperPressed)
                    left = true;
                return left;
            },
            inputMask: TutorialInputMask.FlipperLeft | TutorialInputMask.Pause,
            terminalMode: TutorialTerminalMode.Compact);

        if (_skipRequested) yield break;

        bool right = false;
        yield return Narrate(
            step: "SEQ 04 / FLIP_R",
            line: "> 右挡板：同样按住。左右配合才能稳住局面。",
#if UNITY_ANDROID || UNITY_IOS
            keyHint: "[ 按住 · 屏幕底栏右侧 ]",
#else
            keyHint: "[ 按住 X 或 → ]",
#endif
            mode: TutorialTimeMode.Normal,
            gate: () =>
            {
                if (InputManager.Instance != null && InputManager.Instance.RightFlipperPressed)
                    right = true;
                return right;
            },
            inputMask: TutorialInputMask.FlipperRight | TutorialInputMask.Pause,
            terminalMode: TutorialTerminalMode.Compact);
    }

    private IEnumerator LaunchExplainBeat()
    {
        bool done = false;
        yield return Narrate(
            step: "SEQ 05 / LAUNCH",
            line: "> 挡板就绪。\n> 接下来发射弹珠——这是一切对局的起点。",
#if UNITY_ANDROID || UNITY_IOS
            keyHint: "下一步：点击屏幕发球",
#else
            keyHint: "下一步：左键或空格发球",
#endif
            mode: TutorialTimeMode.HardPause,
            gate: () => done,
            inputMask: TutorialInputMask.UiOnly,
            terminalMode: TutorialTerminalMode.Brief,
            showContinue: true,
            onContinue: () => done = true);
    }

    private IEnumerator LaunchBeat()
    {
        yield return Narrate(
            step: "SEQ 06 / FIRE",
            line: "> 现在，发射弹珠。",
#if UNITY_ANDROID || UNITY_IOS
            keyHint: "[ 点击屏幕 ]",
#else
            keyHint: "[ 左键 / 空格 ]",
#endif
            mode: TutorialTimeMode.HardPause,
            gate: () => BallController.Instance != null && !BallController.Instance.IsWaitingForLaunch,
            inputMask: TutorialInputMask.Launch,
            terminalMode: TutorialTerminalMode.Compact);
    }

    private IEnumerator SkillOnDummyBeat()
    {
        WaveManager.Instance?.ClearTutorialField();

        // 放 3 只静止靶，对应斩杀连锁上限
        var def = WaveManager.Instance?.GetTutorialMinionDefinition();
        if (def != null && WaveManager.Instance != null)
        {
            Vector3[] spots =
            {
                new Vector3(-0.9f, 3.4f, 0f),
                new Vector3(0.6f, 3.2f, 0f),
                new Vector3(0.0f, 4.0f, 0f)
            };
            foreach (var pos in spots)
            {
                var dummy = WaveManager.Instance.SpawnMinion(def, pos, 0);
                if (dummy == null) continue;
                dummy.moveSpeed = 0f;
                dummy.maxHits = 4;
                // 开链前普攻不掉血，避免误杀后无法练 E→Q
                dummy.TutorialExecuteOnlyHits = true;
            }
        }

        bool ack = false;
        yield return Narrate(
            step: "SEQ 07 / SKILL",
            line: "> 标准弹珠连招：先 E 武装斩杀，再立刻 Q 协议改向确认。\n> 武装窗口内确认，可连锁锁定最多 3 个敌人。",
            keyHint: "连招：E → Q",
            mode: TutorialTimeMode.HardPause,
            gate: () => ack,
            inputMask: TutorialInputMask.UiOnly,
            terminalMode: TutorialTerminalMode.Brief,
            showContinue: true,
            onContinue: () => ack = true);

        if (_skipRequested) yield break;

        yield return EnsureBallInPlayOrRelaunch("SEQ 08 / RELAUNCH");
        if (_skipRequested) yield break;

        // ── 先 E：斩杀武装 ──
        RefreshSkillReady(1);
        bool armed = false;
        UnityAction onArm = () => armed = true;
        if (SkillManager.Instance != null)
            SkillManager.Instance.onExecuteArmStarted.AddListener(onArm);

        yield return Narrate(
            step: "SEQ 08 / ARM",
            line: "> 先按 E：武装斩杀协议。\n> 武装后有短暂窗口，必须马上接 Q。",
#if UNITY_ANDROID || UNITY_IOS
            keyHint: "[ 技能槽 2 / E ]",
#else
            keyHint: "[ E ]",
#endif
            mode: TutorialTimeMode.Normal,
            gate: () => armed || (SkillManager.Instance != null && SkillManager.Instance.IsExecuteArmed),
            inputMask: TutorialInputMask.Skill1 | TutorialInputMask.Launch | TutorialInputMask.Flippers | TutorialInputMask.Pause,
            terminalMode: TutorialTerminalMode.Compact,
            timeout: 40f);

        if (SkillManager.Instance != null)
            SkillManager.Instance.onExecuteArmStarted.RemoveListener(onArm);

        if (_skipRequested) yield break;

        // ── 紧接 Q：改向确认开链 ──
        RefreshSkillReady(0);
        RefreshSkillReady(1);
        SkillManager.Instance?.TutorialExtendExecuteArm(60f);

        bool chained = false;
        UnityAction onChain = () => chained = true;
        if (SkillManager.Instance != null)
            SkillManager.Instance.onExecuteChainStarted.AddListener(onChain);

        yield return Narrate(
            step: "SEQ 09 / CHAIN",
            line: "> 立刻 Q：右键瞄准，左键确认。\n> 在武装状态下确认，开启最多 3 段斩杀连锁。",
#if UNITY_ANDROID || UNITY_IOS
            keyHint: "[ 若武装消失先 E，再瞄准确认 ]",
#else
            keyHint: "[ 右键瞄准 · 左键确认 ]（可先补 E）",
#endif
            mode: TutorialTimeMode.Normal,
            gate: () => chained,
            inputMask: TutorialInputMask.Skill0 | TutorialInputMask.Skill1 | TutorialInputMask.Launch | TutorialInputMask.Flippers | TutorialInputMask.Pause,
            terminalMode: TutorialTerminalMode.Compact,
            timeout: 55f,
            whileWaiting: () =>
            {
                var sm = SkillManager.Instance;
                if (sm == null || chained) return;
                RefreshSkillReady(0);
                if (!sm.IsExecuteArmed)
                    RefreshSkillReady(1);
            });

        if (SkillManager.Instance != null)
            SkillManager.Instance.onExecuteChainStarted.RemoveListener(onChain);

        if (_skipRequested) yield break;

        // 连锁在 HardPause 下只启动，须等时间恢复后跑完再清场，否则会残留斩杀态
        yield return WaitForExecuteChainComplete();
        ResetTutorialCombatState();
        WaveManager.Instance?.ClearTutorialField();
    }

    /// <summary>掉球后卡在发球等待时，单独开一拍让玩家重发，避免技能教学死锁。</summary>
    private IEnumerator EnsureBallInPlayOrRelaunch(string step)
    {
        if (BallController.Instance != null && !BallController.Instance.IsWaitingForLaunch)
            yield break;

        yield return Narrate(
            step: step,
            line: "> 球已掉出。先重新发球，再继续技能校准。",
#if UNITY_ANDROID || UNITY_IOS
            keyHint: "[ 点击屏幕发球 ]",
#else
            keyHint: "[ 左键 / 空格 发球 ]",
#endif
            mode: TutorialTimeMode.HardPause,
            gate: () => BallController.Instance != null && !BallController.Instance.IsWaitingForLaunch,
            inputMask: TutorialInputMask.Launch | TutorialInputMask.Pause,
            terminalMode: TutorialTerminalMode.Compact,
            timeout: 0f);
    }

    private IEnumerator BumperBeat()
    {
        ResetTutorialCombatState();

        bool ack = false;
        yield return Narrate(
            step: "SEQ 10 / SYSTEMS",
            line: "> 机制阶段：Bumper、敌人、Boss、弹刀与波次奖励。",
            keyHint: null,
            mode: TutorialTimeMode.HardPause,
            gate: () => ack,
            inputMask: TutorialInputMask.UiOnly,
            terminalMode: TutorialTerminalMode.Brief,
            showContinue: true,
            onContinue: () => ack = true);

        if (_skipRequested) yield break;

        bool hit = false;
        void OnHit() => hit = true;
        Bumper.OnBallHit += OnHit;

        yield return Narrate(
            step: "SEQ 11 / BUMPER",
            line: "> 青色 Bumper：撞上去会弹开并得分。",
            keyHint: "[ 用球撞击保险杠 ]",
            mode: TutorialTimeMode.Normal,
            gate: () => hit,
            inputMask: CombatPlay,
            terminalMode: TutorialTerminalMode.Compact,
            timeout: 30f,
            whileWaiting: EnsureBallPlayable);

        Bumper.OnBallHit -= OnHit;
    }

    private IEnumerator ThreatBeat()
    {
        WaveManager.Instance?.ClearTutorialField();
        var def = WaveManager.Instance?.GetTutorialMinionDefinition();
        EnemyBase minion = null;
        if (def != null && WaveManager.Instance != null)
        {
            minion = WaveManager.Instance.SpawnMinion(def, new Vector3(0f, 4.0f, 0f), 0);
            if (minion != null)
            {
                minion.moveSpeed = 0f;
                minion.maxHits = 1;
            }
        }

        bool ack = false;
        yield return Narrate(
            step: "SEQ 12 / THREAT",
            line: "> 小兵触底会扣生命。先记住这条规则。",
            keyHint: null,
            mode: TutorialTimeMode.HardPause,
            gate: () => ack,
            inputMask: TutorialInputMask.UiOnly,
            terminalMode: TutorialTerminalMode.Brief,
            showContinue: true,
            onContinue: () => ack = true);

        if (_skipRequested) yield break;

        if (minion != null)
            minion.moveSpeed = 0.22f;

        yield return Narrate(
            step: "SEQ 13 / DESTROY",
            line: "> 用弹珠击毁这只小兵。可用 Q 改向瞄准。",
            keyHint: "[ 撞击敌人 · 或 Q 改向 ]",
            mode: TutorialTimeMode.Normal,
            gate: () => minion == null || minion.IsDead,
            inputMask: CombatPlay,
            terminalMode: TutorialTerminalMode.Compact,
            timeout: 45f,
            whileWaiting: EnsureBallPlayable);

        WaveManager.Instance?.ClearTutorialField();
    }

    private IEnumerator BossBeat()
    {
        WaveManager.Instance?.ClearTutorialField();
        var boss = WaveManager.Instance?.SpawnTutorialBoss(0);
        int hitsBefore = boss != null ? boss.CurrentHits : 0;

        yield return Narrate(
            step: "SEQ 14 / BOSS",
            line: "> Boss 在上方。用球撞击削减它的血量。",
            keyHint: "[ 撞击 Boss ]",
            mode: TutorialTimeMode.Normal,
            gate: () => boss == null || boss.IsDead || boss.CurrentHits > hitsBefore,
            inputMask: CombatPlay,
            terminalMode: TutorialTerminalMode.Compact,
            timeout: 45f,
            whileWaiting: EnsureBallPlayable);
    }

    private IEnumerator ParryBeat()
    {
        Boss boss = FindAnyObjectByType<Boss>();
        if (boss == null)
            boss = WaveManager.Instance?.SpawnTutorialBoss(0);

        bool bossHitFromParry = false;
        void OnParryBossHit(bool _) => bossHitFromParry = true;
        BezierMissile.OnAnyParryBossHit += OnParryBossHit;

        float nextFireAt = 0f;
        yield return Narrate(
            step: "SEQ 15 / PARRY",
            line: "> 导弹靠近出现双圈时，在窗口内招架。",
#if UNITY_ANDROID || UNITY_IOS
            keyHint: "[ 点屏幕弹刀 ]",
#else
            keyHint: "[ 左键弹刀 ]",
#endif
            mode: TutorialTimeMode.Normal,
            gate: () => bossHitFromParry,
            inputMask: CombatPlay | TutorialInputMask.Parry,
            terminalMode: TutorialTerminalMode.Compact,
            timeout: 60f,
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

        BezierMissile.OnAnyParryBossHit -= OnParryBossHit;

        // 等 Boss 受击反馈播完，再进老虎机教学
        TutorialTimeControl.Exit();
        yield return new WaitForSecondsRealtime(1.1f);
    }

    private IEnumerator BuffBeat()
    {
        WaveManager.Instance?.ClearTutorialField();
        TutorialTimeControl.Exit();
        TutorialInputGate.Disable();

        bool ack = false;
        yield return Narrate(
            step: "SEQ 16 / SLOT",
            line: "> 清波后进入老虎机：开转 → 可选重摇 → 领取强化。",
            keyHint: null,
            mode: TutorialTimeMode.HardPause,
            gate: () => ack,
            inputMask: TutorialInputMask.UiOnly,
            terminalMode: TutorialTerminalMode.Brief,
            showContinue: true,
            onContinue: () => ack = true);

        if (_skipRequested) yield break;

        if (GameManager.Instance != null)
            GameManager.Instance.CompleteWave();

        // 等老虎机浮层出现
        float waitOpen = 2.5f;
        while (waitOpen > 0f
               && (BuffSelectionController.Instance == null
                   || !BuffSelectionController.Instance.IsOverlayVisible))
        {
            waitOpen -= Time.unscaledDeltaTime;
            yield return null;
        }

        BuffSelectionController.Instance?.SetTutorialGate(SlotTutorialGate.SpinOnly);

        // ── 开转 ──
        yield return Narrate(
            step: "SEQ 17 / SPIN",
            line: "> 拉动右侧能量杆（或空格）开转三轮。",
#if UNITY_ANDROID || UNITY_IOS
            keyHint: "[ 点右侧拉杆 ]",
#else
            keyHint: "[ 拉杆 / 空格 ]",
#endif
            mode: TutorialTimeMode.Normal,
            gate: () => BuffSelectionController.Instance != null
                        && BuffSelectionController.Instance.IsResolvePreviewPhase,
            inputMask: TutorialInputMask.Pause,
            terminalMode: TutorialTerminalMode.Compact,
            timeout: 90f);

        if (_skipRequested) yield break;

        // 说明拍锁交互，避免抢先重转/领取
        BuffSelectionController.Instance?.SetTutorialGate(SlotTutorialGate.Locked);
        BuffSelectionController.Instance?.TutorialEnsureFreeRerolls(1);

        bool ackReroll = false;
        yield return Narrate(
            step: "SEQ 18 / REROLL",
            line: "> 停轮后可重摇：先点选一轮，再拉杆只转该轮。\n> 已发放 1 次免费重转（不挂 Debuff）。付费重转会挂 Debuff。",
            keyHint: "点选一轮 → 拉杆",
            mode: TutorialTimeMode.HardPause,
            gate: () => ackReroll,
            inputMask: TutorialInputMask.UiOnly,
            terminalMode: TutorialTerminalMode.Brief,
            showContinue: true,
            onContinue: () => ackReroll = true);

        if (_skipRequested) yield break;

        BuffSelectionController.Instance?.TutorialEnsureFreeRerolls(1);
        BuffSelectionController.Instance?.SetTutorialGate(SlotTutorialGate.RerollOnly);

        // ── 实操重摇（超时后放行到领取）──
        int rerollsBefore = BuffSelectionController.Instance != null
            ? BuffSelectionController.Instance.TutorialRerollCount
            : 0;

        yield return Narrate(
            step: "SEQ 19 / REROLL_DO",
            line: "> 试一次：点选一轮 → 再拉杆重转。\n> 此步不可领取，完成重转后再继续。",
            keyHint: "[ 点轮 → 拉杆 ]",
            mode: TutorialTimeMode.Normal,
            gate: () =>
            {
                var slot = BuffSelectionController.Instance;
                if (slot == null || !slot.IsOverlayVisible) return true;
                return slot.TutorialRerollCount > rerollsBefore;
            },
            inputMask: TutorialInputMask.Pause,
            terminalMode: TutorialTerminalMode.Compact,
            timeout: 45f,
            allowTimeoutPass: true);

        if (_skipRequested) yield break;

        BuffSelectionController.Instance?.SetTutorialGate(SlotTutorialGate.ClaimOnly);

        // ── 领取 ──
        yield return Narrate(
            step: "SEQ 20 / CLAIM",
            line: "> 满意后点「领取」拿绿色框强化。",
            keyHint: "[ 领取 ]",
            mode: TutorialTimeMode.Normal,
            gate: () => GameManager.Instance != null && GameManager.Instance.State == GameState.Playing,
            inputMask: TutorialInputMask.Pause,
            terminalMode: TutorialTerminalMode.Compact,
            timeout: 120f);

        BuffSelectionController.Instance?.SetTutorialGate(SlotTutorialGate.None);

        if (GameManager.Instance != null
            && GameManager.Instance.State == GameState.BuffSelection)
            GameManager.Instance.OnBuffSelectionDone();
    }

    private void Finish(bool skipped)
    {
        if (!_running) return;
        _running = false;
        TutorialTimeControl.Exit();
        TutorialInputGate.Disable();
        BuffSelectionController.Instance?.SetTutorialGate(SlotTutorialGate.None);
        _ui?.Hide();

        PlayerProfile.Load();
        PlayerProfile.MarkTutorialCompleted();
        WaveManager.Instance?.ClearTutorialField();

        StartCoroutine(FinishRoutine(skipped));
    }

    private IEnumerator FinishRoutine(bool skipped)
    {
        bool ack = false;
        yield return Narrate(
            step: skipped ? "校准跳过" : "校准完成",
            line: skipped
                ? "> 已写入完成标记。可随时再来校准。"
                : "> 协议校准完成。协议币可在商店解锁弹珠。",
            keyHint: null,
            mode: TutorialTimeMode.HardPause,
            gate: () => ack,
            inputMask: TutorialInputMask.UiOnly,
            terminalMode: TutorialTerminalMode.Brief,
            showContinue: true,
            showSkip: false,
            onContinue: () => ack = true);

        TutorialTimeControl.Exit();
        TutorialInputGate.Disable();
        _ui?.Hide();
        if (GameManager.Instance != null)
            GameManager.Instance.TriggerGameOver();
    }

    // ── Narrate core ──────────────────────────────────────────────────────

    private IEnumerator Narrate(
        string step,
        string line,
        string keyHint,
        TutorialTimeMode mode,
        Func<bool> gate,
        TutorialInputMask inputMask,
        TutorialTerminalMode terminalMode,
        float timeout = 0f,
        bool showContinue = false,
        bool showSkip = true,
        bool allowTimeoutPass = false,
        Action onContinue = null,
        Action whileWaiting = null)
    {
        TutorialTimeControl.Enter(mode);
        TutorialInputGate.Enable(inputMask);

        bool cont = false;
        _ui.ShowNarration(
            step,
            line,
            keyHint,
            waitingForInput: !showContinue,
            showContinue: showContinue,
            showSkip: showSkip,
            terminalMode: terminalMode,
            onContinue: () =>
            {
                cont = true;
                onContinue?.Invoke();
            },
            onSkip: () => _skipRequested = true);

        yield return null;
        yield return null;

        float left = timeout > 0f ? timeout : float.PositiveInfinity;
        while (!_skipRequested)
        {
            whileWaiting?.Invoke();
            TutorialTimeControl.Maintain();

            if (showContinue && cont) break;
            if (!showContinue && gate != null && gate()) break;

            if (timeout > 0f)
            {
                left -= Time.unscaledDeltaTime;
                if (left <= 0f)
                {
                    if (allowTimeoutPass) break;
                    left = timeout;
                    _ui.ShowNarration(
                        step, line, keyHint,
                        waitingForInput: true,
                        showContinue: false,
                        showSkip: showSkip,
                        terminalMode: terminalMode,
                        onSkip: () => _skipRequested = true);
                }
            }

            yield return null;
        }

        if (_skipRequested)
        {
            TutorialTimeControl.Exit();
            TutorialInputGate.Disable();
            _ui.Hide();
            yield break;
        }

        _ui.ShowSuccess();
        yield return new WaitForSecondsRealtime(0.35f);
        TutorialTimeControl.Exit();
        TutorialInputGate.Disable();
        _ui.Hide();
        yield return new WaitForSecondsRealtime(0.12f);
    }

    private static void RefreshSkillReady(int slotIndex)
    {
        var sm = SkillManager.Instance;
        if (sm == null || sm.slots == null) return;
        if (slotIndex < 0 || slotIndex >= sm.slots.Length) return;
        sm.slots[slotIndex].currentCD = 0f;
        sm.onSlotCooldownChanged.Invoke(slotIndex, 0f);
    }

    private static void EnsureBallPlayable()
    {
        // 掉球重生由 BallController 处理
    }

    /// <summary>等斩杀连锁跑完（确认后 timeScale 已恢复）。</summary>
    private IEnumerator WaitForExecuteChainComplete(float timeout = 14f)
    {
        TutorialTimeControl.Exit();

        var ball = BallController.Instance;
        if (ball == null || !ball.IsExecuteChainActive)
            yield break;

        _ui?.Hide();

        float left = timeout;
        while (ball != null && ball.IsExecuteChainActive && left > 0f)
        {
            left -= Time.unscaledDeltaTime;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.55f);
    }

    /// <summary>清掉教学残留的武装/瞄准/斩杀连锁，避免后续步骤无法 Q 改向。</summary>
    private static void ResetTutorialCombatState()
    {
        SkillManager.Instance?.CancelAiming();
        SkillManager.Instance?.ClearExecuteArm();

        if (BallController.Instance != null && BallController.Instance.IsExecuteChainActive)
            BallController.Instance.StopExecuteChain();

        SlowMoFX.Instance?.CancelSkillAim();
        SlowMoFX.Instance?.ForceRestore();
        LaunchGuide.Instance?.Hide();

        var sm = SkillManager.Instance;
        if (sm?.slots == null) return;
        for (int i = 0; i < sm.slots.Length; i++)
        {
            sm.slots[i].currentCD = 0f;
            sm.onSlotCooldownChanged.Invoke(i, 0f);
        }
    }
}
