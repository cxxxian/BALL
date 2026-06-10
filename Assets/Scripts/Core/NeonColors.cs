using UnityEngine;

/// <summary>运行时霓虹 Palette 访问入口。</summary>
public static class NeonColors
{
    private static NeonPalette _cached;
    private static NeonPalette _fallback;

    public static NeonPalette Active
    {
        get
        {
            if (_cached != null) return _cached;

            var gm = GameManager.Instance;
            if (gm != null && gm.config != null && gm.config.neonPalette != null)
            {
                _cached = gm.config.neonPalette;
                return _cached;
            }

            var loaded = Resources.Load<NeonPalette>("NeonPalette");
            if (loaded != null)
            {
                _cached = loaded;
                return _cached;
            }

            if (_fallback == null)
                _fallback = NeonPalette.CreateDefault();
            return _fallback;
        }
    }

    /// <summary>MinionDefinition.baseColor 色相 × Palette.entityHdrMultiplier。</summary>
    public static Color ApplyMinionBase(Color defHue) => Active.ApplyEntityHue(defHue);

    public static void InvalidateCache() => _cached = null;
}
