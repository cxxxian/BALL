using UnityEngine;

/// <summary>战场霓虹 Palette → UI/HUD 可读色（Protocol Neon：青白主轴）。</summary>
public static class NeonUiColors
{
    private static readonly Color MenuCyan = new Color(0f, 0.91f, 1f, 1f);
    private static readonly Color IceWhite = new Color(0.85f, 0.96f, 1f, 1f);

    public static Color BumperCyanUi(float intensity = 1f)
    {
        var hdr = NeonColors.Active.GetBase(NeonRole.Bumper);
        return MapHdrToUi(hdr, intensity);
    }

    public static Color BumperCyanUiDim(float intensity = 0.48f) => BumperCyanUi(intensity);

    public static Color DangerUi(float intensity = 1f)
    {
        var hdr = NeonColors.Active.GetBase(NeonRole.Danger);
        return MapHdrToUi(hdr, intensity);
    }

    public static Color ScoreUi(float intensity = 1.05f)
    {
        var c = IceWhite * intensity;
        c.a = 1f;
        return c;
    }

    /// <summary>StyleKit --neon-yellow / Combo 语义，Boss 血格等 HUD 用。</summary>
    public static Color YellowUi(float intensity = 1f)
    {
        var hdr = NeonColors.Active.GetBase(NeonRole.Combo);
        return MapHdrToUi(hdr, intensity);
    }

    /// <summary>与主菜单 / Buff 选卡一致的赛博青。</summary>
    public static Color MenuCyanUi(float intensity = 1f)
    {
        var c = MenuCyan * intensity;
        c.a = 1f;
        return c;
    }

    private static Color MapHdrToUi(Color hdr, float intensity)
    {
        float peak = Mathf.Max(hdr.r, hdr.g, hdr.b, 1f);
        var c = new Color(hdr.r / peak, hdr.g / peak, hdr.b / peak, hdr.a) * intensity;
        c.a = 1f;
        return c;
    }
}
