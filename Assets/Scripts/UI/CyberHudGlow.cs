using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 极细外发光 HUD 字/图标：UI Outline 模拟 TMP Outline + HDR 面色。
/// 挂到 Wave / Score Text 或 Life Image 上即可。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Graphic))]
public class CyberHudGlow : MonoBehaviour
{
    public enum GlowStyle { BumperCyan, WhiteScore, DangerRed }

    [SerializeField] GlowStyle style = GlowStyle.BumperCyan;
    [SerializeField] float outlineSpread = 1f;
    [SerializeField] float glowAlpha = 0.5f;

    private Outline _outline;
    private Graphic _graphic;

    private void Awake() => EnsureComponents();

    private bool EnsureComponents()
    {
        if (_graphic == null) _graphic = GetComponent<Graphic>();
        if (_graphic == null) return false;

        if (_outline == null)
        {
            _outline = GetComponent<Outline>();
            if (_outline == null)
                _outline = gameObject.AddComponent<Outline>();
        }
        return _outline != null;
    }

    public void Apply(GlowStyle? overrideStyle = null)
    {
        if (!EnsureComponents()) return;

        var s = overrideStyle ?? style;
        Color face;
        Color glow;
        switch (s)
        {
            case GlowStyle.WhiteScore:
                face = NeonUiColors.ScoreUi();
                glow = NeonUiColors.MenuCyanUi(0.7f);
                break;
            case GlowStyle.DangerRed:
                face = NeonUiColors.DangerUi(1.1f);
                glow = NeonUiColors.DangerUi(0.55f);
                break;
            default:
                face = NeonUiColors.MenuCyanUi(1.05f);
                glow = NeonUiColors.BumperCyanUi(0.6f);
                break;
        }

        glow.a = glowAlpha;
        _graphic.color = face;
        _outline.effectColor = glow;
        _outline.effectDistance = new Vector2(outlineSpread, -outlineSpread);
        _outline.useGraphicAlpha = true;
    }

    public static void Ensure(Graphic graphic, GlowStyle glowStyle)
    {
        if (graphic == null || !graphic.gameObject.activeInHierarchy) return;

        var glow = graphic.GetComponent<CyberHudGlow>();
        if (glow == null)
            glow = graphic.gameObject.AddComponent<CyberHudGlow>();
        glow.style = glowStyle;
        glow.Apply(glowStyle);
    }
}
