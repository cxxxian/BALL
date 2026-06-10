using UnityEngine;

/// <summary>战场霓虹 Palette → UI/HUD 可读色（保留色相，与 Bumper cyan 等语义对齐）。</summary>
public static class NeonUiColors
{
    private static readonly Color MenuCyan = new Color(0f, 0.949f, 1f, 1f);

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

    public static Color ScoreUi(float intensity = 1.15f) =>
        new Color(1f * intensity, 1f * intensity, 1.05f * intensity, 1f);

    /// <summary>与主菜单 / Buff 选卡 wave-tag 一致的赛博青。</summary>
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
