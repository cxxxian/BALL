using UnityEngine;

/// <summary>
/// Combo 构筑（4b-4）：动能加速叠层 + 过载阶段奖励。
/// 不触发 Pulse / BumperPulse；不降低 Pulse 门槛。Frenzy Epic 留给 4b-5。
/// </summary>
public static class ComboCombat
{
    private static int _momentumHitCounter;
    private static int _lastOverloadTier;
    private static int _tempHitCharges;

    public static string LastOverloadReason { get; private set; } = "";
    public static float LastOverloadUnscaledTime { get; private set; }
    public static int TempHitChargesRemaining => _tempHitCharges;
    public static int MomentumHitCounter => _momentumHitCounter;

    /// <summary>基础 +1 之后：可能再 +1（动能），并对经过的每一档检查过载。</summary>
    public static int ApplyMomentumAndOverload(ref int currentCombo)
    {
        var bm = BuffManager.Instance;
        if (bm == null) return 0;

        // 当前值已是基础 +1 后的 Combo
        TryOverload(bm, currentCombo);

        int bonus = TryMomentumBonus(bm);
        if (bonus > 0)
        {
            currentCombo += bonus;
            TryOverload(bm, currentCombo);
        }

        return bonus;
    }

    public static void OnComboReset()
    {
        _momentumHitCounter = 0;
        _lastOverloadTier = 0;
    }

    /// <summary>球命中额外伤害：过载发放的临伤充能。</summary>
    public static int ConsumeTempHitBonus()
    {
        if (_tempHitCharges <= 0) return 0;
        _tempHitCharges--;
        return 1;
    }

    public static void DevReset()
    {
        _momentumHitCounter = 0;
        _lastOverloadTier = 0;
        _tempHitCharges = 0;
        LastOverloadReason = "";
    }

    private static int TryMomentumBonus(BuffManager bm)
    {
        if (bm.ComboMomentumStacks <= 0) return 0;

        _momentumHitCounter++;
        int need = bm.GetMomentumHitsPerBonus();
        if (_momentumHitCounter < need) return 0;

        _momentumHitCounter = 0;
        return 1;
    }

    private static void TryOverload(BuffManager bm, int combo)
    {
        if (bm.ComboOverloadStacks <= 0 || combo <= 0) return;

        int first = bm.GetOverloadFirstThreshold();
        int interval = bm.GetOverloadInterval();
        if (combo < first) return;
        if ((combo - first) % interval != 0) return;

        int tier = 1 + (combo - first) / interval;
        if (tier <= _lastOverloadTier) return;
        _lastOverloadTier = tier;

        int score = bm.GetOverloadScoreBonus() * tier;
        GameManager.Instance?.AddScore(score);

        int charges = bm.GetOverloadTempHitCharges();
        _tempHitCharges = Mathf.Max(_tempHitCharges, charges);

        Vector2 pos = ComboSystem.Instance != null
            ? ComboSystem.Instance.LastHitWorldPosition
            : Vector2.zero;
        if (pos == Vector2.zero && BallController.Instance != null)
            pos = BallController.Instance.transform.position;

        ImpactFX.Instance?.SpawnHit(pos, new Color(1f, 0.55f, 0.15f), 0.9f);
        CameraShake.Instance?.Shake(tier >= 3
            ? CameraShake.Preset.Medium
            : CameraShake.Preset.Light);

        LastOverloadReason = $"T{tier} +{score} dmg×{charges}";
        LastOverloadUnscaledTime = Time.unscaledTime;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log($"[ComboCombat] Overload {LastOverloadReason} @ combo={combo}");
#endif
    }
}
