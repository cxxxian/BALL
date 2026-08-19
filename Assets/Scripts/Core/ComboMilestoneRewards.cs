using UnityEngine;

/// <summary>
/// Combo 脉冲里程碑：首次达到阈值后每隔 interval 触发一次范围脉冲（25/35/45…）。
/// </summary>
public class ComboMilestoneRewards : MonoBehaviour
{
    public static ComboMilestoneRewards Instance { get; private set; }

    private int _lastPulseCombo;

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

    private void Start()
    {
        if (ComboSystem.Instance != null)
            ComboSystem.Instance.onComboChanged.AddListener(OnComboChanged);

        if (GameManager.Instance != null)
            GameManager.Instance.onGameStart.AddListener(ResetForNewGame);

        if (WaveManager.Instance != null)
            WaveManager.Instance.onWaveStart.AddListener(OnWaveStart);
    }

    private void OnComboChanged(int combo)
    {
        if (combo <= 0)
        {
            _lastPulseCombo = 0;
            return;
        }

        if (!IsPulseMilestoneCombo(combo)) return;
        if (combo <= _lastPulseCombo) return;

        _lastPulseCombo = combo;
        FirePulseMilestone(combo);
    }

    private bool IsPulseMilestoneCombo(int combo)
    {
        int first = GetEffectiveFirstThreshold();
        int interval = GetPulseInterval();
        if (combo < first) return false;
        return (combo - first) % interval == 0;
    }

    private void FirePulseMilestone(int combo)
    {
        Vector2 pos = ComboSystem.Instance != null
            ? ComboSystem.Instance.LastHitWorldPosition
            : Vector2.zero;
        if (BallController.Instance != null && pos == Vector2.zero)
            pos = BallController.Instance.transform.position;

        Color fx = NeonColors.Active.GetBase(NeonRole.Bumper);
        BumperPulse.ReleaseAt(pos, fx);

        CameraShake.Instance?.Shake(combo >= GetEffectiveFirstThreshold() + GetPulseInterval() * 3
            ? CameraShake.Preset.Heavy
            : CameraShake.Preset.Medium);
    }

    public void OnWaveStart(int _) => _lastPulseCombo = 0;

    public void ResetForNewGame() => _lastPulseCombo = 0;

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

    private int GetEffectiveFirstThreshold() =>
        ComboSystem.GetEffectiveThreshold(GetPulseFirstThreshold());

    private int GetPulseFirstThreshold() =>
        Config != null ? Config.comboRewardThreshold25 : 25;

    private int GetPulseInterval() =>
        Config != null ? Config.comboPulseInterval : 10;
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
