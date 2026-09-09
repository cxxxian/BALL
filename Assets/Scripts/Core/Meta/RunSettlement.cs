using UnityEngine;

public struct SettlementResult
{
    public int Score;
    public int Wave;
    public int CreditsEarned;
    public int TotalCredits;
}

/// <summary>局末 Score → 协议币折算，只结算一次。</summary>
public static class RunSettlement
{
    private static bool _settledThisRun;

    public static void ResetForNewRun()
    {
        _settledThisRun = false;
    }

    public static SettlementResult SettleRun(int finalScore, int waveReached, GameConfig config)
    {
        if (_settledThisRun)
        {
            PlayerProfile.Load();
            return new SettlementResult
            {
                Score = finalScore,
                Wave = waveReached,
                CreditsEarned = 0,
                TotalCredits = PlayerProfile.StoredCredits
            };
        }

        _settledThisRun = true;
        PlayerProfile.Load();

        float rate = config != null ? config.scoreToCreditsRate : 0.1f;
        int minCredits = config != null ? config.minCreditsPerRun : 10;
        int earned = Mathf.Max(minCredits, Mathf.FloorToInt(finalScore * rate));

        PlayerProfile.AddCredits(earned);
        PlayerProfile.Save();

        return new SettlementResult
        {
            Score = finalScore,
            Wave = waveReached,
            CreditsEarned = earned,
            // 用真实存档余额，避免测试无限显示掩盖加币验证
            TotalCredits = PlayerProfile.StoredCredits
        };
    }
}
