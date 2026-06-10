using UnityEngine;

/// <summary>
/// 统一 Juice 反馈入口：撞=粒子+震，技能=波纹，里程碑=UI。
/// Bumper 为 Hit 档参考实现。
/// </summary>
public static class JuiceRouter
{
    public enum Tier
    {
        Tap    = 0, // intensity 0.5, 无震
        Hit    = 1, // intensity 1.0, Light
        Skill  = 2, // intensity 1.2, Medium
        Ultimate = 3, // intensity 1.5, Heavy
    }

    private const float WallShakeVelThreshold = 6f;

    public static float IntensityFor(Tier tier)
    {
        switch (tier)
        {
            case Tier.Tap:     return 0.5f;
            case Tier.Hit:     return 1.0f;
            case Tier.Skill:   return 1.2f;
            case Tier.Ultimate: return 1.5f;
            default:           return 1.0f;
        }
    }

    public static void Play(Tier tier, Vector2 worldPos, Color neonColor, float intensityMult = 1f)
    {
        float intensity = IntensityFor(tier) * intensityMult;
        ImpactFX.Instance?.SpawnHit(worldPos, neonColor, intensity);
        ShakeFor(tier);
    }

    public static void WallHit(Vector2 worldPos, Vector2 normal, float relativeVel, Color wallColor)
    {
        float velMag = Mathf.Abs(relativeVel);
        float intensity = Mathf.Clamp01(velMag / 12f) * 0.85f + 0.15f;
        intensity *= 0.85f;

        ImpactFX.Instance?.SpawnHit(worldPos, wallColor, intensity);
        ImpactFX.Instance?.SpawnWallFlash(worldPos, normal, wallColor, intensity);

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

        float[] ratios = { -0.65f, 0f, 0.65f };
        for (int i = 0; i < ratios.Length; i++)
            ImpactFX.Instance?.SpawnHit(new Vector2(ratios[i] * halfWidth, shieldY), color, 1.0f);
    }

    private static void ShakeFor(Tier tier)
    {
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
        return col.gameObject.name.StartsWith("Wall");
    }
}
