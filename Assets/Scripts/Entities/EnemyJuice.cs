using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>统一敌人受击 / 击杀 Juice 反馈（闪白、粒子、震屏、Combo、血条、音效、HitStop）。</summary>
public static class EnemyJuice
{
    private const float FlashDuration = 0.02f; // ~1 帧 HDR 白闪
    private const float ShieldFlashDuration = 0.04f;

    private static readonly Color ShieldFlashColor = new Color(0.2f, 0.95f, 1f, 1f);

    private static readonly HashSet<EnemyBase> _flashing = new HashSet<EnemyBase>();

    public static void OnHit(EnemyBase enemy, bool isFromBall, Vector2? hitPos = null)
    {
        if (enemy == null || enemy.IsDead) return;

        StartHitFlash(enemy);

        Vector2 pos = hitPos ?? (Vector2)enemy.transform.position;
        float speed = BallSpeed();
        bool execute = IsExecuteChain();

        if (isFromBall)
        {
            Color hitColor = enemy.BaseColor;
            var tier = execute ? JuiceRouter.Tier.Skill : JuiceRouter.Tier.Hit;
            JuiceRouter.Play(tier, pos, hitColor, speed, applyHitStop: true);
            ComboSystem.Instance?.RegisterAirtimeHit(pos);
            AudioManager.Instance?.PlayBounce();
        }
        else
        {
            CameraShake.Instance?.Shake(CameraShake.Preset.Light);
        }

        GetHealthBar(enemy)?.OnEnemyHit();
    }

    public static void OnKill(EnemyBase enemy, Vector2 deathPos)
    {
        if (enemy == null) return;

        GetHealthBar(enemy)?.OnEnemyDeath();

        Color enemyColor = enemy.BaseColor;
        float speed = BallSpeed();
        bool execute = IsExecuteChain();
        var tier = execute || enemy is Boss ? JuiceRouter.Tier.Ultimate : JuiceRouter.Tier.Skill;
        JuiceRouter.Play(tier, deathPos, enemyColor, speed, applyHitStop: true);
    }

    /// <summary>球心震爆专用击杀：网格像素散落（血条已由 Die(skip) 处理）。</summary>
    public static void OnPulseKill(EnemyBase enemy, Vector2 deathPos)
    {
        if (enemy == null) return;

        Color enemyColor = enemy.BaseColor;
        float speed = BallSpeed();
        float vMult = JuiceRouter.VelocityMult(speed);
        ImpactFX.Instance?.SpawnMissileShatter(deathPos, enemyColor, 1.25f * vMult);
        JuiceRouter.Play(JuiceRouter.Tier.Skill, deathPos, enemyColor, speed, applyHitStop: true);
        ComboSystem.Instance?.RegisterAirtimeHit(deathPos);
        AudioManager.Instance?.PlayBounce();
    }

    /// <summary>护盾吸收清场：统一闪青（~2 帧）。</summary>
    public static void ShieldAbsorbFlash(EnemyBase enemy)
    {
        if (enemy == null || enemy.IsDead) return;
        if (_flashing.Contains(enemy)) return;
        enemy.StartCoroutine(ShieldFlashRoutine(enemy));
    }

    /// <summary>护盾吸收清场：青闪后解体，不走 TakeHit 死亡粒子。</summary>
    public static void ShieldAbsorbDissolve(EnemyBase enemy)
    {
        if (enemy == null || enemy.IsDead) return;
        enemy.DissolveFromShieldAbsorb();
    }

    /// <summary>Boss 击杀清场：冲线底部解体（非 Boss 金屑）。</summary>
    public static void BreachClearDissolve(EnemyBase enemy)
    {
        if (enemy == null || enemy.IsDead) return;
        enemy.DissolveAsBreachClear();
    }

    private static float BallSpeed()
    {
        if (BallController.Instance != null && BallController.Instance.Rb != null)
            return BallController.Instance.Rb.velocity.magnitude;
        return 8f;
    }

    private static bool IsExecuteChain()
    {
        return BallController.Instance != null && BallController.Instance.IsExecuteChainActive;
    }

    private static void StartHitFlash(EnemyBase enemy)
    {
        if (_flashing.Contains(enemy)) return;
        enemy.StartCoroutine(HitFlashRoutine(enemy));
    }

    private static IEnumerator HitFlashRoutine(EnemyBase enemy)
    {
        _flashing.Add(enemy);
        var sr = enemy.MainSR;
        if (sr != null)
        {
            var palette = NeonColors.Active;
            Color flash = enemy is Boss
                ? new Color(6f, 6f, 6f, 1f)
                : palette.GetFlash(NeonRole.Minion);
            sr.color = flash;
        }

        // 顿帧期间闪白用 realtime，否则停格时看不见闪
        yield return new WaitForSecondsRealtime(FlashDuration);

        if (enemy != null && sr != null)
            sr.color = enemy.BaseColor;

        _flashing.Remove(enemy);
    }

    private static IEnumerator ShieldFlashRoutine(EnemyBase enemy)
    {
        _flashing.Add(enemy);
        var sr = enemy.MainSR;
        if (sr != null)
            sr.color = NeonColors.Active.ForParticle(ShieldFlashColor, 1.2f);

        yield return new WaitForSecondsRealtime(ShieldFlashDuration);

        if (enemy != null && !enemy.IsDead && sr != null)
            sr.color = enemy.BaseColor;

        _flashing.Remove(enemy);
    }

    private static IEnemyHealthBar GetHealthBar(EnemyBase enemy)
    {
        if (enemy == null) return null;
        var minionBar = enemy.GetComponent<MinionHealthBar>();
        if (minionBar != null) return minionBar;
        return enemy.GetComponent<BossHealthBar>();
    }
}
