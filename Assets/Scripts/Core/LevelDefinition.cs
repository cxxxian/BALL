using UnityEngine;

[CreateAssetMenu(fileName = "Level_01", menuName = "Ball/LevelDefinition")]
public class LevelDefinition : ScriptableObject
{
    [Header("Identity")]
    public int    levelID = 1;
    public string levelName = "新关卡";
    [TextArea(3, 5)]
    public string description = "在这里输入关卡介绍、BOSS 弱点和打法提示...";

    [Header("Setup")]
    public BossDefinition bossDef;
    public bool isUnlockedByDefault = false;

    // ── 进度获取与持久化（PlayerPrefs） ────────────────────────────────────
    public bool IsUnlocked()
    {
        if (isUnlockedByDefault || levelID == 1) return true;
        return PlayerPrefs.GetInt($"Level_Unlocked_{levelID}", 0) == 1;
    }

    public void SetUnlocked(bool unlocked)
    {
        PlayerPrefs.SetInt($"Level_Unlocked_{levelID}", unlocked ? 1 : 0);
        PlayerPrefs.Save();
    }

    public int GetStars()
    {
        return PlayerPrefs.GetInt($"Level_Stars_{levelID}", 0);
    }

    public int GetHighScore()
    {
        return PlayerPrefs.GetInt($"Level_HighScore_{levelID}", 0);
    }

    public void SaveProgress(int stars, int score)
    {
        int prevStars = GetStars();
        if (stars > prevStars)
        {
            PlayerPrefs.SetInt($"Level_Stars_{levelID}", stars);
        }

        int prevScore = GetHighScore();
        if (score > prevScore)
        {
            PlayerPrefs.SetInt($"Level_HighScore_{levelID}", score);
        }

        PlayerPrefs.Save();
    }
}
