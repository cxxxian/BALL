using UnityEngine;

/// <summary>霓虹语义角色：Palette 中各 baseColor 的存储约定见字段注释。</summary>
public enum NeonRole
{
    Ball,
    Bumper,
    Minion,
    Boss,
    TowerTesla,
    TowerFrost,
    SkillExecute,
    SkillShield,
    Danger,
    Combo,
    Background,
}

/// <summary>
/// 统一霓虹语法单一事实来源。
/// baseColor 约定：Ball/Bumper 等为 HDR 绝对值（已含角色倍率，直接写入 SpriteRenderer.color）；
/// Minion/Boss 的 Definition.baseColor 为 0–1 色相，运行时经 entityHdrMultiplier 放大。
/// </summary>
[CreateAssetMenu(fileName = "NeonPalette", menuName = "PinballGame/NeonPalette")]
public class NeonPalette : ScriptableObject
{
    [Header("Global HDR Multipliers")]
    [Tooltip("全局基准倍率；Ball/Bumper 的 baseColor 已是绝对值，此项对它们无效")]
    public float baseHdrMultiplier = 1f;
    [Tooltip("受击 Flash 默认倍率（Bumper 白闪等）")]
    public float flashHdrMultiplier = 6f;
    [Tooltip("ImpactFX 粒子爆发倍率")]
    public float particleHdrMultiplier = 3f;

    [Header("Entity HDR Scale")]
    [Tooltip("MinionDefinition / BossDefinition 色相 × 此倍率 → SpriteRenderer HDR")]
    public float entityHdrMultiplier = 2.5f;

    [Header("Gameplay — HDR Absolute")]
    public Color ballBase = new Color(3.5f, 3.5f, 3.8f, 1f);
    public Color bumperBase = new Color(0f, 1.8f, 2.5f, 1f);

    [Header("Bumper Flash")]
    public Color bumperFlashSprite = new Color(6f, 6f, 6f, 1f);
    public Color bumperFlashGlow = new Color(1.5f, 5f, 5f, 1f);

    [Header("Towers — HDR Absolute")]
    public Color towerTeslaBase = new Color(0f, 0.95f, 1f, 1f);
    public Color towerFrostBase = new Color(0.6f, 0.9f, 1f, 1f);

    [Header("Skills")]
    public Color skillExecute = new Color(1f, 0f, 0.47f, 1f);
    public Color skillShield = new Color(0.2f, 0.85f, 1f, 1f);

    [Header("UI — LDR (≤1, 不触发 Bloom)")]
    public Color danger = new Color(1f, 0.3f, 0.3f, 1f);
    public Color combo = new Color(1f, 0.88f, 0.18f, 1f);

    [Header("Background — LDR (禁止 >1)")]
    public Color background = new Color(0.022f, 0.08f, 0.25f, 1f);

    // ── API ─────────────────────────────────────────────────────────────

    public Color GetBase(NeonRole role)
    {
        switch (role)
        {
            case NeonRole.Ball:        return ballBase;
            case NeonRole.Bumper:      return bumperBase;
            case NeonRole.TowerTesla:  return towerTeslaBase;
            case NeonRole.TowerFrost:  return towerFrostBase;
            case NeonRole.SkillExecute: return skillExecute;
            case NeonRole.SkillShield: return skillShield;
            case NeonRole.Danger:      return danger;
            case NeonRole.Combo:       return combo;
            case NeonRole.Background:  return background;
            case NeonRole.Minion:
            case NeonRole.Boss:
            default:
                return ApplyEntityHue(Color.white);
        }
    }

    public Color GetFlash(NeonRole role)
    {
        switch (role)
        {
            case NeonRole.Bumper:
            case NeonRole.Minion:
            case NeonRole.Boss:
                return bumperFlashSprite;
            case NeonRole.Ball:
                return ScaleHdr(ballBase, flashHdrMultiplier);
            default:
                return ScaleHdr(GetBase(role), flashHdrMultiplier);
        }
    }

    public Color GetBumperFlashGlow() => bumperFlashGlow;

    /// <summary>粒子色：baseColor × particleHdrMultiplier × intensity。</summary>
    public Color ForParticle(Color baseColor, float intensity = 1f)
    {
        var c = baseColor * (particleHdrMultiplier * intensity);
        c.a = 1f;
        return c;
    }

    public Color ForParticle(NeonRole role, float intensity = 1f) =>
        ForParticle(GetBase(role), intensity);

    /// <summary>passthrough / disabled 暗化：RGB × factor，保留 alpha。</summary>
    public static Color Dim(Color c, float factor)
    {
        factor = Mathf.Max(0f, factor);
        return new Color(c.r * factor, c.g * factor, c.b * factor, c.a);
    }

    /// <summary>保留 Definition 色相，统一 entityHdrMultiplier。</summary>
    public Color ApplyEntityHue(Color defHue)
    {
        var c = new Color(defHue.r, defHue.g, defHue.b, defHue.a);
        return c * entityHdrMultiplier;
    }

    private static Color ScaleHdr(Color c, float mult)
    {
        var r = c * mult;
        r.a = c.a;
        return r;
    }

    /// <summary>无资产引用时的运行时默认实例。</summary>
    public static NeonPalette CreateDefault()
    {
        var p = CreateInstance<NeonPalette>();
        return p;
    }
}
