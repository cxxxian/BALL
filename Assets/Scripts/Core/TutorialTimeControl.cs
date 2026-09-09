using System.Collections.Generic;
using UnityEngine;

public enum TutorialTimeMode
{
    /// <summary>正常流速。</summary>
    Normal = 0,
    /// <summary>纯读字：timeScale=0，输入仍走 Update。</summary>
    HardPause = 1,
    /// <summary>冻住球/敌人，timeScale=1，挡板与按键可响应。</summary>
    SoftFreeze = 2,
    /// <summary>实战慢放，便于完成击杀/弹刀。</summary>
    SlowMo = 3
}

/// <summary>协议校准时间态。离开时务必 Exit，避免 timeScale 卡死。</summary>
public static class TutorialTimeControl
{
    public const float SlowMoScale = 0.35f;

    private static TutorialTimeMode _mode = TutorialTimeMode.Normal;
    private static readonly List<FrozenBody> _frozen = new List<FrozenBody>(16);
    private static bool _hadSoftFreeze;

    private struct FrozenBody
    {
        public Rigidbody2D Rb;
        public RigidbodyType2D Type;
        public Vector2 Velocity;
        public float AngularVelocity;
    }

    /// <summary>纯读字 HardPause：禁止技能时缓（球已 timeScale=0）。SoftFreeze / SlowMo 操作拍允许正常瞄准。</summary>
    public static bool SuppressSkillSlowMo =>
        RunSession.IsTutorial && _mode == TutorialTimeMode.HardPause;

    public static TutorialTimeMode Mode => _mode;

    public static void Enter(TutorialTimeMode mode)
    {
        // SoftFreeze 需允许刷新冻结列表（新刷的敌人）
        if (_mode == mode && mode != TutorialTimeMode.SoftFreeze)
            return;

        ExitInternal(restoreScale: true);

        _mode = mode;
        _hadSoftFreeze = false;
        switch (mode)
        {
            case TutorialTimeMode.HardPause:
                Time.timeScale = 0f;
                break;
            case TutorialTimeMode.SlowMo:
                Time.timeScale = SlowMoScale;
                break;
            case TutorialTimeMode.SoftFreeze:
                Time.timeScale = 1f;
                FreezeFieldDynamics();
                _hadSoftFreeze = true;
                break;
            default:
                break;
        }
    }

    public static void Exit() => ExitInternal(restoreScale: true);

    private static void ExitInternal(bool restoreScale)
    {
        bool soft = _hadSoftFreeze || _mode == TutorialTimeMode.SoftFreeze;
        UnfreezeFieldDynamics(nudgeBall: soft);
        _hadSoftFreeze = false;
        _mode = TutorialTimeMode.Normal;

        if (!restoreScale) return;
        if (ShouldLeaveTimeScaleAlone()) return;

        if (Time.timeScale < 0.99f || Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = 1f;
    }

    /// <summary>旁白等待期间每帧调用：钉住 SoftFreeze，并拉回被技能时缓改掉的 SlowMo / HardPause。</summary>
    public static void Maintain()
    {
        TickHold();

        if (ShouldLeaveTimeScaleAlone()) return;
        if (SlowMoFX.Instance != null && SlowMoFX.Instance.IsSkillSlowMoActive
            && !SuppressSkillSlowMo)
            return;
        if (SlowMoFX.Instance != null && SlowMoFX.Instance.IsCombatHitStopActive)
            return;

        if (_mode == TutorialTimeMode.HardPause)
        {
            if (!Mathf.Approximately(Time.timeScale, 0f))
                Time.timeScale = 0f;
            return;
        }

        if (_mode != TutorialTimeMode.SlowMo) return;

        if (Mathf.Abs(Time.timeScale - SlowMoScale) > 0.02f)
            Time.timeScale = SlowMoScale;
    }

    public static void TickHold()
    {
        if (_mode != TutorialTimeMode.SoftFreeze) return;
        for (int i = 0; i < _frozen.Count; i++)
        {
            var f = _frozen[i];
            if (f.Rb == null) continue;
            f.Rb.velocity = Vector2.zero;
            f.Rb.angularVelocity = 0f;
        }
    }

    private static bool ShouldLeaveTimeScaleAlone()
    {
        if (GameManager.Instance != null && GameManager.Instance.State == GameState.BuffSelection)
            return true;
        if (PauseMenuController.Instance != null && PauseMenuController.Instance.IsOpen)
            return true;
        return false;
    }

    private static void FreezeFieldDynamics()
    {
        UnfreezeFieldDynamics(nudgeBall: false);
        _frozen.Clear();
        TryFreeze(BallController.Instance != null ? BallController.Instance.Rb : null);

        foreach (var enemy in Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
        {
            if (enemy == null || enemy.IsDead) continue;
            TryFreeze(enemy.GetComponent<Rigidbody2D>());
        }
    }

    private static void TryFreeze(Rigidbody2D rb)
    {
        if (rb == null) return;
        for (int i = 0; i < _frozen.Count; i++)
            if (_frozen[i].Rb == rb) return;

        _frozen.Add(new FrozenBody
        {
            Rb = rb,
            Type = rb.bodyType,
            Velocity = rb.velocity,
            AngularVelocity = rb.angularVelocity
        });
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        if (rb.bodyType == RigidbodyType2D.Dynamic)
            rb.bodyType = RigidbodyType2D.Kinematic;
    }

    private static void UnfreezeFieldDynamics(bool nudgeBall)
    {
        for (int i = 0; i < _frozen.Count; i++)
        {
            var f = _frozen[i];
            if (f.Rb == null) continue;
            f.Rb.bodyType = f.Type;
            f.Rb.velocity = f.Velocity;
            f.Rb.angularVelocity = f.AngularVelocity;
        }
        _frozen.Clear();

        if (!nudgeBall) return;

        var ball = BallController.Instance;
        if (ball != null && ball.Rb != null && !ball.IsWaitingForLaunch
            && ball.Rb.velocity.sqrMagnitude < 0.05f)
        {
            ball.Rb.velocity = new Vector2(UnityEngine.Random.Range(-1.2f, 1.2f), 4.5f);
        }
    }
}
