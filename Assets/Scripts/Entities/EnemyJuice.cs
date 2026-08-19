using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>统一敌人受击 / 击杀 Juice 反馈（闪白、粒子、震屏、Combo、血条、音效）。</summary>
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

        if (isFromBall)
        {
            Color hitColor = enemy.BaseColor;
            Vector2 pos = hitPos ?? (Vector2)enemy.transform.position;
            ImpactFX.Instance?.SpawnHit(pos, hitColor, 1f);
            ComboSystem.Instance?.RegisterAirtimeHit(pos);
            AudioManager.Instance?.PlayBounce();
        }

        CameraShake.Instance?.Shake(CameraShake.Preset.Light);
        GetHealthBar(enemy)?.OnEnemyHit();
    }

    public static void OnKill(EnemyBase enemy, Vector2 deathPos)
    {
        if (enemy == null) return;

        GetHealthBar(enemy)?.OnEnemyDeath();

        Color enemyColor = enemy.BaseColor;
        ImpactFX.Instance?.SpawnHit(deathPos, enemyColor, 1f);
        CameraShake.Instance?.Shake(CameraShake.Preset.Medium);
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

        yield return new WaitForSeconds(FlashDuration);

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
