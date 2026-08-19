using UnityEngine;

/// <summary>敌人攻击线 / 弹珠掉球线 — 统一读取 GameConfig，避免两处 Y 混用。</summary>
public static class MinionLineRules
{
    private const float FallbackAttackLineY = -4.0f;
    private const float FallbackBallFallY   = -8.85f;

    public static float GetAttackLineY()
    {
        if (BlockShield.Instance != null && BlockShield.Instance.IsActive)
            return BlockShield.Instance.shieldY;

        var cfg = GameManager.Instance != null ? GameManager.Instance.config : null;
        return cfg != null ? cfg.minionAttackLineY : FallbackAttackLineY;
    }

    public static float GetBallFallLineY()
    {
        var cfg = GameManager.Instance != null ? GameManager.Instance.config : null;
        return cfg != null ? cfg.ballFallLineY : FallbackBallFallY;
    }

    public static float GetAttackHalfWidth()
    {
        var cfg = GameManager.Instance != null ? GameManager.Instance.config : null;
        return cfg != null ? cfg.worldWidth * 0.5f : 4.5f;
    }
}
