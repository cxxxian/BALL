using UnityEngine;

/// <summary>
/// 显式协议奖励的脉冲服务。保留组件以兼容现有场景；Combo 不再触发伤害脉冲。
/// </summary>
public class ComboMilestoneRewards : MonoBehaviour
{
    public static ComboMilestoneRewards Instance { get; private set; }

    private GameConfig Config => GameManager.Instance != null ? GameManager.Instance.config : null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public int GetBumperPulseDamage()
    {
        int baseDmg = Config != null ? Config.bumperPulseMilestoneDamage : 1;
        int penalty = DebuffManager.Instance != null ? DebuffManager.Instance.BumperDamagePenalty : 0;
        return Mathf.Max(0, baseDmg - penalty);
    }

    public static bool IsGruntPulseTarget(EnemyBase enemy)
    {
        if (enemy == null || enemy.IsDead) return false;
        if (enemy is Boss) return false;
        if (enemy.isBomber) return false;
        return enemy.maxHits <= 2;
    }

}

/// <summary>在指定世界坐标释放一次 Bumper 脉冲（伤害随波前扩散，与 VFX 同步）。</summary>
public static class BumperPulse
{
    public static void ReleaseAt(Vector2 center, Color bumperColor)
    {
        var rewards = ComboMilestoneRewards.Instance;
        if (rewards == null) return;

        var cfg = GameManager.Instance != null ? GameManager.Instance.config : null;
        float radius = cfg != null ? cfg.bumperPulseRadius : 3.2f;
        int damage = rewards.GetBumperPulseDamage();
        float ringDur = cfg != null ? cfg.bumperPulseRingDuration : 1.0f;
        Color waveColor = Color.Lerp(bumperColor, new Color(1.15f, 0.72f, 0.12f), 0.55f);

        if (ImpactFX.Instance != null)
            ImpactFX.Instance.SpawnBumperPulseWave(center, radius, waveColor, ringDur, damage);
    }
}
