using UnityEngine;

/// <summary>
/// 统一 Juice 反馈入口：撞=粒子+震，技能=波纹，里程碑=UI。
/// 档位：Tap 蹭墙 / Hit Bumper·打怪 / Skill 击杀·弹刀 / Ultimate 重事件。
/// </summary>
public static class JuiceRouter
{
    public enum Tier
    {
        Tap      = 0, // intensity 0.5, 无震
        Hit      = 1, // intensity 1.0, Light
        Skill    = 2, // intensity 1.2, Medium + 短 HitStop
        Ultimate = 3, // intensity 1.5, Heavy + HitStop
    }

    private const float WallShakeVelThreshold = 6f;
    private const float DefaultSpeedRef = 12f;

    public static float IntensityFor(Tier tier)
    {
        switch (tier)
        {
            case Tier.Tap:      return 0.5f;
            case Tier.Hit:      return 1.0f;
            case Tier.Skill:    return 1.2f;
            case Tier.Ultimate: return 1.5f;
            default:            return 1.0f;
        }
    }

    /// <summary>相对速度 / 球速 → 0.45–1.0 倍率。</summary>
    public static float VelocityMult(float relativeVel, float speedRef = -1f)
    {
        if (speedRef < 0.01f)
        {
            var cfg = GameManager.Instance != null ? GameManager.Instance.config : null;
            speedRef = cfg != null ? cfg.hitStopSpeedRef : DefaultSpeedRef;
        }

        float velMag = Mathf.Abs(relativeVel);
        return Mathf.Clamp01(velMag / speedRef) * 0.55f + 0.45f;
    }

    public static float BallVelocity01()
    {
        float speed = 0f;
        if (BallController.Instance != null && BallController.Instance.Rb != null)
            speed = BallController.Instance.Rb.velocity.magnitude;
        var cfg = GameManager.Instance != null ? GameManager.Instance.config : null;
        float speedRef = cfg != null ? cfg.hitStopSpeedRef : DefaultSpeedRef;
        return Mathf.Clamp01(speed / Mathf.Max(0.01f, speedRef));
    }

    public static void Play(Tier tier, Vector2 worldPos, Color neonColor, float intensityMult = 1f)
    {
        float intensity = IntensityFor(tier) * intensityMult;
        ImpactFX.Instance?.SpawnHit(worldPos, neonColor, intensity);
        ShakeFor(tier, intensityMult);

        if (tier >= Tier.Skill)
        {
            var weight = tier >= Tier.Ultimate ? HitStop.Weight.Heavy : HitStop.Weight.Medium;
            HitStop.Pulse(weight, Mathf.Clamp01(intensityMult));
        }
    }

    /// <summary>带相对速度的命中：粒子/震/顿帧随速度略放大。</summary>
    public static void Play(Tier tier, Vector2 worldPos, Color neonColor, float relativeVel, bool applyHitStop)
    {
        float vMult = VelocityMult(relativeVel);
        float intensity = IntensityFor(tier) * vMult;
        ImpactFX.Instance?.SpawnHit(worldPos, neonColor, intensity);
        ShakeFor(tier, vMult);

        if (applyHitStop && tier >= Tier.Hit)
        {
            HitStop.Weight w;
            if (tier >= Tier.Ultimate) w = HitStop.Weight.Heavy;
            else if (tier >= Tier.Skill) w = HitStop.Weight.Medium;
            else w = HitStop.Weight.Light;
            HitStop.Pulse(w, BallVelocity01());
        }
    }

    public static void WallHit(Vector2 worldPos, Vector2 normal, float relativeVel, Color wallColor)
    {
        float velMag = Mathf.Abs(relativeVel);
        float intensity = VelocityMult(velMag) * 0.85f;

        ImpactFX.Instance?.SpawnHit(worldPos, wallColor, intensity);
        // 沿外框弧长扩散的墙体霓虹脉冲（取代/补充短 LineRenderer）
        PlayfieldWallPulse.NotifyHit(worldPos, intensity);
        ImpactFX.Instance?.SpawnWallFlash(worldPos, normal, wallColor, intensity * 0.55f);

        if (velMag >= WallShakeVelThreshold)
            CameraShake.Instance?.Shake(CameraShake.Preset.Light);
    }

    public static void FlipperPerfectCatch(Vector2 contactPos, FlipperFX flipperFx)
    {
        Color ballColor = NeonColors.Active.GetBase(NeonRole.Ball);
        Play(Tier.Tap, contactPos, ballColor, 0.75f);
        flipperFx?.TriggerCatchFlash();
    }

    public static void TowerFire(Vector2 towerPos, NeonRole role, bool hitEnemy)
    {
        Color color = NeonColors.Active.GetBase(role);
        ImpactFX.Instance?.SpawnHit(towerPos, color, IntensityFor(Tier.Hit));
        if (hitEnemy)
            CameraShake.Instance?.Shake(CameraShake.Preset.Light);
    }

    public static void ShieldActivate(float shieldY, float halfWidth)
    {
        Color color = NeonColors.Active.GetBase(NeonRole.SkillShield);
        ImpactFX.Instance?.SpawnShieldRipple(shieldY, halfWidth, color, 0.9f);
    }

    public static void ShieldAbsorb(float shieldY, float halfWidth)
    {
        Color color = NeonColors.Active.GetBase(NeonRole.SkillShield);
        ImpactFX.Instance?.SpawnShieldRipple(shieldY, halfWidth, color, 1.1f);

        CameraShake.Instance?.Shake(CameraShake.Preset.Medium);
        HitStop.Pulse(HitStop.Weight.Medium, 0.7f);

        float[] ratios = { -0.65f, 0f, 0.65f };
        for (int i = 0; i < ratios.Length; i++)
            ImpactFX.Instance?.SpawnHit(new Vector2(ratios[i] * halfWidth, shieldY), color, 1.0f);
    }

    private static void ShakeFor(Tier tier, float intensityMult = 1f)
    {
        // 低速轻命中：Hit 档可降为不震，避免连弹糊屏
        if (tier == Tier.Hit && intensityMult < 0.55f)
            return;

        switch (tier)
        {
            case Tier.Hit:
                CameraShake.Instance?.Shake(CameraShake.Preset.Light);
                break;
            case Tier.Skill:
                CameraShake.Instance?.Shake(CameraShake.Preset.Medium);
                break;
            case Tier.Ultimate:
                CameraShake.Instance?.Shake(CameraShake.Preset.Heavy);
                break;
        }
    }

    public static bool IsWallCollider(Collider2D col)
    {
        if (col == null) return false;
        string n = col.gameObject.name;
        return n.StartsWith("Wall") || n.StartsWith("PlayfieldTopArc") || n.StartsWith("Outlane") || n.StartsWith("Inlane");
    }
}
