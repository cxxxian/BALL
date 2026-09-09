using UnityEngine;

/// <summary>
/// 打击顿帧统一入口。底层走 SlowMoFX 战斗顿帧深度，与技能瞄准 / 教程 HardPause·SoftFreeze 互斥。
/// 时长用 unscaled 计时；可叠加重脉冲（更深 scale / 更长时长）。
/// </summary>
public static class HitStop
{
    public enum Weight
    {
        Light  = 0, // 球打怪 ~1–2 帧
        Medium = 1, // 击杀 / 斩杀段 ~2–3 帧
        Heavy  = 2, // 弹刀 Perfect / Boss 级
    }

    /// <summary>velocity01: 0–1，来自球速或相对速度归一化。</summary>
    public static void Pulse(Weight weight, float velocity01 = 0.55f)
    {
        velocity01 = Mathf.Clamp01(velocity01);
        GetParams(weight, velocity01, out float scale, out float seconds);
        PulseRaw(scale, seconds);
    }

    public static void PulseRaw(float stopScale, float unscaledSeconds)
    {
        if (SlowMoFX.Instance == null) return;
        SlowMoFX.Instance.PulseCombatHitStop(stopScale, unscaledSeconds);
    }

    public static void PulseParry(bool perfect)
    {
        float scale = perfect ? 0.035f : 0.08f;
        float dur = perfect ? 0.13f : 0.07f;
        PulseRaw(scale, dur);
    }

    public static bool IsActive =>
        SlowMoFX.Instance != null && SlowMoFX.Instance.IsCombatHitStopActive;

    private static void GetParams(Weight weight, float velocity01, out float scale, out float seconds)
    {
        var cfg = GameManager.Instance != null ? GameManager.Instance.config : null;

        float baseSec;
        float baseScale;
        switch (weight)
        {
            case Weight.Medium:
                baseSec = cfg != null ? cfg.hitStopMediumSeconds : 0.045f;
                baseScale = cfg != null ? cfg.hitStopMediumScale : 0.045f;
                break;
            case Weight.Heavy:
                baseSec = cfg != null ? cfg.hitStopHeavySeconds : 0.08f;
                baseScale = cfg != null ? cfg.hitStopHeavyScale : 0.03f;
                break;
            default:
                baseSec = cfg != null ? cfg.hitStopLightSeconds : 0.028f;
                baseScale = cfg != null ? cfg.hitStopLightScale : 0.07f;
                break;
        }

        // 更快 → 略长、略深（约 ±25%）
        float stretch = Mathf.Lerp(0.85f, 1.25f, velocity01);
        seconds = baseSec * stretch;
        scale = baseScale * Mathf.Lerp(1.15f, 0.75f, velocity01);
        scale = Mathf.Clamp(scale, 0.02f, 0.2f);
        seconds = Mathf.Clamp(seconds, 0.012f, 0.2f);
    }
}
