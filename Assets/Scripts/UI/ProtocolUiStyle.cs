using UnityEngine;
using UnityEngine.UI;

/// <summary>全工程 UI 展示字体（Orbitron）与 Protocol Neon 色的运行时入口。</summary>
public static class ProtocolUiStyle
{
    private const string OrbitronResourcePath = "Fonts/Orbitron-Bold";
    private static Font _orbitron;
    private static Font _builtin;

    public static Font Orbitron
    {
        get
        {
            if (_orbitron == null)
                _orbitron = Resources.Load<Font>(OrbitronResourcePath);
            return _orbitron;
        }
    }

    public static Font BuiltinLatin
    {
        get
        {
            if (_builtin == null)
            {
                try { _builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                catch { _builtin = null; }
            }
            return _builtin;
        }
    }

    public static Color IceFace => NeonUiColors.ScoreUi(1.05f);
    public static Color CyanFace => NeonUiColors.MenuCyanUi(1.05f);
    public static Color AmberFace => NeonUiColors.YellowUi(1.05f);
    public static Color MagentaFace => new Color(1f, 0.35f, 0.82f, 1f);

    public static void ApplyDisplayFont(Text text, int? fontSize = null)
    {
        if (text == null) return;
        var font = Orbitron;
        if (font != null)
            text.font = font;
        if (fontSize.HasValue)
            text.fontSize = fontSize.Value;
        text.fontStyle = FontStyle.Bold;
        text.supportRichText = false;
    }

    public static void ApplyHudTag(Text text)
    {
        ApplyDisplayFont(text, 13);
        if (text == null) return;
        CyberHudGlow.Ensure(text, CyberHudGlow.GlowStyle.BumperCyan);
    }

    public static void ApplyHudValue(Text text, CyberHudGlow.GlowStyle style)
    {
        ApplyDisplayFont(text);
        CyberHudGlow.Ensure(text, style);
    }

    /// <summary>键名标签（SCORE / WAVE / COMBO）：Orbitron + 青色弱字。</summary>
    public static void ApplyKeyLabel(Text text, float alpha = 0.72f)
    {
        if (text == null) return;
        ApplyDisplayFont(text);
        var c = CyanFace;
        c.a = alpha;
        text.color = c;
        ApplyNeonOutline(text, CyanFace, spread: 1.1f, glowAlpha: 0.4f);
    }

    /// <summary>数值面：冰白/青 + 霓虹描边（去掉黑板描边）。</summary>
    public static void ApplyValueFace(Text text, Color face, Color glow, float spread = 1.5f)
    {
        if (text == null) return;
        ApplyDisplayFont(text);
        text.color = face;
        ApplyNeonOutline(text, glow, spread, glowAlpha: 0.62f);
    }

    public static void ApplyNeonOutline(Graphic graphic, Color glowRgb, float spread = 1.4f, float glowAlpha = 0.55f)
    {
        if (graphic == null) return;

        var shadow = graphic.GetComponent<Shadow>();
        if (shadow != null && !(shadow is Outline))
            Object.Destroy(shadow);

        var outline = graphic.GetComponent<Outline>();
        if (outline == null)
            outline = graphic.gameObject.AddComponent<Outline>();

        var glow = glowRgb;
        glow.a = glowAlpha;
        outline.effectColor = glow;
        outline.effectDistance = new Vector2(spread, -spread);
        outline.useGraphicAlpha = true;
        outline.enabled = true;
    }

    /// <summary>飘分优先 Orbitron；缺 '+' 时用内置字体保证符号可见。</summary>
    public static Font ResolvePopFont()
    {
        var orbitron = Orbitron;
        if (orbitron != null && orbitron.HasCharacter('+'))
            return orbitron;
        return BuiltinLatin != null ? BuiltinLatin : orbitron;
    }

    public static string FormatScorePop(int points, Font font)
    {
        string n = Mathf.Max(0, points).ToString();
        if (font != null && font.HasCharacter('+'))
            return "+" + n;
        return n;
    }
}
